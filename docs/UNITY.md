# Unity 앱 구현 기록

> 2026-10-07 · Unity 6000.3.17f1 · Android 전용 · UGUI + TextMeshPro
> 기획: [PLAN.md](PLAN.md) · 작업 기록: [WORKLOG.md](WORKLOG.md)

## 1. 처음 열기

1. Unity Hub → **Add → Add project from disk** → 이 레포의 `unity/` 폴더 선택 (에디터 6000.3.17f1)
2. 처음 열면 **자동 셋업**이 한 번 실행된다 (`Assets/Editor/ProjectSetup.cs`, 메뉴: `Tools > English App > Run Project Setup`)
   - TextMeshPro Essential Resources 가져오기
   - 잘난체 TMP 폰트 템플릿 생성 → `Assets/Resources/Fonts/JalnanGothic SDF.asset` (동적 아틀라스, 외곽선 0.15)
   - 번들 예문 복사: 레포 `content/` → `Assets/StreamingAssets/content/` (git 에는 안 올림, 빌드할 때마다 자동 복사)
   - Player Settings: 세로 고정, 패키지 `com.didrnrgus.englishspeaking`, 앱 이름 `영어 말하기`, minSdk 26, IL2CPP / ARM64
   - `Assets/Scenes/Main.unity` 생성 + 빌드 목록에 등록
3. 셋업이 만든 파일(`ProjectSettings/*`, `Packages/packages-lock.json`, `.meta`, 폰트 에셋, 씬)을 커밋한다
4. File → Build Profiles → **Android** 로 전환 → Play 로 에디터 확인 / Build 로 기기 확인

> 씬에는 아무것도 둘 필요가 없다. 실행하면 `AppRoot` 가 캔버스·이벤트 시스템·화면을 모두 코드로 만든다
> (`[RuntimeInitializeOnLoadMethod]`). UI 는 프리팹 없이 `UIKit` 함수로 생성.

## 2. 화면

```
메인
├─ 가운데 위: 제목 "영어 말하기"            ├─ 오른쪽 위: ⚙ 설정
└─ 가운데보다 조금 위에서 아래로 2열
     문장(말하기) S1~S5        대화 D1~D5
          └─ 레벨 화면: 유닛 10개 목록
                └─ 유닛 화면: 한 페이지에 예문 하나 (좌우로 밀어서 넘김, 1/5)
                     ├─ 문장·대사(또는 ▶ 영어 / ▶ 한국어)를 누르면 개별 듣기
                     ├─ 듣기 순서 칩 (지금 재생 중인 노드 강조, 누르면 설정 > 전체 듣기)
                     └─ [■ 정지] [▶ 전체 듣기 / 일시정지 / 이어 듣기] [반복]
설정 팝업 (어느 화면에서든 ⚙)
├─ 개별 듣기: 음량, 빠르기 슬라이더 + 마지막 문장 다시 듣기
└─ 전체 듣기: 듣기 시퀀스 (끌어서 순서 변경, + 영어 / + 한국어, 노드마다 음량·빠르기·텀), 반복, 기본값
미니 플레이어: 전체 듣기 중 다른 화면으로 나가면 아래에 표시 (누르면 그 유닛으로)
```

- **세로 모드만** — Player Settings + 실행 시 `Screen.orientation = Portrait`
- **Safe Area** — 모든 UI 는 `SafeAreaFitter` 안쪽 (배경색과 팝업의 어두운 덮개만 화면 전체)
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
- **한계(기기 확인 필요):** 앱을 최근 앱 목록에서 밀어서 지우면 Unity 는 액티비티가 끝날 때 프로세스를 종료하므로 재생도 멈출 수 있다
  (화면 끄기·홈 버튼·뒤로 가기는 계속 재생). 이것까지 막으려면 서비스를 별도 프로세스(`android:process`)로 옮기고
  MediaController 커스텀 명령으로 통신해야 한다 → M4 에서 검토
- 빌드 시 `AndroidGradlePostProcess` 가 플러그인 compileSdk 를 unityLibrary 와 맞추고 `android.useAndroidX=true` 를 보장

## 5. 콘텐츠·음성

- 예문 JSON: 원격(GitHub Pages) → 캐시(`persistentDataPath/content`) → 번들(`StreamingAssets/content`) 순서
- mp3: **들을 유닛만** 받아 `persistentDataPath/audio_cache/` 에 저장 (동시 4개). 유닛 파일 `version` 이 바뀌면 그 유닛 mp3 를 다시 받는다
- 오프라인이고 캐시도 없으면 "음성을 받지 못했어요" 안내

## 6. 코드 구조

```
unity/Assets/
├─ Scripts/Core/      AppRoot(시작점·캔버스·뒤로가기), ContentService, AudioCache, AppSettings, ContentModels
├─ Scripts/Audio/     AudioPlayer(재생 제어), AndroidAudioBackend, EditorAudioBackend, PlaybackModels
├─ Scripts/UI/        Theme, Fonts, Sprites(런타임 SDF 아이콘·둥근 사각형), UIKit, Navigator, BaseScreen
│   ├─ Screens/       MainScreen, LevelScreen, UnitScreen, SettingsPopup
│   └─ Widgets/       PagerView, DirectionalScrollRect, LabeledSlider, SequenceListView, SequenceNodeRow,
│                     SequenceDragHandle, MiniPlayerBar, SafeAreaFitter, Toast, PressScale, FillViewportHeight
├─ Editor/            ProjectSetup, ContentSync, AndroidGradlePostProcess
├─ Plugins/Android/EnglishAudio.androidlib/   AudioBridge, PlaybackService, SequenceEngine (Java)
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
- [ ] 유닛 화면 좌우 넘김, 대화 페이지 세로 스크롤 중 좌우 넘김
- [ ] 개별 듣기: 설정의 음량·빠르기를 재생 중에 바꾸면 바로 반영
- [ ] 전체 듣기: 시퀀스 순서대로, 노드별 음량·빠르기·텀이 재생 중 바로 반영, 드래그 순서 변경
- [ ] **화면을 끄고 유닛 끝까지 재생** (텀 포함), 잠금화면·알림 컨트롤(재생/일시정지/이전/다음)
- [ ] 화면을 다시 켜면 현재 예문·줄 표시가 맞음, 홈 버튼으로 나가도 재생 계속
- [ ] 전화 수신·이어폰 분리 시 일시정지
- [ ] 삼성/샤오미 등 절전 정책에서 장시간 재생 (M4)
