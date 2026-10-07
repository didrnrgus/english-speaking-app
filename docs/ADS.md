# 배너 광고 (Unity LevelPlay)

> 2026-10-07 · Android 전용 · `com.unity.services.levelplay` (Unity Package Manager 이름 **Ads Mediation**) 9.5.1
> 앱 구현: [UNITY.md](UNITY.md)

## 1. 한눈에

- **세로 화면 맨 아래에 배너 하나**. 메인·레벨·유닛 화면 모두 같은 배너 (화면을 넘겨도 다시 요청하지 않음)
- 광고 SDK 는 **Unity LevelPlay** (옛 ironSource). Unity Ads 는 LevelPlay 안의 광고 네트워크 하나로 들어간다
  - LevelPlay = 여러 광고 회사(Unity Ads, AdMob, Meta, Pangle …)에 같은 자리를 경매로 파는 '미디에이션'. 네트워크를 늘릴수록 단가가 오른다
- 배너 자리는 **앱 UI 와 분리된 전용 띠**. 버튼·내비게이션 바(3버튼·제스처 바)와 겹치지 않는다
- 키를 넣기 전에는 기기에서 광고 자리 자체가 없다 (에디터에서는 회색 미리보기 상자만)

```
┌──────────────────────────┐
│ 상태 바 / 노치            │ ← Safe Area 밖
├──────────────────────────┤
│                          │
│  화면 (메인·레벨·유닛)     │ ← Content: 광고 띠 높이만큼 줄어든다
│  …                       │
│  [재생 컨트롤 / 미니 플레이어]│
├──────────────────────────┤
│  여백 12dp + 구분선        │ ← 앱 버튼과 광고 사이 간격 (실수 클릭 방지)
│ ┌──────────────────────┐ │
│ │   배너 (적응형 50~90dp) │ │ ← AdSlot (BannerAdSlot)
│ └──────────────────────┘ │
│  여백 4dp                 │
├──────────────────────────┤
│ ◁  ○  □  / ─── 제스처 바   │ ← 내비게이션 바: 숨겨진 전체 화면이어도 이 자리는 비워 둔다
└──────────────────────────┘
```

## 2. 처음 설정 (키 받기 → 넣기)

1. **LevelPlay 계정**: <https://platform.ironsrc.com> 가입 (Unity ID 로 로그인 가능)
2. **앱 추가**: Apps → Add App → Android, 패키지 `com.didrnrgus.englishspeaking`
   (아직 스토어에 없으면 'App not live yet' 으로 추가, 출시 후 스토어 연결)
3. **App Key** 복사 (앱 목록에 표시되는 문자열)
4. **배너 Ad Unit 만들기**: Ad Units → 배너 → Ad Unit ID 복사
   - 새로고침(refresh) 간격: **60초** 권장 (30초 미만 금지 — AdMob 등 정책 위반, 낮은 품질 노출)
5. `unity/Assets/Scripts/Ads/AdConfig.cs` 에 두 값 넣기

   ```csharp
   public static readonly string AndroidAppKey = "여기에 App Key";
   public static readonly string AndroidBannerAdUnitId = "여기에 배너 Ad Unit ID";
   ```

6. Unity 에서 열기
   - `Packages/manifest.json` 에 **Ads Mediation 9.5.1** 이 들어 있어 자동 설치된다
   - 메뉴 **Ads Mediation → Network Manager** 에서 **Unity Ads** 어댑터가 설치됐는지 확인 (9.x 는 기본 포함)
   - 의존성 해결: **Assets → Mobile Dependency Manager (또는 External Dependency Manager) → Android Resolver → Resolve**
     메뉴가 없으면 패키지가 띄우는 안내대로 리졸버를 설치한 뒤 다시 한다. 네트워크를 추가·변경할 때마다 Resolve
   - 셋업이 만든 파일(`Assets/LevelPlay/…`, `Assets/Plugins/Android/…` 의 aar·설정, `Packages/packages-lock.json`)은 커밋
7. 빌드 → 기기에서 `adb logcat -s Unity` 로 `[Ads] LevelPlay 초기화 완료, 배너 …dp` 확인

> 패키지가 없거나 키가 비어 있어도 앱은 그대로 컴파일·실행된다 (광고만 안 나옴).
> 광고 코드는 `EnglishApp.Ads` 어셈블리에 있고, 패키지가 있을 때만 `LEVELPLAY_INSTALLED` 가 켜진다 (asmdef version define).

## 3. 테스트할 때 — 내 광고를 누르면 안 된다

**개발자 본인이 실제 광고를 보거나 누르는 것은 무효 트래픽**이고, 반복되면 계정이 정지되고 수익이 회수된다.

- LevelPlay 대시보드 → **Test devices** 에 내 폰의 광고 ID(GAID) 를 등록 → 그 기기에는 테스트 광고가 나온다
  (GAID: 폰 설정 → Google → 광고 / 개인정보 보호 → 광고)
- 개발 빌드(Development Build)는 `is_test_suite` 를 켜고 `ValidateIntegration()` 을 호출한다
  → logcat 에서 어댑터 연동 상태 확인. 테스트 스위트 화면은 `LevelPlay.LaunchTestSuite()` 로 띄울 수 있다 (필요하면 추가)
- 가족·지인에게 "광고 눌러 줘" 금지 (이것도 무효 클릭)
- 내부 테스트 트랙 테스터 기기도 Test devices 에 등록해 두면 안전

## 4. 수익 설계

| 항목 | 결정 | 이유 |
|---|---|---|
| 형식 | **적응형(adaptive) 배너** | 화면 폭을 꽉 채워 고정 320x50 보다 노출 면적·단가(eCPM)가 높다. 높이는 기기에 맞춰 SDK 가 정함 (폭 411dp 폰 ≈ 64dp) |
| 위치 | 하단 고정, **모든 화면** | 학습 화면(유닛)에서 오래 머무르므로 노출 시간의 대부분이 여기서 나온다. 상단은 제목·뒤로·설정 버튼과 가까워 실수 클릭 위험 |
| 개수 | **앱 전체에 배너 1개** | 화면마다 새로 만들면 요청만 늘고 노출 시간이 짧아져 단가가 떨어진다. 하나를 계속 띄워 두고 새로고침은 SDK 에 맡김 |
| 새로고침 | 대시보드 60초 | 너무 짧으면 노출당 단가와 광고주 평가가 떨어진다 |
| 가려질 때 | 숨김 + 새로고침 정지 | 설정 팝업처럼 화면을 덮을 때. 안 보이는 노출은 무효 노출 → 네트워크가 단가를 깎는다 |
| 네트워크 | 처음엔 Unity Ads, 이후 **비딩 네트워크 추가** | 경쟁이 늘수록 단가 상승. 한국은 AdMob, Pangle, Mintegral, Liftoff, InMobi 등이 자주 쓰임. Network Manager 에서 설치 + 대시보드에서 각 네트워크 계정 연결 |
| app-ads.txt | **필수** | Play 스토어 등록 개발자 웹사이트 루트에 `app-ads.txt` (LevelPlay 대시보드가 내용 제공). 없으면 많은 광고주가 입찰하지 않는다. GitHub Pages 로 올려도 된다 |
| 전면·보상형 | 지금은 넣지 않음 | 듣기 앱이라 흐름을 끊는 전면 광고는 이탈을 부른다. 나중에 넣는다면 '유닛 완료 후' 처럼 자연스러운 쉼표에만 |
| 광고 제거 | 아직 없음 | `AdConfig.BannerEnabled` 하나로 끄도록 되어 있어 나중에 인앱결제와 연결하기 쉽다 |

## 5. 무효 트래픽(어뷰징) 방지 — 코드에 들어간 것

| 장치 | 위치 |
|---|---|
| 광고 전용 띠를 **처음부터** 잡아 둠 → 광고가 늦게 떠도 화면이 밀리지 않음 (밀린 버튼 대신 광고를 누르는 사고 방지) | `BannerAdSlot` |
| 네이티브 배너를 띠 안쪽 좌표(dp)에 정확히 배치, 앱 UI 와 겹침 없음 | `BannerAdSlot.BannerPositionDp` |
| 앱 버튼과 배너 사이 12dp 여백 + 구분선, 띠 배경은 버튼과 다른 차분한 색, 띠의 빈 곳을 눌러도 아래 화면에 터치가 새지 않음 | `BannerAdSlot`, `Theme.AdSlotBg` |
| 내비게이션 바(3버튼·제스처 바) 자리를 항상 비움 — 숨겨진 전체 화면에서 바가 잠깐 나타나도 배너·버튼이 깔리지 않음, 홈 제스처가 광고에 닿지 않음 | `DeviceScreen`, `SafeAreaFitter` |
| 팝업이 열려 있으면 숨김 + 새로고침 정지 | `AppRoot.OpenSettings` → `Ads.Block` |
| 코드에서 광고를 억지로 다시 불러오지 않음 (처음 불러오기 실패 때만 15초→30초→…최대 5분 간격 재시도) | `BannerAdSlot.OnLoadFailed` |
| **클릭 보호**: 30분 안에 3번 클릭되면 24시간 동안 배너를 내림 (실수 연타·장난·아이). 기기 시계를 되돌려도 최대 24시간 | `BannerAdSlot.ClickGuard`, 값은 `AdConfig.ClickGuard*` |
| 클릭을 유도하는 문구·화살표·보상 없음, 광고를 앱 콘텐츠처럼 꾸미지 않음 | (정책) |

운영에서 지킬 것: 위 3장 (내 광고 안 누르기), 새로고침 30초 미만 금지, 광고를 가리는 UI 를 새로 만들면 `App.Ads.Block("이름")` / `Unblock` 으로 감싸기.

## 6. 출시 전 체크

- [ ] Play Console → **앱 콘텐츠 → 광고: '광고 포함'**
- [ ] **데이터 보안(Data safety)**: 광고 ID·기기 식별자·앱 상호작용·대략적 위치 등 광고 SDK 가 수집하는 항목 신고
      (LevelPlay·Unity Ads 및 추가한 네트워크 각각의 Data safety 안내 참고)
- [ ] **개인정보처리방침 URL**: 광고 SDK(Unity LevelPlay/Unity Ads)가 광고 ID 등을 수집한다는 내용 + Unity 개인정보처리방침 링크
- [ ] **타깃 연령**: 13세 이상 (어린이 대상이 아니므로 가족 정책 광고 제한 없음)
- [ ] 빌드된 매니페스트에 `com.google.android.gms.permission.AD_ID` 가 있는지 (targetSdk 33+ 에서 광고 ID 사용에 필요, 보통 SDK 가 넣어 줌)
- [ ] **app-ads.txt** 게시
- [ ] **EU·영국 출시 시**: 동의(GDPR) 팝업 필요 — 인증된 CMP 를 붙이고 `LevelPlayPrivacySettings` 로 전달. 그 전까지는 한국 등 비EU 국가만 출시 권장

## 7. 기기 확인 (처음 빌드 때)

- [ ] 3버튼 내비게이션 / 제스처 내비게이션 둘 다: 배너가 바 위에 있고 겹치지 않음
- [ ] 배너가 회색 띠 안에 정확히 들어감 (위아래로 어긋나면 → `AdConfig.BannerOriginIsScreen` 을 true 로 바꿔 보기)
- [ ] 유닛 화면 재생 컨트롤·미니 플레이어가 배너와 겹치지 않고 여백이 있음
- [ ] 설정 팝업을 열면 배너가 사라지고, 닫으면 다시 나옴
- [ ] 비행기 모드로 시작 → 띠만 비어 있고 앱은 정상 → 인터넷 켜면 잠시 뒤 광고가 뜸
- [ ] 화면 끄고 재생 → 다시 켜도 배너 위치 그대로

## 8. 코드

```
unity/Assets/Scripts/
├─ Ads/                       (어셈블리 EnglishApp.Ads — LevelPlay 를 부르는 유일한 곳)
│  ├─ EnglishApp.Ads.asmdef   Unity.LevelPlay 참조, 패키지 9.0+ 있으면 LEVELPLAY_INSTALLED
│  ├─ AdConfig.cs             App Key, 배너 Ad Unit ID, 켜기/끄기, 클릭 보호 값
│  └─ LevelPlayBanner.cs      Init / 적응형 크기 / Create(dp 좌표) / Load·Show·Hide / 새로고침 정지·재개 / Destroy
├─ Core/DeviceScreen.cs       dp 밀도, 내비게이션 바 겹침 높이 (JNI)
└─ UI/Widgets/
   ├─ BannerAdSlot.cs         광고 띠: 자리 잡기, 배너 위치 계산, 초기화·재시도, 숨김, 클릭 보호
   └─ SafeAreaFitter.cs       Safe Area + 내비게이션 바 자리
```

- SDK API 가 바뀌어 컴파일 오류가 나면 `LevelPlayBanner.cs` 한 파일만 고치면 된다
- 검증: 에디터가 없는 환경이라 Unity 참조 DLL + TMP·LevelPlay 시그니처 스텁으로 컴파일 검사
  (기기+SDK / SDK 없음 / 에디터 3가지, 오류·경고 0). LevelPlay API 는 9.5.1 기준 문서·사용 예로 맞췄고 **실제 패키지로 컴파일·기기 실행은 아직**
