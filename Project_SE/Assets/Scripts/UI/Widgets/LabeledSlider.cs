using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// "라벨 [━━━●━━] 값" 한 줄 슬라이더 (LabeledSlider 프리팹). 라벨 글자는 프리팹에 있다.
    /// 값은 step 단위로 맞추고, 바뀔 때마다 바로 알린다.
    /// </summary>
    public class LabeledSlider : MonoBehaviour
    {
        [SerializeField] internal Slider slider;
        [SerializeField] internal TextMeshProUGUI valueText;
        [Tooltip("강조색이 들어가는 그림 (채움, 손잡이)")]
        [SerializeField] internal Graphic[] accentGraphics;

        private Func<float, string> format;
        private Action<float> onChanged;
        private float step;
        private float lastValue;

        public Slider Slider => slider;
        public float Value => lastValue;

        public void Setup(float min, float max, float step, float value, Func<float, string> format, Color accent, Action<float> onChanged)
        {
            this.format = format;
            this.onChanged = onChanged;
            this.step = step;
            foreach (var g in accentGraphics) g.color = accent;

            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(OnSliderChanged);
            valueText.text = format(value);
            lastValue = value;
        }

        /// <summary>외부에서 값 반영 (알림 없음).</summary>
        public void SetValue(float value)
        {
            lastValue = value;
            slider.SetValueWithoutNotify(value);
            valueText.text = format(value);
        }

        private void OnSliderChanged(float raw)
        {
            float v = AppSettings.Snap(raw, step);
            v = Mathf.Clamp(v, slider.minValue, slider.maxValue);
            if (!Mathf.Approximately(v, raw)) slider.SetValueWithoutNotify(v);
            valueText.text = format(v);
            if (Mathf.Approximately(v, lastValue)) return;
            lastValue = v;
            onChanged?.Invoke(v);
        }

        // ---- 표시 형식 ----------------------------------------------------------------------

        public static string FormatVolume(float v) => Mathf.RoundToInt(v * 100f) + "%";
        public static string FormatSpeed(float v) => "x" + v.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
        public static string FormatGap(float v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "초";
    }
}
