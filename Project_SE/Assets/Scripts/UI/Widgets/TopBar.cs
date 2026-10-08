using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>위쪽 바 (TopBar 프리팹): [뒤로] 제목 [설정].</summary>
    public class TopBar : MonoBehaviour
    {
        [SerializeField] internal TextMeshProUGUI title;
        [SerializeField] internal Button backButton;
        [SerializeField] internal Button settingsButton;

        public void Setup(string text, Action onBack, Action onSettings)
        {
            title.text = text;
            if (backButton != null) backButton.onClick.AddListener(() => onBack());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => onSettings());
        }
    }
}
