using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>레벨 화면: 유닛(주제) 10개 목록.</summary>
    public class LevelScreen : BaseScreen
    {
        public LevelInfo Level;

        private readonly Dictionary<string, TextMeshProUGUI> statusTexts = new Dictionary<string, TextMeshProUGUI>();

        public override void Build()
        {
            BuildTopBar(Level.level + " · " + Level.title, true);

            var scroll = UIKit.VScroll(Root, "Units", out var content, 22, UIKit.Pad(40, 40, 16, 240));
            scroll.GetComponent<RectTransform>().Fill(0, Theme.TopBarHeight, 0, 0);

            var desc = UIKit.Text(content, "Desc", Level.desc + " · 유닛을 골라 주세요", 32, Theme.TextSub, TextAlignmentOptions.MidlineLeft).SingleLine();
            UIKit.Layout(desc, preferredHeight: 60);

            var color = Theme.TrackColor(Level.IsDialogue);
            foreach (var unit in App.Content.UnitsOf(Level.level))
            {
                var u = unit;
                var card = UIKit.Button(content, "Unit_" + u.unit, Theme.Surface, 36, () => App.OpenUnit(u));
                UIKit.Layout(card, preferredHeight: 150, minHeight: 150);
                UIKit.AddShadow(card.targetGraphic, 5);
                UIKit.HLayout(card, 24, UIKit.Pad(26, 28, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);

                var badge = UIKit.Image(card.transform, "Badge", color, Sprites.Circle);
                UIKit.Layout(badge, preferredHeight: 92, preferredWidth: 92);
                var num = UIKit.Text(badge.transform, "Num", u.ShortCode.Replace("U", ""), 36, Theme.TextOnColor);
                num.rectTransform.Fill();

                var texts = UIKit.Rect("Texts", card.transform);
                UIKit.Layout(texts, preferredHeight: 120, flexibleWidth: 1);
                var title = UIKit.Text(texts, "Title", u.title, 42, Theme.Text, TextAlignmentOptions.BottomLeft).SingleLine().AutoSize(28, 42);
                title.rectTransform.Fill(0, 4, 0, 56);
                var status = UIKit.Text(texts, "Status", "", 28, Theme.TextSub, TextAlignmentOptions.TopLeft).SingleLine();
                status.rectTransform.Fill(0, 70, 0, 4);
                statusTexts[u.unit] = status;

                var chevron = UIKit.Icon(card.transform, IconKind.ChevronRight, Theme.TextSub, 40);
                UIKit.Layout(chevron, preferredHeight: 40, preferredWidth: 40);
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
            foreach (var pair in statusTexts)
            {
                bool playing = player.IsActive && player.Mode == Audio.PlaybackMode.Full && player.Unit != null && player.Unit.unit == pair.Key;
                var loaded = App.Content.GetLoadedUnit(pair.Key);
                int count = loaded != null ? loaded.items.Count : 5;
                pair.Value.text = playing ? "<color=#FF7A45>▶ 전체 듣기 중</color>" : "예문 " + count + "개";
            }
        }
    }
}
