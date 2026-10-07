using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 듣기 시퀀스의 노드 한 줄.
    /// [≡ 손잡이] [번호] [영어/한국어 (눌러서 바꿈)] [요약: 음량 · 빠르기 · 텀] [펼치기] [삭제]
    /// 펼치면 음량 / 빠르기 / 텀 슬라이더 — 바꾸는 즉시 재생 중인 소리에 적용된다.
    /// </summary>
    public class SequenceNodeRow : MonoBehaviour
    {
        private const float HeaderHeight = 124f;

        private SequenceListView list;
        internal SequenceListView List => list;
        private AppSettings settings;
        private int uid;

        private Image background;
        private Image langBg;
        private TextMeshProUGUI langText;
        private TextMeshProUGUI summaryText;
        private Image expandIcon;
        private Button deleteButton;
        private RectTransform details;
        private LabeledSlider volumeSlider;
        private LabeledSlider speedSlider;
        private LabeledSlider gapSlider;
        private bool expanded;
        private bool playing;
        private bool draggingNow;

        public int Index { get; private set; }

        private SequenceNode Node => settings.Nodes[Index];

        public static SequenceNodeRow Create(Transform parent, SequenceListView list, AppSettings settings, int index, bool expanded)
        {
            var bg = UIKit.Rounded(parent, "Node" + (index + 1), Theme.Surface, 28);
            UIKit.AddShadow(bg, 4);
            UIKit.VLayout(bg, 0, UIKit.Pad(0, 0, 0, 0));

            var row = bg.gameObject.AddComponent<SequenceNodeRow>();
            row.list = list;
            row.settings = settings;
            row.Index = index;
            row.uid = settings.Nodes[index].uid;
            row.background = bg;
            row.Build();
            row.SetExpanded(expanded);
            return row;
        }

        private void Build()
        {
            var node = Node;

            // ---- 머리줄 ----
            var header = UIKit.Rect("Header", transform);
            UIKit.Layout(header, preferredHeight: HeaderHeight, minHeight: HeaderHeight);
            UIKit.HLayout(header, 12, UIKit.Pad(8, 16, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);

            // 손잡이 (여기를 끌어서 순서 변경)
            var handle = UIKit.Image(header, "DragHandle", Color.clear);
            handle.raycastTarget = true;
            UIKit.Layout(handle, preferredHeight: HeaderHeight, preferredWidth: 84);
            handle.gameObject.AddComponent<SequenceDragHandle>().row = this;
            UIKit.Icon(handle.transform, IconKind.Drag, Theme.TextSub, 48);

            // 번호
            var num = UIKit.Image(header, "Index", Theme.Line, Sprites.Circle);
            UIKit.Layout(num, preferredHeight: 60, preferredWidth: 60);
            var numText = UIKit.Text(num.transform, "Text", (Index + 1).ToString(), 30, Theme.Text);
            numText.rectTransform.Fill();

            // 언어 (누르면 영어 ↔ 한국어)
            var langButton = UIKit.TextButton(header, "Lang", Theme.LangName(node.lang), 34, Theme.LangColor(node.lang), Theme.TextOnColor, 40,
                ToggleLang, out langText);
            langBg = langButton.GetComponent<Image>();
            UIKit.Layout(langButton, preferredHeight: 80, preferredWidth: 180);

            // 요약 (눌러서 펼치기)
            var summary = UIKit.Image(header, "Summary", Color.clear);
            summary.raycastTarget = true;
            var summaryButton = summary.gameObject.AddComponent<Button>();
            summaryButton.transition = Selectable.Transition.None;
            summaryButton.navigation = new Navigation { mode = Navigation.Mode.None };
            summaryButton.onClick.AddListener(() => SetExpanded(!expanded));
            UIKit.Layout(summary, preferredHeight: 90, flexibleWidth: 1);
            summaryText = UIKit.Text(summary.transform, "Text", "", 28, Theme.TextSub, TextAlignmentOptions.MidlineLeft).SingleLine().AutoSize(20, 28);
            summaryText.rectTransform.Fill(8, 0, 0, 0);

            var expandButton = UIKit.IconButton(header, "Expand", IconKind.ChevronDown, Theme.TextSub, Color.clear, 76, 36,
                () => SetExpanded(!expanded), out expandIcon);
            UIKit.Layout(expandButton, preferredHeight: 76, preferredWidth: 76);

            deleteButton = UIKit.IconButton(header, "Delete", IconKind.Close, Theme.Danger, Color.clear, 76, 32,
                () => settings.RemoveNode(Index), out _);
            UIKit.Layout(deleteButton, preferredHeight: 76, preferredWidth: 76);
            deleteButton.interactable = settings.CanRemoveNode;

            // ---- 펼친 영역: 슬라이더 3개 ----
            details = UIKit.Rect("Details", transform);
            UIKit.VLayout(details, 0, UIKit.Pad(36, 36, 0, 24));
            var accent = Theme.LangColor(node.lang);
            volumeSlider = LabeledSlider.Create(details, "음량", AppSettings.VolumeMin, AppSettings.VolumeMax, AppSettings.VolumeStep,
                node.volume, LabeledSlider.FormatVolume, accent, v => Apply());
            speedSlider = LabeledSlider.Create(details, "빠르기", AppSettings.SpeedMin, AppSettings.SpeedMax, AppSettings.SpeedStep,
                node.speed, LabeledSlider.FormatSpeed, accent, v => Apply());
            gapSlider = LabeledSlider.Create(details, "텀", AppSettings.GapMin, AppSettings.GapMax, AppSettings.GapStep,
                node.gap, LabeledSlider.FormatGap, accent, v => Apply());

            RefreshValues();
        }

        private void Apply()
        {
            settings.SetNodeParams(Index, volumeSlider.Value, speedSlider.Value, gapSlider.Value);
            UpdateSummary();
        }

        private void ToggleLang()
        {
            settings.SetNodeLang(Index, Node.lang == Lang.En ? Lang.Ko : Lang.En); // → 목록 다시 만듦
        }

        public void RefreshValues()
        {
            var node = Node;
            volumeSlider.SetValue(node.volume);
            speedSlider.SetValue(node.speed);
            gapSlider.SetValue(node.gap);
            langText.text = Theme.LangName(node.lang);
            langBg.color = Theme.LangColor(node.lang);
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var node = Node;
            summaryText.text = LabeledSlider.FormatVolume(node.volume) + "  ·  " + LabeledSlider.FormatSpeed(node.speed) +
                               "  ·  텀 " + LabeledSlider.FormatGap(node.gap);
        }

        public void SetExpanded(bool value)
        {
            expanded = value;
            details.gameObject.SetActive(value);
            expandIcon.sprite = Sprites.Icon(value ? IconKind.ChevronUp : IconKind.ChevronDown);
            list.SetExpanded(uid, value);
        }

        public void SetPlaying(bool value)
        {
            if (playing == value) return;
            playing = value;
            UpdateBackground();
        }

        public void SetDragging(bool value)
        {
            draggingNow = value;
            transform.localScale = value ? new Vector3(1.02f, 1.02f, 1f) : Vector3.one;
            UpdateBackground();
        }

        private void UpdateBackground()
        {
            background.color = draggingNow ? Theme.SurfaceAlt : playing ? Theme.Highlight : Theme.Surface;
        }
    }
}
