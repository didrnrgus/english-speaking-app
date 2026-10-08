using LeTai.Asset.TranslucentImage;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>ThemeColor 가 칠하는 색 역할 (ThemePalette 의 항목).</summary>
    public enum ColorRole
    {
        Bg,
        Surface,
        Line,
        Text,
        TextSub,
        Track,
        AdSlotBg,
        /// <summary>배경 그림 (Image.sprite 를 팔레트의 배경으로)</summary>
        Background,
        /// <summary>흰 카드 유리 (TranslucentImage 의 색·불투명도·밝기)</summary>
        GlassCard,
        /// <summary>동그란 아이콘 버튼 유리</summary>
        GlassClear,
        /// <summary>설정 팝업 패널 유리</summary>
        GlassPanel,
    }

    /// <summary>
    /// 그림·글자에 붙여서 밝은 / 어두운 모드에 맞는 색으로 칠한다 (역할 → 지금 팔레트의 색).
    /// 프리팹에 보이는 색은 밝은 모드 색이다. 실행 중 모드가 바뀌면 다시 칠한다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class ThemeColor : MonoBehaviour
    {
        [SerializeField] internal ColorRole role;

        private Graphic graphic;

        public ColorRole Role => role;

        /// <summary>프리팹을 만든 직후: 꺼져 있는 것까지 지금 모드로 칠한다 (UIKit.Spawn).</summary>
        public static void ApplyAll(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<ThemeColor>(true)) t.Apply();
        }

        private void OnEnable()
        {
            Theme.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Theme.Changed -= Apply;
        }

        public void Apply()
        {
            var palette = Theme.Palette;
            if (palette == null) return;
            if (graphic == null) graphic = GetComponent<Graphic>();

            switch (role)
            {
                case ColorRole.Background:
                    if (graphic is Image img && palette.background != null) img.sprite = palette.background;
                    break;
                case ColorRole.GlassCard:
                case ColorRole.GlassClear:
                case ColorRole.GlassPanel:
                    var tone = palette.Glass(role);
                    graphic.color = tone.color;
                    if (graphic is TranslucentImage glass)
                    {
                        glass.foregroundOpacity = tone.opacity;
                        glass.brightness = tone.brightness;
                    }
                    break;
                default:
                    graphic.color = palette.ColorOf(role);
                    break;
            }
        }
    }
}
