"""content/*.json → audio/{en,ko}/*.mp3 생성.

TTS 엔진은 아직 미정 (docs/PLAN.md 5장, 10장: 재배포 약관 확인 필요).
엔진이 정해지면 synthesize() 만 구현하면 된다.

사용법:
    python tools/generate_tts.py --dry-run   # 생성 대상 목록만 출력
    python tools/generate_tts.py             # 없는 파일만 생성
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# 화자별 목소리 (PLAN 5-2): 말하기 트랙은 중립 1개, 대화는 A=여성 / B=남성
VOICES = {"en": {None: "en-US-neutral", "A": "en-US-female", "B": "en-US-male"},
          "ko": {None: "ko-KR-neutral", "A": "ko-KR-female", "B": "ko-KR-male"}}


def synthesize(text, lang, voice, out_path):
    raise NotImplementedError("TTS 엔진 선택 후 구현 (Google Cloud TTS / Azure Neural TTS 등)")


def targets():
    for path in sorted((ROOT / "content").glob("*/*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        for item in data["items"]:
            for seg in item.get("turns", [item]):
                for lang in ("en", "ko"):
                    voice = VOICES[lang][seg.get("speaker")]
                    yield seg[lang], lang, voice, ROOT / seg["audio"][lang]


def main():
    dry = "--dry-run" in sys.argv
    todo = [t for t in targets() if not t[3].exists()]
    for text, lang, voice, out in todo:
        print(f"{out.relative_to(ROOT)}  [{voice}]  {text[:50]}")
        if not dry:
            out.parent.mkdir(parents=True, exist_ok=True)
            synthesize(text, lang, voice, out)
    print(f"생성 대상 {len(todo)}개")


if __name__ == "__main__":
    main()
