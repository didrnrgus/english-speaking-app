"""content/ 검증: index.json 목록/스키마/필드 누락/id 중복/오디오 경로 규칙/스테이지별 개수.

사용법:
    python tools/validate_content.py              # 콘텐츠 구조 검사
    python tools/validate_content.py --audio      # + <콘텐츠 폴더>/audio/ 아래 mp3 존재 여부까지 검사
    python tools/validate_content.py --content ../didrnrgus.github.io/project_speaking_english
                                                  # 원격(GitHub Pages) 콘텐츠 폴더 검사
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
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


def is_version(v):
    return isinstance(v, int) and not isinstance(v, bool) and v >= 1


def check_index(content, files, errors):
    path = content / "index.json"
    try:
        index = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        errors.append("index.json 없음")
        return
    except json.JSONDecodeError as e:
        errors.append(f"index.json: JSON 파싱 실패 ({e})")
        return
    if not is_version(index.get("version")):
        errors.append("index.json: version 은 1 이상의 정수")
    listed = []
    for entry in index.get("stages") or []:
        file, stage, track = entry.get("file"), entry.get("stage"), entry.get("track")
        if file != f"{track}/{(stage or '').lower()}.json":
            errors.append(f"index.json: {stage} 의 file({file}) 이 규칙과 다름")
        listed.append(file)
    if len(set(listed)) != len(listed):
        errors.append("index.json: 같은 파일이 중복으로 등록됨")
    on_disk = [f.relative_to(content).as_posix() for f in files]
    for f in sorted(set(listed) - set(on_disk)):
        errors.append(f"index.json: 등록된 {f} 파일이 없음")
    for f in sorted(set(on_disk) - set(listed)):
        errors.append(f"index.json: {f} 가 목록에 없음 (앱이 받지 않음)")


def validate(content):
    errors, ids, audio_paths = [], set(), []
    files = sorted(content.glob("*/*.json"))
    if not files:
        errors.append(f"{content} 아래 JSON 파일이 없음")
    check_index(content, files, errors)
    for path in files:
        rel = path.relative_to(content)
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
        if not is_version(data.get("version")):
            errors.append(f"{rel}: version 은 1 이상의 정수")
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
    args = sys.argv[1:]
    content = ROOT / "content"
    if "--content" in args:
        content = Path(args[args.index("--content") + 1]).resolve()
    errors, ids, audio_paths = validate(content)
    if "--audio" in sys.argv:
        errors += [f"오디오 파일 없음: {p}" for p in audio_paths if not (content / p).is_file()]
    for e in errors:
        print("ERROR", e)
    print(f"예문 {len(ids)}개, 오디오 경로 {len(audio_paths)}개 검사, 오류 {len(errors)}개")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
