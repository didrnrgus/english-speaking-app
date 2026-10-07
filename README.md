# English Speaking App

**"듣고, 따라 말하고, 화면을 꺼놓고도 계속 들을 수 있는"** 레벨별 영어 말하기·대화 학습 앱 (Android 전용, **Unity** 로 개발).

- 난이도 5단계 × 2트랙(말하기 / 대화하기) = **10개 스테이지**, 스테이지당 예문 5개 (총 50개)
- 예문마다 영어 문장 / 한국어 해석 / 영어 음성 / 한국어 음성 (TTS)
- 예문 JSON은 **GitHub Pages에서 원격으로 받아** 앱 업데이트 없이 수정 가능 (아래 '예문 수정')
- 플레이리스트식 **연속 재생**, **화면 꺼짐 재생**
  - Unity는 화면이 꺼지면 오디오가 멈추므로, 재생은 **Android 네이티브 플러그인**(Media3 `MediaSessionService` + ExoPlayer)이 담당하고 Unity(C#)는 UI와 재생 지시만 맡는다

기획서 전문: [docs/PLAN.md](docs/PLAN.md)

## 구조

```
unity/                    # Unity 프로젝트 (아래 '시작하기' 참고) — M1부터
  Assets/Scripts/          #   C# (UI, 콘텐츠 로드, 플레이어)
  Assets/Plugins/Android/  #   네이티브 오디오 플러그인 (Media3 서비스)
  Assets/StreamingAssets/  #   content/, audio/ 복사본
content/                  # 번들용 예문 (원격 원본의 복사본, 오프라인 첫 실행용)
  index.json                     # 스테이지 목록 (앱이 가장 먼저 읽음)
  speaking/s1.json ... s5.json   # 말하기 S1~S5
  dialogue/d1.json ... d5.json   # 대화하기 D1~D5
tools/
  validate_content.py     # 스키마/필드 누락/id 중복/오디오 경로 검사
  generate_tts.py         # JSON → mp3 생성 (TTS 엔진 미정, 골격만)
audio/                    # 생성된 mp3 (unity/Assets/StreamingAssets 로 복사)
docs/PLAN.md              # 기획서 v0.2
```

## 스테이지

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

예문 원본은 [didrnrgus/didrnrgus.github.io](https://github.com/didrnrgus/didrnrgus.github.io) 레포의
`project_speaking_english/` 폴더이며, 이 레포의 `content/` 와 같은 구조다.

```
https://didrnrgus.github.io/project_speaking_english/index.json
https://didrnrgus.github.io/project_speaking_english/speaking/s1.json   # ... s5.json
https://didrnrgus.github.io/project_speaking_english/dialogue/d1.json   # ... d5.json
```

1. Pages 레포에서 해당 스테이지 파일(예: `speaking/s2.json`)을 고치고 그 파일의 `version` 을 1 올린다
2. 검사 후 `main` 에 푸시 → GitHub Pages 배포 (CDN 캐시 때문에 반영까지 최대 10분 정도)
   ```bash
   python tools/validate_content.py --content ../didrnrgus.github.io/project_speaking_english
   ```
3. 다음 앱 빌드 전에 번들 복사본도 맞춘다: `cp -r ../didrnrgus.github.io/project_speaking_english/. content/`

앱 동작(M1에서 구현): 실행 시 `index.json` → 각 스테이지 파일을 받아 검사 → 통과하면 캐시에 저장, 실패하거나 오프라인이면 캐시 → 번들(`StreamingAssets/content`) 순으로 사용.

> ⚠️ 문장을 바꾸면 그 문장의 mp3도 다시 만들어야 한다. 오디오가 앱에 번들된 상태로는 원격 수정이 텍스트에만 반영되므로,
> 원격으로 문장을 바꾸려면 오디오도 같은 폴더(`project_speaking_english/audio/...`)에 올리고 앱이 받아 쓰도록 해야 한다 (`audio` 경로는 콘텐츠 폴더 기준 상대 경로).

## 콘텐츠 검사

```bash
python tools/validate_content.py          # 구조 검사 (index.json, version 포함)
python tools/validate_content.py --audio  # mp3 존재 여부까지
python tools/validate_content.py --content <폴더>   # 다른 콘텐츠 폴더(Pages 레포) 검사
python tools/generate_tts.py --dry-run    # 생성할 오디오 목록
```

## 로드맵

| 단계 | 내용 | 상태 |
|---|---|---|
| M0 콘텐츠 | 예문 50개, JSON 스키마, TTS 스크립트 | 예문·스키마·검증 완료 / TTS 엔진 미정 |
| M1 프로토타입 | Unity 프로젝트 생성, 스테이지 목록 → 예문 리스트 → 단일 재생 | |
| M2 핵심 | Android 플러그인 연동 → 연속 재생 + 화면 꺼짐 재생 + 알림 컨트롤 | |
| M3 학습 기능 | 재생 패턴, 반복, 속도, 수면 타이머, 진행률 | |
| M4 출시 준비 | 기기별 백그라운드 테스트, 스토어 자료 | |
| M5 확장 | 예문 추가, 롤플레이, 발음 체크 | |
