using System.Collections.Generic;
using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 레벨 화면 (LevelScreen 프리팹): 맨 위 'S1 전체 듣기' 버튼 + 유닛(주제) 10개 목록.
    /// - 'S1 전체 듣기' = 레벨 전체 듣기 (마지막으로 듣던 유닛부터 레벨 끝까지 이어서)
    /// - 유닛 카드에는 재생 중인 유닛(레벨 / 유닛 전체 듣기 구분)과 이어 듣기 위치가 표시된다.
    /// </summary>
    public class LevelScreen : BaseScreen
    {
        public LevelInfo Level;

        [SerializeField] internal RectTransform list;
        [SerializeField] internal TextMeshProUGUI descText;
        [SerializeField] internal UnitCard unitCardPrefab;

        [Header("레벨 전체 듣기 버튼 (없으면 숨김)")]
        [SerializeField] internal Button levelPlayButton;
        [SerializeField] internal Image levelPlayIcon;
        [SerializeField] internal TextMeshProUGUI levelPlayLabel;
        [SerializeField] internal TextMeshProUGUI levelPlaySub;
        [SerializeField] internal Sprite playSprite;
        [SerializeField] internal Sprite pauseSprite;
        [SerializeField] internal Sprite stopSprite;

        private readonly List<KeyValuePair<UnitInfo, UnitCard>> cards = new List<KeyValuePair<UnitInfo, UnitCard>>();

        public override void Init()
        {
            SetupTopBar(Level.level + " · " + Level.title);
            descText.text = Level.desc + " · 유닛을 골라 주세요";

            if (levelPlayButton != null) levelPlayButton.onClick.AddListener(() => App.ToggleLevelPlayback(Level));

            foreach (var unit in App.Content.UnitsOf(Level.level))
            {
                var u = unit;
                var card = UIKit.Spawn(unitCardPrefab, list);
                card.Bind(u, Level.IsDialogue, () => App.OpenUnit(u));
                cards.Add(new KeyValuePair<UnitInfo, UnitCard>(u, card));
            }

            App.Player.Changed += RefreshStatus;
            RefreshStatus();
        }

        public override void OnShow()
        {
            RefreshStatus();
        }

        private void OnDestroy()
        {
            if (AppRoot.I != null) AppRoot.I.Player.Changed -= RefreshStatus;
        }

        private void RefreshStatus()
        {
            var player = App.Player;
            bool levelActive = player.IsLevelFull(Level.level) && player.IsActive;
            RefreshLevelButton(player, levelActive);

            // 듣는 중이 아니면 '이어 듣기' 위치를 카드에 표시
            string resumeId = null;
            string playingId = player.Mode == PlaybackMode.Full && player.IsActive ? player.UnitId : null;
            bool anyHere = playingId != null && cards.Exists(c => c.Key.unit == playingId);
            if (!anyHere)
            {
                int resume = player.ResumeUnitIndex(Level);
                if (resume > 0 && resume < cards.Count) resumeId = cards[resume].Key.unit;
            }

            foreach (var pair in cards)
            {
                string id = pair.Key.unit;
                var loaded = App.Content.GetLoadedUnit(id);
                int count = loaded != null ? loaded.items.Count : 5;
                string normal = "예문 " + count + "개";

                if (player.Mode == PlaybackMode.Full && player.IsActive && player.UnitId == id)
                {
                    // "▶ S1 전체 듣기 중 · 예문 2/5" / "▶ 유닛 전체 듣기 중 · 예문 2/5"
                    string scope = player.IsLevelMode ? PlaybackText.LevelName(Level) : PlaybackText.UnitName;
                    string state = player.IsPreparing ? scope + " 준비 중"
                        : player.IsPlaying ? "▶ " + scope + " 중"
                        : scope + " 일시정지";
                    pair.Value.SetStatus(PlaybackText.Color(PlaybackText.Join(state, PlaybackText.PageProgress(player))));
                }
                else if (id == resumeId)
                {
                    pair.Value.SetStatus(normal + " · " + PlaybackText.Color("이어 듣기 위치"));
                }
                else
                {
                    pair.Value.SetStatus(normal);
                }
            }
        }

        private void RefreshLevelButton(AudioPlayer player, bool levelActive)
        {
            if (levelPlayButton == null) return;
            string label;
            string sub;
            Sprite icon;
            string name = PlaybackText.LevelName(Level);

            if (levelActive && player.IsPreparing)
            {
                label = PlaybackText.Preparing(player, "받는 중 ");
                sub = name + " 준비 · 받은 음성은 다음부터 바로 재생돼요";
                icon = stopSprite;
            }
            else if (levelActive)
            {
                bool playing = player.IsPlaying;
                label = playing ? "일시정지" : "이어 듣기";
                sub = PlaybackText.Join(playing ? name + " 중" : name, PlaybackText.CurrentUnit(player), PlaybackText.UnitProgress(player));
                icon = playing ? pauseSprite : playSprite;
            }
            else
            {
                label = name;
                int resume = player.ResumeUnitIndex(Level);
                if (resume > 0 && resume < cards.Count)
                {
                    var u = cards[resume].Key;
                    sub = u.ShortCode + " " + u.title + "부터 이어서 · 유닛 " + cards.Count + "개";
                }
                else
                {
                    sub = "유닛 " + cards.Count + "개를 처음부터 이어서";
                }
                icon = playSprite;
            }

            if (levelPlayLabel != null) levelPlayLabel.text = label;
            if (levelPlaySub != null) levelPlaySub.text = sub;
            if (levelPlayIcon != null && icon != null) levelPlayIcon.sprite = icon;
        }
    }
}
