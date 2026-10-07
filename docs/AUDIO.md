# 음성(mp3) 생성 기록

> 최종 갱신: 2026-10-07

## 현재 상태

| 항목 | 상태 |
|---|---|
| 생성 스크립트 `tools/generate_tts.py` | ✅ 구현 완료 (Supertonic-3) |
| 검사 `tools/validate_content.py --audio` | ✅ 콘텐츠 폴더 기준으로 mp3 존재 여부 검사 — 오류 0개 |
| mp3 파일 | ✅ **3,700 / 3,700 생성, Pages `main` 배포** — 예문 500개(100유닛), 약 111MB, 영어 약 107분 + 한국어 약 115분 |
| 저장 위치 | Pages 레포 `didrnrgus.github.io/project_speaking_english/audio/{en,ko}/<레벨>/` + `audio/tts_manifest.json` |
| 청취 확인 | 샘플(S2-U01-01, D3-U01-01 t1/t2 영·한) 들어 보고 채택. 영어가 약간 빠르게 느껴짐 → 생성 속도는 1.0 유지, 앱 전역 속도 설정으로 해결 (PLAN 6-4) |
| 이력 | 1차 370개(예문 50개) → 유닛 구조로 확장하며 기존 370개는 U01 경로로 옮기고(재생성 없음) 3,330개 추가 생성 |

자동 점검(3,700개 전체): 파일별 음량 고름(평균 -31 ~ -25 dB, 클리핑 0개), 문장 길이 대비 재생 시간 이상치 없음
(한두 단어짜리 35개는 모델 최소 길이 1.44초 때문에 느리게 측정됨 — 정상). 철자를 한 글자씩 읽는 문장("M-I-N-A")은 TTS가 단어로 읽을 수 있어 다른 대화로 교체함.
음성 인식으로 내용을 대조하는 검사는 하지 않았다 — 새 문장을 만들면 몇 개 직접 들어 볼 것.

### 생성 환경 메모

- Claude Code 클라우드 세션에서 만들려면 환경 Network access 에 `huggingface.co`, `*.hf.co` 가 허용돼 있어야 한다 (모델 다운로드).
  안내: https://code.claude.com/docs/en/cloud-environments#network-access
- 내 PC에서는 아래 '생성 절차' 그대로 실행 (Python 3.10+, ffmpeg 필요). 4코어 CPU 기준 3,330개 약 70분 (370개 약 8분)
- 생성기는 50개마다 `tts_manifest.json` 을 저장하므로 중간에 끊겨도 다시 실행하면 남은 것만 만든다
- 비용: 0원. 모델 파일만 받아 로컬에서 실행 (Hugging Face API·크레딧·계정 사용 안 함)

## 생성 절차

```bash
# 두 레포를 나란히 클론해 둔 상태 (english-speaking-app/, didrnrgus.github.io/)
cd english-speaking-app
pip install supertonic soundfile          # ffmpeg 는 별도 설치
P=../didrnrgus.github.io/project_speaking_english

python tools/generate_tts.py --content $P --dry-run   # 대상 목록 (새로 만들거나 문장이 바뀐 것만)
python tools/generate_tts.py --content $P             # 생성 (첫 실행 때 모델 다운로드)
python tools/validate_content.py --content $P --audio # 오류 0개 확인

cd $P/.. && git add project_speaking_english/audio && git commit -m "project_speaking_english: 예문 mp3 추가" && git push
```

- 결과: `audio/en/S1/S1-U01-01.mp3` … `audio/ko/D5/D5-U10-05_t12.mp3` (3,700개) + `audio/tts_manifest.json`
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

#### Supertonic-3 라이선스 (BigScience OpenRAIL-M, 원문 확인함)

- 상업적 이용 가능, 로열티 없음. 6조: 생성한 출력물(음성)에 대해 라이선스 제공자는 권리를 주장하지 않음
- 부록 A 사용 제한(불법, 미성년자 착취, 딥페이크·사칭, 차별, 의료 조언 등) — 영어 학습 앱과는 무관
- **부록 A (e)항: 생성 콘텐츠를 배포할 때 기계가 만든 것임을 명확히 고지해야 함**
  → 앱 설정 '정보'에 "음성은 AI(TTS)로 생성되었습니다", 스토어 설명에도 같은 문구 (PLAN 7장, 10장 1번)
- 모델 파일 자체는 앱에 넣지 않으므로 모델 재배포 조건은 해당 없음

> 조건 없는 라이선스가 필요해지면 대안: 영어 Kokoro-82M (Apache-2.0) + 한국어 Qwen3-TTS CustomVoice 의 Sohee (Apache-2.0) 또는 MeloTTS-Korean (MIT).
> 단 Qwen3-TTS 는 원어민 목소리가 한국어 여성·영어 남성뿐이라 대화 A/B 구성이 제한적이다.

> 출시 전에 Supertonic-3 모델 라이선스(OpenRAIL-M) 전문을 한 번 더 확인할 것.

### 생성 설정 (`tools/generate_tts.py` 상단 상수)

| 항목 | 값 | 이유 |
|---|---|---|
| 목소리 | 말하기 트랙 `F1`, 대화 A=`F1` / B=`M1` | PLAN 5-2: 말하기는 1개 목소리, 대화는 화자별로 다르게. 영어·한국어 같은 목소리 |
| 속도 | 1.0 | 빠르게/느리게는 앱 설정 팝업의 전역 재생 속도(x1.5 / x1.25 / x1.0 / x0.75 / x0.5)로 처리 (PLAN 6-4) |
| 품질 단계 | `total_steps=8` | Supertonic 기본값 |
| 포맷 | mp3 64kbps 모노 | 음성 전용이라 충분. 3,700개 합계 약 111MB |

### 저장 위치: Pages 레포, Git LFS 사용 안 함

- 예문 JSON 과 같은 곳(`project_speaking_english/`)에 둔다. JSON 의 `audio` 경로가 이 폴더 기준 상대 경로라 URL 이 바로 맞는다
  → `https://didrnrgus.github.io/project_speaking_english/audio/en/S1/S1-U01-01.mp3`
- **LFS 를 쓰면 안 된다.** GitHub Pages 는 LFS 파일 대신 포인터 텍스트를 내보내므로 앱이 mp3 를 받을 수 없다.
  전체 약 111MB 라 일반 커밋으로 충분하다 (Pages 사이트 한도 1GB).
- 앱은 **들을 유닛 mp3만** 받아 캐시한다 — GitHub Pages 월 트래픽 권장 한도(약 100GB) 때문 (PLAN 10장 5번)
- 앱 레포에는 mp3 를 두지 않는다 (기존 빈 `audio/` 폴더 삭제). 앱은 원격 mp3 를 받아 캐시해 두고 재생한다.

## 남은 일

1. **S5/D5 '1분' 길이:** 실제 영어 재생 시간은 예문당 약 30~40초(중앙값 34초, 속도 1.0). 1분을 채우려면 약 150~170단어로 늘리거나, 이름을 '30초 말하기'로 바꾸는 것 중 결정 필요
2. 앱(M1~M2): mp3 다운로드·캐시 구현 (들을 유닛만), 첫 실행이 오프라인일 때 처리 방식 결정 (PLAN 10장 5번)
3. 앱(M3): 설정 팝업의 전역 재생 속도 드롭다운 (PLAN 6-4)
4. 출시 전: 설정 '정보'와 스토어 설명에 AI 음성 고지 문구 넣기
