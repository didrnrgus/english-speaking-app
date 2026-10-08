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

        public override void Init()
        {
            settingsButton.onClick.AddListener(() => App.OpenSettings(0));
            UIKit.SetActive(loadingText, true);
            UIKit.SetActive(errorText, false);
            columns.SetActive(false);

            if (App.Content.IsIndexLoaded) BuildColumns();
            else App.Content.IndexLoaded += BuildColumns;
        }

        private void OnDestroy()
        {
            if (AppRoot.I != null) AppRoot.I.Content.IndexLoaded -= BuildColumns;
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
