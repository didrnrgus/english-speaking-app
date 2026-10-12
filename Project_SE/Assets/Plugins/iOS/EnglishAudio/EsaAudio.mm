// 듣기 시퀀스 재생기 (iOS). Android 의 SequenceEngine 과 같은 규칙으로 동작한다.
//
// - Unity 가 펼쳐서 보낸 step 목록(파일 + 노드 번호)을 차례로 AVAudioPlayer 로 재생한다.
//   step 마다 [음성 클립 → 텀(무음)] 순서. 음량·빠르기는 노드 단위 값이고, 클립이 시작될 때 / 값이 바뀌는 즉시 적용한다.
// - 텀은 무음 파일을 반복 재생하면서 타이머로 끊는다 → 텀을 바꾸면 진행 중인 텀에도 바로 반영되고,
//   그동안에도 오디오가 '재생 중' 이라 화면이 꺼진 백그라운드에서 앱이 멈추지 않는다 (UIBackgroundModes: audio).
// - 잠금화면·제어 센터·이어폰 버튼: 재생/일시정지, 이전/다음 = 예문(페이지) 단위 이동.
// - 전화·알람 등으로 끊기면 일시정지, 끝나고 시스템이 '다시 재생해도 된다' 고 하면 이어서. 이어폰을 빼면 일시정지.
//
// Unity 는 iOS 메인 스레드에서 부르므로 모든 함수는 메인 스레드에서 실행된다.
// 재생 상태는 EsaAudio_GetState() 로 조회한다 (Unity 가 0.1초마다 폴링).

#import <AVFoundation/AVFoundation.h>
#import <MediaPlayer/MediaPlayer.h>
#import <QuartzCore/QuartzCore.h>
#import <UIKit/UIKit.h>
#include <string.h>

static const int kSilenceSeconds = 2;  // 반복 재생하므로 짧아도 된다
static const int kSilenceSampleRate = 8000;

@interface EsaNode : NSObject
@property (nonatomic) float volume;
@property (nonatomic) float speed;
@property (nonatomic) float gap;
@end

@implementation EsaNode
@end

@interface EsaStep : NSObject
@property (nonatomic, copy) NSString* path;
@property (nonatomic) int node;
@property (nonatomic) int page;
@property (nonatomic, copy) NSString* title;
@property (nonatomic, copy) NSString* subtitle;
@end

@implementation EsaStep
@end

static float EsaClamp(float v, float min, float max)
{
    if (isnan(v)) return min;
    return MAX(min, MIN(max, v));
}

/// JSON 숫자 (없으면 기본값 — Android optDouble 과 같게).
static float EsaFloat(NSDictionary* o, NSString* key, float fallback)
{
    id v = o[key];
    return [v isKindOfClass:[NSNumber class]] ? [v floatValue] : fallback;
}

@interface EsaEngine : NSObject <AVAudioPlayerDelegate>
@end

@implementation EsaEngine
{
    NSArray<EsaNode*>* _nodes;
    NSArray<EsaStep*>* _steps;
    int _requestId;
    BOOL _repeat;
    NSString* _title;

    BOOL _active;  // 시퀀스 진행 중 (일시정지 포함)
    BOOL _playing; // 재생 의도 (일시정지면 NO)
    BOOL _ended;
    int _step;

    AVAudioPlayer* _clip;
    AVAudioPlayer* _silence;
    int _failures; // 연속으로 열지 못한 파일 수 (전부 깨졌으면 멈춘다)

    // 텀(무음) 타이머 — 일시정지 동안은 멈춘다
    BOOL _inGap;
    double _gapElapsed;
    double _gapResumedAt; // < 0 이면 멈춘 상태
    NSTimer* _gapTimer;

    BOOL _resumeAfterInterruption;
    BOOL _remoteRegistered;
}

+ (instancetype)shared
{
    static EsaEngine* instance;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ instance = [[EsaEngine alloc] init]; });
    return instance;
}

- (instancetype)init
{
    if ((self = [super init]))
    {
        _nodes = @[];
        _steps = @[];
        _title = @"";
        _step = -1;
        _gapResumedAt = -1;

        NSNotificationCenter* nc = [NSNotificationCenter defaultCenter];
        [nc addObserver:self selector:@selector(onInterruption:) name:AVAudioSessionInterruptionNotification object:nil];
        [nc addObserver:self selector:@selector(onRouteChange:) name:AVAudioSessionRouteChangeNotification object:nil];
        // Unity 가 포커스 변경 때 오디오 세션 카테고리를 자기 값(Ambient)으로 되돌릴 수 있다 → 재생 중이면 다시 맞춘다
        [nc addObserver:self selector:@selector(onAppStateChanged:) name:UIApplicationDidEnterBackgroundNotification object:nil];
        [nc addObserver:self selector:@selector(onAppStateChanged:) name:UIApplicationDidBecomeActiveNotification object:nil];
    }
    return self;
}

// ---------------------------------------------------------------------------------------------
// 명령

// {"id":3, "title":"S2-U01 여행 첫걸음", "repeat":false, "startStep":0,
//  "nodes":[{"volume":1.0,"speed":1.0,"gap":1.0}, ...],
//  "steps":[{"path":"/var/.../S2-U01-01.mp3","node":0,"page":0,"title":"I'm from Korea.","subtitle":"..."}, ...]}
- (void)load:(NSString*)json
{
    NSDictionary* root = nil;
    NSData* data = [json dataUsingEncoding:NSUTF8StringEncoding];
    if (data) root = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    if (![root isKindOfClass:[NSDictionary class]])
    {
        NSLog(@"[EsaAudio] load failed: bad json");
        [self stop];
        return;
    }

    NSMutableArray<EsaNode*>* nodes = [NSMutableArray array];
    for (NSDictionary* o in root[@"nodes"])
    {
        EsaNode* n = [[EsaNode alloc] init];
        n.volume = EsaClamp(EsaFloat(o, @"volume", 1.f), 0.f, 1.f);
        n.speed = EsaClamp(EsaFloat(o, @"speed", 1.f), 0.5f, 2.f); // AVAudioPlayer.rate 범위
        n.gap = EsaClamp(EsaFloat(o, @"gap", 1.f), 0.f, 10.f);
        [nodes addObject:n];
    }
    NSMutableArray<EsaStep*>* steps = [NSMutableArray array];
    for (NSDictionary* o in root[@"steps"])
    {
        EsaStep* s = [[EsaStep alloc] init];
        s.path = o[@"path"] ?: @"";
        s.node = [o[@"node"] intValue];
        s.page = [o[@"page"] intValue];
        s.title = o[@"title"] ?: @"";
        s.subtitle = o[@"subtitle"] ?: @"";
        if (s.node < 0 || s.node >= (int)nodes.count) s.node = 0;
        [steps addObject:s];
    }
    if (steps.count == 0 || nodes.count == 0)
    {
        [self stop];
        return;
    }

    _nodes = nodes;
    _steps = steps;
    _requestId = root[@"id"] ? [root[@"id"] intValue] : _requestId + 1;
    _repeat = [root[@"repeat"] boolValue];
    _title = root[@"title"] ?: @"";
    int start = MAX(0, MIN([root[@"startStep"] intValue], (int)steps.count - 1));

    [self registerRemoteCommands];
    _failures = 0;
    [self seekToStep:start play:YES];
}

- (void)pause
{
    _playing = NO;
    [_clip pause];
    [_silence pause];
    [self pauseGapTimer];
    [self updateNowPlaying];
}

- (void)resume
{
    if (_steps.count == 0) return;
    if (_ended || !_active)
    {
        [self seekToStep:0 play:YES];
        return;
    }
    _playing = YES;
    [self activateSession];
    if (_inGap)
    {
        [self playSilence];
        [self resumeGapTimer];
    }
    else if (_clip)
    {
        [_clip play];
    }
    else
    {
        [self startStep:_step]; // 끊긴 뒤 클립이 없어진 경우
    }
    [self updateNowPlaying];
}

- (void)stop
{
    [self cancelGap];
    _active = NO;
    _ended = NO;
    _playing = NO;
    _step = -1;
    _steps = @[]; // 멈춘 뒤 이어폰 버튼으로 이전 시퀀스가 다시 재생되지 않게
    [self releaseClip];
    [_silence stop];
    [MPNowPlayingInfoCenter defaultCenter].nowPlayingInfo = nil;
    [[AVAudioSession sharedInstance] setActive:NO
                                   withOptions:AVAudioSessionSetActiveOptionNotifyOthersOnDeactivation
                                         error:nil];
}

- (void)seekToStep:(int)step play:(BOOL)play
{
    if (_steps.count == 0) return;
    step = MAX(0, MIN(step, (int)_steps.count - 1));
    _active = YES;
    _ended = NO;
    _playing = play;
    if (play) [self activateSession];
    [self startStep:step];
}

- (void)setRepeat:(BOOL)repeat
{
    _repeat = repeat;
}

- (void)updateNode:(int)index volume:(float)volume speed:(float)speed gap:(float)gap
{
    if (index < 0 || index >= (int)_nodes.count) return;
    EsaNode* n = _nodes[index];
    n.volume = EsaClamp(volume, 0.f, 1.f);
    n.speed = EsaClamp(speed, 0.5f, 2.f);
    n.gap = EsaClamp(gap, 0.f, 10.f);
    if (!_active || _step < 0) return;
    if (_steps[_step].node != index) return;
    if (_inGap)
    {
        [self scheduleGap]; // 남은 텀을 새 값으로 다시 계산
    }
    else if (_clip)
    {
        _clip.volume = n.volume;
        _clip.rate = n.speed;
    }
}

- (NSString*)stateJson
{
    BOOL playing = _active && _playing;
    return [NSString stringWithFormat:@"{\"id\":%d,\"active\":%@,\"playing\":%@,\"ended\":%@,\"step\":%d,\"gap\":%@,\"repeat\":%@}",
            _requestId, _active ? @"true" : @"false", playing ? @"true" : @"false", _ended ? @"true" : @"false",
            _step, _inGap ? @"true" : @"false", _repeat ? @"true" : @"false"];
}

/// 잠금화면의 다음 버튼: 다음 예문(페이지)의 첫 step.
- (void)nextPage
{
    if (_steps.count == 0) return;
    int step = MAX(0, _step);
    int page = _steps[step].page;
    for (int i = step + 1; i < (int)_steps.count; i++)
    {
        if (_steps[i].page != page)
        {
            [self seekToStep:i play:YES];
            return;
        }
    }
    if (_repeat) [self seekToStep:0 play:YES];
}

/// 잠금화면의 이전 버튼: 이전 예문(페이지)의 첫 step.
- (void)prevPage
{
    if (_steps.count == 0) return;
    int step = MAX(0, _step);
    int first = [self firstStepOfPage:_steps[step].page];
    if (first > 0)
        [self seekToStep:[self firstStepOfPage:_steps[first - 1].page] play:YES];
    else
        [self seekToStep:0 play:YES];
}

// ---------------------------------------------------------------------------------------------
// 진행

/// step 의 클립을 연다. 재생 의도(_playing)면 바로 재생한다.
- (void)startStep:(int)step
{
    [self cancelGap];
    [_silence stop];
    [self releaseClip];
    _step = step;

    EsaStep* s = _steps[step];
    EsaNode* n = _nodes[s.node];
    NSError* error = nil;
    AVAudioPlayer* player = [[AVAudioPlayer alloc] initWithContentsOfURL:[NSURL fileURLWithPath:s.path] error:&error];
    if (!player)
    {
        NSLog(@"[EsaAudio] open failed: %@ %@", s.path, error);
        [self skipBrokenStep];
        return;
    }
    player.delegate = self;
    player.enableRate = YES; // prepareToPlay 전에 켜야 한다 (음높이는 유지)
    player.rate = n.speed;
    player.volume = n.volume;
    [player prepareToPlay];
    _clip = player;
    _failures = 0;
    if (_playing) [player play];
    [self updateNowPlaying];
}

/// 파일 하나가 깨졌으면 그 step 을 건너뛴다. 전부 깨졌으면 끝낸다.
- (void)skipBrokenStep
{
    _failures++;
    int next = [self nextStep:_step];
    if (next < 0 || _failures >= (int)_steps.count)
    {
        [self finish];
        return;
    }
    // 같은 호출 안에서 계속 다음 파일로 넘어가지 않도록 다음 런루프에서
    int requestId = _requestId;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (requestId != self->_requestId || !self->_active) return;
        [self startStep:next];
    });
}

- (void)audioPlayerDidFinishPlaying:(AVAudioPlayer*)player successfully:(BOOL)flag
{
    if (player != _clip || !_active) return;
    [self startGap];
}

- (void)audioPlayerDecodeErrorDidOccur:(AVAudioPlayer*)player error:(NSError*)error
{
    if (player != _clip || !_active) return;
    NSLog(@"[EsaAudio] decode error: %@", error);
    [self releaseClip];
    [self skipBrokenStep];
}

- (void)startGap
{
    [self releaseClip];
    [self cancelGap];
    _inGap = YES;
    if (_playing)
    {
        [self playSilence];
        [self resumeGapTimer];
    }
}

- (void)onGapFinished
{
    [self cancelGap];
    if (!_active || _steps.count == 0) return;
    int next = [self nextStep:MAX(0, _step)];
    if (next >= 0)
        [self startStep:next];
    else
        [self finish];
}

- (void)finish
{
    [self cancelGap];
    [self releaseClip];
    [_silence stop];
    _active = NO;
    _ended = YES;
    _playing = NO;
    [self updateNowPlaying];
}

- (int)nextStep:(int)step
{
    if (step + 1 < (int)_steps.count) return step + 1;
    return _repeat ? 0 : -1;
}

- (int)firstStepOfPage:(int)page
{
    for (int i = 0; i < (int)_steps.count; i++)
    {
        if (_steps[i].page == page) return i;
    }
    return 0;
}

- (void)releaseClip
{
    if (!_clip) return;
    _clip.delegate = nil;
    [_clip stop];
    _clip = nil;
}

// ---------------------------------------------------------------------------------------------
// 텀 타이머

- (float)currentGapSeconds
{
    if (_step < 0 || _steps.count == 0) return 0.f;
    return _nodes[_steps[_step].node].gap;
}

- (void)resumeGapTimer
{
    if (_gapResumedAt < 0) _gapResumedAt = CACurrentMediaTime();
    [self scheduleGap];
}

- (void)pauseGapTimer
{
    if (_gapResumedAt >= 0)
    {
        _gapElapsed += CACurrentMediaTime() - _gapResumedAt;
        _gapResumedAt = -1;
    }
    [_gapTimer invalidate];
    _gapTimer = nil;
}

/// 현재 텀 값 기준으로 남은 시간만큼 타이머를 다시 건다.
- (void)scheduleGap
{
    [_gapTimer invalidate];
    _gapTimer = nil;
    if (!_inGap || _gapResumedAt < 0) return;
    double elapsed = _gapElapsed + (CACurrentMediaTime() - _gapResumedAt);
    double remaining = MAX(0.0, [self currentGapSeconds] - elapsed);
    _gapTimer = [NSTimer timerWithTimeInterval:remaining target:self selector:@selector(onGapTimer:) userInfo:nil repeats:NO];
    [[NSRunLoop mainRunLoop] addTimer:_gapTimer forMode:NSRunLoopCommonModes];
}

- (void)onGapTimer:(NSTimer*)timer
{
    if (timer != _gapTimer) return;
    _gapTimer = nil;
    [self onGapFinished];
}

- (void)cancelGap
{
    [_gapTimer invalidate];
    _gapTimer = nil;
    _inGap = NO;
    _gapElapsed = 0;
    _gapResumedAt = -1;
}

// ---------------------------------------------------------------------------------------------
// 무음 (텀 동안 오디오를 계속 내보내 백그라운드에서 앱이 멈추지 않게)

- (void)playSilence
{
    if (!_silence)
    {
        NSURL* url = [self silenceFileURL];
        if (url) _silence = [[AVAudioPlayer alloc] initWithContentsOfURL:url error:nil];
        _silence.numberOfLoops = -1;
        [_silence prepareToPlay];
    }
    [_silence play];
}

/// 8kHz 16bit 모노 무음 WAV (캐시 폴더에 한 번 만든다).
- (NSURL*)silenceFileURL
{
    NSString* dir = NSSearchPathForDirectoriesInDomains(NSCachesDirectory, NSUserDomainMask, YES).firstObject;
    NSString* path = [dir stringByAppendingPathComponent:[NSString stringWithFormat:@"esa_silence_%ds.wav", kSilenceSeconds]];
    NSFileManager* fm = [NSFileManager defaultManager];
    if (![fm fileExistsAtPath:path])
    {
        uint32_t dataSize = kSilenceSampleRate * 2 * kSilenceSeconds;
        NSMutableData* wav = [NSMutableData dataWithCapacity:44 + dataSize];
        uint32_t u32;
        uint16_t u16;
        [wav appendBytes:"RIFF" length:4];
        u32 = 36 + dataSize; [wav appendBytes:&u32 length:4];
        [wav appendBytes:"WAVEfmt " length:8];
        u32 = 16; [wav appendBytes:&u32 length:4];                     // fmt chunk size
        u16 = 1; [wav appendBytes:&u16 length:2];                      // PCM
        u16 = 1; [wav appendBytes:&u16 length:2];                      // mono
        u32 = kSilenceSampleRate; [wav appendBytes:&u32 length:4];
        u32 = kSilenceSampleRate * 2; [wav appendBytes:&u32 length:4]; // byte rate
        u16 = 2; [wav appendBytes:&u16 length:2];                      // block align
        u16 = 16; [wav appendBytes:&u16 length:2];                     // bits per sample
        [wav appendBytes:"data" length:4];
        u32 = dataSize; [wav appendBytes:&u32 length:4];
        [wav increaseLengthBy:dataSize];
        if (![wav writeToFile:path atomically:YES]) return nil;
    }
    return [NSURL fileURLWithPath:path];
}

// ---------------------------------------------------------------------------------------------
// 오디오 세션 · 잠금화면

- (void)activateSession
{
    AVAudioSession* session = [AVAudioSession sharedInstance];
    NSError* error = nil;
    if (![session.category isEqualToString:AVAudioSessionCategoryPlayback] ||
        ![session.mode isEqualToString:AVAudioSessionModeSpokenAudio])
    {
        [session setCategory:AVAudioSessionCategoryPlayback mode:AVAudioSessionModeSpokenAudio options:0 error:&error];
        if (error) NSLog(@"[EsaAudio] setCategory failed: %@", error);
    }
    error = nil;
    [session setActive:YES error:&error];
    if (error) NSLog(@"[EsaAudio] setActive failed: %@", error);
}

- (void)onAppStateChanged:(NSNotification*)note
{
    if (!_active || !_playing) return;
    [self activateSession];
    // Unity 가 같은 알림 뒤에 세션을 다시 건드릴 수 있어 한 번 더
    dispatch_async(dispatch_get_main_queue(), ^{
        if (self->_active && self->_playing) [self activateSession];
    });
}

- (void)onInterruption:(NSNotification*)note
{
    dispatch_async(dispatch_get_main_queue(), ^{
        NSUInteger type = [note.userInfo[AVAudioSessionInterruptionTypeKey] unsignedIntegerValue];
        if (type == AVAudioSessionInterruptionTypeBegan)
        {
            if (self->_active && self->_playing)
            {
                self->_resumeAfterInterruption = YES;
                [self pause];
            }
        }
        else if (type == AVAudioSessionInterruptionTypeEnded)
        {
            NSUInteger options = [note.userInfo[AVAudioSessionInterruptionOptionKey] unsignedIntegerValue];
            BOOL shouldResume = (options & AVAudioSessionInterruptionOptionShouldResume) != 0;
            if (self->_resumeAfterInterruption && shouldResume && self->_active) [self resume];
            self->_resumeAfterInterruption = NO;
        }
    });
}

- (void)onRouteChange:(NSNotification*)note
{
    NSUInteger reason = [note.userInfo[AVAudioSessionRouteChangeReasonKey] unsignedIntegerValue];
    if (reason != AVAudioSessionRouteChangeReasonOldDeviceUnavailable) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (self->_active && self->_playing) [self pause]; // 이어폰 분리 시 일시정지
    });
}

- (void)registerRemoteCommands
{
    if (_remoteRegistered) return;
    _remoteRegistered = YES;
    MPRemoteCommandCenter* center = [MPRemoteCommandCenter sharedCommandCenter];
    [center.playCommand addTargetWithHandler:^MPRemoteCommandHandlerStatus(MPRemoteCommandEvent* e) {
        [self resume];
        return MPRemoteCommandHandlerStatusSuccess;
    }];
    [center.pauseCommand addTargetWithHandler:^MPRemoteCommandHandlerStatus(MPRemoteCommandEvent* e) {
        [self pause];
        return MPRemoteCommandHandlerStatusSuccess;
    }];
    [center.togglePlayPauseCommand addTargetWithHandler:^MPRemoteCommandHandlerStatus(MPRemoteCommandEvent* e) {
        if (self->_active && self->_playing) [self pause];
        else [self resume];
        return MPRemoteCommandHandlerStatusSuccess;
    }];
    [center.nextTrackCommand addTargetWithHandler:^MPRemoteCommandHandlerStatus(MPRemoteCommandEvent* e) {
        [self nextPage];
        return MPRemoteCommandHandlerStatusSuccess;
    }];
    [center.previousTrackCommand addTargetWithHandler:^MPRemoteCommandHandlerStatus(MPRemoteCommandEvent* e) {
        [self prevPage];
        return MPRemoteCommandHandlerStatusSuccess;
    }];
    center.playCommand.enabled = YES;
    center.pauseCommand.enabled = YES;
    center.togglePlayPauseCommand.enabled = YES;
    center.nextTrackCommand.enabled = YES;
    center.previousTrackCommand.enabled = YES;
}

/// 잠금화면·제어 센터 표시: 제목 = 영어 문장, 아티스트 = 유닛, 앨범 = 한국어 해석.
- (void)updateNowPlaying
{
    if (_step < 0 || _step >= (int)_steps.count) return;
    EsaStep* s = _steps[_step];
    BOOL playing = _active && _playing;
    [MPNowPlayingInfoCenter defaultCenter].nowPlayingInfo = @{
        MPMediaItemPropertyTitle: s.title ?: @"",
        MPMediaItemPropertyArtist: _title ?: @"",
        MPMediaItemPropertyAlbumTitle: s.subtitle ?: @"",
        MPNowPlayingInfoPropertyPlaybackRate: @(playing ? 1.0 : 0.0),
    };
}

@end

// -------------------------------------------------------------------------------------------------
// Unity(C#) 가 부르는 C 함수 — Scripts/Audio/IosAudioBackend.cs

static NSString* EsaString(const char* s)
{
    return s ? [NSString stringWithUTF8String:s] : @"";
}

extern "C" {

void EsaAudio_Play(const char* json)
{
    [[EsaEngine shared] load:EsaString(json)];
}

void EsaAudio_Pause()
{
    [[EsaEngine shared] pause];
}

void EsaAudio_Resume()
{
    [[EsaEngine shared] resume];
}

void EsaAudio_Stop()
{
    [[EsaEngine shared] stop];
}

void EsaAudio_SeekStep(int step)
{
    [[EsaEngine shared] seekToStep:step play:YES];
}

void EsaAudio_SetRepeat(bool repeat)
{
    [[EsaEngine shared] setRepeat:repeat];
}

void EsaAudio_UpdateNode(int index, float volume, float speed, float gap)
{
    [[EsaEngine shared] updateNode:index volume:volume speed:speed gap:gap];
}

/// 반환 문자열은 malloc 으로 만든다 (마샬러가 free 한다).
char* EsaAudio_GetState()
{
    const char* utf8 = [[[EsaEngine shared] stateJson] UTF8String];
    size_t len = strlen(utf8) + 1;
    char* copy = (char*)malloc(len);
    memcpy(copy, utf8, len);
    return copy;
}

}
