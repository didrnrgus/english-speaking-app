# 영어 스피킹 학습 앱 — 초안 기획서 (v0.1)

> 작성일: 2026-10-07 · 플랫폼: **Android 전용** · 엔진: **Unity** (+ Android 네이티브 오디오 플러그인) · 상태: 초안 (다음 단계: GitHub 레포 파서로 개발 고도화)
>
> v0.2 변경: 개발 엔진을 네이티브(Kotlin/Compose) → **Unity** 로 변경. 화면 꺼짐 재생은 Android 네이티브 플러그인이 담당 (6장, 8장, 11장).

---

## 1. 한 줄 요약

**"듣고, 따라 말하고, 화면을 꺼놓고도 계속 들을 수 있는" 레벨별 영어 말하기 · 대화 학습 앱.**
왕초보 단어 수준부터 1분 말하기/대화까지 10개 스테이지, 스테이지당 예문 5개를 영어/한국어 텍스트 + 영어/한국어 TTS 음성으로 제공한다.

## 2. 핵심 요구사항

| # | 요구사항 | 비고 |
|---|---|---|
| R1 | 난이도 5단계 × 2트랙(말하기 / 대화하기) = **10개 스테이지** | 아래 3장 |
| R2 | 스테이지당 예문 **5개** → 총 **50개** (초안 예문은 부록 A에 포함) | |
| R3 | 예문마다 **영어 문장 / 한국어 해석 / 영어 음성 / 한국어 음성** | 음성은 TTS |
| R4 | 음성 **연속 재생** (플레이리스트처럼 자동으로 다음 예문) | |
| R5 | **화면을 꺼도 재생 유지** (잠금화면/알림창 컨트롤 포함) | **핵심 기능** |
| R6 | Android 전용 출시 | iOS는 범위 밖 |

> 📝 해석 메모: "각 단계 총 10개 / 5개씩"을 **10개 스테이지 × 스테이지당 5개 예문 = 50개**로 해석했다. 스테이지당 예문 수를 늘리고 싶으면 데이터만 추가하면 된다(구조 변경 없음).

## 3. 학습 구조 (10 스테이지)

### 트랙 A. 말하기 (Speaking) — 문장 단위

| 스테이지 | 이름 | 분량 기준 | 예시 |
|---|---|---|---|
| S1 | 왕초보 말하기 | 1~3 단어 | Thank you. |
| S2 | 초급 말하기 | 4~6 단어 한 문장 | How much is this? |
| S3 | 중급 말하기 | 8~12 단어 한 문장 | Could you tell me how to get to the subway station? |
| S4 | 중상급 말하기 | 2~3 문장 (약 20~35 단어) | 짧은 상황 설명 |
| S5 | **1분 말하기** | 8~10 문장 (약 90~110 단어, 약 1분) | 자기소개, 하루 일과 등 |

### 트랙 B. 대화하기 (Dialogue) — A/B 두 화자

| 스테이지 | 이름 | 분량 기준 | 예시 |
|---|---|---|---|
| D1 | 왕초보 대화하기 | 2턴, 1~3 단어씩 | Thank you. / You're welcome. |
| D2 | 초급 대화하기 | 4턴 | 카페 주문, 길 묻기 |
| D3 | 중급 대화하기 | 6턴 | 식당, 호텔, 약국 |
| D4 | 중상급 대화하기 | 8턴 (약 30초) | 면접, 환불, 공항 |
| D5 | **1분 동안 대화하기** | 12턴 (약 90~120 단어, 약 1분) | 여행 계획, 집 구하기 |

### 학습 흐름 (예문 1개 기준)

```
[영어 문장 듣기] → (선택) 따라 말하기 간격 → [한국어 해석 듣기] → 다음 예문
```

재생 패턴은 사용자가 선택할 수 있게 한다.

| 패턴 | 순서 | 용도 |
|---|---|---|
| 영→한 | 영어 → 한국어 | 기본 |
| 한→영 | 한국어 → 영어 | 영작 반사 훈련 |
| 영×2→한 | 영어 → 영어 → 한국어 | 귀 익히기 |
| 쉐도잉 | 영어 → **무음 구간(따라 말하기)** → 영어 | 말하기 연습 |
| 영어만 | 영어만 반복 | 집중 암기 |

## 4. 콘텐츠 데이터 모델

### 4-1. 스테이지 / 예문 (말하기)

```json
{
  "stage": "S2",
  "version": 1,
  "track": "speaking",
  "title": "초급 말하기",
  "items": [
    {
      "id": "S2-01",
      "en": "I'm from Korea.",
      "ko": "저는 한국에서 왔어요.",
      "audio": {
        "en": "audio/en/S2-01.mp3",
        "ko": "audio/ko/S2-01.mp3"
      }
    }
  ]
}
```

### 4-2. 대화

```json
{
  "stage": "D3",
  "version": 1,
  "track": "dialogue",
  "title": "중급 대화하기",
  "items": [
    {
      "id": "D3-01",
      "topic": "식당",
      "turns": [
        {
          "speaker": "A",
          "en": "Welcome! Do you have a reservation?",
          "ko": "어서 오세요! 예약하셨나요?",
          "audio": { "en": "audio/en/D3-01_t1.mp3", "ko": "audio/ko/D3-01_t1.mp3" }
        },
        {
          "speaker": "B",
          "en": "No, we don't. Do you have a table for two?",
          "ko": "아니요. 두 명 자리 있나요?",
          "audio": { "en": "audio/en/D3-01_t2.mp3", "ko": "audio/ko/D3-01_t2.mp3" }
        }
      ]
    }
  ]
}
```

### 4-3. 규칙

- 오디오 파일명은 `id` 기준 (`S2-01.mp3`, 대화는 `D3-01_t1.mp3`)
- 콘텐츠는 **JSON 파일로 분리**(앱 코드와 독립) → 이후 GitHub 레포에서 파싱/검증/빌드 가능
- 스테이지마다 파일 1개, 파일마다 `version`(1 이상 정수) — 내용을 고치면 1 올린다
- `index.json` 이 스테이지 목록(`stage`, `track`, `file`)을 가진다. 앱은 이 목록에 있는 파일만 받는다

### 4-4. 원격 콘텐츠 (GitHub Pages)

배포 후에도 예문을 고칠 수 있도록 예문 JSON 원본은 GitHub Pages 레포에 둔다.

- 원본: `didrnrgus/didrnrgus.github.io` 레포의 `project_speaking_english/` (JSON 구조는 이 레포의 `content/` 와 동일, mp3는 `audio/` — 원격에만 둔다)
- URL: `https://didrnrgus.github.io/project_speaking_english/index.json`, `.../speaking/s1.json`, `.../audio/en/S1-01.mp3` 등
- `audio` 경로는 콘텐츠 폴더 기준 상대 경로 → 앱은 `index.json` 과 같은 기준 URL로 mp3를 받는다
- 앱 로드 순서: 원격 `index.json` → 각 스테이지 파일 다운로드 → 검사 통과 시 캐시에 저장 → 실패/오프라인이면 **캐시 → 번들** 순으로 사용. mp3도 받아서 캐시해 두고 재생
- 번들: 이 레포의 `content/` (JSON만)를 `StreamingAssets/content/` 로 복사 (첫 실행·오프라인용). 빌드 전에 원격 원본과 맞춘다
- 배포 반영: Pages CDN 캐시로 최대 10분 정도 지연
- mp3는 Git LFS 없이 일반 커밋 (Pages는 LFS 파일을 서빙하지 않음). 64kbps 모노 기준 전체 약 10MB(추정)
- 문장을 고치면 `generate_tts.py` 가 `audio/tts_manifest.json` 과 비교해 바뀐 mp3만 다시 만든다
- 화자 A/B는 **서로 다른 목소리**로 합성 (예: A=여성, B=남성)

## 5. 오디오(TTS) 설계

### 5-1. 방식 비교

| 방식 | 장점 | 단점 |
|---|---|---|
| **A. 사전 생성 mp3 (앱에 번들)** | 음질 일정, 기기 영향 없음, 오프라인, **백그라운드 재생 구현이 가장 단순** | 앱 용량 증가(50개 × 2언어 ≈ 100개 파일, 작음), 콘텐츠 추가 시 재생성 필요 |
| B. 기기 내장 TTS (`TextToSpeech`) | 용량 0, 어떤 문장이든 즉시 재생 | 기기·엔진마다 음질/속도 편차, 한국어/영어 음성 데이터 미설치 가능, MediaSession 연동이 번거로움 |
| C. 실시간 클라우드 TTS | 고음질, 유연 | 네트워크·비용·지연, 백그라운드 안정성 낮음 |

**추천: A를 기본으로, B는 사용자 추가 문장용 폴백(후순위).**
이유: "화면 끄고 연속 재생"이 핵심인데, 이미 만들어진 오디오 파일은 일반 미디어 플레이어(ExoPlayer)로 재생할 수 있어 잠금화면 컨트롤·블루투스·오디오 포커스 처리가 표준 방식으로 해결된다.

### 5-2. 음성 생성 파이프라인 (오프라인 배치)

> 진행 상황·엔진 선택 이유·생성 절차는 [AUDIO.md](AUDIO.md)에 기록한다. (2026-10-07 mp3 370개 생성·배포 완료)

엔진: **Supertonic-3** (Supertone, 로컬 ONNX 실행, 무료). 영어·한국어를 한 모델로 생성. 코드 MIT / 모델 OpenRAIL-M (상업적 이용 가능, 사용 제한 조항 준수)

1. Pages 레포 `project_speaking_english/` 의 JSON 읽기 (`tools/generate_tts.py --content <폴더>`)
2. 각 `en` / `ko` 텍스트 합성 → wav → mp3(64kbps 모노) 저장
   - 목소리: 말하기 트랙 F1, 대화 A=F1(여성) / B=M1(남성). 영어·한국어 같은 목소리
   - 속도 1.0 고정, 빠르게/느리게 듣기는 앱의 전역 재생 속도 설정으로 조절 (6-4)
3. `audio/tts_manifest.json` 에 (문장, 목소리, 설정) 기록 → 문장이 바뀐 파일만 재생성
4. `validate_content.py --content <폴더> --audio` 로 경로·파일 검증 후 Pages 레포에 커밋 (LFS 사용 안 함)
5. 앱은 원격 mp3를 받아 캐시 → 플러그인의 ExoPlayer가 캐시 파일(또는 URL)로 재생

## 6. 연속 재생 & 화면 꺼짐 재생 (핵심 기능)

### 6-1. 구조

```
Unity (C#, UI)
   │  AndroidJavaObject 호출: setPlaylist / play / pause / next / setSpeed ...
   ▼
Android 네이티브 플러그인 (Assets/Plugins/Android)
   MediaController  ←→  MediaSessionService (Foreground Service)
                              └─ ExoPlayer (플레이리스트)
                                    ├─ 영어 mp3
                                    ├─ 무음 구간 (SilenceMediaSource)
                                    └─ 한국어 mp3
```

> ⚠️ **왜 플러그인이 필요한가:** Unity 앱은 화면이 꺼지거나 앱이 백그라운드로 가면 플레이어 루프가 멈추고 `AudioSource` 재생도 멈춘다.
> 그래서 **재생은 전부 네이티브 서비스가 담당**하고, Unity는 화면·학습 UI와 "무엇을 재생할지" 지시만 맡는다.

- **Unity ↔ 플러그인 역할 분담**
  - Unity: 콘텐츠 JSON 로드, 화면(UI), 설정/진행률 저장, 플레이리스트 구성 후 플러그인에 전달
  - 플러그인: 실제 재생, 포그라운드 서비스, 알림/잠금화면 컨트롤, 오디오 포커스
  - 플러그인 → Unity 알림(현재 예문 변경 등)은 `UnitySendMessage` 또는 `AndroidJavaProxy` 콜백 사용
  - 화면이 꺼진 동안 Unity는 멈춰 있으므로 콜백을 놓칠 수 있다 → **앱 복귀(`OnApplicationPause(false)`) 시 플러그인에서 현재 상태(예문 인덱스, 재생 여부)를 조회해 UI 동기화**
- **Media3 `MediaSessionService`** + ExoPlayer 사용 → 포그라운드 서비스, 알림 컨트롤, 잠금화면 컨트롤, 블루투스/이어폰 버튼을 표준으로 지원
- 선택한 스테이지의 예문을 `MediaItem` 목록(플레이리스트)으로 구성해 **서비스에서 재생** (Activity가 꺼져도 유지)
- "따라 말하기" 간격은 **무음 `MediaItem`(SilenceMediaSource 등)을 사이에 삽입** → 타이머/Handler에 의존하지 않아 Doze 환경에서도 안정적

### 6-2. 구현 체크리스트

- [ ] `foregroundServiceType="mediaPlayback"` 선언 (Android 14+ 필수)
- [ ] 권한: `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PLAYBACK`, `POST_NOTIFICATIONS`(Android 13+ 런타임 요청)
- [ ] 알림 컨트롤: 이전 / 재생·일시정지 / 다음
- [ ] 오디오 포커스 처리 (전화·다른 앱 재생 시 일시정지/복귀)
- [ ] 이어폰 분리 시 자동 일시정지 (`handleAudioBecomingNoisy`)
- [ ] `setWakeMode(C.WAKE_MODE_LOCAL)` 등 재생 중 CPU 유지 검토
- [ ] 재생 상태 저장(스테이지, 예문 인덱스, 패턴) → 앱 재시작 시 이어듣기
- [ ] 수면 타이머(15/30/60분), 반복(1개 / 스테이지 전체), 셔플
- [ ] 전역 재생 속도 (x1.5 / x1.25 / x1.0 / x0.75 / x0.5) — 6-4
- [ ] 제조사별 절전 정책(삼성/샤오미 등) 테스트 및 안내 문구 준비

### 6-3. 대화 트랙 재생 방식

- 기본: A→B→A→B… 순서대로 영어 재생 후 한국어 해석 재생
- 옵션: **한 턴씩(영어→한국어)** / **대화 전체 영어 → 전체 한국어**
- 롤플레이 모드(후순위): 내 차례(예: B)는 **무음 + 화면 자막만** 표시해 직접 말하게 함

### 6-4. 재생 속도 (전역 설정)

- **위치:** 설정 팝업 안의 드롭다운 1개. 플레이어 화면에는 따로 두지 않는다
- **항목 (이 순서):** `x 1.5` / `x 1.25` / `x 1.0` / `x 0.75` / `x 0.5` — 기본값 `x 1.0`
- **전역:** 트랙·스테이지·영어/한국어 구분 없이 모든 재생에 같은 속도
- **저장:** PlayerPrefs `playback_speed` (float). 앱을 다시 켜도 유지
- **적용:** 바꾸는 즉시 플러그인 `setSpeed(float)` → ExoPlayer `setPlaybackSpeed()` (음높이 유지). 재생 중이면 바로 반영되고,
  서비스가 다시 시작돼도 유지되도록 플레이리스트를 넘길 때 현재 값도 같이 넘긴다
- **따라 말하기 간격:** 무음 구간도 재생 속도의 영향을 받으므로 `간격 × 속도` 길이로 넣는다 → 어떤 속도에서도 설정한 초(2/3/5초)만큼 쉰다
- **에디터 대체 재생(`AudioSource`):** `pitch` 로 속도를 바꾸면 음높이도 같이 바뀐다 → 에디터에서는 확인용으로만 쓰고 실제 품질은 기기에서 확인
- mp3 는 1.0 속도로 한 벌만 만든다 (속도별로 따로 만들지 않음)

## 7. 화면 구성 (IA)

```
홈
├─ 트랙 선택: [말하기] [대화하기]
├─ 스테이지 목록 (S1~S5 / D1~D5) — 진행률 표시
│    └─ 스테이지 상세: 예문 5개 리스트 (영어/한국어, 개별 재생 버튼)
├─ 플레이어 (하단 미니 플레이어 → 전체 화면)
│    ├─ 현재 문장 (영어 크게, 한국어 작게, 가림 토글)
│    ├─ 재생/이전/다음, 반복, 재생 패턴
│    └─ 수면 타이머
└─ 설정 (팝업 — 어느 화면에서든 톱니바퀴로 열기)
     ├─ 재생 속도 드롭다운: x1.5 / x1.25 / x1.0 / x0.75 / x0.5 (전역, 6-4)
     ├─ 재생 패턴 기본값, 따라 말하기 간격(2/3/5초)
     ├─ 텍스트 크기, 다크 모드
     ├─ 백그라운드 재생 안내 (배터리 최적화)
     └─ 정보: "음성은 AI(TTS)로 생성되었습니다" 고지, 오픈소스 라이선스
```

## 8. 기술 스택 (제안)

| 영역 | 선택 |
|---|---|
| 엔진 / UI | **Unity** (C#, uGUI) — 기존 프로젝트와 같은 Unity 6000.3.17f1 기준 |
| 오디오 | Android 네이티브 플러그인 (Java/Kotlin) + AndroidX **Media3** (ExoPlayer + MediaSessionService) |
| Unity ↔ 네이티브 | `AndroidJavaObject` / `AndroidJavaProxy` |
| 로컬 저장 | PlayerPrefs 또는 JSON 파일 (설정/진행률) |
| 콘텐츠 | 원격 `didrnrgus.github.io/project_speaking_english/` (JSON + mp3 원본) + 번들 `Assets/StreamingAssets/content/**` (JSON 폴백) |
| 콘텐츠 빌드 도구 | Python — JSON 검증 + TTS 생성(Supertonic-3) + 경로 검증 |
| 최소 SDK | Android 8.0 (API 26) 이상 권장 (확정 필요) |

## 9. 로드맵

| 단계 | 내용 |
|---|---|
| **M0 — 콘텐츠** | 예문 50개 확정(부록 A), JSON 스키마 확정, TTS 생성 스크립트 |
| **M1 — 프로토타입** | Unity 프로젝트 생성, 스테이지 목록 → 예문 리스트 → 단일 예문 재생 (에디터에서는 `AudioSource` 로 확인) |
| **M2 — 핵심** | Android 플러그인(Media3 서비스) 연동 → 연속 재생 + 화면 꺼짐 재생 + 알림 컨트롤 |
| **M3 — 학습 기능** | 재생 패턴(쉐도잉 등), 반복, 전역 재생 속도(설정 팝업), 수면 타이머, 진행률 |
| **M4 — 출시 준비** | 기기별 백그라운드 테스트, 아이콘/스토어 자료, 비공개 테스트 |
| **M5 — 확장** | 예문 추가, 롤플레이 모드, 음성 인식 발음 체크(후순위) |

## 10. 리스크 / 오픈 이슈

1. **TTS 음성 라이선스** — Supertonic-3 모델은 OpenRAIL-M (상업 이용 가능, 로열티 없음, 생성 음성에 대한 권리 주장 없음).
   단 부록 A (e)항에 따라 **AI 생성 음성임을 고지해야 함** → 앱 설정 '정보'와 스토어 설명에 문구 표기 (7장). 출시 전 라이선스 전문 재확인
2. **제조사별 백그라운드 제한** — 일부 기기에서 서비스 종료 가능 → 실기기 테스트, 안내 화면
3. **1분 말하기/대화 문장 품질** — 원어민 감수 여부 결정 (AI 초안 → 검수 권장)
4. **수익 모델 미정** — 무료 / 광고 / 유료 스테이지 해제 (초안 단계에서는 미결정)
5. **원격 콘텐츠** — 예문 JSON·mp3 모두 GitHub Pages(4-4). 첫 실행이 오프라인이면 음성이 없음 → 필요하면 기본 스테이지 mp3만 번들 검토
6. **Unity ↔ 네이티브 플러그인 연동** — 플러그인은 Unity 에디터에서 동작하지 않으므로 화면 꺼짐 재생은 실기기에서만 테스트 가능. 에디터용 대체 재생(`AudioSource`)을 같은 인터페이스 뒤에 둔다

## 11. 다음 단계 — GitHub 레포 개발 시 제안 구조

```
repo/
├─ unity/                    # Unity 프로젝트 (Unity Hub로 생성)
│   └─ Assets/
│       ├─ Scripts/          # C# (UI, 콘텐츠 로드, 플레이어 인터페이스)
│       ├─ Plugins/Android/  # 네이티브 오디오 플러그인 (Media3 서비스)
│       └─ StreamingAssets/  # content/ 복사본 (JSON)
├─ content/                   # 번들 복사본 (원본은 Pages 레포 project_speaking_english/)
│   ├─ index.json
│   ├─ speaking/s1.json ... s5.json
│   └─ dialogue/d1.json ... d5.json
├─ tools/
│   ├─ validate_content.py   # 스키마/필드 누락/id 중복 검사
│   └─ generate_tts.py       # JSON → mp3 생성 (Supertonic-3) → Pages 레포 audio/
└─ docs/PLAN.md              # 이 문서
```

---

# 부록 A. 예문 초안 (10 스테이지 × 5개 = 50개)

> 모든 예문은 초안이며 원어민 감수를 권장한다. 이름·지명은 중립적인 예시다.

## A-1. 트랙 A: 말하기

### S1. 왕초보 말하기 (1~3 단어)

| ID | English | 한국어 |
|---|---|---|
| S1-01 | Hello. | 안녕하세요. |
| S1-02 | Thank you. | 감사합니다. |
| S1-03 | I'm sorry. | 죄송합니다. |
| S1-04 | Nice to meet you. | 만나서 반갑습니다. |
| S1-05 | Excuse me. | 실례합니다. / 저기요. |

### S2. 초급 말하기 (4~6 단어)

| ID | English | 한국어 |
|---|---|---|
| S2-01 | I'm from Korea. | 저는 한국에서 왔어요. |
| S2-02 | I like coffee very much. | 저는 커피를 아주 좋아해요. |
| S2-03 | Where is the bathroom? | 화장실이 어디예요? |
| S2-04 | I want some water, please. | 물 좀 주세요. |
| S2-05 | How much is this? | 이거 얼마예요? |

### S3. 중급 말하기 (8~12 단어)

| ID | English | 한국어 |
|---|---|---|
| S3-01 | I usually wake up at seven and have breakfast at home. | 저는 보통 7시에 일어나서 집에서 아침을 먹어요. |
| S3-02 | Could you tell me how to get to the subway station? | 지하철역에 어떻게 가는지 알려주시겠어요? |
| S3-03 | I'm learning English because I want to travel abroad. | 저는 해외여행을 하고 싶어서 영어를 배우고 있어요. |
| S3-04 | It was raining this morning, so I took an umbrella with me. | 오늘 아침에 비가 와서 우산을 가져갔어요. |
| S3-05 | Can I have a cup of hot coffee and a piece of cake? | 따뜻한 커피 한 잔과 케이크 한 조각 주시겠어요? |

### S4. 중상급 말하기 (2~3 문장)

| ID | English | 한국어 |
|---|---|---|
| S4-01 | I work at a small company in Seoul. I usually commute by subway, and it takes about forty minutes. | 저는 서울에 있는 작은 회사에서 일해요. 보통 지하철로 출근하는데, 약 40분 걸려요. |
| S4-02 | Last weekend, I went hiking with my friends. The weather was great, and the view from the top was amazing. | 지난 주말에 친구들과 등산을 갔어요. 날씨가 좋았고, 정상에서 보는 경치가 정말 멋졌어요. |
| S4-03 | I'd like to book a table for two tonight. Do you have any seats available around seven? | 오늘 밤 두 명 자리를 예약하고 싶어요. 7시쯤 자리가 있나요? |
| S4-04 | I've been studying English for three months. It's difficult, but I enjoy it more every day. | 저는 3개월 동안 영어를 공부해 왔어요. 어렵지만 매일 조금씩 더 즐거워요. |
| S4-05 | My favorite season is autumn. The air is cool, the leaves change color, and it's perfect for walking. | 제가 가장 좋아하는 계절은 가을이에요. 공기가 시원하고 단풍이 들어서 산책하기 딱 좋아요. |

### S5. 1분 말하기 (약 90~110 단어)

**S5-01. 자기소개**

- EN: Hello, everyone. My name is Minjun, and I'm thirty years old. I live in Seoul, the capital of Korea. I work as a software developer at a small company, and I really enjoy my job because I can create new things every day. In my free time, I like playing video games and watching movies. I also enjoy cooking simple meals on weekends. Recently, I started studying English every morning because I want to communicate with people from other countries. My goal is to speak English confidently within a year. Thank you for listening.
- KO: 안녕하세요, 여러분. 제 이름은 민준이고, 서른 살입니다. 저는 한국의 수도인 서울에 살고 있어요. 작은 회사에서 소프트웨어 개발자로 일하는데, 매일 새로운 것을 만들 수 있어서 제 일이 정말 좋아요. 여가 시간에는 비디오 게임을 하거나 영화 보는 것을 좋아해요. 주말에는 간단한 요리를 하는 것도 즐깁니다. 최근에는 다른 나라 사람들과 소통하고 싶어서 매일 아침 영어 공부를 시작했어요. 제 목표는 1년 안에 자신 있게 영어로 말하는 것입니다. 들어주셔서 감사합니다.

**S5-02. 하루 일과**

- EN: I usually wake up at six thirty on weekdays. First, I wash my face and make a cup of coffee. Then I have a light breakfast, such as toast and fruit. I leave home at eight and take the subway to work. It takes about forty minutes, so I listen to English podcasts on the way. I work from nine to six, and I have lunch with my coworkers at noon. After work, I go to the gym for an hour. When I get home, I take a shower, eat dinner, and relax. I usually go to bed around eleven.
- KO: 저는 평일에 보통 6시 30분에 일어나요. 먼저 세수를 하고 커피 한 잔을 만들어요. 그러고 나서 토스트와 과일 같은 가벼운 아침을 먹어요. 8시에 집을 나서서 지하철을 타고 출근해요. 40분 정도 걸려서 가는 동안 영어 팟캐스트를 들어요. 9시부터 6시까지 일하고, 점심은 정오에 동료들과 먹어요. 퇴근 후에는 한 시간 동안 헬스장에 가요. 집에 오면 샤워하고, 저녁을 먹고, 쉬어요. 보통 11시쯤 잠자리에 들어요.

**S5-03. 주말 이야기**

- EN: On weekends, I like to spend time outside. Last Saturday, I went to a park near my house with my friend. We rented bicycles and rode along the river for two hours. The weather was sunny, and a cool breeze was blowing. After that, we had sandwiches and cold drinks on the grass. In the evening, I went home and cooked pasta for dinner. On Sunday, I stayed home and watched a movie. It was a very relaxing weekend, and I felt ready to start the new week.
- KO: 주말에는 밖에서 시간을 보내는 것을 좋아해요. 지난 토요일에는 친구와 집 근처 공원에 갔어요. 자전거를 빌려서 두 시간 동안 강변을 따라 탔어요. 날씨는 화창했고 시원한 바람이 불고 있었어요. 그 후에 잔디밭에서 샌드위치와 시원한 음료를 먹었어요. 저녁에는 집에 돌아와서 파스타를 만들어 먹었어요. 일요일에는 집에서 영화를 봤어요. 정말 여유로운 주말이었고, 새로운 한 주를 시작할 준비가 된 느낌이었어요.

**S5-04. 여행 경험**

- EN: Last summer, I traveled to Jeju Island with my family. We stayed there for four days. On the first day, we visited a beautiful beach and swam in the clear water. On the second day, we climbed a small volcanic hill and enjoyed the view. The local food was delicious, especially the fresh seafood and black pork. We also took many photos together. It was my first trip with my family in a long time, so it was very special to me. I hope to visit Jeju again next year.
- KO: 지난여름에 가족과 함께 제주도로 여행을 갔어요. 거기서 4일 동안 머물렀어요. 첫째 날에는 아름다운 해변에 가서 맑은 물에서 수영했어요. 둘째 날에는 작은 오름에 올라가서 경치를 즐겼어요. 현지 음식이 맛있었는데, 특히 신선한 해산물과 흑돼지가 최고였어요. 함께 사진도 많이 찍었어요. 오랜만에 가족과 함께한 첫 여행이라 저에게 매우 특별했어요. 내년에 제주도에 다시 가고 싶어요.

**S5-05. 내년 목표**

- EN: I have three goals for next year. First, I want to improve my English speaking skills. I'm going to practice for thirty minutes every day, and I want to be able to have a simple conversation with a foreigner. Second, I want to stay healthy, so I plan to exercise three times a week. Third, I'd like to save some money for a trip to Europe. These goals won't be easy, but I believe that small steps every day will take me there. I'm excited about the year ahead.
- KO: 저는 내년에 세 가지 목표가 있어요. 첫째, 영어 말하기 실력을 향상시키고 싶어요. 매일 30분씩 연습할 거고, 외국인과 간단한 대화를 할 수 있으면 좋겠어요. 둘째, 건강을 유지하고 싶어서 일주일에 세 번 운동할 계획이에요. 셋째, 유럽 여행을 위해 돈을 조금 모으고 싶어요. 이 목표들이 쉽지는 않겠지만, 매일의 작은 걸음이 저를 그곳으로 데려다 줄 거라고 믿어요. 다가오는 한 해가 기대돼요.

---

## A-2. 트랙 B: 대화하기

### D1. 왕초보 대화하기 (2턴)

**D1-01. 인사**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hi! How are you? | 안녕! 잘 지내? |
| B | I'm good, thanks. | 잘 지내, 고마워. |

**D1-02. 감사**

| 화자 | English | 한국어 |
|---|---|---|
| A | Thank you. | 감사합니다. |
| B | You're welcome. | 천만에요. |

**D1-03. 사과**

| 화자 | English | 한국어 |
|---|---|---|
| A | Sorry! | 미안해요! |
| B | It's okay. | 괜찮아요. |

**D1-04. 준비 확인**

| 화자 | English | 한국어 |
|---|---|---|
| A | Are you ready? | 준비됐어요? |
| B | Yes, I am. | 네, 됐어요. |

**D1-05. 물 요청**

| 화자 | English | 한국어 |
|---|---|---|
| A | Water, please. | 물 주세요. |
| B | Here you are. | 여기 있습니다. |

### D2. 초급 대화하기 (4턴)

**D2-01. 카페 주문**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hi. What would you like? | 안녕하세요. 뭘 드릴까요? |
| B | A latte, please. | 라떼 한 잔 주세요. |
| A | Hot or iced? | 따뜻한 걸로요, 아이스로요? |
| B | Iced, please. | 아이스로 주세요. |

**D2-02. 길 묻기**

| 화자 | English | 한국어 |
|---|---|---|
| A | Excuse me. Where is the station? | 실례합니다. 역이 어디예요? |
| B | Go straight for two blocks. | 두 블록 쭉 가세요. |
| A | Is it far? | 멀어요? |
| B | No, it's five minutes on foot. | 아니요, 걸어서 5분이에요. |

**D2-03. 첫 만남**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hi, I'm Jisu. What's your name? | 안녕하세요, 저는 지수예요. 이름이 뭐예요? |
| B | I'm Tom. Nice to meet you. | 저는 톰이에요. 만나서 반가워요. |
| A | Where are you from? | 어디서 오셨어요? |
| B | I'm from Canada. | 캐나다에서 왔어요. |

**D2-04. 쇼핑**

| 화자 | English | 한국어 |
|---|---|---|
| A | Can I help you? | 도와드릴까요? |
| B | Yes. How much is this shirt? | 네. 이 셔츠 얼마예요? |
| A | It's twenty dollars. | 20달러예요. |
| B | OK. I'll take it. | 좋아요. 이걸로 할게요. |

**D2-05. 시간 묻기**

| 화자 | English | 한국어 |
|---|---|---|
| A | What time is it? | 지금 몇 시예요? |
| B | It's three o'clock. | 3시예요. |
| A | Are we late? | 우리 늦었나요? |
| B | No, we have time. | 아니요, 시간 있어요. |

### D3. 중급 대화하기 (6턴)

**D3-01. 식당**

| 화자 | English | 한국어 |
|---|---|---|
| A | Welcome! Do you have a reservation? | 어서 오세요! 예약하셨나요? |
| B | No, we don't. Do you have a table for two? | 아니요. 두 명 자리 있나요? |
| A | Yes, please follow me. Here is the menu. | 네, 따라오세요. 메뉴 여기 있습니다. |
| B | Thank you. What do you recommend? | 감사합니다. 뭘 추천하세요? |
| A | Our pasta is very popular today. | 오늘은 파스타가 인기가 많아요. |
| B | Great. I'll have the pasta, please. | 좋네요. 파스타로 주세요. |

**D3-02. 호텔 체크인**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hello. I have a reservation under Kim. | 안녕하세요. 김 이름으로 예약했어요. |
| B | Let me check. Yes, a room for two nights, right? | 확인해 볼게요. 네, 2박 객실 맞으시죠? |
| A | That's correct. What time is check-out? | 맞아요. 체크아웃은 몇 시예요? |
| B | Check-out is at eleven in the morning. | 체크아웃은 오전 11시입니다. |
| A | Is breakfast included? | 조식이 포함되어 있나요? |
| B | Yes, it's served on the second floor from seven. | 네, 2층에서 7시부터 제공됩니다. |

**D3-03. 친구와 약속**

| 화자 | English | 한국어 |
|---|---|---|
| A | Are you free this Saturday? | 이번 토요일에 시간 있어? |
| B | I think so. What do you have in mind? | 아마도. 뭐 생각하고 있어? |
| A | How about watching a movie? | 영화 보는 건 어때? |
| B | Sounds good. What time? | 좋아. 몇 시에? |
| A | Let's meet at two in front of the theater. | 2시에 극장 앞에서 만나자. |
| B | Perfect. See you then! | 완벽해. 그때 보자! |

**D3-04. 택시**

| 화자 | English | 한국어 |
|---|---|---|
| A | Where to, please? | 어디로 모실까요? |
| B | To the airport, please. | 공항으로 가주세요. |
| A | Sure. Are you in a hurry? | 알겠습니다. 급하세요? |
| B | Yes, my flight is at six. | 네, 비행기가 6시예요. |
| A | Don't worry. We'll be there in thirty minutes. | 걱정 마세요. 30분 안에 도착할 거예요. |
| B | Thank you so much. | 정말 감사합니다. |

**D3-05. 약국**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hi. I have a headache and a sore throat. | 안녕하세요. 머리가 아프고 목도 아파요. |
| B | Do you have a fever? | 열은 있으세요? |
| A | A little, since last night. | 어젯밤부터 조금요. |
| B | Take this medicine after meals, three times a day. | 이 약을 식후에 하루 세 번 드세요. |
| A | Is there anything I should avoid? | 피해야 할 게 있나요? |
| B | Please drink lots of water and get some rest. | 물을 많이 마시고 푹 쉬세요. |

### D4. 중상급 대화하기 (8턴)

**D4-01. 면접**

| 화자 | English | 한국어 |
|---|---|---|
| A | Thank you for coming in today. Please have a seat. | 오늘 와주셔서 감사합니다. 앉으세요. |
| B | Thank you for having me. | 불러주셔서 감사합니다. |
| A | Could you tell me a little about yourself? | 간단히 자기소개를 해주시겠어요? |
| B | Sure. I have three years of experience in marketing. | 네. 저는 마케팅 분야에서 3년 경력이 있습니다. |
| A | Why do you want to work for our company? | 왜 저희 회사에서 일하고 싶으세요? |
| B | I admire your products, and I want to grow with your team. | 귀사의 제품을 존경하고, 팀과 함께 성장하고 싶습니다. |
| A | What would you say is your biggest strength? | 가장 큰 강점이 뭐라고 생각하세요? |
| B | I'm a quick learner, and I work well with others. | 저는 빨리 배우고, 다른 사람들과 협업을 잘합니다. |

**D4-02. 교환/환불**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hello. How can I help you? | 안녕하세요. 무엇을 도와드릴까요? |
| B | I bought this jacket yesterday, but it's too small. | 어제 이 재킷을 샀는데, 너무 작아요. |
| A | I'm sorry to hear that. Do you have the receipt? | 유감이네요. 영수증 가지고 계세요? |
| B | Yes, here it is. | 네, 여기 있어요. |
| A | Would you like to exchange it or get a refund? | 교환하시겠어요, 환불하시겠어요? |
| B | Can I exchange it for a bigger size? | 더 큰 사이즈로 교환할 수 있나요? |
| A | Of course. Let me check if we have one in stock. | 물론이죠. 재고가 있는지 확인해 볼게요. |
| B | Thank you. I appreciate your help. | 감사합니다. 도와주셔서 고마워요. |

**D4-03. 공항**

| 화자 | English | 한국어 |
|---|---|---|
| A | Excuse me. I can't find my gate. | 실례합니다. 제 게이트를 못 찾겠어요. |
| B | May I see your boarding pass? | 탑승권을 보여주시겠어요? |
| A | Sure. Here you go. | 네. 여기요. |
| B | You're going to gate twenty-two. It's on the second floor. | 22번 게이트네요. 2층에 있어요. |
| A | How long does it take to get there? | 거기까지 얼마나 걸려요? |
| B | About ten minutes on foot. Take the escalator on your right. | 걸어서 10분 정도예요. 오른쪽 에스컬레이터를 타세요. |
| A | Thanks. Does the flight leave on time? | 감사합니다. 비행기는 정시에 출발하나요? |
| B | Yes, boarding starts in thirty minutes. | 네, 30분 후에 탑승이 시작돼요. |

**D4-04. 영화 이야기**

| 화자 | English | 한국어 |
|---|---|---|
| A | Did you watch the new movie yesterday? | 어제 새 영화 봤어? |
| B | Yes, I did. I thought it was really exciting. | 응, 봤어. 정말 흥미진진하다고 생각했어. |
| A | Really? I heard the ending was a bit disappointing. | 정말? 결말이 좀 아쉽다고 들었는데. |
| B | Well, it was unexpected, but I liked it. | 글쎄, 예상 밖이었지만 난 좋았어. |
| A | What was your favorite part? | 가장 좋았던 부분이 뭐야? |
| B | The music was amazing, and the actors were great. | 음악이 멋졌고 배우들도 훌륭했어. |
| A | Maybe I should watch it this weekend. | 이번 주말에 봐야겠다. |
| B | You should. I'm sure you'll enjoy it. | 꼭 봐. 분명 재미있을 거야. |

**D4-05. 배달 주문 전화**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hello, thank you for calling Tasty Pizza. | 안녕하세요, 테이스티 피자에 전화 주셔서 감사합니다. |
| B | Hi. I'd like to order a large pepperoni pizza for delivery. | 안녕하세요. 라지 페퍼로니 피자를 배달로 주문하고 싶어요. |
| A | Sure. Would you like anything to drink? | 네. 음료도 드릴까요? |
| B | Yes, two bottles of cola, please. | 네, 콜라 두 병 주세요. |
| A | Could I have your address? | 주소를 알려주시겠어요? |
| B | It's 25 Maple Street, apartment 302. | 메이플 가 25번지, 302호예요. |
| A | Your total is twenty-five dollars. It'll arrive in thirty minutes. | 총 25달러입니다. 30분 안에 도착합니다. |
| B | Great. Can I pay by card when it arrives? | 좋아요. 도착했을 때 카드로 결제해도 되나요? |

### D5. 1분 동안 대화하기 (12턴)

**D5-01. 여행 계획**

| 화자 | English | 한국어 |
|---|---|---|
| A | Have you decided where to go for vacation? | 휴가 어디로 갈지 정했어? |
| B | Not yet. I'm thinking about Japan or Thailand. | 아직. 일본이나 태국을 생각 중이야. |
| A | Japan sounds great. Autumn is beautiful there. | 일본 좋겠다. 가을에 거기 정말 예뻐. |
| B | True, but flights to Thailand are cheaper. | 맞아, 근데 태국행 비행기가 더 싸. |
| A | How long are you planning to stay? | 얼마나 있을 계획이야? |
| B | About five days. I can't take a longer break. | 5일 정도. 더 길게는 못 쉬어. |
| A | Then Japan might be better. You can see a lot in five days. | 그럼 일본이 더 낫겠다. 5일이면 많이 볼 수 있어. |
| B | Good point. What should I see in Tokyo? | 맞는 말이야. 도쿄에서 뭘 봐야 할까? |
| A | Visit the old temples and try the street food. | 오래된 절들을 가보고 길거리 음식을 먹어봐. |
| B | That sounds wonderful. Will you recommend a hotel? | 멋지다. 호텔도 추천해 줄래? |
| A | Sure. I'll send you the link tonight. | 그럼. 오늘 밤에 링크 보내줄게. |
| B | Thanks a lot. I'm really excited now! | 정말 고마워. 이제 진짜 설레! |

**D5-02. 직장 동료에게 도움 요청**

| 화자 | English | 한국어 |
|---|---|---|
| A | Do you have a minute? I need some help. | 잠깐 시간 돼요? 도움이 필요해요. |
| B | Sure. What's the problem? | 그럼요. 무슨 문제예요? |
| A | I'm preparing the presentation for Friday's meeting. | 금요일 회의 발표를 준비하고 있어요. |
| B | Oh, how is it going so far? | 아, 지금까지 어떻게 되고 있어요? |
| A | I finished the slides, but I'm nervous about speaking. | 슬라이드는 끝냈는데, 말하는 게 긴장돼요. |
| B | Don't worry. Why don't you practice in front of me? | 걱정 마요. 제 앞에서 연습해 보는 게 어때요? |
| A | Really? That would be a big help. | 정말요? 큰 도움이 될 거예요. |
| B | No problem. How about after lunch today? | 문제없어요. 오늘 점심 후 어때요? |
| A | That works for me. I'll bring my laptop. | 저는 좋아요. 노트북 가져갈게요. |
| B | Great. Try to keep it under ten minutes. | 좋아요. 10분 안에 끝내도록 해봐요. |
| A | Okay. Thank you so much for your time. | 알겠어요. 시간 내주셔서 정말 감사해요. |
| B | Anytime. I'm sure you'll do a great job. | 언제든지요. 분명 잘 해낼 거예요. |

**D5-03. 집 구하기**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hi, I saw your ad for the apartment. Is it still available? | 안녕하세요, 아파트 광고 봤어요. 아직 있나요? |
| B | Yes, it is. Would you like to see it? | 네, 있어요. 보시겠어요? |
| A | Definitely. How big is it? | 물론이죠. 크기가 어떻게 되나요? |
| B | It has one bedroom and a small living room. | 침실 하나에 작은 거실이 있어요. |
| A | How much is the monthly rent? | 월세는 얼마예요? |
| B | It's seven hundred dollars, and utilities are not included. | 700달러이고, 공과금은 포함되지 않아요. |
| A | Is it near public transportation? | 대중교통이 가깝나요? |
| B | Yes, the subway station is a five-minute walk away. | 네, 지하철역이 걸어서 5분 거리예요. |
| A | That's convenient. Are pets allowed? | 편리하네요. 반려동물은 가능한가요? |
| B | Small pets are fine. Do you have one? | 작은 동물은 괜찮아요. 키우세요? |
| A | I have a cat. When can I visit? | 고양이 한 마리 있어요. 언제 방문할 수 있나요? |
| B | How about tomorrow at four o'clock? | 내일 4시 어때요? |

**D5-04. 제품 환불**

| 화자 | English | 한국어 |
|---|---|---|
| A | Hello. I bought these headphones last week, but one side doesn't work. | 안녕하세요. 지난주에 이 헤드폰을 샀는데 한쪽이 안 돼요. |
| B | I'm very sorry about that. May I see them? | 정말 죄송합니다. 한번 볼 수 있을까요? |
| A | Here you are. I only used them twice. | 여기요. 두 번밖에 안 썼어요. |
| B | Let me check. You're right, there's a problem with the left speaker. | 확인해 볼게요. 맞네요, 왼쪽 스피커에 문제가 있어요. |
| A | Can I get a refund, please? | 환불받을 수 있을까요? |
| B | Certainly. Do you have the receipt with you? | 물론이죠. 영수증 가지고 계세요? |
| A | Yes, I have it right here. | 네, 여기 있어요. |
| B | Thank you. The refund will go back to your card. | 감사합니다. 환불은 카드로 들어갑니다. |
| A | How long will it take? | 얼마나 걸리나요? |
| B | It usually takes three to five days. | 보통 3~5일 걸려요. |
| A | That's fine. Thank you for your quick help. | 괜찮아요. 빠르게 도와주셔서 감사합니다. |
| B | You're welcome. Sorry for the inconvenience. | 천만에요. 불편 끼쳐 죄송합니다. |

**D5-05. 영어 공부 조언**

| 화자 | English | 한국어 |
|---|---|---|
| A | Your English is getting better. How do you practice? | 영어 실력이 늘고 있네요. 어떻게 연습해요? |
| B | Thanks! I listen to English for thirty minutes every day. | 고마워요! 매일 30분씩 영어를 들어요. |
| A | Do you use any apps? | 앱도 써요? |
| B | Yes, I use an app that plays short sentences. | 네, 짧은 문장을 들려주는 앱을 써요. |
| A | Do you repeat them out loud? | 소리 내서 따라 해요? |
| B | Always. Speaking out loud helps me remember. | 항상요. 소리 내어 말하면 기억에 잘 남아요. |
| A | I'm too shy to speak. What should I do? | 저는 말하기가 너무 부끄러워요. 어떻게 해야 할까요? |
| B | Start alone at home. Nobody will hear you. | 집에서 혼자 시작해 보세요. 아무도 안 들어요. |
| A | That's a good idea. How often should I practice? | 좋은 생각이네요. 얼마나 자주 연습해야 해요? |
| B | A little every day is better than a long time once a week. | 매일 조금씩 하는 게 일주일에 한 번 오래 하는 것보다 나아요. |
| A | I'll try that starting today. | 오늘부터 해볼게요. |
| B | Great. Let's practice together sometime! | 좋아요. 언제 같이 연습해요! |

---

## 부록 B. 콘텐츠 품질 체크리스트

- [ ] 스테이지별 난이도(단어 수/턴 수)가 3장 기준에 맞는가
- [ ] 한국어 해석이 직역이 아닌 자연스러운 구어체인가
- [ ] S5 / D5의 실제 TTS 재생 시간이 약 60초 내외인가 (길면 문장 축약)
- [ ] 한국어 음성에서 고유명사/숫자 읽기가 자연스러운가 (예: "302호", "7시")
- [ ] 영어 숫자·시간 표기의 발음이 의도대로 합성되는가
- [ ] id 중복 / 오디오 경로 누락 없음 (`validate_content.py`로 검사)
