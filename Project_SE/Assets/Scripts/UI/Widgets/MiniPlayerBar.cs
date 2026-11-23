using EnglishApp.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 전체 듣기 중에 다른 화면(메인·레벨)으로 나갔을 때 아래에 뜨는 작은 플레이어 (AppCanvas 프리팹 안).
    /// 누르면 지금 재생 중인 유닛 화면으로 간다.
    /// 레벨 전체 듣기:  [S1 전체 듣기 · U03 가족]  유닛 3/10 · 예문 2/5 · 영어
    /// 유닛 전체 듣기:  [S1-U03  가족]            유닛 전체 듣기 · 예문 2/5 · 영어
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

        /// <summary>어두운 바 위의 범위 표시 색 (S1 전체 듣기 / 유닛 전체 듣기).</summary>
        private const string TagColor = "#FFB08A";

        private static AppRoot App => AppRoot.I;

        /// <summary>지금 떠 있는지 (메인 화면의 해/달 버튼이 이 위로 비켜 올라간다).</summary>
        public bool IsVisible => visible;

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
            bool show = player.Mode == PlaybackMode.Full && player.IsActive && player.UnitId != null;
            if (show)
            {
                var top = App.Nav.Top as UnitScreen;
                if (top != null && top.UnitId == player.UnitId) show = false;
            }
            SetVisible(show, false);
            if (!show) return;

            transform.SetAsLastSibling();
            var step = player.CurrentStep;
            string lang = step != null ? Theme.LangName(step.lang) : null;
            if (player.IsLevelMode)
            {
                title.text = PlaybackText.Join(PlaybackText.Color(PlaybackText.LevelName(player.Level), TagColor),
                    player.IsPreparing ? player.Level.title : PlaybackText.CurrentUnit(player));
                sub.text = player.IsPreparing
                    ? PlaybackText.Preparing(player)
                    : PlaybackText.Join(PlaybackText.UnitProgress(player), PlaybackText.PageProgress(player), lang);
            }
            else
            {
                title.text = player.UnitId + "  " + player.UnitTitle;
                sub.text = player.IsPreparing
                    ? PlaybackText.Preparing(player)
                    : PlaybackText.Join(PlaybackText.Color(PlaybackText.UnitName, TagColor), PlaybackText.PageProgress(player), lang);
            }
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
            if (player.UnitId == null) return;
            var info = App.Content.FindUnit(player.UnitId);
            if (info != null) App.OpenUnit(info);
        }
    }
}
