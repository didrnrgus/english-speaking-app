# 작업 기록 (2026-10-07)

> 기획서 v0.1 이후 콘텐츠·음성·원격 배포까지 진행한 내용과 결정 이유를 한곳에 정리한다.
> 세부 사양은 각 문서에: 기획 [PLAN.md](PLAN.md) · 유닛 목록 [CURRICULUM.md](CURRICULUM.md) · 음성 [AUDIO.md](AUDIO.md)

## 1. 현재 상태 한눈에

| 항목 | 상태 |
|---|---|
| 콘텐츠 | ✅ 레벨 10개 × 유닛 10개 × 예문 5개 = **100유닛, 예문 500개** (하루 1유닛 ≈ 3개월) |
| 음성 | ✅ **mp3 3,700개** (Supertonic-3, 영어 약 107분 + 한국어 약 115분, 약 111MB) |
| 원격 배포 | ✅ GitHub Pages `https://didrnrgus.github.io/project_speaking_english/` — 3,801개 파일 전수 확인 |
| 도구 | ✅ `validate_content.py`(구조·오디오 검사), `generate_tts.py`(바뀐 문장만 mp3 재생성) |
| 앱 (Unity) | 🟡 코드 작성 완료, **에디터·기기 확인 전** — `unity/` 프로젝트, 화면 3종 + 설정 팝업, 네이티브 재생 플러그인 ([UNITY.md](UNITY.md)) |
| 결정 필요 | S5/D5 '1분' 레벨 실제 길이 약 34초 → 분량 늘리기 / 이름 바꾸기 (7장) |

## 2. 진행 순서와 결정 이유

| # | 한 일 | 이유 / 결정 |
|---|---|---|
| 1 | 예문 JSON 을 Pages 레포 `project_speaking_english/` 로 옮김 | **배포 후에도 앱 업데이트 없이 예문을 고치기 위해.** 목록 `index.json` + 파일별 `version` (처음엔 스테이지마다 파일 1개 → 7번에서 유닛 파일로) |
| 2 | TTS 엔진 선택: **Supertonic-3** | 무료, 영어·한국어를 한 모델로, 로컬 실행. 샘플 청취 후 목소리·속도(1.0) 확정 |
| 3 | 라이선스 확인 (OpenRAIL-M 원문) | 상업 이용 가능·로열티 없음·출력물 권리 주장 없음. **단 AI 생성 음성 고지 의무** → 앱 설정 '정보'와 스토어 설명에 표기. 조건 없는 대안(Kokoro + Qwen3-TTS/MeloTTS)은 [AUDIO.md](AUDIO.md)에 기록 |
| 4 | mp3 도 Pages 레포에 둠, **Git LFS 안 씀** | JSON 과 같은 곳에 있어야 문장 수정 시 음성도 같이 바뀜. Pages 는 LFS 파일을 서빙하지 않음(포인터 텍스트가 나감) |
| 5 | 비용 | 0원. Hugging Face 는 모델 다운로드에만 사용(API·크레딧 미사용). 클라우드 세션 네트워크에 `huggingface.co`, `*.hf.co`, `didrnrgus.github.io` 허용 |
| 6 | 재생 속도 | 생성은 1.0 한 벌만, 빠르기는 앱에서 조절. 처음엔 설정 팝업 전역 드롭다운(x1.5~x0.5)으로 정했다가 앱 구현 때 **개별 듣기·듣기 시퀀스 노드별 슬라이더(x0.5~x2.0)** 로 바뀜 (8장) |
| 7 | 분량 확장: 50개 → **500개** | 50개(약 24분)는 하루이틀이면 끝남. "하루 1유닛" 기준으로 레벨당 주제별 유닛 10개. 기존 50개는 각 레벨 U01 로 이동 |
| 8 | 예문 450개 작성 + mp3 3,330개 추가 생성 | 레벨별 기준(단어 수·문장 수·턴 수) 자동 검사 통과. 기존 mp3 370개는 경로만 옮겨 재사용 |
| 9 | 검증 | 구조 검사 오류 0, 음량 고름·클리핑 0, 사이트 3,801개 파일이 로컬과 크기까지 일치. 철자 읽기("M-I-N-A") 대화 1개는 TTS 위험으로 교체 |

## 3. 현재 구조

### 두 레포의 역할

| 레포 | 역할 |
|---|---|
| `didrnrgus/english-speaking-app` (이 레포) | 앱(예정), 번들용 예문 JSON `content/`, 도구 `tools/`, 문서 `docs/` |
| `didrnrgus/didrnrgus.github.io` | **원격 콘텐츠 원본** `project_speaking_english/` — JSON + mp3, GitHub Pages 로 배포 |

### 파일 구조 (Pages 레포 `project_speaking_english/`, 이 레포 `content/` 는 JSON 만 같은 구조)

```
index.json                          레벨·유닛 목록 (앱이 가장 먼저 읽음)
speaking/s1/u01.json … s5/u10.json  말하기 유닛 파일
dialogue/d1/u01.json … d5/u10.json  대화 유닛 파일
audio/en/S1/S1-U01-01.mp3           음성 (언어 / 레벨 / 예문 id)
audio/ko/D3/D3-U01-01_t1.mp3        대화는 턴 번호까지
audio/tts_manifest.json             파일별 문장·목소리·설정 기록 (바뀐 문장만 재생성용)
```

- id: 유닛 `S2-U03`, 예문 `S2-U03-01`. JSON 의 `audio` 경로는 콘텐츠 폴더 기준 상대 경로
- 목소리: 말하기 F1, 대화 A=F1(여) / B=M1(남), 영어·한국어 같은 목소리, 64kbps 모노

### 앱이 콘텐츠를 쓰는 방식 (코드 구현됨, 기기 확인 전 — [UNITY.md](UNITY.md) 5장)

1. 원격 `index.json` → 유닛 파일 다운로드 → 검사 통과 시 캐시, 실패·오프라인이면 캐시 → 번들(`StreamingAssets/content`) — `ContentService`
2. mp3 는 **들을 유닛만** 받아 캐시 (전체 일괄 다운로드 금지 — 6장 트래픽) — `AudioCache`
3. 원격 기준 URL 은 `ContentService.RemoteBase` 한 곳 (JSON·mp3 공통) → 나중에 다른 저장소로 옮겨도 이 값만 바꾸면 됨
4. 번들 JSON 은 빌드할 때 `ContentSync` 가 레포 `content/` → `StreamingAssets/content/` 로 자동 복사

## 4. 운영 방법

```bash
# 두 레포를 나란히 클론 (english-speaking-app/, didrnrgus.github.io/)
cd english-speaking-app
pip install supertonic soundfile        # + ffmpeg
P=../didrnrgus.github.io/project_speaking_english
```

| 하고 싶은 일 | 방법 |
|---|---|
| 예문 고치기 | `$P/<트랙>/<레벨>/uNN.json` 수정 → 그 파일 `version` +1 → `python tools/generate_tts.py --content $P` (바뀐 문장만 생성) |
| 유닛 추가 | 유닛 파일 추가 + `index.json` 의 `units` 에 등록 (목록에 없으면 앱이 받지 않음) → mp3 생성 |
| 검사 | `python tools/validate_content.py --content $P --audio` → 오류 0개 확인 |
| 배포 | Pages 레포 `main` 에 커밋·푸시 → 1~2분 뒤 반영 (CDN 캐시로 최대 10분) |
| 번들 맞추기 (앱 빌드 전) | `cp $P/index.json content/ && cp -r $P/speaking $P/dialogue content/` |

- 생성 시간: 4코어 CPU 기준 약 1.3초/파일 (3,330개 약 70분). 50개마다 manifest 저장 → 끊겨도 이어서 생성
- 클라우드 세션에서 생성하려면 환경 Network access 에 `huggingface.co`, `*.hf.co` 허용 필요

## 5. 결정 사항 요약

| 주제 | 결정 |
|---|---|
| 콘텐츠 구조 | 레벨(10) → 유닛(주제, 레벨당 10) → 예문(유닛당 5) |
| 콘텐츠 위치 | GitHub Pages 레포가 원본, 앱 레포 `content/` 는 번들 복사본(JSON 만) |
| TTS | Supertonic-3, 속도 1.0, F1/M1 |
| 음성 저장 | Pages 레포 일반 커밋 (LFS 미사용) |
| 재생 속도 | mp3 는 1.0 한 벌, 앱 설정의 개별 듣기·듣기 시퀀스 노드별 빠르기 슬라이더 x0.5 ~ x2.0 (v0.3 의 전역 드롭다운에서 변경) |
| 라이선스 의무 | AI 생성 음성 고지 (앱 설정 '정보' + 스토어 설명) |

## 6. GitHub 한도와 비용

| 항목 | 한도 | 현재 | 판단 |
|---|---|---|---|
| Pages 사이트 크기 | 1GB | 약 112MB | ✅ |
| 레포 크기 | 1GB 이하 권장 | 약 115MB | ✅ |
| 파일 하나 | 50MB 경고 / 100MB 차단 | mp3 하나 수백 KB 이하 | ✅ |
| Pages 월 트래픽 | 약 100GB (소프트 한도) | 사용자 수에 비례 | ⚠️ 유닛당 평균 약 1.1MB, 하루 1유닛이면 사용자당 월 약 33MB → 월 활성 약 3,000명까지 여유 (캐시 전제, 대략치) |

- **유료 결제는 필요 없고, 결제해도 Pages 한도는 늘지 않는다.** 소프트 한도라 넘으면 GitHub 이 연락하는 방식
- 사용자가 크게 늘면 mp3 만 다운로드 비용 없는 저장소(예: Cloudflare R2)로 옮기고 앱의 기준 URL 만 바꾼다

## 7. 남은 일

| 우선 | 내용 |
|---|---|
| 결정 | **S5/D5 '1분' 길이** — 실제 영어 재생 약 30~40초(중앙값 34초). 예문 100개를 약 150~170단어로 늘리기 / '30초 말하기·대화'로 이름 바꾸기 중 선택 |
| 확인 | **Unity 에디터에서 처음 열기 → 콘솔 오류 확인, 기기에서 화면 꺼짐 재생 테스트** ([UNITY.md](UNITY.md) 7장 체크리스트) |
| M3 | 수면 타이머, 진행률·오늘의 유닛, 텍스트 크기 |
| M4 | 최근 앱 목록에서 지워도 재생 유지(서비스 별도 프로세스), 배터리 최적화 안내, 제조사별 테스트 |
| 출시 전 | AI 음성 고지 문구, Supertonic-3 라이선스 전문 재확인, 예문 원어민 감수 권장 |
| 광고 | LevelPlay 계정·App Key·배너 Ad Unit ID 를 `AdConfig.cs` 에 넣기, Test devices 등록, app-ads.txt, Play 광고·데이터 보안 신고 ([ADS.md](ADS.md) 2·3·6장) |

## 8. 앱 구현 (M1 ~ M3 일부)

요청: 세로 모드 전용, 화면 꺼져도 재생, Safe Area, UGUI, 잘난체 고딕(TMP 동적, 외곽선 0.15),
메인 = 가운데 위 제목 / 오른쪽 위 설정 / 가운데보다 조금 위에서 2열(문장·대화 단계 버튼),
설정 = 개별 듣기(음량·빠르기) + 전체 듣기(드래그로 듣기 시퀀스, 노드별 텀 0.1~5초·음량·빠르기, 슬라이더 즉시 적용).

| 결정 | 내용 / 이유 |
|---|---|
| 화면 흐름 | 레벨 → 유닛 10개 목록 → 유닛 안에서 한 페이지에 예문 1개, '전체 듣기' = 유닛 5개 (질문 후 확정) |
| 대화 시퀀스 | 턴(대사)마다 시퀀스 전체 적용 — 말하기의 '문장 1개' 와 같게 (질문 후 확정) |
| Unity 프로젝트 | 레포에 최소 골격(`Packages/manifest.json`, `ProjectVersion.txt`)만 두고 나머지는 처음 열 때 자동 셋업 |
| UI 생성 | 처음엔 씬·프리팹 없이 코드로 생성 (`UIKit`). → 2026-10-09 프리팹으로 옮김 (에디터에서 보고 고치기 위해). 생성기 `UIPrefabBuilder` 가 같은 배치로 한 번 굽고, 이후엔 프리팹이 원본. 아이콘·둥근 사각형·배경은 SDF 로 그린 PNG (`Assets/UI/Sprites`) |
| 폰트 | 템플릿 에셋은 셰이더·외곽선 값만, 실제 글리프는 실행 시 만든 런타임 에셋에 → 에셋 파일이 커지지 않음 |
| 텀 구현 | 무음 트랙을 재생하며 타이머로 끊음 → 텀 중에도 '재생 중' 이라 포그라운드 유지, 텀 변경이 진행 중 텀에 바로 반영 |
| 상태 동기화 | 콜백 대신 0.1초 폴링 (`getState`) → 화면이 꺼져 Unity 가 멈춰 있던 동안의 변화도 복귀 즉시 반영 |
| 속도 사양 변경 | PLAN 6-4 의 드롭다운 전역 속도 → 개별/노드별 슬라이더 (x0.5~x2.0) |

검증: Unity 에디터가 없는 환경이라 C# 은 Unity 참조 DLL + ugui/TMP 스텁으로 mono 컴파일 검사, Java 는 Media3 1.4.1 소스 + Android 14
프레임워크로 javac 검사 (둘 다 오류 0). **실제 에디터·기기 실행은 아직** — 처음 열 때 나오는 오류부터 고친다.

## 8-1. 배너 광고 (Unity LevelPlay)

요청: Android 세로 하단 배너, Safe Area·내비게이션 버튼을 피해서, 모든 화면, 수익이 잘 나오게 하되 어뷰징은 절대 없게.
질문 후 확정: 대상 13세 이상(일반 광고), 키는 아직 없음(자리만), 유닛 화면 포함 모든 화면, 광고 제거 결제는 나중에.

| 결정 | 내용 / 이유 |
|---|---|
| SDK | Ads Mediation(`com.unity.services.levelplay`) 9.5.1. Unity Ads 는 그 안의 네트워크 — 나중에 비딩 네트워크를 붙여 단가를 올리기 쉬움 |
| 배치 | Safe Area 맨 아래 **전용 띠**에 적응형 배너, 화면은 그 위로 줄임. 네이티브 배너를 띠 좌표(dp)에 맞춰 놓아 앱 UI 와 겹침 0 |
| 내비게이션 바 | Unity 기본 전체 화면은 바를 숨기지만 밀어 올리면 나타남 → 숨겨져 있어도 바 높이만큼 항상 비움 (JNI `getInsetsIgnoringVisibility`) |
| 배너 개수 | 앱 전체에 1개를 계속 띄움 (화면마다 새로 만들지 않음) |
| 어뷰징 방지 | 자리 미리 확보(레이아웃 밀림 X), 12dp 간격, 팝업 중 숨김+새로고침 정지, 강제 재요청 없음, 클릭 보호(30분 3회 → 24시간 중지) |
| 컴파일 안전 | 광고 코드는 별도 asmdef + version define → 패키지가 없어도 프로젝트가 컴파일, 키가 없으면 광고 자리도 안 만듦 |

검증: Unity 2021.3 참조 DLL + TMP·LevelPlay 스텁으로 Roslyn 컴파일 (기기+SDK / SDK 없음 / 에디터, 오류·경고 0). 실제 패키지·기기 확인은 아직.

## 9. 커밋 이력

### 이 레포 (`english-speaking-app`, `main`)

| 커밋 | 내용 |
|---|---|
| `9930210` | 초기 구조: 기획서 v0.1, 예문 50개 콘텐츠 JSON, 검증/TTS 도구 |
| `c98a668` | 개발 엔진을 Unity로 변경 |
| `b43b275` | 예문 JSON 원격화 준비: index.json, version, Pages 레포 문서화 |
| `9a5a4c5` | TTS를 Supertonic-3로 구현, mp3는 Pages 레포에 두도록 변경 |
| `e386617` | 음성 생성 기록 `docs/AUDIO.md` |
| `2f88c50` | manifest 를 스테이지 파일로 읽던 버그 수정 |
| `0eb9251` | 전역 재생 속도 설정 사양, mp3 생성 완료 반영 |
| `7af192c` | 레벨 → 유닛 구조로 확장: 100유닛, 예문 500개 |
| `ec1b69f` | README·AUDIO 유닛 구조 반영 |
| `61e16f3` | mp3 3,700개 생성 반영, D1-U03-02 교체 |
| `ccc2e25` | 작업 기록 `docs/WORKLOG.md` |
| `45a9a5b` (PR #1) | Unity 앱 구현: 메인/레벨/유닛 화면, 설정(듣기 시퀀스), 화면 꺼짐 재생 플러그인 |
| `b88e18b` (PR #2) | 프로젝트 소개서 `docs/OVERVIEW.md` |
| `7a8fb77` (PR #3) | 하단 배너 광고 (Unity LevelPlay), 내비게이션 바 자리 피하기 |
| (이번) | 문서 정리: 속도 사양(슬라이더)·앱 구현 상태를 WORKLOG·AUDIO·README 에 반영, 커밋 이력 갱신 |

### Pages 레포 (`didrnrgus.github.io`, `main`)

| 커밋 | 내용 |
|---|---|
| `b60f70b` | `project_speaking_english` 예문 JSON 추가 |
| `4d3f477` | 예문 mp3 370개 추가 (Supertonic-3) |
| `a276c02` | 레벨 → 유닛 구조로 전환 (기존 mp3 370개 U01 경로로 이동) |
| `a0a7e04` … `f2b8a26` | 레벨별 유닛 U02~U10 mp3 추가 (S1~S5 각 90개, D1 180 · D2 360 · D3 540 · D4 720 · D5 1,080개) |
