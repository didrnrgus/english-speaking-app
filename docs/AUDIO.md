# 음성(mp3) 생성 기록

> 최종 갱신: 2026-10-07

## 현재 상태

| 항목 | 상태 |
|---|---|
| 생성 스크립트 `tools/generate_tts.py` | ✅ 구현 완료 (Supertonic-3) — `--dry-run` 으로 대상 370개 확인 |
| 검사 `tools/validate_content.py --audio` | ✅ 콘텐츠 폴더 기준으로 mp3 존재 여부 검사 |
| mp3 파일 | ❌ **미생성 (0 / 370)** |
| 저장 위치 | Pages 레포 `didrnrgus.github.io/project_speaking_english/audio/{en,ko}/` (아직 폴더 없음) |

### 막힌 이유

Claude Code 클라우드 세션의 네트워크 정책이 `huggingface.co` 접속을 막고 있다(403).
Supertonic-3 모델 파일(~400MB)은 Hugging Face 에만 있어서 세션 안에서는 모델을 받을 수 없다.
(같은 세션에서 PyPI, GitHub 릴리스 다운로드는 가능 — `supertonic` 패키지 설치까지는 됨)

### 해결 방법 (둘 중 하나)

- **A. 클라우드 세션에서 생성** — 환경 설정 → Network access 를 Custom 으로 바꾸고 Allowed domains 에
  `huggingface.co`, `*.hf.co` (실제 모델 파일 다운로드 주소) 추가. 기본 패키지 매니저 목록은 유지.
  안내: https://code.claude.com/docs/en/cloud-environments#network-access
- **B. 내 PC에서 생성** — 아래 '생성 절차' 그대로 실행 (Python 3.10+, ffmpeg 필요)

## 생성 절차

```bash
# 두 레포를 나란히 클론해 둔 상태 (english-speaking-app/, didrnrgus.github.io/)
cd english-speaking-app
pip install supertonic soundfile          # ffmpeg 는 별도 설치
P=../didrnrgus.github.io/project_speaking_english

python tools/generate_tts.py --content $P --dry-run   # 대상 목록 (처음엔 370개)
python tools/generate_tts.py --content $P             # 생성 (첫 실행 때 모델 다운로드)
python tools/validate_content.py --content $P --audio # 오류 0개 확인

cd $P/.. && git add project_speaking_english/audio && git commit -m "project_speaking_english: 예문 mp3 추가" && git push
```

- 결과: `audio/en/S1-01.mp3` … `audio/ko/D5-05_t12.mp3` (370개) + `audio/tts_manifest.json`
- 문장을 고친 뒤 다시 실행하면 `tts_manifest.json` 과 비교해 **바뀐 문장의 mp3만** 다시 만든다. 전부 다시 만들려면 `--force`

## 결정 사항

### 엔진: Supertonic-3 (Supertone)

조건: **무료 + 영어·한국어 품질 + 앱에 넣어 배포해도 되는 라이선스**

| 후보 | 결과 |
|---|---|
| **Supertonic-3** | ✅ 채택. 영어·한국어(31개 언어)를 한 모델로, 로컬 ONNX 실행, 무료. 코드 MIT / 모델 OpenRAIL-M (상업적 이용 가능, 사용 제한 조항 준수) |
| Kokoro-82M | 영어 품질 좋음(Apache 2.0)이나 한국어 미지원 |
| Piper | GPL-3.0, 쓸 만한 한국어 목소리 없음 |
| Meta MMS (한국어) | CC-BY-NC — 상업 이용 불가 |
| Edge TTS / Google 번역 TTS | 비공식 엔드포인트, 앱 배포용 약관 불명확 (세션에서도 차단됨) |
| Google Cloud / Azure TTS | 품질 좋지만 계정·결제 등록 필요, 생성 음성 재배포 약관 확인 필요 |

> 출시 전에 Supertonic-3 모델 라이선스(OpenRAIL-M) 전문을 한 번 더 확인할 것.

### 생성 설정 (`tools/generate_tts.py` 상단 상수)

| 항목 | 값 | 이유 |
|---|---|---|
| 목소리 | 말하기 트랙 `F1`, 대화 A=`F1` / B=`M1` | PLAN 5-2: 말하기는 1개 목소리, 대화는 화자별로 다르게. 영어·한국어 같은 목소리 |
| 속도 | 1.0 | 느리게 듣기는 플레이어 재생 속도(0.7x / 0.85x)로 처리 |
| 품질 단계 | `total_steps=8` | Supertonic 기본값 |
| 포맷 | mp3 64kbps 모노 | 음성 전용이라 충분. 370개 합계 약 10MB 예상 (영어 약 8,900자 + 한국어 약 4,300자 기준 추정) |

### 저장 위치: Pages 레포, Git LFS 사용 안 함

- 예문 JSON 과 같은 곳(`project_speaking_english/`)에 둔다. JSON 의 `audio` 경로가 이 폴더 기준 상대 경로라 URL 이 바로 맞는다
  → `https://didrnrgus.github.io/project_speaking_english/audio/en/S1-01.mp3`
- **LFS 를 쓰면 안 된다.** GitHub Pages 는 LFS 파일 대신 포인터 텍스트를 내보내므로 앱이 mp3 를 받을 수 없다.
  전체 약 10MB 라 일반 커밋으로 충분하다 (Pages 사이트 한도 1GB).
- 앱 레포에는 mp3 를 두지 않는다 (기존 빈 `audio/` 폴더 삭제). 앱은 원격 mp3 를 받아 캐시해 두고 재생한다.

## 남은 일

1. 위 해결 방법 A/B 중 하나로 mp3 370개 생성 → Pages 레포 커밋·배포
2. 생성 후 몇 개 들어 보고 목소리(F1/M1)·속도가 학습용으로 괜찮은지 확인 — 바꾸려면 상수 수정 후 `--force`
3. 앱(M1~M2): mp3 다운로드·캐시 구현, 첫 실행이 오프라인일 때 처리 방식 결정 (PLAN 10장 5번)
