"""콘텐츠 JSON → <콘텐츠 폴더>/audio/{en,ko}/*.mp3 생성 (Supertonic-3, 로컬 실행).

엔진: Supertonic-3 (Supertone) — 영어·한국어를 한 모델로, 무료, 오프라인 실행.
      코드 MIT / 모델 OpenRAIL-M (상업적 이용 가능, 사용 제한 조항 있음).
      첫 실행 때 Hugging Face 에서 모델(~400MB)을 받는다.
오디오 원본은 GitHub Pages 레포에 둔다 (JSON 의 audio 경로는 콘텐츠 폴더 기준 상대 경로).
Pages 는 Git LFS 파일을 서빙하지 않으므로 mp3 는 일반 파일로 커밋한다.

audio/tts_manifest.json 에 파일별 (문장, 목소리, 설정)을 기록해 두고,
문장을 고치면 바뀐 파일만 다시 만든다.

사용법:
    pip install supertonic soundfile    # + ffmpeg
    python tools/generate_tts.py --content ../didrnrgus.github.io/project_speaking_english --dry-run
    python tools/generate_tts.py --content ../didrnrgus.github.io/project_speaking_english
    python tools/generate_tts.py --content <폴더> --force   # 전부 다시 생성
"""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

MODEL = "supertonic-3"
# 화자별 목소리 (PLAN 5-2): 말하기 트랙은 1개, 대화는 A=여성 / B=남성. 영어·한국어 같은 목소리
VOICES = {None: "F1", "A": "F1", "B": "M1"}
SPEED = 1.0           # 재생 속도는 플레이어에서 조절 (PLAN 5-2)
TOTAL_STEPS = 8       # 품질/속도 트레이드오프 (Supertonic 기본값)
MP3_ARGS = ["-ac", "1", "-codec:a", "libmp3lame", "-b:a", "64k"]  # 음성용 모노 64kbps
MANIFEST = "audio/tts_manifest.json"


def targets(content):
    for path in sorted(content.glob("*/*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        for item in data["items"]:
            for seg in item.get("turns", [item]):
                for lang in ("en", "ko"):
                    spec = {"text": seg[lang], "lang": lang, "voice": VOICES[seg.get("speaker")],
                            "model": MODEL, "speed": SPEED, "steps": TOTAL_STEPS}
                    yield seg["audio"][lang], spec


def main():
    args = sys.argv[1:]
    if "--content" not in args:
        sys.exit(__doc__)
    content = Path(args[args.index("--content") + 1]).resolve()
    dry, force = "--dry-run" in args, "--force" in args

    manifest_path = content / MANIFEST
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}
    all_targets = list(targets(content))
    todo = [(rel, spec) for rel, spec in all_targets
            if force or manifest.get(rel) != spec or not (content / rel).exists()]
    for rel, spec in todo:
        print(f"{rel}  [{spec['voice']}]  {spec['text'][:50]}")
    print(f"생성 대상 {len(todo)}개 / 전체 {len(all_targets)}개")
    if dry or not todo:
        return

    from supertonic import TTS  # 실제 생성할 때만 필요
    tts = TTS(model=MODEL)
    styles = {}
    with tempfile.TemporaryDirectory() as tmp:
        wav_path = Path(tmp) / "out.wav"
        for n, (rel, spec) in enumerate(todo, 1):
            style = styles.setdefault(spec["voice"], tts.get_voice_style(spec["voice"]))
            wav, _ = tts.synthesize(spec["text"], voice_style=style, lang=spec["lang"],
                                    speed=SPEED, total_steps=TOTAL_STEPS)
            tts.save_audio(wav, str(wav_path))
            out = content / rel
            out.parent.mkdir(parents=True, exist_ok=True)
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav_path), *MP3_ARGS, str(out)],
                           check=True)
            manifest[rel] = spec
            print(f"[{n}/{len(todo)}] {rel}")

    # 콘텐츠에서 빠진 항목은 manifest 에서도 정리
    live = {rel for rel, _ in all_targets}
    manifest = {k: manifest[k] for k in sorted(manifest) if k in live}
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
