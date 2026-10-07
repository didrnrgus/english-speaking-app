package com.didrnrgus.englishspeaking.audio;

import android.app.PendingIntent;
import android.content.Intent;

import androidx.annotation.Nullable;
import androidx.annotation.OptIn;
import androidx.media3.common.AudioAttributes;
import androidx.media3.common.C;
import androidx.media3.common.Player;
import androidx.media3.common.util.UnstableApi;
import androidx.media3.exoplayer.ExoPlayer;
import androidx.media3.session.MediaSession;
import androidx.media3.session.MediaSessionService;

/**
 * 화면이 꺼져도 재생을 이어가는 포그라운드 미디어 서비스.
 *
 * <p>Media3 가 재생 중일 때 알림(잠금화면·알림창 컨트롤)과 포그라운드 상태를 관리한다.
 * 문장 사이 '텀' 도 무음 트랙으로 재생하므로 시퀀스가 끝날 때까지 플레이어는 계속 '재생 중' 상태이고,
 * 그동안 서비스가 포그라운드로 유지된다.
 */
@OptIn(markerClass = UnstableApi.class)
public class PlaybackService extends MediaSessionService {
    @Nullable
    static PlaybackService instance;

    private ExoPlayer player;
    private MediaSession session;
    private SequenceEngine engine;

    @Override
    public void onCreate() {
        super.onCreate();
        AudioAttributes attributes = new AudioAttributes.Builder()
                .setUsage(C.USAGE_MEDIA)
                .setContentType(C.AUDIO_CONTENT_TYPE_SPEECH)
                .build();
        player = new ExoPlayer.Builder(this)
                .setAudioAttributes(attributes, /* handleAudioFocus= */ true)
                .setHandleAudioBecomingNoisy(true) // 이어폰 분리 시 일시정지
                .setWakeMode(C.WAKE_MODE_LOCAL) // 재생(무음 텀 포함) 중 CPU 유지
                .build();
        engine = new SequenceEngine(this, player);

        MediaSession.Builder builder = new MediaSession.Builder(this, engine.sessionPlayer());
        PendingIntent launch = launchIntent();
        if (launch != null) builder.setSessionActivity(launch);
        session = builder.build();

        instance = this;
        AudioBridge.onServiceReady(this);
    }

    SequenceEngine engine() {
        return engine;
    }

    @Nullable
    @Override
    public MediaSession onGetSession(MediaSession.ControllerInfo controllerInfo) {
        return session;
    }

    @Override
    public void onTaskRemoved(@Nullable Intent rootIntent) {
        // 앱을 최근 목록에서 지웠을 때: 재생 중이면 계속, 아니면 종료
        if (player == null || !player.getPlayWhenReady() || player.getMediaItemCount() == 0
                || player.getPlaybackState() == Player.STATE_ENDED) {
            stopSelf();
        }
    }

    @Override
    public void onDestroy() {
        instance = null;
        if (engine != null) engine.release();
        if (session != null) session.release();
        if (player != null) player.release();
        session = null;
        player = null;
        engine = null;
        AudioBridge.onServiceDestroyed();
        super.onDestroy();
    }

    @Nullable
    private PendingIntent launchIntent() {
        Intent intent = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (intent == null) return null;
        intent.addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
        return PendingIntent.getActivity(this, 0, intent,
                PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
    }
}
