using System;
using UnityEngine;

namespace EnglishApp.UI
{
    public enum ThemeMode
    {
        Light = 0,
        Dark = 1,
    }

    /// <summary>밝은 / 어두운 모드에서 바뀌는 색.</summary>
    [Serializable]
    public class PaletteColors
    {
        [Tooltip("카메라 배경 (배경 그림 뒤)")] public Color bg;
        [Tooltip("카드 (글래스가 아닌 곳)")] public Color surface;
        [Tooltip("구분선, 번호 원")] public Color line;
        [Tooltip("글자, 아이콘")] public Color text;
        [Tooltip("보조 글자, 보조 아이콘")] public Color textSub;
        [Tooltip("슬라이더 트랙, 탭 바탕, 토글")] public Color track;
        [Tooltip("재생 중인 줄 배경 (문장 카드 안, 글래스 아님)")] public Color highlight;
        [Tooltip("재생 중인 말풍선·시퀀스 줄의 글래스 틴트 (배경에 곱해지므로 밝은 색)")] public Color glassHighlight;
        [Tooltip("재생 중인 줄의 재생 중인 언어 글자")] public Color highlightText;
        [Tooltip("하단 배너 광고 띠")] public Color adSlotBg;
    }

    /// <summary>
    /// 글래스 판의 모드별 톤 (TranslucentImage 의 색 · Foreground Opacity · Brightness).
    /// 글래스 머티리얼(Colorful 모드)은 "배경 밝기를 brightness 쪽으로 opacity 만큼 맞춘 뒤 × 색" 이다.
    /// 그래서 색을 어둡게 하면 배경이 검게 눌린다 — 어둡게 하려면 색은 밝게 두고 brightness 를 낮춘다.
    /// </summary>
    [Serializable]
    public struct GlassTone
    {
        public Color color;
        [Range(0, 1)] public float opacity;
        [Range(-1, 1)] public float brightness;

        public GlassTone(Color color, float opacity, float brightness)
        {
            this.color = color;
            this.opacity = opacity;
            this.brightness = brightness;
        }
    }

    /// <summary>
    /// 화면 모드 하나의 팔레트 (Assets/UI/Themes/Light.asset, Dark.asset). 인스펙터에서 고치면 된다.
    /// 프리팹의 그림·글자에 붙은 ThemeColor 가 자기 역할의 색을 여기서 가져간다.
    /// 강조색(파랑·초록·오렌지)과 색 버튼, 어두운 유리(미니 플레이어 등)는 두 모드 공통이라 여기 없다 (Theme.cs).
    /// </summary>
    [CreateAssetMenu(fileName = "Palette", menuName = "English App/Theme Palette")]
    public class ThemePalette : ScriptableObject
    {
        public PaletteColors colors = new PaletteColors();

        [Tooltip("글래스 뒤에 깔리는 배경 그림")]
        public Sprite background;

        [Header("글래스 톤")]
        [Tooltip("흰 카드 (유닛 카드, 예문 카드, 말풍선, 설정 카드, 칩, 이전/다음·정지·반복 버튼)")]
        public GlassTone glassCard;
        [Tooltip("배경 위 동그란 아이콘 버튼 (뒤로, 설정, 닫기, 해/달)")]
        public GlassTone glassClear;
        [Tooltip("설정 팝업 패널")]
        public GlassTone glassPanel;

        public GlassTone Glass(ColorRole role)
        {
            switch (role)
            {
                case ColorRole.GlassClear: return glassClear;
                case ColorRole.GlassPanel: return glassPanel;
                default: return glassCard;
            }
        }

        public Color ColorOf(ColorRole role)
        {
            switch (role)
            {
                case ColorRole.Bg: return colors.bg;
                case ColorRole.Surface: return colors.surface;
                case ColorRole.Line: return colors.line;
                case ColorRole.Text: return colors.text;
                case ColorRole.TextSub: return colors.textSub;
                case ColorRole.Track: return colors.track;
                case ColorRole.AdSlotBg: return colors.adSlotBg;
                default: return Color.white;
            }
        }

        // ---- 기본값 (프리팹 생성기가 처음 에셋을 만들 때) ----------------------------------

        public static PaletteColors LightColors() => new PaletteColors
        {
            bg = Theme.Hex("#FFF4E3"),
            surface = Theme.Hex("#FFFFFF"),
            line = Theme.Hex("#EFE4D3"),
            text = Theme.Hex("#2F2A3D"),
            textSub = Theme.Hex("#8A8399"),
            track = Theme.Hex("#EDE5DA"),
            highlight = Theme.Hex("#FFE7AE"),
            glassHighlight = Theme.Hex("#FFE7AE"),
            highlightText = Theme.Hex("#E8612C"),
            adSlotBg = Theme.Hex("#F5EBDC"),
        };

        public static PaletteColors DarkColors() => new PaletteColors
        {
            bg = Theme.Hex("#16131F"),
            surface = Theme.Hex("#514A6B"),
            line = Theme.Hex("#3E3852"),
            text = Theme.Hex("#F3EFFA"),
            textSub = Theme.Hex("#A9A1BC"),
            track = Theme.Hex("#3A3450"),
            highlight = Theme.Hex("#6B4F1C"),
            glassHighlight = Theme.Hex("#FFC86E"),
            highlightText = Theme.Hex("#FFB27A"),
            adSlotBg = Theme.Hex("#1D1A28"),
        };
    }
}
