using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>색상·크기. 기준 해상도 1080 x 1920 (세로).</summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex("#FFF4E3");
        public static readonly Color Surface = Hex("#FFFFFF");
        public static readonly Color SurfaceAlt = Hex("#FFF9F0");
        public static readonly Color Line = Hex("#EFE4D3");
        public static readonly Color Text = Hex("#2F2A3D");
        public static readonly Color TextSub = Hex("#8A8399");
        public static readonly Color TextOnColor = Hex("#FFFFFF");

        public static readonly Color Primary = Hex("#FF7A45");
        public static readonly Color PrimaryDark = Hex("#E8612C");
        public static readonly Color Speaking = Hex("#4C9AFF");
        public static readonly Color Dialogue = Hex("#2DBE8C");
        public static readonly Color En = Hex("#4C9AFF");
        public static readonly Color Ko = Hex("#FF7A45");
        public static readonly Color SpeakerA = Hex("#FF8A5C");
        public static readonly Color SpeakerB = Hex("#5B8DEF");
        public static readonly Color Highlight = Hex("#FFE7AE");
        public static readonly Color HighlightLine = Hex("#FFC94D");
        public static readonly Color Track = Hex("#EDE5DA");
        public static readonly Color Danger = Hex("#F2545B");
        public static readonly Color Dim = new Color(0.12f, 0.1f, 0.16f, 0.55f);
        public static readonly Color AdSlotBg = Hex("#F5EBDC"); // 하단 배너 광고 띠 (앱 버튼과 구분되는 차분한 색)
        public static readonly Color Shadow = new Color(0.35f, 0.25f, 0.1f, 0.12f);

        public const float TopBarHeight = 150f;
        public const float Gutter = 40f;

        public static Color TrackColor(bool dialogue) => dialogue ? Dialogue : Speaking;
        public static Color LangColor(Lang lang) => lang == Lang.En ? En : Ko;
        public static string LangName(Lang lang) => lang == Lang.En ? "영어" : "한국어";
        public static string LangShort(Lang lang) => lang == Lang.En ? "영" : "한";

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
