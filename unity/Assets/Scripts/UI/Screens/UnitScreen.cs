using System;
using System.Collections;
using System.Collections.Generic;
using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 유닛 화면: 한 페이지에 예문 하나 (좌우로 넘김).
    /// - 문장/대사나 ▶ 버튼을 누르면 개별 듣기 (설정의 개별 듣기 음량·빠르기)
    /// - 아래 '전체 듣기' 는 유닛 전체를 듣기 시퀀스대로 이어서 재생 (화면이 꺼져도 계속)
    /// </summary>
    public class UnitScreen : BaseScreen
    {
        private const float ControlsHeight = 440f;

        public UnitInfo Info;
        public string UnitId => Info != null ? Info.unit : null;

        private class LineView
        {
            public Image bg;
            public Color baseColor;
            public TextMeshProUGUI en;
            public TextMeshProUGUI ko;
            public RectTransform rect;
        }

        private class PageView
        {
            public readonly List<LineView> lines = new List<LineView>();
            public ScrollRect scroll;
            public float scrollTarget = -1f;
        }

        private UnitData unit;
        private TextMeshProUGUI centerText;
        private Button retryButton;

        private PagerView pager;
        private readonly List<PageView> pages = new List<PageView>();
        private TextMeshProUGUI pageText;
        private Button prevButton;
        private Button nextButton;
        private Image playIcon;
        private TextMeshProUGUI playLabel;
        private Button stopButton;
        private Image repeatBg;
        private Image repeatIcon;
        private RectTransform chipsRow;
        private readonly List<Image> chips = new List<Image>();
        private readonly List<TextMeshProUGUI> chipTexts = new List<TextMeshProUGUI>();
        private int lastFollowedStep = -1;
        private bool subscribed;

        public override void Build()
        {
            BuildTopBar(Info.unit + " · " + Info.title, true);

            centerText = UIKit.Text(Root, "Center", "", 40, Theme.TextSub);
            centerText.rectTransform.Fill(60, 500, 60, 600);
            retryButton = UIKit.TextButton(Root, "Retry", "다시 시도", 40, Theme.Primary, Theme.TextOnColor, 50,
                () => StartCoroutine(Load()), out _);
            ((RectTransform)retryButton.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(360, 110));

            StartCoroutine(Load());
        }

        private IEnumerator Load()
        {
            UIKit.SetActive(centerText, true);
            UIKit.SetActive(retryButton, false);
            centerText.text = "불러오는 중…";

            UnitData data = null;
            yield return App.Content.LoadUnit(Info, u => data = u);
            if (data == null)
            {
                centerText.text = "예문을 불러오지 못했어요.\n인터넷 연결을 확인해 주세요.";
                UIKit.SetActive(retryButton, true);
                yield break;
            }

            unit = data;
            UIKit.SetActive(centerText, false);
            BuildContent();
        }

        private void OnDestroy()
        {
            if (!subscribed || AppRoot.I == null) return;
            AppRoot.I.Player.Changed -= Refresh;
            AppRoot.I.Settings.SequenceStructureChanged -= RebuildChips;
            AppRoot.I.Settings.RepeatChanged -= Refresh;
        }

        public override void OnShow()
        {
            if (unit != null) Refresh();
        }

        // ---- 화면 만들기 ------------------------------------------------------------------

        private void BuildContent()
        {
            pager = PagerView.Create(Root, "Pager");
            ((RectTransform)pager.transform).Fill(0, Theme.TopBarHeight, 0, ControlsHeight);
            for (int p = 0; p < unit.items.Count; p++)
            {
                var pageRoot = pager.AddPage();
                pages.Add(unit.IsDialogue ? BuildDialoguePage(pageRoot, p) : BuildSpeakingPage(pageRoot, p));
            }
            pager.PageChanged += OnPageChanged;

            BuildControls();

            // 이 유닛을 전체 듣기 중이면 그 예문부터 보여준다
            int start = 0;
            if (App.Player.IsFull(unit.unit) && App.Player.CurrentStep != null) start = App.Player.CurrentStep.page;
            pager.GoTo(start, false, true);

            App.Player.Changed += Refresh;
            App.Settings.SequenceStructureChanged += RebuildChips;
            App.Settings.RepeatChanged += Refresh;
            subscribed = true;

            RebuildChips();
            UpdatePageText();
            Refresh();
        }

        private PageView BuildSpeakingPage(RectTransform page, int p)
        {
            var view = new PageView();
            var line = unit.items[p].Lines[0];

            var card = UIKit.Rounded(page, "Card", Theme.Surface, 48);
            card.rectTransform.Fill(Theme.Gutter, 24, Theme.Gutter, 24);
            UIKit.AddShadow(card, 6);

            var scroll = UIKit.VScroll(card.transform, "Scroll", out var content, 24, UIKit.Pad(36, 36, 40, 40), directional: true);
            ((RectTransform)scroll.transform).Fill();
            content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            content.gameObject.AddComponent<FillViewportHeight>().viewport = (RectTransform)scroll.transform;
            view.scroll = scroll;

            var lv = new LineView { baseColor = new Color(1, 1, 1, 0) };
            lv.bg = UIKit.Rounded(content, "Line", lv.baseColor, 36);
            lv.rect = lv.bg.rectTransform;
            UIKit.VLayout(lv.bg, 18, UIKit.Pad(24, 24, 28, 28));
            lv.en = TappableText(lv.bg.transform, line.en, EnSize(line.en), Theme.Text, TextAlignmentOptions.Center,
                () => PlaySingle(p, 0, Lang.En));
            var divider = UIKit.Image(lv.bg.transform, "Divider", Theme.Line);
            UIKit.Layout(divider, preferredHeight: 4, minHeight: 4);
            lv.ko = TappableText(lv.bg.transform, line.ko, KoSize(line.ko), Theme.TextSub, TextAlignmentOptions.Center,
                () => PlaySingle(p, 0, Lang.Ko));
            view.lines.Add(lv);

            BuildLineButtons(content, p, 0, 96, 260, TextAnchor.MiddleCenter);
            return view;
        }

        private PageView BuildDialoguePage(RectTransform page, int p)
        {
            var view = new PageView();
            var item = unit.items[p];

            var topic = UIKit.Text(page, "Topic", string.IsNullOrEmpty(item.topic) ? "대화" : item.topic, 46, Theme.Dialogue).SingleLine();
            topic.rectTransform.TopStrip(84, 12, Theme.Gutter, Theme.Gutter);

            var scroll = UIKit.VScroll(page, "Turns", out var content, 22, UIKit.Pad(32, 32, 8, 40), directional: true);
            ((RectTransform)scroll.transform).Fill(0, 104, 0, 0);
            view.scroll = scroll;

            var lines = item.Lines;
            for (int l = 0; l < lines.Count; l++)
            {
                var line = lines[l];
                int lineIndex = l;
                bool isA = line.speaker != "B";

                var row = UIKit.Rect("Turn" + (l + 1), content);
                UIKit.HLayout(row, 16, null, isA ? TextAnchor.UpperLeft : TextAnchor.UpperRight, expandHeight: false);

                if (isA) SpeakerBadge(row, line.speaker, Theme.SpeakerA);

                var lv = new LineView { baseColor = Theme.Surface };
                lv.bg = UIKit.Rounded(row, "Bubble", lv.baseColor, 36);
                lv.rect = row;
                UIKit.AddShadow(lv.bg, 4);
                UIKit.Layout(lv.bg, flexibleWidth: 1);
                UIKit.VLayout(lv.bg, 10, UIKit.Pad(28, 28, 22, 22));
                lv.en = TappableText(lv.bg.transform, line.en, 40, Theme.Text, TextAlignmentOptions.TopLeft,
                    () => PlaySingle(p, lineIndex, Lang.En));
                lv.ko = TappableText(lv.bg.transform, line.ko, 32, Theme.TextSub, TextAlignmentOptions.TopLeft,
                    () => PlaySingle(p, lineIndex, Lang.Ko));
                BuildLineButtons(lv.bg.transform, p, lineIndex, 68, 170, TextAnchor.MiddleLeft);
                view.lines.Add(lv);

                if (!isA) SpeakerBadge(row, line.speaker, Theme.SpeakerB);
            }
            return view;
        }

        private static Image SpeakerBadge(Transform parent, string speaker, Color color)
        {
            var badge = UIKit.Image(parent, "Speaker", color, Sprites.Circle);
            UIKit.Layout(badge, preferredHeight: 84, preferredWidth: 84, minHeight: 84, minWidth: 84);
            var t = UIKit.Text(badge.transform, "Text", speaker, 40, Theme.TextOnColor);
            t.rectTransform.Fill();
            return badge;
        }

        /// <summary>문장 글자 — 누르면 그 언어로 개별 듣기.</summary>
        private static TextMeshProUGUI TappableText(Transform parent, string text, float size, Color color, TextAlignmentOptions align, Action onClick)
        {
            var b = UIKit.Button(parent, "Tap", new Color(1, 1, 1, 0), 0, onClick);
            b.GetComponent<PressScale>().pressedScale = 0.98f;
            UIKit.VLayout(b, 0);
            var t = UIKit.Text(b.transform, "Text", text, size, color, align);
            return t;
        }

        private void BuildLineButtons(Transform parent, int page, int line, float height, float width, TextAnchor align)
        {
            var row = UIKit.Rect("Buttons", parent);
            UIKit.Layout(row, preferredHeight: height, minHeight: height);
            UIKit.HLayout(row, 20, null, align, expandHeight: true);
            float font = height * 0.4f;
            var en = UIKit.TextButton(row, "PlayEn", "▶ 영어", font, Theme.En, Theme.TextOnColor, height * 0.5f,
                () => PlaySingle(page, line, Lang.En), out _);
            UIKit.Layout(en, preferredWidth: width);
            var ko = UIKit.TextButton(row, "PlayKo", "▶ 한국어", font, Theme.Ko, Theme.TextOnColor, height * 0.5f,
                () => PlaySingle(page, line, Lang.Ko), out _);
            UIKit.Layout(ko, preferredWidth: width);
        }

        private static float EnSize(string s)
        {
            int n = s != null ? s.Length : 0;
            if (n <= 24) return 72;
            if (n <= 60) return 58;
            if (n <= 140) return 48;
            return 42;
        }

        private static float KoSize(string s)
        {
            int n = s != null ? s.Length : 0;
            if (n <= 20) return 46;
            if (n <= 60) return 40;
            return 34;
        }

        private void BuildControls()
        {
            var controls = UIKit.Rect("Controls", Root).BottomStrip(ControlsHeight);
            UIKit.VLayout(controls, 18, UIKit.Pad(40, 40, 6, 36));

            // 페이지 이동
            var nav = UIKit.Rect("PageNav", controls);
            UIKit.Layout(nav, preferredHeight: 100);
            UIKit.HLayout(nav, 40, null, TextAnchor.MiddleCenter, expandHeight: false);
            prevButton = UIKit.IconButton(nav, "Prev", IconKind.ChevronLeft, Theme.Text, Theme.Surface, 100, 40,
                () => pager.GoTo(pager.Current - 1, true), out _);
            UIKit.Layout(prevButton, preferredHeight: 100, preferredWidth: 100);
            UIKit.AddShadow(prevButton.targetGraphic, 4);
            pageText = UIKit.Text(nav, "Page", "", 44, Theme.Text).SingleLine();
            UIKit.Layout(pageText, preferredHeight: 80, preferredWidth: 220);
            nextButton = UIKit.IconButton(nav, "Next", IconKind.ChevronRight, Theme.Text, Theme.Surface, 100, 40,
                () => pager.GoTo(pager.Current + 1, true), out _);
            UIKit.Layout(nextButton, preferredHeight: 100, preferredWidth: 100);
            UIKit.AddShadow(nextButton.targetGraphic, 4);

            // 듣기 순서 (눌러서 설정 > 전체 듣기)
            chipsRow = UIKit.Rect("Sequence", controls);
            UIKit.Layout(chipsRow, preferredHeight: 72);
            UIKit.HLayout(chipsRow, 10, null, TextAnchor.MiddleCenter, expandHeight: false);

            // 재생 버튼
            var transport = UIKit.Rect("Transport", controls);
            UIKit.Layout(transport, preferredHeight: 150);
            UIKit.HLayout(transport, 28, null, TextAnchor.MiddleCenter, expandHeight: false);

            stopButton = UIKit.IconButton(transport, "Stop", IconKind.Stop, Theme.Text, Theme.Surface, 120, 44,
                () => App.Player.Stop(), out _);
            UIKit.Layout(stopButton, preferredHeight: 120, preferredWidth: 120);
            UIKit.AddShadow(stopButton.targetGraphic, 4);

            var play = UIKit.Button(transport, "PlayAll", Theme.Primary, 70, OnPlayAll);
            UIKit.Layout(play, preferredHeight: 140, preferredWidth: 540);
            UIKit.AddShadow(play.targetGraphic, 6);
            UIKit.HLayout(play, 18, UIKit.Pad(36, 36, 0, 0), TextAnchor.MiddleCenter, expandHeight: false);
            playIcon = UIKit.Image(play.transform, "Icon", Theme.TextOnColor, Sprites.Icon(IconKind.Play));
            playIcon.preserveAspect = true;
            UIKit.Layout(playIcon, preferredHeight: 56, preferredWidth: 56);
            playLabel = UIKit.Text(play.transform, "Label", "전체 듣기", 46, Theme.TextOnColor).SingleLine().AutoSize(30, 46);
            UIKit.Layout(playLabel, preferredHeight: 100, preferredWidth: 360);

            var repeat = UIKit.IconButton(transport, "Repeat", IconKind.Repeat, Theme.TextSub, Theme.Surface, 120, 56,
                () => App.Settings.SetRepeat(!App.Settings.Sequence.repeat), out repeatIcon);
            repeatBg = repeat.GetComponent<Image>();
            UIKit.Layout(repeat, preferredHeight: 120, preferredWidth: 120);
            UIKit.AddShadow(repeat.targetGraphic, 4);
        }

        private void RebuildChips()
        {
            if (chipsRow == null) return;
            for (int i = chipsRow.childCount - 1; i >= 0; i--)
            {
                var child = chipsRow.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            chips.Clear();
            chipTexts.Clear();

            var label = UIKit.Text(chipsRow, "Label", "듣기 순서", 28, Theme.TextSub).SingleLine();
            UIKit.Layout(label, preferredHeight: 60, preferredWidth: 140);
            var nodes = App.Settings.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var chip = UIKit.Button(chipsRow, "Chip" + (i + 1), Theme.Surface, 32, () => App.OpenSettings(1));
                UIKit.Layout(chip, preferredHeight: 64, preferredWidth: 64);
                var t = UIKit.Text(chip.transform, "Text", Theme.LangShort(nodes[i].lang), 30, Theme.LangColor(nodes[i].lang));
                t.rectTransform.Fill();
                chips.Add((Image)chip.targetGraphic);
                chipTexts.Add(t);
            }
            Refresh();
        }

        // ---- 재생 상태 → 화면 ------------------------------------------------------------

        private void Refresh()
        {
            if (unit == null) return;
            var player = App.Player;
            bool mine = player.Mode != PlaybackMode.None && player.Unit != null && player.Unit.unit == unit.unit;
            bool mineFull = mine && player.Mode == PlaybackMode.Full;

            // 전체 듣기 버튼
            string label;
            IconKind icon;
            if (mineFull && player.IsPreparing)
            {
                label = player.PrepareTotal > 0 ? "받는 중 " + player.PrepareDone + "/" + player.PrepareTotal : "준비 중…";
                icon = IconKind.Stop;
            }
            else if (mineFull && player.IsPlaying)
            {
                label = "일시정지";
                icon = IconKind.Pause;
            }
            else if (mineFull && player.State.active)
            {
                label = "이어 듣기";
                icon = IconKind.Play;
            }
            else
            {
                label = "전체 듣기";
                icon = IconKind.Play;
            }
            playLabel.text = label;
            playIcon.sprite = Sprites.Icon(icon);
            stopButton.interactable = mine && player.IsActive;

            bool repeat = App.Settings.Sequence.repeat;
            repeatBg.color = repeat ? Theme.Primary : Theme.Surface;
            repeatIcon.color = repeat ? Theme.TextOnColor : Theme.TextSub;

            // 지금 재생 중인 줄·언어 표시
            var step = mine && player.State.active ? player.CurrentStep : null;
            for (int p = 0; p < pages.Count; p++)
            {
                var lines = pages[p].lines;
                for (int l = 0; l < lines.Count; l++)
                {
                    var lv = lines[l];
                    bool on = step != null && step.page == p && step.line == l;
                    lv.bg.color = on ? Theme.Highlight : lv.baseColor;
                    lv.en.color = on && step.lang == Lang.En ? Theme.PrimaryDark : Theme.Text;
                    lv.ko.color = on && step.lang == Lang.Ko ? Theme.PrimaryDark : Theme.TextSub;
                }
            }

            // 듣기 순서 칩
            int node = mineFull && step != null ? step.node : -1;
            var nodes = App.Settings.Nodes;
            for (int i = 0; i < chips.Count && i < nodes.Count; i++)
            {
                var c = Theme.LangColor(nodes[i].lang);
                chips[i].color = i == node ? c : Theme.Surface;
                chipTexts[i].color = i == node ? Theme.TextOnColor : c;
            }

            // 전체 듣기 중이면 재생 위치를 따라간다
            if (mineFull && step != null)
            {
                if (player.State.step != lastFollowedStep)
                {
                    lastFollowedStep = player.State.step;
                    if (pager.Current != step.page) pager.GoTo(step.page, false);
                    ScrollToLine(step.page, step.line);
                }
            }
            else
            {
                lastFollowedStep = -1;
            }
        }

        private void OnPageChanged(int page, bool byUser)
        {
            UpdatePageText();
            if (!byUser) return;
            // 전체 듣기가 재생 중일 때 넘기면 그 예문부터 이어서 재생 (일시정지 중에는 둘러보기만)
            var player = App.Player;
            if (player.IsFull(unit.unit) && player.IsPlaying && !player.IsPreparing) player.SeekPage(page);
        }

        private void UpdatePageText()
        {
            pageText.text = (pager.Current + 1) + " / " + pager.Count;
            prevButton.interactable = pager.Current > 0;
            nextButton.interactable = pager.Current < pager.Count - 1;
        }

        private void OnPlayAll()
        {
            var player = App.Player;
            if (player.IsFull(unit.unit))
            {
                if (player.IsPreparing)
                {
                    player.Stop();
                    return;
                }
                if (player.IsPlaying)
                {
                    player.Pause();
                    return;
                }
                if (player.State.active)
                {
                    var step = player.CurrentStep;
                    if (step != null && step.page != pager.Current) player.SeekPage(pager.Current);
                    else player.Resume();
                    return;
                }
            }
            player.PlayFull(unit, pager.Current);
        }

        private void PlaySingle(int page, int line, Lang lang)
        {
            App.Player.PlaySingle(unit, page, line, lang);
        }

        // ---- 대화: 재생 중인 대사가 보이도록 스크롤 ------------------------------------

        private void ScrollToLine(int page, int line)
        {
            if (page < 0 || page >= pages.Count) return;
            var view = pages[page];
            if (view.scroll == null || line < 0 || line >= view.lines.Count) return;
            var content = view.scroll.content;
            var viewport = view.scroll.viewport;
            var rt = view.lines[line].rect;
            float top = -(rt.localPosition.y + rt.rect.yMax);
            float target = top - (viewport.rect.height - rt.rect.height) * 0.35f;
            view.scrollTarget = Mathf.Clamp(target, 0f, Mathf.Max(0f, content.rect.height - viewport.rect.height));
        }

        private void Update()
        {
            for (int i = 0; i < pages.Count; i++)
            {
                var view = pages[i];
                if (view.scrollTarget < 0f || view.scroll == null) continue;
                var c = view.scroll.content;
                var pos = c.anchoredPosition;
                float y = Mathf.Lerp(pos.y, view.scrollTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
                if (Mathf.Abs(y - view.scrollTarget) < 1f)
                {
                    y = view.scrollTarget;
                    view.scrollTarget = -1f;
                }
                view.scroll.StopMovement();
                c.anchoredPosition = new Vector2(pos.x, y);
            }
        }
    }
}
