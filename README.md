# English Speaking App

**"듣고, 따라 말하고, 화면을 꺼놓고도 계속 들을 수 있는"** 레벨별 영어 말하기·대화 학습 앱 (Android 전용, **Unity** 로 개발).

- 난이도 5단계 × 2트랙(말하기 / 대화하기) = **10개 레벨**, 레벨당 주제별 **유닛 10개**, 유닛당 예문 5개
  → **100유닛, 예문 500개** (하루 1유닛 ≈ 3개월). 유닛 목록: [docs/CURRICULUM.md](docs/CURRICULUM.md)
- 예문마다 영어 문장 / 한국어 해석 / 영어 음성 / 한국어 음성 (TTS: Supertonic-3, 로컬 생성)
- 예문 JSON과 mp3는 **GitHub Pages에서 원격으로 받아** 앱 업데이트 없이 수정 가능 (아래 '예문 수정')
- 플레이리스트식 **연속 재생**, **화면 꺼짐 재생**
  - Unity는 화면이 꺼지면 오디오가 멈추므로, 재생은 **Android 네이티브 플러그인**(Media3 `MediaSessionService` + ExoPlayer)이 담당하고 Unity(C#)는 UI와 재생 지시만 맡는다

기획서 전문: [docs/PLAN.md](docs/PLAN.md) · 음성 생성 기록: [docs/AUDIO.md](docs/AUDIO.md)

## 구조

```
unity/                    # Unity 프로젝트 (아래 '시작하기' 참고) — M1부터
  Assets/Scripts/          #   C# (UI, 콘텐츠 로드, 플레이어)
  Assets/Plugins/Android/  #   네이티브 오디오 플러그인 (Media3 서비스)
  Assets/StreamingAssets/  #   content/ 복사본
content/                  # 번들용 예문 (원격 원본의 복사본, 오프라인 첫 실행용)
  index.json                          # 레벨·유닛 목록 (앱이 가장 먼저 읽음)
  speaking/s1/u01.json ... s5/u10.json  # 말하기 S1~S5 × 유닛 U01~U10
  dialogue/d1/u01.json ... d5/u10.json  # 대화하기 D1~D5 × 유닛 U01~U10
tools/
  validate_content.py     # 스키마/필드 누락/id 중복/오디오 경로 검사
  generate_tts.py         # JSON → mp3 생성 (Supertonic-3), Pages 레포 폴더에 바로 저장
docs/PLAN.md              # 기획서 v0.3
docs/CURRICULUM.md        # 유닛 목록 (100유닛)
docs/AUDIO.md             # 음성(mp3) 생성 기록 — 현황, 엔진 선택 이유, 생성 절차
```

## 레벨

각 레벨에 주제별 유닛 10개 (예: S2 → U01 여행 첫걸음, U02 자기소개, U03 가족 …), 유닛당 예문 5개.

| 말하기 | 분량 | 대화하기 | 분량 |
|---|---|---|---|
| S1 왕초보 | 1~3 단어 | D1 왕초보 | 2턴 |
| S2 초급 | 4~6 단어 | D2 초급 | 4턴 |
| S3 중급 | 8~12 단어 | D3 중급 | 6턴 |
| S4 중상급 | 2~3 문장 | D4 중상급 | 8턴 |
| S5 1분 말하기 | 90~110 단어 | D5 1분 대화 | 12턴 |

## 시작하기 (Unity 프로젝트 만들기)

1. Unity Hub → New project → **Unity 6000.3.17f1**, 템플릿 선택
2. Location은 이 레포 폴더, Project name은 **`unity`** → `english-speaking-app/unity/` 에 생성
3. File → Build Profiles → **Android** 로 전환
4. 커밋 / 푸시 (`unity/Library`, `Temp` 등은 `.gitignore` 로 제외됨)

## 예문 수정 (원격 콘텐츠)

예문·음성 원본은 [didrnrgus/didrnrgus.github.io](https://github.com/didrnrgus/didrnrgus.github.io) 레포의
`project_speaking_english/` 폴더다. JSON은 이 레포의 `content/` 와 같은 구조이고, mp3는 그 폴더의 `audio/` 에만 있다.

```
https://didrnrgus.github.io/project_speaking_english/index.json
https://didrnrgus.github.io/project_speaking_english/speaking/s1/u01.json       # 유닛 파일
https://didrnrgus.github.io/project_speaking_english/audio/en/S1/S1-U01-01.mp3  # JSON audio 경로 = 이 폴더 기준 상대 경로
```

1. Pages 레포에서 해당 유닛 파일(예: `speaking/s2/u03.json`)을 고치고 그 파일의 `version` 을 1 올린다
   - 유닛을 새로 추가할 때는 `index.json` 의 `units` 에도 등록한다 (목록에 없으면 앱이 받지 않음)
2. 바뀐 문장의 mp3만 다시 생성하고 검사한다 (`audio/tts_manifest.json` 과 비교해 바뀐 것만 생성)
   ```bash
   P=../didrnrgus.github.io/project_speaking_english
   python tools/generate_tts.py --content $P
   python tools/validate_content.py --content $P --audio
   ```
3. Pages 레포 `main` 에 푸시 → GitHub Pages 배포 (CDN 캐시 때문에 반영까지 최대 10분 정도)
4. 다음 앱 빌드 전에 번들 JSON도 맞춘다: `cp $P/index.json content/ && cp -r $P/speaking $P/dialogue content/`

앱 동작(M1에서 구현): 실행 시 `index.json` → 유닛 파일을 받아 검사 → 통과하면 캐시에 저장, 실패하거나 오프라인이면 캐시 → 번들(`StreamingAssets/content`) 순으로 사용. mp3는 **들을 유닛만** 같은 주소 기준으로 받아 캐시해 두고 재생한다 (GitHub Pages 트래픽 한도 때문에 전체 일괄 다운로드는 하지 않음).

> mp3는 **Git LFS를 쓰지 않는다.** GitHub Pages는 LFS 파일 대신 포인터 텍스트를 내보내므로 앱이 받을 수 없다.
> 64kbps 모노 3,700개 전체가 약 111MB 라 일반 커밋으로 충분하다 (Pages 사이트 한도 1GB).

### TTS (Supertonic-3)

`tools/generate_tts.py` — Supertone의 Supertonic-3로 영어·한국어를 로컬에서 생성 (무료, 코드 MIT / 모델 OpenRAIL-M: 상업적 이용 가능, 사용 제한 조항 준수).
목소리: 말하기 트랙 F1, 대화 A=F1 / B=M1. 첫 실행 때 Hugging Face에서 모델(~400MB)을 받는다.

> mp3 3,700개(예문 500개) 생성·배포 완료 (속도 1.0, 앱에서 전역 속도 조절). 라이선스상 **AI 생성 음성 고지 필요** → [docs/AUDIO.md](docs/AUDIO.md)

```bash
pip install supertonic soundfile   # + ffmpeg
python tools/generate_tts.py --content ../didrnrgus.github.io/project_speaking_english --dry-run
```

## 콘텐츠 검사

```bash
python tools/validate_content.py          # 구조 검사 (index.json, 유닛 파일, id 규칙, 개수)
python tools/validate_content.py --content <폴더>           # 다른 콘텐츠 폴더(Pages 레포) 검사
python tools/validate_content.py --content <폴더> --audio   # mp3 존재 여부까지
python tools/generate_tts.py --content <폴더> --dry-run     # 생성할 오디오 목록
```

## 로드맵

| 단계 | 내용 | 상태 |
|---|---|---|
| M0 콘텐츠 | 레벨·유닛 구조, 예문 500개, JSON 스키마, TTS | ✅ 완료 — 100유닛·예문 500개, mp3 3,700개 (Supertonic-3), 원격 배치 |
| M1 프로토타입 | Unity 프로젝트 생성, 레벨 목록 → 유닛 목록 → 예문 리스트 → 단일 재생 | |
| M2 핵심 | Android 플러그인 연동 → 연속 재생 + 화면 꺼짐 재생 + 알림 컨트롤 | |
| M3 학습 기능 | 재생 패턴, 반복, 전역 재생 속도(설정 팝업 드롭다운 x1.5~x0.5), 수면 타이머, 진행률 | |
| M4 출시 준비 | 기기별 백그라운드 테스트, 스토어 자료 | |
| M5 확장 | 유닛 추가, 롤플레이, 발음 체크 | |
