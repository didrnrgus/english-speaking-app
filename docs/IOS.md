# iOS 기기 설치 (테스트용)

> 2026-10-12 · Unity 6000.3.17f1 · iOS 15.0 이상 · **스토어 배포 아님 — 내 iPhone 에 설치해서 확인하는 용도**
> 앱 구현: [UNITY.md](UNITY.md) · 작업 기록: [WORKLOG.md](WORKLOG.md)

## 1. 무엇이 되어 있나

| 항목 | iOS 에서 |
|---|---|
| 화면·콘텐츠·설정 | Android 와 같은 코드 (Unity UI, 원격 예문·mp3 캐시) |
| **화면 꺼짐 재생** | 네이티브 플러그인 `Assets/Plugins/iOS/EnglishAudio/EsaAudio.mm` (AVAudioPlayer + 백그라운드 오디오 모드) |
| 잠금화면·제어 센터·이어폰 버튼 | 재생/일시정지, 이전/다음 = 예문 단위 이동 (Android 알림 컨트롤과 같은 동작) |
| 전화·알람 / 이어폰 분리 | 일시정지 (통화가 끝나고 iOS 가 허락하면 이어서 재생) |
| 노치·홈 인디케이터 | `Screen.safeArea` 로 피함 (Android 의 내비게이션 바 계산은 iOS 에선 0) |
| 배너 광고 | **표시 안 함** — 광고 코드는 Android 에서만 켜진다 (`LevelPlayBanner`). LevelPlay 패키지 자체는 빌드에 들어감 (CocoaPods 필요, 3장) |
| 뒤로 가기·알림 권한 | iOS 에는 없음 (Android 전용 코드는 `#if UNITY_ANDROID` 로 빠진다) |

빌드 설정은 자동으로 맞춰진다 (`Assets/Editor/IosBuildSettings.cs`, 빌드 대상이 iOS 일 때 / 메뉴 `Tools > English App > Apply iOS Settings`):

- 번들 ID `com.yang.projectse` (Android 패키지명과 같게), 최소 iOS 15.0
- Background Modes: **Audio** (`Info.plist` `UIBackgroundModes=audio` 는 Xcode 프로젝트 생성 직후에도 한 번 더 확인)
- 자동 서명 켬 → Xcode 에서 **Team 만 고르면 된다**
- 스크립팅 심볼을 Android 와 같게 (`LEVELPLAY_DEPENDENCIES_INSTALLED;LETAI_PARAFORM`)

## 2. 준비물

| 무엇 | 비고 |
|---|---|
| **Mac + Xcode** | 3장 A·B 경로. **Mac 이 없으면 3장 C** (GitHub Actions 로 빌드 → Windows 에서 Sideloadly 로 설치) |
| Unity **iOS Build Support** 모듈 | Unity Hub → Installs → 6000.3.17f1 → ⚙ → Add modules → iOS Build Support (빌드하는 PC 에) |
| CocoaPods | Mac 에서 `brew install cocoapods` (또는 `sudo gem install cocoapods`). LevelPlay 의 iOS SDK 를 받는 데 필요 |
| Apple ID | 무료 계정으로 충분 (Xcode → Settings → Accounts 에 추가). 유료(연 $99) 개발자 계정이면 1년 유지 |
| iPhone + 케이블 | iOS 16 이상은 **개발자 모드**를 켜야 설치된다 (4장) |

> 무료 계정 제한: 설치한 앱이 **7일 뒤 열리지 않는다** (다시 Run 하면 7일 연장), 동시에 설치 가능한 개발 앱 3개.

## 3. 빌드

### A. Mac 에서 Unity 로 빌드 (가장 간단)

1. Mac 에 Unity 6000.3.17f1 + iOS Build Support 설치, 레포 클론 → Unity Hub 에서 `Project_SE/` 열기
2. File → Build Profiles → **iOS** → Switch Platform
   - 콘솔에 `[EnglishApp] iOS 설정: 번들 ID com.yang.projectse, 백그라운드 오디오, 자동 서명 …` 로그가 나오면 설정 완료
3. **Build** → 출력 폴더 `Project_SE/Build/iOS` (git 에 안 올라감)
   - CocoaPods 가 설치되어 있으면 빌드 끝에 `pod install` 이 자동으로 돌고 `Unity-iPhone.xcworkspace` 가 생긴다
4. 4장으로

### B. Windows 에서 Xcode 프로젝트만 만들고 Mac 으로 옮기기

1. Windows Unity 에 iOS Build Support 설치 → Build Profiles → **iOS** → Switch Platform → Build → `Project_SE/Build/iOS`
2. 폴더를 통째로 Mac 으로 복사 (zip 으로 묶어 옮기면 실행 권한이 유지된다)
3. Mac 터미널에서:
   ```sh
   cd Build/iOS
   pod install          # Podfile 이 있으면. Windows 에서는 Unity 가 이 단계를 못 한다
   open Unity-iPhone.xcworkspace
   ```
4. 4장으로

> 반드시 **`.xcworkspace`** 를 연다 (`.xcodeproj` 로 열면 LevelPlay pod 가 링크되지 않아 빌드가 실패한다).

### C. Mac 없이: GitHub Actions + Sideloadly (Windows PC 에서 설치)

워크플로 `.github/workflows/build-ios.yml` 이 Linux 에서 Unity 로 Xcode 프로젝트를 만들고(`Editor/CiBuild.cs`),
macOS 러너에서 `pod install` → **서명 없이** 빌드해 `.ipa` 를 Release 에 올린다. 서명은 설치할 때 Sideloadly 가 내 Apple ID 로 한다.

1. **시크릿 등록** (처음 한 번) — 레포 Settings → Secrets and variables → Actions → New repository secret.
   Unity 는 라이선스 없이는 실행되지 않아서 개인 Unity 계정의 Personal 라이선스가 필요하다.
   1. Unity Hub 에 개인 Unity ID 로 로그인 → Settings → Licenses → Add → **Get a free personal license**
   2. 라이선스 파일을 메모장으로 연다: `C:\ProgramData\Unity\Unity_lic.ulf`
   3. 시크릿 3개: `UNITY_LICENSE` = 파일 내용 전체, `UNITY_EMAIL` / `UNITY_PASSWORD` = Unity ID 로그인 정보
   - Personal 라이선스는 주기적으로 만료된다. 빌드가 라이선스 오류로 실패하면 2~3 을 다시.
2. **빌드** — main 에 `Project_SE/` 나 `content/` 변경을 푸시하면 자동으로 돈다. 바로 돌리려면 Actions → **Build iOS (unsigned ipa)** → Run workflow.
   Unity 내보내기 + Xcode 빌드로 30분~1시간 걸린다.
3. **받기** — 레포 **Releases** 에서 `EnglishSpeaking-iOS-v0.1.2-b<빌드번호>.ipa` 다운로드 (태그 `ios-v…`, Pre-release 로 표시됨).
   버전은 ProjectSettings 의 `bundleVersion`, 빌드 번호는 워크플로 실행 번호.
4. **Windows 준비** (처음 한 번)
   - **iTunes**, **iCloud** 를 Apple 사이트에서 받은 버전으로 설치 (MS Store 버전은 Sideloadly 가 기기를 못 찾는 경우가 있다)
   - [Sideloadly](https://sideloadly.io) 설치
   - iPhone 을 USB 로 연결 → iPhone 에서 "이 컴퓨터를 신뢰" → 설정 → 개인정보 보호 및 보안 → **개발자 모드** 켜기 (재부팅)
5. **설치** — Sideloadly 에 `.ipa` 를 끌어다 놓고, Apple ID 입력 → Start
   - 설치 후 처음 실행 때 "신뢰하지 않는 개발자" → 설정 → 일반 → **VPN 및 기기 관리** → 내 Apple ID → 신뢰
6. **7일마다 만료** (무료 Apple ID) — 같은 Apple ID 로 다시 설치하면 데이터는 유지된 채 7일 연장된다.

> GitHub 무료 분량: 공개 레포는 무제한, 비공개 레포는 월 2,000분이고 macOS 는 10배로 계산된다 (Xcode 빌드 한 번 ≈ 15~25분 → 150~250분 소진).

## 4. Xcode 에서 기기에 설치

1. 왼쪽 프로젝트 → 타깃 **Unity-iPhone** → **Signing & Capabilities**
   - Automatically manage signing 체크 → **Team** 에서 내 Apple ID(Personal Team) 선택
   - "Bundle Identifier is not available" 이 나오면 `com.yang.projectse.dev` 처럼 뒤에 붙여서 바꾼다
   - **Background Modes → Audio, AirPlay, and Picture in Picture** 가 체크돼 있는지 확인 (없으면 + Capability 로 추가)
2. iPhone 연결 → 상단 실행 대상에서 내 iPhone 선택
3. iPhone: 설정 → 개인정보 보호 및 보안 → **개발자 모드** 켜기 (재부팅)
4. Xcode **Run (⌘R)**
5. 처음 실행 때 "신뢰하지 않는 개발자" → iPhone 설정 → 일반 → **VPN 및 기기 관리** → 내 Apple ID → 신뢰 → 다시 실행

## 5. 재생 구조 (iOS)

```
C# AudioPlayer ──(DllImport "__Internal": EsaAudio_Play / Pause / Resume / Stop / SeekStep / SetRepeat / UpdateNode / GetState)
                      │  Scripts/Audio/IosAudioBackend.cs
                      ▼
EsaEngine (EsaAudio.mm, 메인 스레드)
  ├─ step 마다: 음성 클립(AVAudioPlayer, enableRate) → 텀(무음 WAV 반복 재생 + 타이머) → 다음 step
  ├─ AVAudioSession: Playback / SpokenAudio  ← 무음 스위치를 켜도 들리고, 화면이 꺼져도 계속
  └─ MPRemoteCommandCenter / MPNowPlayingInfoCenter  ← 잠금화면·제어 센터 (제목 = 영어, 아티스트 = 유닛, 앨범 = 해석)
```

- Android `SequenceEngine` 과 같은 규칙: 음량·빠르기는 노드 값, 재생 중 바꾸면 즉시 반영, 텀 변경은 진행 중 텀의 남은 시간도 다시 계산
- 텀 동안에도 무음을 재생한다 — 소리를 멈추면 iOS 가 몇 초 뒤 백그라운드 앱을 정지시키기 때문
- 빠르기는 `AVAudioPlayer.rate` (음높이 유지), 범위 x0.5~x2.0 — 앱 설정 범위와 같다
- 깨진 mp3 는 건너뛰고, 전부 깨졌으면 끝낸다
- Unity 는 0.1초마다 `EsaAudio_GetState()` 를 읽는다 (Android 와 같은 JSON)
- Unity 가 포커스가 바뀔 때 오디오 세션을 자기 값(Ambient)으로 되돌릴 수 있어, 재생 중이면 백그라운드 진입·복귀 때 Playback 으로 다시 맞춘다
- 앱을 앱 전환기에서 위로 밀어 종료하면 재생도 멈춘다 (iOS 정책, 잠금화면 표시도 같이 사라짐)

## 6. 검증 상태

**Mac·Xcode 가 없는 Windows 환경에서 작성했다 — 아직 빌드·기기 확인 전.** 처음 Xcode 빌드에서 오류가 나면 그 메시지로 고친다.

### 기기 테스트 체크리스트

- [ ] 설치·실행, 폰트·글래스 UI 표시, 노치·홈 인디케이터에 UI 가 가리지 않음
- [ ] 개별 듣기 (무음 스위치 켠 상태에서도 소리 남), 재생 중 음량·빠르기 변경 즉시 반영
- [ ] 전체 듣기: 시퀀스 순서·노드별 음량·빠르기·텀
- [ ] **화면을 잠그고 유닛 끝까지 재생** (텀 포함, 특히 텀 5초에서 끊기지 않는지)
- [ ] 잠금화면·제어 센터: 제목·유닛 표시, 재생/일시정지/이전/다음
- [ ] 홈으로 나갔다 돌아오면 현재 예문·줄 표시가 맞음, 돌아온 뒤 다시 잠가도 계속 재생 (세션 재설정 확인)
- [ ] 전화·알람 → 일시정지 → 끝나면 이어서, 이어폰 분리 → 일시정지
- [ ] 반복 켜고 끝까지 → 처음부터 다시
- [ ] 다른 앱(음악) 재생 중에 듣기 시작 → 음악이 멈춤, 앱에서 정지하면 음악 앱이 다시 재생 가능

## 7. 문제 해결

| 증상 | 해결 |
|---|---|
| `IronSource/IronSource.h file not found` 등 LevelPlay 헤더 오류 | `pod install` 을 안 했거나 `.xcodeproj` 로 열었다 → 3장 B-3, `.xcworkspace` 로 열기 |
| `pod install` 이 `CDN: trunk` 오류 | `pod repo update` 후 다시 |
| Signing 오류 "No Account for Team" | Xcode → Settings → Accounts 에 Apple ID 추가 후 Team 다시 선택 |
| 화면을 잠그면 소리가 멈춤 | Signing & Capabilities 의 Background Modes → Audio 확인. Xcode 콘솔에 `[EsaAudio] setCategory failed` 가 있는지 |
| `Undefined symbol: _EsaAudio_Play` | `EsaAudio.mm` 이 빌드에 안 들어감 → Unity 에서 파일 선택 → Inspector 에서 iOS 플랫폼 체크 확인 |
