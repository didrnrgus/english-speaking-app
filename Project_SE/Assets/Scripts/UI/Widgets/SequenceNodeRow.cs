using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 듣기 시퀀스의 노드 한 줄 (SequenceNodeRow 프리팹).
    /// [≡ 손잡이] [번호] [영어/한국어 (눌러서 바꿈)] [요약: 음량 · 빠르기 · 텀] [펼치기] [삭제]
    /// 펼치면 음량 / 빠르기 / 텀 슬라이더 — 바꾸는 즉시 재생 중인 소리에 적용된다.
    /// </summary>
    public class SequenceNodeRow : MonoBehaviour
    {
        [SerializeField] internal Image background;
        [SerializeField] internal SequenceDragHandle dragHandle;
        [SerializeField] internal TextMeshProUGUI indexText;
        [SerializeField] internal Button langButton;
        [SerializeField] internal TextMeshProUGUI langText;
        [SerializeField] internal Button summaryButton;
        [SerializeField] internal TextMeshProUGUI summaryText;
        [SerializeField] internal Button expandButton;
        [SerializeField] internal Image expandIcon;
        [SerializeField] internal Button deleteButton;
        [SerializeField] internal GameObject details;
        [SerializeField] internal LabeledSlider volumeSlider;
        [SerializeField] internal LabeledSlider speedSlider;
        [SerializeField] internal LabeledSlider gapSlider;
        [SerializeField] internal Sprite chevronDown;
        [SerializeField] internal Sprite chevronUp;

        private SequenceListView list;
        internal SequenceListView List => list;
        private AppSettings settings;
        private int uid;
        private Color baseColor;
        private bool expanded;
        private bool playing;
        private bool draggingNow;

        public int Index { get; private set; }

        private SequenceNode Node => settings.Nodes[Index];

        public static SequenceNodeRow Create(SequenceNodeRow prefab, Transform parent, SequenceListView list, AppSettings settings, int index, bool expanded)
        {
            var row = UIKit.Spawn(prefab, parent);
            row.name = "Node" + (index + 1);
            row.list = list;
            row.settings = settings;
            row.Index = index;
            row.uid = settings.Nodes[index].uid;
            row.baseColor = row.background.color;
            row.Init();
            row.SetExpanded(expanded);
            return row;
        }

        private void Init()
        {
            var node = Node;
            dragHandle.row = this;
            indexText.text = (Index + 1).ToString();
            langButton.onClick.AddListener(ToggleLang);
            summaryButton.onClick.AddListener(() => SetExpanded(!expanded));
            expandButton.onClick.AddListener(() => SetExpanded(!expanded));
            deleteButton.onClick.AddListener(() => settings.RemoveNode(Index));
            deleteButton.interactable = settings.CanRemoveNode;

            var accent = Theme.LangColor(node.lang);
            volumeSlider.Setup(AppSettings.VolumeMin, AppSettings.VolumeMax, AppSettings.VolumeStep,
                node.volume, LabeledSlider.FormatVolume, accent, v => Apply());
            speedSlider.Setup(AppSettings.SpeedMin, AppSettings.SpeedMax, AppSettings.SpeedStep,
                node.speed, LabeledSlider.FormatSpeed, accent, v => Apply());
            gapSlider.Setup(AppSettings.GapMin, AppSettings.GapMax, AppSettings.GapStep,
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
            langButton.targetGraphic.color = Theme.LangColor(node.lang);
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
            details.SetActive(value);
            expandIcon.sprite = value ? chevronUp : chevronDown;
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
            // 끄는 중엔 크기만 키운다 (글래스 색은 배경에 곱해져서 다른 색으로 구분하기 어려움)
            background.color = playing && !draggingNow ? Theme.GlassHighlight : baseColor;
        }
    }
}
