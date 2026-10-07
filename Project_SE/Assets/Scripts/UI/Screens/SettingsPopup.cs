using System.Collections;
using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 설정 팝업 (오른쪽 위 톱니바퀴).
    /// - 개별 듣기: 음량, 빠르기
    /// - 전체 듣기: 듣기 시퀀스 (끌어서 순서 변경, 노드 추가/삭제, 노드마다 음량·빠르기·텀), 반복
    /// 슬라이더는 바꾸는 즉시 재생 중인 소리에 적용된다.
    /// </summary>
    public class SettingsPopup : MonoBehaviour
    {
        private static readonly string[] TabNames = { "개별 듣기", "전체 듣기" };

        private AppRoot App => AppRoot.I;

        private CanvasGroup group;
        private readonly Image[] tabBgs = new Image[2];
        private readonly TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[2];
        private readonly GameObject[] tabPages = new GameObject[2];
        private readonly GameObject[] nowCards = new GameObject[2];
        private readonly TextMeshProUGUI[] nowTexts = new TextMeshProUGUI[2];
        private readonly Image[] nowIcons = new Image[2];

        private LabeledSlider singleVolume;
        private LabeledSlider singleSpeed;
        private Button previewButton;
        private SequenceListView sequenceList;
        private Button addEn;
        private Button addKo;
        private Image repeatTrack;
        private RectTransform repeatKnob;
        private bool closing;

        public static SettingsPopup Open(RectTransform overlay, int tab)
        {
            var root = UIKit.Rect("SettingsPopup", overlay).Fill();
            var popup = root.gameObject.AddComponent<SettingsPopup>();
            popup.Build();
            popup.SelectTab(tab);
            popup.StartCoroutine(popup.Fade(0f, 1f, null));
            return popup;
        }

        private void Build()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // 바깥 (누르면 닫힘)
            var dim = UIKit.Image(transform, "Dim", Theme.Dim);
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);

            var safe = UIKit.Rect("SafeArea", transform).Fill();
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var panel = UIKit.Rounded(safe, "Panel", Theme.Bg, 48);
            panel.rectTransform.Fill(24, 60, 24, 30);
            panel.raycastTarget = true;
            UIKit.AddShadow(panel, 10);

            // 머리: 제목 + 닫기
            var header = UIKit.Rect("Header", panel.transform).TopStrip(130);
            var title = UIKit.Text(header, "Title", "설정", 58, Theme.Text, TextAlignmentOptions.MidlineLeft).SingleLine();
            title.rectTransform.Fill(48, 0, 150, 0);
            var close = UIKit.IconButton(header, "Close", IconKind.Close, Theme.Text, Color.clear, 112, 46, Close, out _);
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(112, 112));

            // 탭
            var tabs = UIKit.Rounded(panel.transform, "Tabs", Theme.Track, 50);
            tabs.rectTransform.TopStrip(104, 130, 36, 36);
            var tabsLayout = UIKit.HLayout(tabs, 8, UIKit.Pad(8), TextAnchor.MiddleCenter, expandHeight: true);
            tabsLayout.childForceExpandWidth = true;
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var b = UIKit.TextButton(tabs.transform, "Tab" + i, TabNames[i], 38, Color.clear, Theme.TextSub, 44,
                    () => SelectTab(index), out tabTexts[i]);
                tabBgs[i] = (Image)b.targetGraphic;
                UIKit.Layout(b, flexibleWidth: 1);
            }

            tabPages[0] = BuildSinglePage(panel.transform);
            tabPages[1] = BuildSequencePage(panel.transform);

            App.Player.Changed += RefreshDynamic;
            App.Settings.RepeatChanged += RefreshDynamic;
            App.Settings.SequenceStructureChanged += OnStructureChanged;
            RefreshDynamic();
        }

        private void OnDestroy()
        {
            if (AppRoot.I == null) return;
            AppRoot.I.Player.Changed -= RefreshDynamic;
            AppRoot.I.Settings.RepeatChanged -= RefreshDynamic;
            AppRoot.I.Settings.SequenceStructureChanged -= OnStructureChanged;
        }

        public void SelectTab(int tab)
        {
            tab = Mathf.Clamp(tab, 0, 1);
            for (int i = 0; i < 2; i++)
            {
                bool on = i == tab;
                tabBgs[i].color = on ? Theme.Surface : new Color(1, 1, 1, 0);
                tabTexts[i].color = on ? Theme.Text : Theme.TextSub;
                tabPages[i].SetActive(on);
            }
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            group.blocksRaycasts = false;
            App.OnSettingsClosed(this);
            StartCoroutine(Fade(1f, 0f, () => Destroy(gameObject)));
        }

        private IEnumerator Fade(float from, float to, System.Action done)
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
            {
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            group.alpha = to;
            done?.Invoke();
        }

        // ---- 개별 듣기 ---------------------------------------------------------------------

        private GameObject BuildSinglePage(Transform panel)
        {
            var scroll = PageScroll(panel, "SinglePage", out var content);
            BuildNowPlaying(content, 0);

            var card = Card(content);
            CardTitle(card, "개별 듣기");
            CardDesc(card, "예문 화면에서 문장이나 ▶ 버튼을 눌러 한 문장씩 들을 때 적용돼요.\n슬라이더를 움직이면 재생 중인 소리에 바로 적용돼요.");

            var s = App.Settings.Single;
            singleVolume = LabeledSlider.Create(card, "음량", AppSettings.VolumeMin, AppSettings.VolumeMax, AppSettings.VolumeStep,
                s.volume, LabeledSlider.FormatVolume, Theme.Primary, v => App.Settings.SetSingle(v, singleSpeed.Value));
            singleSpeed = LabeledSlider.Create(card, "빠르기", AppSettings.SpeedMin, AppSettings.SpeedMax, AppSettings.SpeedStep,
                s.speed, LabeledSlider.FormatSpeed, Theme.Primary, v => App.Settings.SetSingle(singleVolume.Value, v));

            previewButton = UIKit.TextButton(card, "Preview", "▶  마지막 문장 다시 듣기", 34, Theme.Primary, Theme.TextOnColor, 44,
                () => App.Player.ReplayLastSingle(), out _);
            UIKit.Layout(previewButton, preferredHeight: 96);
            return scroll.gameObject;
        }

        // ---- 전체 듣기 ---------------------------------------------------------------------

        private GameObject BuildSequencePage(Transform panel)
        {
            var scroll = PageScroll(panel, "SequencePage", out var content);
            BuildNowPlaying(content, 1);

            var card = Card(content);
            CardTitle(card, "듣기 시퀀스");
            CardDesc(card, "전체 듣기는 예문마다(대화는 대사마다) 아래 순서대로 재생해요.\n" +
                           "≡ 손잡이를 끌어서 순서를 바꾸고, 영어/한국어를 누르면 언어가 바뀌어요.\n" +
                           "줄을 누르면 음량 · 빠르기 · 텀(다음 소리까지 쉬는 시간)을 바꿀 수 있어요.");

            sequenceList = SequenceListView.Create(content, App.Settings, scroll);

            var addRow = UIKit.Rect("AddRow", content);
            UIKit.Layout(addRow, preferredHeight: 100);
            var addLayout = UIKit.HLayout(addRow, 20, null, TextAnchor.MiddleCenter, expandHeight: true);
            addLayout.childForceExpandWidth = true;
            addEn = UIKit.TextButton(addRow, "AddEn", "+ 영어 듣기", 36, Theme.En, Theme.TextOnColor, 50,
                () => App.Settings.AddNode(Lang.En), out _);
            UIKit.Layout(addEn, flexibleWidth: 1);
            addKo = UIKit.TextButton(addRow, "AddKo", "+ 한국어 듣기", 36, Theme.Ko, Theme.TextOnColor, 50,
                () => App.Settings.AddNode(Lang.Ko), out _);
            UIKit.Layout(addKo, flexibleWidth: 1);

            // 반복
            var repeatCard = UIKit.Rounded(content, "Repeat", Theme.Surface, 36);
            UIKit.AddShadow(repeatCard, 4);
            UIKit.Layout(repeatCard, preferredHeight: 130);
            UIKit.HLayout(repeatCard, 20, UIKit.Pad(40, 32, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);
            var repeatText = UIKit.Text(repeatCard.transform, "Text", "유닛 끝나면 처음부터 반복", 36, Theme.Text, TextAlignmentOptions.MidlineLeft)
                .SingleLine().AutoSize(26, 36);
            UIKit.Layout(repeatText, preferredHeight: 80, flexibleWidth: 1);
            var toggle = UIKit.Button(repeatCard.transform, "Toggle", Theme.Track, 36,
                () => App.Settings.SetRepeat(!App.Settings.Sequence.repeat));
            UIKit.Layout(toggle, preferredHeight: 72, preferredWidth: 128);
            repeatTrack = (Image)toggle.targetGraphic;
            var knob = UIKit.Image(toggle.transform, "Knob", Color.white, Sprites.Circle);
            repeatKnob = knob.rectTransform;
            repeatKnob.sizeDelta = new Vector2(56, 56);
            UIKit.AddShadow(knob, 3);

            var reset = UIKit.TextButton(content, "Reset", "기본 순서로 되돌리기", 32, Theme.Surface, Theme.TextSub, 44,
                () => App.Settings.ResetSequence(), out _);
            UIKit.Layout(reset, preferredHeight: 96);
            UIKit.AddShadow(reset.targetGraphic, 4);
            return scroll.gameObject;
        }

        private void OnStructureChanged()
        {
            sequenceList.Rebuild();
            RefreshDynamic();
        }

        // ---- 공통 ------------------------------------------------------------------------

        private static ScrollRect PageScroll(Transform panel, string name, out RectTransform content)
        {
            var scroll = UIKit.VScroll(panel, name, out content, 24, UIKit.Pad(36, 36, 8, 60));
            ((RectTransform)scroll.transform).Fill(0, 250, 0, 24);
            return scroll;
        }

        private static RectTransform Card(Transform parent)
        {
            var card = UIKit.Rounded(parent, "Card", Theme.Surface, 40);
            UIKit.AddShadow(card, 4);
            UIKit.VLayout(card, 16, UIKit.Pad(40, 40, 36, 36));
            return card.rectTransform;
        }

        private static void CardTitle(Transform card, string text)
        {
            var t = UIKit.Text(card, "Title", text, 46, Theme.Text, TextAlignmentOptions.MidlineLeft).SingleLine();
            UIKit.Layout(t, preferredHeight: 64);
        }

        private static void CardDesc(Transform card, string text)
        {
            UIKit.Text(card, "Desc", text, 29, Theme.TextSub, TextAlignmentOptions.TopLeft);
        }

        /// <summary>지금 재생 중인 것 + 재생/일시정지 (설정을 바꾸면서 바로 들어볼 수 있게).</summary>
        private void BuildNowPlaying(Transform parent, int index)
        {
            var card = UIKit.Rounded(parent, "NowPlaying", Theme.Text, 36);
            UIKit.Layout(card, preferredHeight: 120);
            UIKit.HLayout(card, 20, UIKit.Pad(36, 20, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);
            var text = UIKit.Text(card.transform, "Text", "", 30, Theme.TextOnColor, TextAlignmentOptions.MidlineLeft).SingleLine().AutoSize(22, 30);
            UIKit.Layout(text, preferredHeight: 90, flexibleWidth: 1);
            var button = UIKit.IconButton(card.transform, "PlayPause", IconKind.Pause, Theme.TextOnColor, Theme.Primary, 88, 40, TogglePlayback, out var icon);
            UIKit.Layout(button, preferredHeight: 88, preferredWidth: 88);
            nowCards[index] = card.gameObject;
            nowTexts[index] = text;
            nowIcons[index] = icon;
        }

        private void TogglePlayback()
        {
            var player = App.Player;
            if (player.IsPlaying) player.Pause();
            else player.Resume();
        }

        private void RefreshDynamic()
        {
            var player = App.Player;
            var settings = App.Settings;

            // 재생 중 표시
            string info = null;
            if (player.Mode != PlaybackMode.None && player.Unit != null && (player.IsActive))
            {
                var step = player.CurrentStep;
                if (player.Mode == PlaybackMode.Full)
                {
                    info = "전체 듣기 · " + player.Unit.unit;
                    if (step != null)
                        info += " · " + (step.page + 1) + "/" + player.Unit.items.Count + " · " + (step.node + 1) + "번 " + Theme.LangName(step.lang);
                }
                else
                {
                    info = "개별 듣기 · " + player.Unit.unit;
                    if (step != null) info += " · " + Theme.LangName(step.lang);
                }
                if (player.IsPreparing) info = "음성 준비 중…";
            }
            for (int i = 0; i < 2; i++)
            {
                if (nowCards[i] == null) continue;
                nowCards[i].SetActive(info != null);
                if (info == null) continue;
                nowTexts[i].text = info;
                nowIcons[i].sprite = Sprites.Icon(player.IsPlaying ? IconKind.Pause : IconKind.Play);
            }

            // 시퀀스에서 지금 재생 중인 노드
            if (sequenceList != null)
            {
                var step = player.Mode == PlaybackMode.Full && player.State.active ? player.CurrentStep : null;
                sequenceList.SetPlayingNode(step != null ? step.node : -1);
            }

            if (addEn != null) addEn.interactable = settings.CanAddNode;
            if (addKo != null) addKo.interactable = settings.CanAddNode;
            if (previewButton != null) previewButton.interactable = player.HasLastSingle;

            bool repeat = settings.Sequence.repeat;
            if (repeatTrack != null)
            {
                repeatTrack.color = repeat ? Theme.Primary : Theme.Track;
                repeatKnob.anchorMin = repeatKnob.anchorMax = new Vector2(repeat ? 1f : 0f, 0.5f);
                repeatKnob.pivot = new Vector2(repeat ? 1f : 0f, 0.5f);
                repeatKnob.anchoredPosition = new Vector2(repeat ? -8f : 8f, 0f);
            }
        }
    }
}
