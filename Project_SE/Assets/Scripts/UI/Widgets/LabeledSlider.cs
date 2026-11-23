using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// "라벨 [━━━●━━] 값" 한 줄 슬라이더 (LabeledSlider 프리팹). 라벨 글자는 프리팹에 있다.
    /// 값은 step 단위로 맞추고, 바뀔 때마다 바로 알린다.
    /// '섞임' 상태(여러 값을 한꺼번에 보여줄 때 값이 서로 다름)면 값 글자를 "-" 로 보여주고,
    /// 슬라이더를 움직이면 그 값으로 알린다.
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
        private bool mixed;

        public const string MixedText = "-";

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

        /// <summary>외부에서 값 반영 (알림 없음). mixed 면 값 글자를 "-" 로.</summary>
        public void SetValue(float value, bool mixed = false)
        {
            lastValue = value;
            this.mixed = mixed;
            slider.SetValueWithoutNotify(value);
            valueText.text = mixed ? MixedText : format(value);
        }

        /// <summary>바꿀 대상이 없을 때 (예: 그 언어 노드가 없음): 값 "-", 슬라이더 잠금.</summary>
        public void SetInteractable(bool value)
        {
            slider.interactable = value;
            if (!value) valueText.text = MixedText;
        }

        private void OnSliderChanged(float raw)
        {
            float v = AppSettings.Snap(raw, step);
            v = Mathf.Clamp(v, slider.minValue, slider.maxValue);
            if (!Mathf.Approximately(v, raw)) slider.SetValueWithoutNotify(v);
            if (mixed && Mathf.Approximately(v, lastValue)) return; // 섞임 상태에서 손잡이가 제자리면 그대로 "-"
            valueText.text = format(v);
            if (Mathf.Approximately(v, lastValue)) return;
            mixed = false;
            lastValue = v;
            onChanged?.Invoke(v);
        }

        // ---- 표시 형식 ----------------------------------------------------------------------

        public static string FormatVolume(float v) => Mathf.RoundToInt(v * 100f) + "%";
        public static string FormatSpeed(float v) => "x" + v.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
        public static string FormatGap(float v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "초";
    }
}
