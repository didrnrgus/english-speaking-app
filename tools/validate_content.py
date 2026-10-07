"""content/*.json 검증: 스키마/필드 누락/id 중복/오디오 경로 규칙/스테이지별 개수.

사용법:
    python tools/validate_content.py              # 콘텐츠 구조 검사
    python tools/validate_content.py --audio      # + audio/ 아래 mp3 존재 여부까지 검사
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "content"
ITEMS_PER_STAGE = 5
# 대화 스테이지별 턴 수 (docs/PLAN.md 3장)
DIALOGUE_TURNS = {"D1": 2, "D2": 4, "D3": 6, "D4": 8, "D5": 12}


def check_text_audio(obj, audio_id, where, errors, audio_paths):
    for key in ("en", "ko"):
        if not isinstance(obj.get(key), str) or not obj[key].strip():
            errors.append(f"{where}: '{key}' 텍스트 누락")
    audio = obj.get("audio") or {}
    for lang in ("en", "ko"):
        expected = f"audio/{lang}/{audio_id}.mp3"
        if audio.get(lang) != expected:
            errors.append(f"{where}: audio.{lang} = {audio.get(lang)!r}, 기대값 {expected!r}")
        else:
            audio_paths.append(expected)


def validate():
    errors, ids, audio_paths = [], set(), []
    files = sorted(CONTENT.glob("*/*.json"))
    if not files:
        errors.append("content/ 아래 JSON 파일이 없음")
    for path in files:
        rel = path.relative_to(ROOT)
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            errors.append(f"{rel}: JSON 파싱 실패 ({e})")
            continue
        stage, track = data.get("stage"), data.get("track")
        if path.stem.upper() != stage:
            errors.append(f"{rel}: 파일명과 stage({stage}) 불일치")
        expected_track = {"S": "speaking", "D": "dialogue"}.get((stage or " ")[0])
        if track != expected_track or path.parent.name != track:
            errors.append(f"{rel}: track({track}) 오류")
        if not data.get("title"):
            errors.append(f"{rel}: title 누락")
        items = data.get("items") or []
        if len(items) != ITEMS_PER_STAGE:
            errors.append(f"{rel}: 예문 {len(items)}개 (기대 {ITEMS_PER_STAGE}개)")
        for item in items:
            iid = item.get("id", "")
            where = f"{rel}:{iid or '?'}"
            if not iid.startswith(f"{stage}-"):
                errors.append(f"{where}: id 형식 오류")
            if iid in ids:
                errors.append(f"{where}: id 중복")
            ids.add(iid)
            if track == "speaking":
                check_text_audio(item, iid, where, errors, audio_paths)
            elif track == "dialogue":
                turns = item.get("turns") or []
                if len(turns) != DIALOGUE_TURNS.get(stage):
                    errors.append(f"{where}: 턴 {len(turns)}개 (기대 {DIALOGUE_TURNS.get(stage)}개)")
                for n, turn in enumerate(turns, 1):
                    if turn.get("speaker") not in ("A", "B"):
                        errors.append(f"{where} t{n}: speaker는 A/B")
                    check_text_audio(turn, f"{iid}_t{n}", f"{where} t{n}", errors, audio_paths)
    return errors, ids, audio_paths


def main():
    errors, ids, audio_paths = validate()
    if "--audio" in sys.argv:
        errors += [f"오디오 파일 없음: {p}" for p in audio_paths if not (ROOT / p).is_file()]
    for e in errors:
        print("ERROR", e)
    print(f"예문 {len(ids)}개, 오디오 경로 {len(audio_paths)}개 검사, 오류 {len(errors)}개")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
