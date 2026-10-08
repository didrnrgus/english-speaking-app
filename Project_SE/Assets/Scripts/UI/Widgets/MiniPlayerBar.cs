using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 전체 듣기 중에 다른 화면(메인·레벨)으로 나갔을 때 아래에 뜨는 작은 플레이어 (AppCanvas 프리팹 안).
    /// 누르면 그 유닛 화면으로 돌아간다.
    /// </summary>
    public class MiniPlayerBar : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup group;
        [Tooltip("바 전체 (누르면 유닛 화면으로)")]
        [SerializeField] internal Button openButton;
        [SerializeField] internal TextMeshProUGUI title;
        [SerializeField] internal TextMeshProUGUI sub;
        [SerializeField] internal Button playButton;
        [SerializeField] internal Image playIcon;
        [SerializeField] internal Button stopButton;
        [SerializeField] internal Sprite playSprite;
        [SerializeField] internal Sprite pauseSprite;

        private bool visible;

        private static AppRoot App => AppRoot.I;

        private void Awake()
        {
            SetVisible(false, true);
            openButton.onClick.AddListener(OpenUnit);
            playButton.onClick.AddListener(TogglePlay);
            stopButton.onClick.AddListener(() => App.Player.Stop());
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
            playIcon.sprite = player.IsPlaying ? pauseSprite : playSprite;
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
