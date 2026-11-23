using System.Collections;
using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 설정 팝업 (SettingsPopup 프리팹, 오른쪽 위 톱니바퀴).
    /// - 개별 듣기: 음량, 빠르기
    /// - 전체 듣기: 듣기 시퀀스 (끌어서 순서 변경, 노드 추가/삭제, 노드마다 음량·빠르기·텀), 반복
    /// 슬라이더는 바꾸는 즉시 재생 중인 소리에 적용된다.
    /// </summary>
    public class SettingsPopup : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup group;
        [Tooltip("바깥 (누르면 닫힘)")]
        [SerializeField] internal Button dimButton;
        [SerializeField] internal Button closeButton;

        [Header("탭: 0 = 개별 듣기, 1 = 전체 듣기")]
        [SerializeField] internal Button[] tabButtons = new Button[2];
        [SerializeField] internal TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[2];
        [SerializeField] internal GameObject[] tabPages = new GameObject[2];
        [SerializeField] internal NowPlayingCard[] nowCards = new NowPlayingCard[2];

        [Header("개별 듣기")]
        [SerializeField] internal LabeledSlider singleVolume;
        [SerializeField] internal LabeledSlider singleSpeed;
        [SerializeField] internal Button previewButton;

        [Header("전체 듣기")]
        [SerializeField] internal SequenceListView sequenceList;
        [SerializeField] internal Button addEn;
        [SerializeField] internal Button addKo;
        [SerializeField] internal Button repeatToggle;
        [SerializeField] internal RectTransform repeatKnob;
        [SerializeField] internal Button resetButton;

        private AppRoot App => AppRoot.I;

        private Color repeatOffColor;
        private bool closing;

        public static SettingsPopup Open(SettingsPopup prefab, RectTransform overlay, int tab)
        {
            var popup = UIKit.Spawn(prefab, overlay);
            ((RectTransform)popup.transform).Fill();
            popup.Init();
            popup.SelectTab(tab);
            popup.StartCoroutine(popup.Fade(0f, 1f, null));
            return popup;
        }

        private void Init()
        {
            group.alpha = 0f;
            dimButton.onClick.AddListener(Close);
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
                nowCards[i].Init(TogglePlayback);
            }

            // 개별 듣기
            var s = App.Settings.Single;
            singleVolume.Setup(AppSettings.VolumeMin, AppSettings.VolumeMax, AppSettings.VolumeStep,
                s.volume, LabeledSlider.FormatVolume, Theme.Primary, v => App.Settings.SetSingle(v, singleSpeed.Value));
            singleSpeed.Setup(AppSettings.SpeedMin, AppSettings.SpeedMax, AppSettings.SpeedStep,
                s.speed, LabeledSlider.FormatSpeed, Theme.Primary, v => App.Settings.SetSingle(singleVolume.Value, v));
            previewButton.onClick.AddListener(() => App.Player.ReplayLastSingle());

            // 전체 듣기
            sequenceList.Init(App.Settings);
            addEn.onClick.AddListener(() => App.Settings.AddNode(Lang.En));
            addKo.onClick.AddListener(() => App.Settings.AddNode(Lang.Ko));
            repeatToggle.onClick.AddListener(() => App.Settings.SetRepeat(!App.Settings.Sequence.repeat));
            repeatOffColor = repeatToggle.targetGraphic.color;
            resetButton.onClick.AddListener(() => App.Settings.ResetSequence());

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
                tabButtons[i].targetGraphic.color = on ? Theme.Surface : new Color(1, 1, 1, 0);
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

        private void OnStructureChanged()
        {
            sequenceList.Rebuild();
            RefreshDynamic();
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
            if (player.Mode != PlaybackMode.None && player.UnitId != null && player.IsActive)
            {
                var step = player.CurrentStep;
                if (player.Mode == PlaybackMode.Full)
                {
                    // "S1 전체 듣기 · U03 · 2/5 · 3번 영어" / "유닛 전체 듣기 · S1-U03 · 2/5 · 3번 영어"
                    info = player.IsLevelMode
                        ? PlaybackText.LevelName(player.Level) + " · " + PlaybackText.ShortCode(player.UnitId)
                        : PlaybackText.UnitName + " · " + player.UnitId;
                    if (step != null && player.Unit != null)
                        info += " · " + (step.page + 1) + "/" + player.Unit.items.Count + " · " + (step.node + 1) + "번 " + Theme.LangName(step.lang);
                }
                else
                {
                    info = "개별 듣기 · " + player.UnitId;
                    if (step != null) info += " · " + Theme.LangName(step.lang);
                }
                if (player.IsPreparing) info = player.IsLevelMode ? PlaybackText.LevelName(player.Level) + " · " + PlaybackText.Preparing(player) : "음성 준비 중…";
            }
            foreach (var card in nowCards) card.Show(info, player.IsPlaying);

            // 시퀀스에서 지금 재생 중인 노드
            var fullStep = player.Mode == PlaybackMode.Full && player.State.active ? player.CurrentStep : null;
            sequenceList.SetPlayingNode(fullStep != null ? fullStep.node : -1);

            addEn.interactable = settings.CanAddNode;
            addKo.interactable = settings.CanAddNode;
            previewButton.interactable = player.HasLastSingle;

            bool repeat = settings.Sequence.repeat;
            repeatToggle.targetGraphic.color = repeat ? Theme.Primary : repeatOffColor;
            repeatKnob.anchorMin = repeatKnob.anchorMax = new Vector2(repeat ? 1f : 0f, 0.5f);
            repeatKnob.pivot = new Vector2(repeat ? 1f : 0f, 0.5f);
            repeatKnob.anchoredPosition = new Vector2(repeat ? -8f : 8f, 0f);
        }
    }
}
