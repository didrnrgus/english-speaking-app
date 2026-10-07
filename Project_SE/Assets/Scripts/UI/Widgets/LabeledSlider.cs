using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>"라벨 [━━━●━━] 값" 한 줄 슬라이더. 값은 step 단위로 맞추고, 바뀔 때마다 바로 알린다.</summary>
    public class LabeledSlider : MonoBehaviour
    {
        private const float HandleSize = 60f;

        public Slider Slider { get; private set; }
        private TextMeshProUGUI valueText;
        private Func<float, string> format;
        private Action<float> onChanged;
        private float step;
        private float lastValue;

        public float Value => lastValue;

        public static LabeledSlider Create(Transform parent, string label, float min, float max, float step, float value,
            Func<float, string> format, Color accent, Action<float> onChanged)
        {
            var row = UIKit.Rect("Slider_" + label, parent);
            UIKit.Layout(row, preferredHeight: 96, minHeight: 96);
            UIKit.HLayout(row, 20, UIKit.Pad(0, 0, 0, 0), TextAnchor.MiddleCenter, expandHeight: false);

            var ls = row.gameObject.AddComponent<LabeledSlider>();
            ls.format = format;
            ls.onChanged = onChanged;
            ls.step = step;

            var labelText = UIKit.Text(row, "Label", label, 34, Theme.Text, TextAlignmentOptions.MidlineLeft).SingleLine();
            UIKit.Layout(labelText, preferredHeight: 60, preferredWidth: 130);

            // 슬라이더 본체
            var sRoot = UIKit.Rect("Slider", row);
            UIKit.Layout(sRoot, preferredHeight: 80, flexibleWidth: 1);
            var slider = sRoot.gameObject.AddComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;

            var track = UIKit.Rounded(sRoot, "Track", Theme.Track, 10);
            track.raycastTarget = true;
            SetHorizontalBand(track.rectTransform, 20, 0);

            var fillArea = UIKit.Rect("FillArea", sRoot);
            SetHorizontalBand(fillArea, 20, HandleSize * 0.5f);
            var fill = UIKit.Rounded(fillArea, "Fill", accent, 10);
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = UIKit.Rect("HandleArea", sRoot);
            handleArea.Fill(HandleSize * 0.5f, 0, HandleSize * 0.5f, 0);
            var handle = UIKit.Image(handleArea, "Handle", accent, Sprites.Circle);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(HandleSize, HandleSize);
            UIKit.AddShadow(handle, 4);
            var dot = UIKit.Image(handle.transform, "Dot", Color.white, Sprites.Circle);
            dot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(ls.OnSliderChanged);
            ls.Slider = slider;

            ls.valueText = UIKit.Text(row, "Value", format(value), 34, Theme.Text, TextAlignmentOptions.MidlineRight).SingleLine();
            UIKit.Layout(ls.valueText, preferredHeight: 60, preferredWidth: 140);

            ls.lastValue = value;
            return ls;
        }

        private static void SetHorizontalBand(RectTransform rt, float height, float inset)
        {
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, -height * 0.5f);
            rt.offsetMax = new Vector2(-inset, height * 0.5f);
        }

        /// <summary>외부에서 값 반영 (알림 없음).</summary>
        public void SetValue(float value)
        {
            lastValue = value;
            Slider.SetValueWithoutNotify(value);
            valueText.text = format(value);
        }

        private void OnSliderChanged(float raw)
        {
            float v = AppSettings.Snap(raw, step);
            v = Mathf.Clamp(v, Slider.minValue, Slider.maxValue);
            if (!Mathf.Approximately(v, raw)) Slider.SetValueWithoutNotify(v);
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
