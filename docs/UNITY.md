# Unity 앱 구현 기록

> 2026-10-07 · Unity 6000.3.29f1 · Android (+ iOS 기기 설치 테스트, 2026-10-12) · UGUI + TextMeshPro
> 기획: [PLAN.md](PLAN.md) · 작업 기록: [WORKLOG.md](WORKLOG.md) · 배너 광고: [ADS.md](ADS.md) · iOS: [IOS.md](IOS.md)

## 1. 처음 열기

1. Unity Hub → **Add → Add project from disk** → 이 레포의 `unity/` 폴더 선택 (에디터 6000.3.29f1)
2. 처음 열면 **자동 셋업**이 한 번 실행된다 (`Assets/Editor/ProjectSetup.cs`, 메뉴: `Tools > English App > Run Project Setup`)
   - TextMeshPro Essential Resources 가져오기
   - 잘난체 TMP 폰트 템플릿 생성 → `Assets/Resources/Fonts/JalnanGothic SDF.asset` (동적 아틀라스, 외곽선 0.15)
   - 번들 예문 복사: 레포 `content/` → `Assets/StreamingAssets/content/` (git 에는 안 올림, 빌드할 때마다 자동 복사)
   - Player Settings: 세로 고정, 패키지 `com.didrnrgus.englishspeaking`, 앱 이름 `영어 말하기`, minSdk 26, IL2CPP / ARM64
   - `Assets/Scenes/Main.unity` 생성 + 빌드 목록에 등록
3. 셋업이 만든 파일(`ProjectSettings/*`, `Packages/packages-lock.json`, `.meta`, 폰트 에셋, 씬)을 커밋한다
4. File → Build Profiles → **Android** 로 전환 → Play 로 에디터 확인 / Build 로 기기 확인

> 씬에는 아무것도 둘 필요가 없다. 실행하면 `AppRoot` 가 카메라·이벤트 시스템을 만들고 캔버스·화면 프리팹을 띄운다
> (`[RuntimeInitializeOnLoadMethod]`). 화면 모양은 `Assets/UI/Prefabs/*.prefab` 에 있다 — 열어서 고치면 된다.
> 프리팹 목록은 `Assets/Resources/UI/UIPrefabs.asset`. 프리팹이 없으면 셋업이 `Tools > English App > Build UI Prefabs`
> (`UIPrefabBuilder`)로 만든다. 이 메뉴를 다시 누르면 코드 기본값으로 덮어쓴다.
>
> UI 는 Le Tai 의 Translucent Image + Paraform(리퀴드 글래스) 기반. 카메라는 배경 캔버스(`Assets/UI/Sprites/Background.png`)만 그리고
> `TranslucentImageSource` 가 흐리게 만든 화면을 글래스(`TranslucentImage`, 머티리얼 `Assets/UI/Materials/Glass.mat`)가 비춰 보인다.
> 블러 세기는 `Assets/UI/GlassBlur.asset`.
>
> 밝은 / 어두운 모드: 메인 화면 오른쪽 아래 해/달 버튼 (선택은 PlayerPrefs `ui_theme_mode` 에 저장).
> 색은 팔레트 `Assets/UI/Themes/Light.asset`, `Dark.asset` 에서 고친다 (글자·선·트랙·배경 그림·글래스 톤).
> 프리팹의 그림·글자에 붙은 `ThemeColor`(역할)가 지금 팔레트의 색으로 칠한다 — 프리팹에 보이는 색은 밝은 모드 색.
> 글래스 색은 배경에 곱해지므로 어두운 유리는 틴트를 밝게 두고 Brightness 를 낮춘다 (`ThemePalette.cs` 주석).

## 2. 화면

```
메인
├─ 가운데 위: 제목 "영어 말하기"            ├─ 오른쪽 위: ⚙ 설정
└─ 가운데보다 조금 위에서 아래로 2열
     문장(말하기) S1~S5        대화 D1~D5      (레벨 카드 오른쪽 ▶ = 레벨 전체 듣기, 듣는 중이면 카드에 위치 표시)
          └─ 레벨 화면: [▶ S1 전체 듣기] + 유닛 10개 목록 (재생 중인 유닛·이어 듣기 위치 표시)
                └─ 유닛 화면: 한 페이지에 예문 하나 (좌우로 밀어서 넘김, 1/5)
                     ├─ 문장·대사(또는 ▶ 영어 / ▶ 한국어)를 누르면 개별 듣기
                     ├─ 듣기 순서 칩 (지금 재생 중인 노드 강조, 누르면 설정 > 전체 듣기)
                     ├─ 맨 위 줄: 지금 어떤 전체 듣기인지 (S1 전체 듣기 중 · 유닛 3/10 / 유닛 전체 듣기 중)
                     └─ [■ 정지] [▶ 전체 듣기 / 일시정지 / 이어 듣기] [반복]
설정 팝업 (어느 화면에서든 ⚙)
├─ 개별 듣기: 음량, 빠르기 슬라이더 + 마지막 문장 다시 듣기
└─ 전체 듣기: 듣기 시퀀스 (끌어서 순서 변경, + 영어 / + 한국어, 노드마다 음량·빠르기·텀), 반복, 기본값
미니 플레이어: 전체 듣기 중 다른 화면으로 나가면 아래에 표시 (누르면 지금 재생 중인 유닛으로)
               [S1 전체 듣기 · U03 가족] 유닛 3/10 · 예문 2/5 · 영어   /   [S1-U03 가족] 유닛 전체 듣기 · 예문 2/5 · 영어
배너 광고: 모든 화면 맨 아래 전용 띠 (화면은 그 위까지만) → ADS.md
```

- **세로 모드만** — Player Settings + 실행 시 `Screen.orientation = Portrait`
- **Safe Area** — 모든 UI 는 `SafeAreaFitter` 안쪽 (배경색과 팝업의 어두운 덮개만 화면 전체).
  Android 는 내비게이션 바(3버튼·제스처 바) 자리도 피한다 — 바가 숨겨진 전체 화면이어도 (`DeviceScreen`)
- **배너 광고** — Safe Area 맨 아래 전용 띠(`BannerAdSlot`), 화면·미니 플레이어·토스트는 그 위. 설정 팝업이 열리면 배너 숨김 → [ADS.md](ADS.md)
- **폰트** — 잘난체 고딕 TTF, TMP 동적 아틀라스, 외곽선 0.15. 실행 시에는 템플릿 머티리얼 값을 복사한 런타임 폰트 에셋을
  새로 만들어 쓴다 → 한글 글리프가 템플릿 에셋 파일에 쌓이지 않는다. 외곽선 색·두께는 템플릿 머티리얼에서 바꾸면 된다
- **기준 해상도** 1080 x 1920. 폰처럼 길쭉하면 가로 기준, 태블릿처럼 덜 길쭉하면 세로 기준으로 맞춤
- 뒤로 가기: 팝업 닫기 → 이전 화면 → (메인에서) 앱을 뒤로 보냄 (종료하지 않아 재생 유지)

## 3. 듣기

### 개별 듣기
문장 하나를 한 언어로. 설정의 **개별 듣기 음량·빠르기** 적용.

### 전체 듣기 — 듣기 시퀀스
유닛의 예문 5개를 차례로, 예문마다(대화는 **턴마다**) 시퀀스 노드 순서대로 재생한다.

```
기본 시퀀스: 한국어 → 영어 → 영어 → 한국어 → 영어
노드 = { 언어, 음량 0~100%, 빠르기 x0.5~x2.0 (0.05 단위), 텀 0.1~5.0초 (0.1 단위) }
텀 = 그 노드를 재생한 뒤 다음 소리까지 쉬는 시간
```

- 최대 10노드, 최소 1노드. 반복을 켜면 유닛 끝에서 처음으로
- **슬라이더를 바꾸는 즉시 재생 중인 소리에 적용**: 지금 나는 소리의 음량·빠르기, 진행 중인 텀의 남은 시간이 바로 바뀐다
- 순서·추가·삭제·언어를 바꾸면 지금 예문부터 새 시퀀스로 다시 재생 (일시정지 중이었으면 일시정지 유지)
- 재생 중 페이지를 넘기면 그 예문부터 이어서 재생, 일시정지 중에는 둘러보기만
- 저장: `PlayerPrefs` `app_settings_v1` (JSON)

### 전체 듣기의 두 가지 — 레벨 / 유닛

| | 레벨 전체 듣기 ("S1 전체 듣기") | 유닛 전체 듣기 ("유닛 전체 듣기") |
|---|---|---|
| 시작 | 메인 레벨 카드의 ▶, 레벨 화면 맨 위 버튼 | 유닛 화면 아래 '전체 듣기' |
| 범위 | 레벨의 유닛 10개를 U01 → U10 순서로 이어서 | 그 유닛의 예문 5개 |
| 시작 위치 | **마지막으로 듣던 유닛**부터 (레벨 끝까지 들으면 다음엔 처음부터) | 보고 있는 예문부터 |
| 반복 | 레벨 끝에서 처음 유닛으로 | 유닛 끝에서 처음 예문으로 |

- 화면에서는 항상 범위를 붙여 부른다 (`PlaybackText`): 메인 카드·레벨 화면·유닛 화면 맨 위 줄·미니 플레이어·설정 팝업이 같은 문구
- 레벨 전체 듣기는 시작할 때 **레벨의 유닛 파일과 mp3 를 모두 받은 뒤** 하나의 시퀀스로 네이티브에 넘긴다
  → 화면이 꺼져 Unity 가 멈춰도 다음 유닛으로 넘어간다. 처음 한 번은 오래 걸릴 수 있다 (D5 레벨 ≈ mp3 1,200개), 받은 것은 캐시
- 네이티브에 넘기는 `page` 는 레벨 전체에서 이어지는 번호 → 알림·잠금화면의 이전/다음이 유닛 경계를 넘어 이동
- 유닛 화면에서 레벨 전체 듣기가 이 유닛을 재생하다 다음 유닛으로 넘어가면 **화면도 그 유닛으로 바뀐다** (`Navigator.Replace`).
  다른 유닛을 직접 열어 둘러보는 중이면 따라가지 않는다
- 이어 듣기 위치: `app_settings_v1.resume` (레벨마다 마지막 유닛 id). 유닛 전체 듣기로 들은 유닛도 그 레벨의 위치로 기록

> PLAN.md 6-4 의 "설정 팝업 드롭다운 x1.5~x0.5 전역 속도" 는 이 슬라이더 방식으로 바뀌었다.

## 4. 화면 꺼짐 재생 (Android 네이티브 플러그인)

Unity 는 화면이 꺼지면 멈추므로 재생은 전부 `Assets/Plugins/Android/EnglishAudio.androidlib` (Java, Media3 1.4.1) 가 한다.

```
C# AudioPlayer ──(JNI: play / pause / resume / stop / seekStep / setRepeat / updateNode / getState)──▶ AudioBridge
                                                                         │ 메인 스레드로 넘김
                                                                         ▼
                                     PlaybackService (MediaSessionService, 포그라운드, 알림·잠금화면 컨트롤)
                                        └─ SequenceEngine ── ExoPlayer 플레이리스트 [클립0, 텀0, 클립1, 텀1, …]
```

- step(= 예문·줄·노드 하나)마다 **[음성 클립, 무음 텀]** 두 항목. 텀은 20초짜리 무음 WAV 를 재생하면서 타이머로 끊는다
  - 텀 동안에도 플레이어가 '재생 중' → 포그라운드 서비스와 wake lock 이 유지되어 화면이 꺼져도 끊기지 않는다
  - 텀 값을 바꾸면 진행 중인 텀의 남은 시간도 바로 다시 계산
- 음량 `player.setVolume`, 빠르기 `setPlaybackParameters` (음높이 유지). 다음 클립의 빠르기는 텀이 시작될 때 미리 바꿔 둔다
- 알림·잠금화면·블루투스: 재생/일시정지, 이전/다음 = **예문 단위** 이동
- 오디오 포커스(전화 등), 이어폰 분리 시 일시정지는 ExoPlayer 기본 처리
- Unity 는 0.1초마다 `getState()` 를 읽는다 → 화면이 꺼졌다 켜지면 바로 현재 위치로 동기화
- 에디터·PC 에서는 같은 규칙의 `EditorAudioBackend` (AudioSource) 로 동작. 빠르기는 pitch 라 음높이가 바뀐다 — 실제 품질은 기기에서
- **한계:** 앱을 최근 앱 목록에서 밀어서 지우면 Unity 는 액티비티 onDestroy 에서 `Process.killProcess` 로 프로세스를 끝내므로 재생도 멈춘다
  (화면 끄기·홈 버튼·뒤로 가기는 계속 재생). 정리 코드가 돌 틈이 없어 일시정지 알림·미디어 카드가 재부팅 전까지 남던 문제가 있었다 →
  `AudioBridge` 가 `onActivityPreDestroyed`(Android 10+, 9 이하는 `onTaskRemoved`)에서 세션 해제·알림 제거를 동기로 하고,
  시스템 '재생 재개' 카드가 생기지 않게 매니페스트에서 `MediaBrowserService` intent-filter 를 뺐다.
  앱 시작 시 이전 실행이 남긴 알림도 지운다. 재생 자체를 유지하려면 서비스를 별도 프로세스(`android:process`)로 옮기고
  MediaController 커스텀 명령으로 통신해야 한다 → M4 에서 검토
- 빌드 시 `AndroidGradlePostProcess` 가 플러그인 compileSdk 를 unityLibrary 와 맞추고 `android.useAndroidX=true` 를 보장

### iOS (`Assets/Plugins/iOS/EnglishAudio/EsaAudio.mm`)

같은 `IAudioBackend` 를 `IosAudioBackend` 가 `DllImport("__Internal")` 로 구현한다. 규칙(step = [클립 → 무음 텀], 노드 값 즉시 반영,
예문 단위 이전/다음, 0.1초 `GetState` 폴링 JSON)은 Android 와 같고, 재생은 AVAudioPlayer + 백그라운드 오디오 모드 + 잠금화면 컨트롤.
빌드 설정은 `Editor/IosBuildSettings.cs` 가 맞춘다. 자세한 구조·설치 방법·체크리스트는 [IOS.md](IOS.md).

## 5. 콘텐츠·음성

- 예문 JSON: 원격(GitHub Pages) → 캐시(`persistentDataPath/content`) → 번들(`StreamingAssets/content`) 순서
- mp3: **들을 유닛만** 받아 `persistentDataPath/audio_cache/` 에 저장 (동시 4개). 유닛 파일 `version` 이 바뀌면 그 유닛 mp3 를 다시 받는다
- 오프라인이고 캐시도 없으면 "음성을 받지 못했어요" 안내

## 6. 코드 구조

```
unity/Assets/
├─ Scripts/Core/      AppRoot(시작점·캔버스·뒤로가기), ContentService, AudioCache, AppSettings, ContentModels, DeviceScreen
├─ Scripts/Ads/       (어셈블리 EnglishApp.Ads) AdConfig, LevelPlayBanner — 배너 광고 SDK 래퍼
├─ Scripts/Audio/     AudioPlayer(재생 제어), AndroidAudioBackend, IosAudioBackend, EditorAudioBackend, PlaybackModels
├─ Scripts/UI/        Theme(모드·색), ThemePalette, Fonts, UIKit, UIPrefabs(프리팹 목록), AppCanvas, Navigator, BaseScreen, PlaybackText(재생 상태 문구)
│   ├─ Screens/       MainScreen, LevelScreen, UnitScreen, SettingsPopup
│   └─ Widgets/       ThemeColor, TopBar, LevelButton, UnitCard, UnitPage/SpeakingPage/DialoguePage, SentenceLine, SequenceChip,
│                     NowPlayingCard, PagerView, DirectionalScrollRect, LabeledSlider, SequenceListView, SequenceNodeRow,
│                     SequenceDragHandle, MiniPlayerBar, SafeAreaFitter, Toast, PressScale, FillViewportHeight, BannerAdSlot
├─ Editor/            ProjectSetup, ContentSync, AndroidGradlePostProcess, IosBuildSettings(iOS 설정·Info.plist)
│   └─ UI/            UIPrefabBuilder(프리팹 생성기), UIBuild(글래스 프리셋·생성 도우미), SpriteBaker(아이콘·배경 PNG)
├─ UI/                Prefabs/, Sprites/, Themes/(Light·Dark 팔레트), Materials/Glass.mat, GlassBlur.asset  (생성기가 만들고 이후엔 직접 수정)
├─ Plugins/Android/EnglishAudio.androidlib/   AudioBridge, PlaybackService, SequenceEngine (Java)
├─ Plugins/iOS/EnglishAudio/                  EsaAudio.mm (AVAudioPlayer·잠금화면, Objective-C++)
└─ Resources/Fonts/   JalnanGothicTTF.ttf (+ 셋업이 만드는 JalnanGothic SDF.asset)
```

## 7. 검증 상태

이 구현은 Unity 에디터가 없는 환경에서 작성했다. 대신:

- C#: Unity 참조 DLL + ugui/TMP 시그니처 스텁으로 mono 컴파일 검사 (에디터 / Android / PC 정의 3가지, 에디터 스크립트 포함) — 오류 0
- Java: Media3 1.4.1 실제 소스 + Android 14 프레임워크로 javac 검사 — 플러그인 코드 오류 0

**아직 실제 Unity 에디터·기기에서 돌려보지 않았다.** 처음 열 때 콘솔 오류가 나면 그 메시지로 고친다.

### 기기 테스트 체크리스트

- [ ] 처음 열기 → 자동 셋업 로그, 폰트(잘난체 + 외곽선) 표시
- [ ] 메인 2열, 노치/펀치홀·하단 제스처 바에 UI 가 가리지 않음, 가로로 돌려도 세로 유지
- [ ] 배너 광고: 3버튼·제스처 내비게이션 모두 배너가 바 위 전용 띠 안에, 앱 버튼과 겹치지 않음 ([ADS.md](ADS.md) 7장)
- [ ] 유닛 화면 좌우 넘김, 대화 페이지 세로 스크롤 중 좌우 넘김
- [ ] 개별 듣기: 설정의 음량·빠르기를 재생 중에 바꾸면 바로 반영
- [ ] 전체 듣기: 시퀀스 순서대로, 노드별 음량·빠르기·텀이 재생 중 바로 반영, 드래그 순서 변경
- [ ] **화면을 끄고 유닛 끝까지 재생** (텀 포함), 잠금화면·알림 컨트롤(재생/일시정지/이전/다음)
- [ ] 레벨 전체 듣기: 메인 ▶ / 레벨 화면 버튼으로 시작, 받는 중 진행률, **화면을 끈 채 다음 유닛으로 넘어감**,
      알림의 이전/다음이 유닛 경계를 넘음, 유닛 화면이 다음 유닛으로 따라감, 껐다 켜면 마지막 유닛부터 이어서
- [ ] 레벨 전체 듣기 중 메인 카드·레벨 화면 카드·유닛 화면 맨 위 줄·미니 플레이어가 모두 같은 위치(유닛·예문)를 표시
- [ ] 화면을 다시 켜면 현재 예문·줄 표시가 맞음, 홈 버튼으로 나가도 재생 계속
- [ ] 전화 수신·이어폰 분리 시 일시정지
- [ ] 삼성/샤오미 등 절전 정책에서 장시간 재생 (M4)
- [ ] iOS: [IOS.md](IOS.md) 6장 체크리스트
