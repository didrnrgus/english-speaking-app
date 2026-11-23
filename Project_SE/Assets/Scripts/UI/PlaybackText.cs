using EnglishApp.Audio;

namespace EnglishApp.UI
{
    /// <summary>
    /// 재생 상태 문구 (모든 화면이 같은 말을 쓰도록 한곳에).
    /// 전체 듣기는 두 가지라 항상 범위를 붙여 부른다:
    ///   레벨 전체 듣기 → "S1 전체 듣기"   (레벨의 유닛 10개를 이어서)
    ///   유닛 전체 듣기 → "유닛 전체 듣기" (유닛 하나)
    /// </summary>
    public static class PlaybackText
    {
        /// <summary>강조색 (레벨 화면의 '전체 듣기 중' 과 같은 주황).</summary>
        public const string Accent = "#FF7A45";

        public static string Color(string text, string hex = Accent) => "<color=" + hex + ">" + text + "</color>";

        /// <summary>"S1 전체 듣기"</summary>
        public static string LevelName(LevelInfo level) => level.level + " 전체 듣기";

        public const string UnitName = "유닛 전체 듣기";

        /// <summary>"S2-U03" → "U03"</summary>
        public static string ShortCode(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return "";
            int dash = unitId.LastIndexOf('-');
            return dash >= 0 ? unitId.Substring(dash + 1) : unitId;
        }

        /// <summary>준비 중 문구: "음성 받는 중 37/300" / "준비 중…"</summary>
        public static string Preparing(AudioPlayer p, string prefix = "음성 받는 중 ")
        {
            return p.PrepareTotal > 0 ? prefix + p.PrepareDone + "/" + p.PrepareTotal : "준비 중…";
        }

        /// <summary>지금 유닛: "U03 가족"</summary>
        public static string CurrentUnit(AudioPlayer p) => ShortCode(p.UnitId) + " " + p.UnitTitle;

        /// <summary>유닛 진행: "유닛 3/10"</summary>
        public static string UnitProgress(AudioPlayer p) => "유닛 " + (p.UnitIndex + 1) + "/" + p.UnitCount;

        /// <summary>예문 진행: "예문 2/5" (재생 위치를 모르면 null).</summary>
        public static string PageProgress(AudioPlayer p)
        {
            var step = p.CurrentStep;
            var unit = p.Unit;
            if (step == null || unit == null) return null;
            return "예문 " + (step.page + 1) + "/" + unit.items.Count;
        }

        /// <summary>a · b · c (null/빈 값은 건너뜀).</summary>
        public static string Join(params string[] parts)
        {
            string s = "";
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                s += s.Length == 0 ? part : " · " + part;
            }
            return s;
        }
    }
}
