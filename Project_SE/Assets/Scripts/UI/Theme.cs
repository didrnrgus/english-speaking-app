using System;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>
    /// 색상·크기. 기준 해상도 1080 x 1920 (세로).
    /// 밝은 / 어두운 모드에서 바뀌는 색(Bg, Text 등)은 지금 팔레트(ThemePalette)에서 읽는다.
    /// 프리팹 색은 ThemeColor 가 모드가 바뀔 때 다시 칠한다.
    /// </summary>
    public static class Theme
    {
        private const string PrefsKey = "ui_theme_mode";

        // ---- 두 모드 공통 ------------------------------------------------------------------

        public static readonly Color TextOnColor = Hex("#FFFFFF");
        public static readonly Color Primary = Hex("#FF7A45");
        public static readonly Color PrimaryDark = Hex("#E8612C");
        public static readonly Color Speaking = Hex("#4C9AFF");
        public static readonly Color Dialogue = Hex("#2DBE8C");
        public static readonly Color En = Hex("#4C9AFF");
        public static readonly Color Ko = Hex("#FF7A45");
        public static readonly Color SpeakerA = Hex("#FF8A5C");
        public static readonly Color SpeakerB = Hex("#5B8DEF");
        public static readonly Color Danger = Hex("#F2545B");
        public static readonly Color Dim = new Color(0.12f, 0.1f, 0.16f, 0.55f);
        public static readonly Color Shadow = new Color(0.35f, 0.25f, 0.1f, 0.12f);

        // ---- 모드에 따라 바뀜 (팔레트) -----------------------------------------------------

        private static readonly PaletteColors LightDefaults = ThemePalette.LightColors();
        private static ThemePalette lightPalette;
        private static ThemePalette darkPalette;

        public static ThemeMode Mode { get; private set; } = ThemeMode.Light;

        /// <summary>지금 모드의 팔레트 (아직 없으면 null — 그때는 밝은 모드 기본값).</summary>
        public static ThemePalette Palette => Mode == ThemeMode.Dark ? darkPalette : lightPalette;

        private static PaletteColors C => Palette != null ? Palette.colors : LightDefaults;

        public static Color Bg => C.bg;
        public static Color Surface => C.surface;
        public static Color Line => C.line;
        public static Color Text => C.text;
        public static Color TextSub => C.textSub;
        public static Color Track => C.track;
        public static Color Highlight => C.highlight;
        public static Color GlassHighlight => C.glassHighlight;
        public static Color HighlightText => C.highlightText;
        public static Color AdSlotBg => C.adSlotBg;

        /// <summary>모드가 바뀌었을 때 (ThemeColor 가 다시 칠하고, 화면이 아이콘 등을 바꾼다).</summary>
        public static event Action Changed;

        /// <summary>앱 시작 때: 팔레트를 받고 저장해 둔 모드를 불러온다.</summary>
        public static void Init(ThemePalette light, ThemePalette dark)
        {
            lightPalette = light;
            darkPalette = dark;
            Mode = PlayerPrefs.GetInt(PrefsKey, 0) == 1 && dark != null ? ThemeMode.Dark : ThemeMode.Light;
        }

        public static void SetMode(ThemeMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            PlayerPrefs.SetInt(PrefsKey, (int)mode);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Toggle() => SetMode(Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark);

        // ---- 크기 ---------------------------------------------------------------------------

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
