# English Speaking App

**"듣고, 따라 말하고, 화면을 꺼놓고도 계속 들을 수 있는"** 레벨별 영어 말하기·대화 학습 앱 (Android 전용).

- 난이도 5단계 × 2트랙(말하기 / 대화하기) = **10개 스테이지**, 스테이지당 예문 5개 (총 50개)
- 예문마다 영어 문장 / 한국어 해석 / 영어 음성 / 한국어 음성 (TTS)
- 플레이리스트식 **연속 재생**, **화면 꺼짐 재생** (Media3 `MediaSessionService` + ExoPlayer)

기획서 전문: [docs/PLAN.md](docs/PLAN.md)

## 구조

```
app/                      # Android 앱 (Kotlin / Compose / Media3) — M1부터
content/
  speaking/s1.json ... s5.json   # 말하기 S1~S5
  dialogue/d1.json ... d5.json   # 대화하기 D1~D5
tools/
  validate_content.py     # 스키마/필드 누락/id 중복/오디오 경로 검사
  generate_tts.py         # JSON → mp3 생성 (TTS 엔진 미정, 골격만)
audio/                    # 생성된 mp3 (앱 assets로 복사)
docs/PLAN.md              # 기획서 v0.1
```

## 스테이지

| 말하기 | 분량 | 대화하기 | 분량 |
|---|---|---|---|
| S1 왕초보 | 1~3 단어 | D1 왕초보 | 2턴 |
| S2 초급 | 4~6 단어 | D2 초급 | 4턴 |
| S3 중급 | 8~12 단어 | D3 중급 | 6턴 |
| S4 중상급 | 2~3 문장 | D4 중상급 | 8턴 |
| S5 1분 말하기 | 90~110 단어 | D5 1분 대화 | 12턴 |

## 콘텐츠 검사

```bash
python tools/validate_content.py          # 구조 검사
python tools/validate_content.py --audio  # mp3 존재 여부까지
python tools/generate_tts.py --dry-run    # 생성할 오디오 목록
```

## 로드맵

| 단계 | 내용 | 상태 |
|---|---|---|
| M0 콘텐츠 | 예문 50개, JSON 스키마, TTS 스크립트 | 예문·스키마·검증 완료 / TTS 엔진 미정 |
| M1 프로토타입 | 스테이지 목록 → 예문 리스트 → 단일 재생 | |
| M2 핵심 | 연속 재생 + 화면 꺼짐 재생 + 알림 컨트롤 | |
| M3 학습 기능 | 재생 패턴, 반복, 속도, 수면 타이머, 진행률 | |
| M4 출시 준비 | 기기별 백그라운드 테스트, 스토어 자료 | |
| M5 확장 | 예문 추가, 롤플레이, 발음 체크 | |
