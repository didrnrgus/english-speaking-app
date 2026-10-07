using System;
using System.Collections.Generic;

namespace EnglishApp
{
    public enum Lang
    {
        En = 0,
        Ko = 1,
    }

    // JSON 스키마는 docs/PLAN.md 4-2 와 같다 (JsonUtility 로 읽음).

    [Serializable]
    public class ContentIndex
    {
        public int version;
        public List<LevelInfo> levels = new List<LevelInfo>();
        public List<UnitInfo> units = new List<UnitInfo>();
    }

    [Serializable]
    public class LevelInfo
    {
        public string level;
        public string track; // "speaking" | "dialogue"
        public string title;
        public string desc;

        public bool IsDialogue => track == "dialogue";
    }

    [Serializable]
    public class UnitInfo
    {
        public string unit;
        public string level;
        public string title;
        public string file;

        /// <summary>"S2-U03" → "U03"</summary>
        public string ShortCode
        {
            get
            {
                int dash = unit != null ? unit.LastIndexOf('-') : -1;
                return dash >= 0 ? unit.Substring(dash + 1) : unit;
            }
        }
    }

    [Serializable]
    public class AudioPaths
    {
        public string en;
        public string ko;

        public string Get(Lang lang) => lang == Lang.En ? en : ko;
    }

    [Serializable]
    public class TurnData
    {
        public string speaker;
        public string en;
        public string ko;
        public AudioPaths audio;
    }

    [Serializable]
    public class ItemData
    {
        public string id;
        public string en; // 말하기
        public string ko;
        public AudioPaths audio;
        public string topic; // 대화
        public List<TurnData> turns;

        public bool IsDialogue => turns != null && turns.Count > 0;

        [NonSerialized] private List<ContentLine> lines;

        /// <summary>
        /// 재생 단위(줄). 말하기는 예문 1줄, 대화는 턴마다 1줄.
        /// 전체 듣기 시퀀스는 줄마다 적용된다.
        /// </summary>
        public List<ContentLine> Lines
        {
            get
            {
                if (lines != null) return lines;
                lines = new List<ContentLine>();
                if (IsDialogue)
                {
                    foreach (var t in turns)
                        lines.Add(new ContentLine { speaker = t.speaker, en = t.en, ko = t.ko, audio = t.audio });
                }
                else
                {
                    lines.Add(new ContentLine { speaker = null, en = en, ko = ko, audio = audio });
                }
                return lines;
            }
        }
    }

    public class ContentLine
    {
        public string speaker; // 대화만 "A" / "B"
        public string en;
        public string ko;
        public AudioPaths audio;

        public string Text(Lang lang) => lang == Lang.En ? en : ko;
    }

    [Serializable]
    public class UnitData
    {
        public string unit;
        public int version;
        public string level;
        public string track;
        public string title;
        public List<ItemData> items = new List<ItemData>();

        public bool IsDialogue => track == "dialogue";

        /// <summary>이 유닛의 모든 mp3 상대 경로 (중복 없음).</summary>
        public List<string> AllAudioPaths()
        {
            var set = new HashSet<string>();
            var list = new List<string>();
            foreach (var item in items)
            foreach (var line in item.Lines)
            {
                if (line.audio == null) continue;
                foreach (var p in new[] { line.audio.en, line.audio.ko })
                    if (!string.IsNullOrEmpty(p) && set.Add(p)) list.Add(p);
            }
            return list;
        }

        /// <summary>앱이 쓸 수 있는 파일인지 (필수 필드 누락 검사).</summary>
        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(unit)) { error = "unit 없음"; return false; }
            if (items == null || items.Count == 0) { error = "items 없음"; return false; }
            foreach (var item in items)
            {
                if (item == null) { error = "빈 item"; return false; }
                var lines = item.Lines;
                if (lines.Count == 0) { error = item.id + ": 문장 없음"; return false; }
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line.en) || string.IsNullOrEmpty(line.ko))
                    { error = item.id + ": en/ko 없음"; return false; }
                    if (line.audio == null || string.IsNullOrEmpty(line.audio.en) || string.IsNullOrEmpty(line.audio.ko))
                    { error = item.id + ": audio 경로 없음"; return false; }
                }
            }
            return true;
        }
    }
}
