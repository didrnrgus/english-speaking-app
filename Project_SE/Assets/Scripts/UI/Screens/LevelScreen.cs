using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>레벨 화면 (LevelScreen 프리팹): 유닛(주제) 10개 목록.</summary>
    public class LevelScreen : BaseScreen
    {
        public LevelInfo Level;

        [SerializeField] internal RectTransform list;
        [SerializeField] internal TextMeshProUGUI descText;
        [SerializeField] internal UnitCard unitCardPrefab;

        private readonly Dictionary<string, UnitCard> cards = new Dictionary<string, UnitCard>();

        public override void Init()
        {
            SetupTopBar(Level.level + " · " + Level.title);
            descText.text = Level.desc + " · 유닛을 골라 주세요";

            foreach (var unit in App.Content.UnitsOf(Level.level))
            {
                var u = unit;
                var card = UIKit.Spawn(unitCardPrefab, list);
                card.Bind(u, Level.IsDialogue, () => App.OpenUnit(u));
                cards[u.unit] = card;
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
            foreach (var pair in cards)
            {
                bool playing = player.IsActive && player.Mode == Audio.PlaybackMode.Full && player.Unit != null && player.Unit.unit == pair.Key;
                var loaded = App.Content.GetLoadedUnit(pair.Key);
                int count = loaded != null ? loaded.items.Count : 5;
                pair.Value.SetStatus(playing ? "<color=#FF7A45>▶ 전체 듣기 중</color>" : "예문 " + count + "개");
            }
        }
    }
}
