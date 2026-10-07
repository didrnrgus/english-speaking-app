using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 전체 듣기 중에 다른 화면(메인·레벨)으로 나갔을 때 아래에 뜨는 작은 플레이어.
    /// 누르면 그 유닛 화면으로 돌아간다.
    /// </summary>
    public class MiniPlayerBar : MonoBehaviour
    {
        private CanvasGroup group;
        private TextMeshProUGUI title;
        private TextMeshProUGUI sub;
        private Image playIcon;
        private bool visible;

        private static AppRoot App => AppRoot.I;

        public static MiniPlayerBar Create(Transform parent)
        {
            var bg = UIKit.Rounded(parent, "MiniPlayer", Theme.Text, 44);
            bg.raycastTarget = true;
            bg.rectTransform.BottomStrip(150, 24, 28, 28);
            UIKit.AddShadow(bg, 8);
            var bar = bg.gameObject.AddComponent<MiniPlayerBar>();
            bar.group = bg.gameObject.AddComponent<CanvasGroup>();
            bar.SetVisible(false, true);

            var open = bg.gameObject.AddComponent<Button>();
            open.transition = Selectable.Transition.None;
            open.navigation = new Navigation { mode = Navigation.Mode.None };
            open.onClick.AddListener(bar.OpenUnit);

            bar.title = UIKit.Text(bg.transform, "Title", "", 34, Theme.TextOnColor, TextAlignmentOptions.BottomLeft).SingleLine().AutoSize(26, 34);
            bar.title.rectTransform.Fill(44, 18, 300, 74);
            bar.sub = UIKit.Text(bg.transform, "Sub", "", 28, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.TopLeft).SingleLine();
            bar.sub.rectTransform.Fill(44, 82, 300, 18);

            var play = UIKit.IconButton(bg.transform, "PlayPause", IconKind.Pause, Theme.TextOnColor, Theme.Primary, 104, 46, bar.TogglePlay, out bar.playIcon);
            ((RectTransform)play.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-148, 0), new Vector2(104, 104));
            var stop = UIKit.IconButton(bg.transform, "Stop", IconKind.Close, Theme.TextOnColor, new Color(1, 1, 1, 0.15f), 104, 40,
                () => App.Player.Stop(), out _);
            ((RectTransform)stop.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(104, 104));
            return bar;
        }

        private void Update()
        {
            if (App == null || App.Nav == null) return;
            var player = App.Player;
            bool show = player.Mode == PlaybackMode.Full && player.Unit != null && player.IsActive;
            if (show)
            {
                var top = App.Nav.Top as UnitScreen;
                if (top != null && top.UnitId == player.Unit.unit) show = false;
            }
            SetVisible(show, false);
            if (!show) return;

            transform.SetAsLastSibling();
            title.text = player.Unit.unit + "  " + player.Unit.title;
            var step = player.CurrentStep;
            sub.text = player.IsPreparing
                ? "음성 준비 중…"
                : step != null
                    ? "전체 듣기 · " + (step.page + 1) + " / " + player.Unit.items.Count + " · " + Theme.LangName(step.lang)
                    : "전체 듣기";
            playIcon.sprite = Sprites.Icon(player.IsPlaying ? IconKind.Pause : IconKind.Play);
        }

        private void SetVisible(bool value, bool force)
        {
            if (visible == value && !force) return;
            visible = value;
            group.alpha = value ? 1f : 0f;
            group.blocksRaycasts = value;
            group.interactable = value;
        }

        private void TogglePlay()
        {
            var player = App.Player;
            if (player.IsPlaying) player.Pause();
            else player.Resume();
        }

        private void OpenUnit()
        {
            var player = App.Player;
            if (player.Unit == null) return;
            var info = App.Content.FindUnit(player.Unit.unit);
            if (info != null) App.OpenUnit(info);
        }
    }
}
