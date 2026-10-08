using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 메인 화면 (MainScreen 프리팹).
    /// - 가운데 위: 제목
    /// - 오른쪽 위: 설정
    /// - 가운데보다 조금 위에서 아래로 2열: 왼쪽 문장(말하기) S1~S5, 오른쪽 대화 D1~D5
    /// - 오른쪽 아래: 해/달 버튼 (밝은 / 어두운 모드 전환, 미니 플레이어가 뜨면 그 위로 비켜 올라감)
    /// </summary>
    public class MainScreen : BaseScreen
    {
        /// <summary>앱 이름 (Player Settings 의 제품 이름). 화면의 제목 글자는 프리팹에 있다.</summary>
        public const string AppTitle = "영어 말하기";

        [SerializeField] internal Button settingsButton;
        [SerializeField] internal TextMeshProUGUI loadingText;
        [SerializeField] internal TextMeshProUGUI errorText;
        [Tooltip("두 열을 담은 스크롤 (목록을 받으면 켜진다)")]
        [SerializeField] internal GameObject columns;
        [SerializeField] internal RectTransform speakingColumn;
        [SerializeField] internal RectTransform dialogueColumn;
        [SerializeField] internal LevelButton levelButtonPrefab;

        [Header("해/달 버튼")]
        [SerializeField] internal Button themeButton;
        [SerializeField] internal Image themeIcon;
        [Tooltip("어두운 모드일 때 보이는 아이콘 (누르면 밝은 모드)")]
        [SerializeField] internal Sprite sunSprite;
        [Tooltip("밝은 모드일 때 보이는 아이콘 (누르면 어두운 모드)")]
        [SerializeField] internal Sprite moonSprite;
        [Tooltip("미니 플레이어가 떠 있을 때 버튼을 위로 올리는 거리")]
        [SerializeField] internal float raiseForMiniPlayer = 174f;

        private RectTransform themeRect;
        private float themeBaseY;

        public override void Init()
        {
            settingsButton.onClick.AddListener(() => App.OpenSettings(0));
            themeButton.onClick.AddListener(Theme.Toggle);
            themeRect = (RectTransform)themeButton.transform;
            themeBaseY = themeRect.anchoredPosition.y;
            Theme.Changed += UpdateThemeIcon;
            UpdateThemeIcon();
            UIKit.SetActive(loadingText, true);
            UIKit.SetActive(errorText, false);
            columns.SetActive(false);

            if (App.Content.IsIndexLoaded) BuildColumns();
            else App.Content.IndexLoaded += BuildColumns;
        }

        private void OnDestroy()
        {
            Theme.Changed -= UpdateThemeIcon;
            if (AppRoot.I != null) AppRoot.I.Content.IndexLoaded -= BuildColumns;
        }

        private void UpdateThemeIcon()
        {
            themeIcon.sprite = Theme.Mode == ThemeMode.Dark ? sunSprite : moonSprite;
        }

        private void Update()
        {
            if (themeRect == null) return;
            bool raised = App.MiniPlayer != null && App.MiniPlayer.IsVisible;
            var pos = themeRect.anchoredPosition;
            float target = themeBaseY + (raised ? raiseForMiniPlayer : 0f);
            if (Mathf.Abs(pos.y - target) < 0.5f) return;
            pos.y = Mathf.Lerp(pos.y, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            themeRect.anchoredPosition = pos;
        }

        private void BuildColumns()
        {
            App.Content.IndexLoaded -= BuildColumns;
            UIKit.SetActive(loadingText, false);

            var speaking = App.Content.LevelsOf("speaking");
            var dialogue = App.Content.LevelsOf("dialogue");
            if (speaking.Count == 0 && dialogue.Count == 0)
            {
                UIKit.SetActive(errorText, true);
                return;
            }

            columns.SetActive(true);
            AddLevels(speakingColumn, speaking, false);
            AddLevels(dialogueColumn, dialogue, true);
        }

        private void AddLevels(Transform column, List<LevelInfo> levels, bool dialogue)
        {
            foreach (var level in levels)
            {
                var l = level;
                UIKit.Spawn(levelButtonPrefab, column).Bind(l, dialogue, () => App.OpenLevel(l));
            }
        }
    }
}
