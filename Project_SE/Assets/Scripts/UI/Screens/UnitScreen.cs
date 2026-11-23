using System.Collections;
using System.Collections.Generic;
using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 유닛 화면 (UnitScreen 프리팹): 한 페이지에 예문 하나 (좌우로 넘김).
    /// - 문장/대사나 ▶ 버튼을 누르면 개별 듣기 (설정의 개별 듣기 음량·빠르기)
    /// - 아래 '전체 듣기' 는 유닛 전체를 듣기 시퀀스대로 이어서 재생 (화면이 꺼져도 계속)
    /// - 레벨 전체 듣기(S1 전체 듣기)가 이 유닛을 재생 중이면 같은 버튼으로 일시정지 / 이어 듣기,
    ///   재생이 다음 유닛으로 넘어가면 화면도 그 유닛으로 따라간다.
    /// - 컨트롤 맨 위 줄(scopeText)에 지금 어떤 전체 듣기인지 표시 (S1 전체 듣기 중 · 유닛 3/10 / 유닛 전체 듣기 중)
    /// </summary>
    public class UnitScreen : BaseScreen
    {
        public UnitInfo Info;
        public string UnitId => Info != null ? Info.unit : null;

        [Header("불러오기")]
        [SerializeField] internal TextMeshProUGUI centerText;
        [SerializeField] internal Button retryButton;

        [Header("페이지")]
        [SerializeField] internal PagerView pager;
        [SerializeField] internal SpeakingPage speakingPagePrefab;
        [SerializeField] internal DialoguePage dialoguePagePrefab;

        [Header("아래 컨트롤")]
        [SerializeField] internal GameObject controls;
        [Tooltip("지금 어떤 전체 듣기인지 (없으면 표시 안 함)")]
        [SerializeField] internal TextMeshProUGUI scopeText;
        [SerializeField] internal Button prevButton;
        [SerializeField] internal Button nextButton;
        [SerializeField] internal TextMeshProUGUI pageText;
        [SerializeField] internal RectTransform chipsRow;
        [SerializeField] internal SequenceChip chipPrefab;
        [SerializeField] internal Button stopButton;
        [SerializeField] internal Button playButton;
        [SerializeField] internal Image playIcon;
        [SerializeField] internal TextMeshProUGUI playLabel;
        [SerializeField] internal Button repeatButton;
        [SerializeField] internal Image repeatIcon;

        [Header("아이콘")]
        [SerializeField] internal Sprite playSprite;
        [SerializeField] internal Sprite pauseSprite;
        [SerializeField] internal Sprite stopSprite;

        private UnitData unit;
        private readonly List<UnitPage> pages = new List<UnitPage>();
        private readonly List<SequenceChip> chips = new List<SequenceChip>();
        private Color repeatOffBg;
        private Color repeatOffIcon;
        private int lastFollowedStep = -1;
        private bool subscribed;
        private bool followingLevel; // 레벨 전체 듣기가 이 유닛을 재생하는 걸 보고 있었음 (다음 유닛으로 넘어가면 따라감)

        public override void Init()
        {
            SetupTopBar(Info.unit + " · " + Info.title);
            retryButton.onClick.AddListener(() => StartCoroutine(Load()));
            pager.gameObject.SetActive(false);
            controls.SetActive(false);
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

        // ---- 화면 채우기 ------------------------------------------------------------------

        private void BuildContent()
        {
            pager.gameObject.SetActive(true);
            controls.SetActive(true);
            for (int p = 0; p < unit.items.Count; p++)
            {
                int pageIndex = p;
                var pageRoot = pager.AddPage();
                UnitPage page = unit.IsDialogue
                    ? UIKit.Spawn(dialoguePagePrefab, pageRoot)
                    : (UnitPage)UIKit.Spawn(speakingPagePrefab, pageRoot);
                ((RectTransform)page.transform).Fill();
                page.Bind(unit.items[p], (line, lang) => PlaySingle(pageIndex, line, lang));
                pages.Add(page);
            }
            pager.PageChanged += OnPageChanged;

            prevButton.onClick.AddListener(() => pager.GoTo(pager.Current - 1, true));
            nextButton.onClick.AddListener(() => pager.GoTo(pager.Current + 1, true));
            stopButton.onClick.AddListener(() => App.Player.Stop());
            playButton.onClick.AddListener(OnPlayAll);
            repeatButton.onClick.AddListener(() => App.Settings.SetRepeat(!App.Settings.Sequence.repeat));
            repeatOffBg = repeatButton.targetGraphic.color;
            repeatOffIcon = repeatIcon.color;

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

        /// <summary>듣기 순서 칩 (누르면 설정 > 전체 듣기). 칩 앞의 '듣기 순서' 글자는 프리팹에 있다.</summary>
        private void RebuildChips()
        {
            foreach (var chip in chips) UIKit.DestroyNow(chip);
            chips.Clear();

            foreach (var node in App.Settings.Nodes)
            {
                var chip = UIKit.Spawn(chipPrefab, chipsRow);
                chip.name = "Chip" + (chips.Count + 1);
                chip.Bind(node.lang);
                chip.Button.onClick.AddListener(() => App.OpenSettings(1));
                chips.Add(chip);
            }
            Refresh();
        }

        // ---- 재생 상태 → 화면 ------------------------------------------------------------

        private void Refresh()
        {
            if (unit == null) return;
            var player = App.Player;
            if (FollowLevelPlayback(player)) return;
            bool mine = player.Mode != PlaybackMode.None && player.UnitId == unit.unit;
            bool mineFull = mine && player.Mode == PlaybackMode.Full;

            // 전체 듣기 버튼
            string label;
            Sprite icon;
            if (mineFull && player.IsPreparing)
            {
                label = player.PrepareTotal > 0 ? "받는 중 " + player.PrepareDone + "/" + player.PrepareTotal : "준비 중…";
                icon = stopSprite;
            }
            else if (mineFull && player.IsPlaying)
            {
                label = "일시정지";
                icon = pauseSprite;
            }
            else if (mineFull && player.State.active)
            {
                label = "이어 듣기";
                icon = playSprite;
            }
            else
            {
                label = "전체 듣기";
                icon = playSprite;
            }
            playLabel.text = label;
            playIcon.sprite = icon;
            stopButton.interactable = mine && player.IsActive;
            RefreshScope(player, mineFull);

            bool repeat = App.Settings.Sequence.repeat;
            repeatButton.targetGraphic.color = repeat ? Theme.Primary : repeatOffBg;
            repeatIcon.color = repeat ? Theme.TextOnColor : repeatOffIcon;

            // 지금 재생 중인 줄·언어 표시
            var step = mine && player.State.active ? player.CurrentStep : null;
            for (int p = 0; p < pages.Count; p++)
            {
                var lines = pages[p].Lines;
                for (int l = 0; l < lines.Count; l++)
                {
                    bool on = step != null && step.page == p && step.line == l;
                    lines[l].SetPlaying(on, on ? step.lang : Lang.En);
                }
            }

            // 듣기 순서 칩
            int node = mineFull && step != null ? step.node : -1;
            for (int i = 0; i < chips.Count; i++) chips[i].SetPlaying(i == node);

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

        /// <summary>
        /// 레벨 전체 듣기가 이 유닛을 재생하다 다음 유닛으로 넘어가면 화면도 그 유닛으로 바꾼다.
        /// 다른 유닛을 직접 열어 둘러보는 중이면 따라가지 않는다 (이 유닛이 재생 중일 때 보고 있었던 경우만).
        /// </summary>
        private bool FollowLevelPlayback(AudioPlayer player)
        {
            bool levelHere = player.IsLevelMode && player.IsActive && player.Level.level == unit.level;
            if (!levelHere)
            {
                followingLevel = false;
                return false;
            }
            string playingId = player.UnitId;
            if (playingId == unit.unit)
            {
                followingLevel = true;
                return false;
            }
            if (!followingLevel || player.IsPreparing || App.Nav.Top != this) return false;
            followingLevel = false;
            App.FollowUnit(playingId);
            return true;
        }

        /// <summary>컨트롤 맨 위 줄: 지금 어떤 전체 듣기인지.</summary>
        private void RefreshScope(AudioPlayer player, bool mineFull)
        {
            if (scopeText == null) return;
            string text = "";
            if (mineFull && player.IsActive)
            {
                string state = player.IsPreparing ? " 준비 중" : player.IsPlaying ? " 중" : " 일시정지";
                text = player.IsLevelMode
                    ? PlaybackText.Join(PlaybackText.Color(PlaybackText.LevelName(player.Level) + state), PlaybackText.UnitProgress(player))
                    : PlaybackText.Color(PlaybackText.UnitName + state);
            }
            scopeText.text = text;
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
            if (view.Scroll == null || line < 0 || line >= view.Lines.Count) return;
            var content = view.Scroll.content;
            var viewport = view.Scroll.viewport;
            var rt = view.Lines[line].FollowTarget;
            float top = -(rt.localPosition.y + rt.rect.yMax);
            float target = top - (viewport.rect.height - rt.rect.height) * 0.35f;
            view.ScrollTarget = Mathf.Clamp(target, 0f, Mathf.Max(0f, content.rect.height - viewport.rect.height));
        }

        private void Update()
        {
            for (int i = 0; i < pages.Count; i++)
            {
                var view = pages[i];
                if (view.ScrollTarget < 0f || view.Scroll == null) continue;
                var c = view.Scroll.content;
                var pos = c.anchoredPosition;
                float y = Mathf.Lerp(pos.y, view.ScrollTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
                if (Mathf.Abs(y - view.ScrollTarget) < 1f)
                {
                    y = view.ScrollTarget;
                    view.ScrollTarget = -1f;
                }
                view.Scroll.StopMovement();
                c.anchoredPosition = new Vector2(pos.x, y);
            }
        }
    }
}
