"""콘텐츠 폴더 검증: index.json(레벨/유닛 목록)/유닛 파일 스키마/필드 누락/id 중복/오디오 경로 규칙/개수.

구조: 레벨(S1~S5, D1~D5) × 유닛(U01, U02, ...) × 유닛당 예문 5개
    index.json                      레벨·유닛 목록 (앱이 가장 먼저 읽음)
    speaking/s1/u01.json ...        유닛 파일
    audio/{en,ko}/S1/S1-U01-01.mp3  음성 (대화는 D3/D3-U01-01_t1.mp3)

사용법:
    python tools/validate_content.py              # content/ 구조 검사
    python tools/validate_content.py --content ../didrnrgus.github.io/project_speaking_english
                                                  # 원격(GitHub Pages) 콘텐츠 폴더 검사
    python tools/validate_content.py --content <폴더> --audio   # + mp3 존재 여부까지
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ITEMS_PER_UNIT = 5
TRACKS = {"S": "speaking", "D": "dialogue"}
# 대화 레벨별 턴 수 (docs/PLAN.md 3장)
DIALOGUE_TURNS = {"D1": 2, "D2": 4, "D3": 6, "D4": 8, "D5": 12}
LEVEL_RE = re.compile(r"^[SD][1-5]$")
UNIT_RE = re.compile(r"^([SD][1-5])-U(\d{2})$")


def is_version(v):
    return isinstance(v, int) and not isinstance(v, bool) and v >= 1


def load(path, errors, where):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        errors.append(f"{where}: 파일 없음")
    except json.JSONDecodeError as e:
        errors.append(f"{where}: JSON 파싱 실패 ({e})")
    return None


def check_text_audio(obj, level, audio_id, where, errors, audio_paths):
    for key in ("en", "ko"):
        if not isinstance(obj.get(key), str) or not obj[key].strip():
            errors.append(f"{where}: '{key}' 텍스트 누락")
    audio = obj.get("audio") or {}
    for lang in ("en", "ko"):
        expected = f"audio/{lang}/{level}/{audio_id}.mp3"
        if audio.get(lang) != expected:
            errors.append(f"{where}: audio.{lang} = {audio.get(lang)!r}, 기대값 {expected!r}")
        else:
            audio_paths.append(expected)


def check_unit(data, entry, rel, errors, ids, audio_paths):
    unit, level = entry["unit"], entry["level"]
    track = TRACKS[level[0]]
    for key, want in (("unit", unit), ("level", level), ("track", track), ("title", entry.get("title"))):
        if data.get(key) != want:
            errors.append(f"{rel}: {key}={data.get(key)!r}, index.json 과 다름 ({want!r})")
    if not is_version(data.get("version")):
        errors.append(f"{rel}: version 은 1 이상의 정수")
    items = data.get("items") or []
    if len(items) != ITEMS_PER_UNIT:
        errors.append(f"{rel}: 예문 {len(items)}개 (기대 {ITEMS_PER_UNIT}개)")
    for n, item in enumerate(items, 1):
        iid = item.get("id", "")
        where = f"{rel}:{iid or '?'}"
        if iid != f"{unit}-{n:02d}":
            errors.append(f"{where}: id 는 {unit}-{n:02d} 이어야 함 (순서대로)")
        if iid in ids:
            errors.append(f"{where}: id 중복")
        ids.add(iid)
        if track == "speaking":
            check_text_audio(item, level, iid, where, errors, audio_paths)
        else:
            if not item.get("topic"):
                errors.append(f"{where}: topic 누락")
            turns = item.get("turns") or []
            if len(turns) != DIALOGUE_TURNS[level]:
                errors.append(f"{where}: 턴 {len(turns)}개 (기대 {DIALOGUE_TURNS[level]}개)")
            for t, turn in enumerate(turns, 1):
                if turn.get("speaker") != "AB"[(t - 1) % 2]:
                    errors.append(f"{where} t{t}: speaker 는 A/B 번갈아")
                check_text_audio(turn, level, f"{iid}_t{t}", f"{where} t{t}", errors, audio_paths)


def validate(content):
    errors, ids, audio_paths = [], set(), []
    index = load(content / "index.json", errors, "index.json")
    if index is None:
        return errors, ids, audio_paths
    if not is_version(index.get("version")):
        errors.append("index.json: version 은 1 이상의 정수")

    levels = {}
    for lv in index.get("levels") or []:
        level = lv.get("level", "")
        if not LEVEL_RE.match(level) or lv.get("track") != TRACKS[level[0]]:
            errors.append(f"index.json: 레벨 {level!r} 의 level/track 오류")
            continue
        if not lv.get("title"):
            errors.append(f"index.json: 레벨 {level} title 누락")
        if level in levels:
            errors.append(f"index.json: 레벨 {level} 중복")
        levels[level] = lv

    listed, seen_units = set(), set()
    for entry in index.get("units") or []:
        unit, level, file = entry.get("unit", ""), entry.get("level"), entry.get("file")
        m = UNIT_RE.match(unit)
        if not m or m.group(1) != level or level not in levels:
            errors.append(f"index.json: 유닛 {unit!r} 의 unit/level 오류")
            continue
        if unit in seen_units:
            errors.append(f"index.json: 유닛 {unit} 중복")
        seen_units.add(unit)
        if not entry.get("title"):
            errors.append(f"index.json: 유닛 {unit} title 누락")
        expected_file = f"{TRACKS[level[0]]}/{level.lower()}/u{m.group(2)}.json"
        if file != expected_file:
            errors.append(f"index.json: 유닛 {unit} 의 file({file}) 은 {expected_file} 이어야 함")
            continue
        listed.add(file)
        data = load(content / file, errors, file)
        if data is not None:
            check_unit(data, entry, file, errors, ids, audio_paths)

    on_disk = {f.relative_to(content).as_posix() for t in TRACKS.values() for f in content.glob(f"{t}/*/*.json")}
    for f in sorted(on_disk - listed):
        errors.append(f"index.json: {f} 가 목록에 없음 (앱이 받지 않음)")
    return errors, ids, audio_paths


def main():
    args = sys.argv[1:]
    content = ROOT / "content"
    if "--content" in args:
        content = Path(args[args.index("--content") + 1]).resolve()
    errors, ids, audio_paths = validate(content)
    if "--audio" in args:
        errors += [f"오디오 파일 없음: {p}" for p in audio_paths if not (content / p).is_file()]
        extra = {f.relative_to(content).as_posix() for f in content.glob("audio/*/*/*.mp3")} - set(audio_paths)
        errors += [f"콘텐츠에 없는 오디오 파일: {p}" for p in sorted(extra)]
    for e in errors:
        print("ERROR", e)
    print(f"예문 {len(ids)}개, 오디오 경로 {len(audio_paths)}개 검사, 오류 {len(errors)}개")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
