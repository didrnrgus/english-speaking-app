using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>메인 화면의 레벨 버튼 (LevelButton 프리팹): [코드 배지] 제목 / 설명.</summary>
    public class LevelButton : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal TextMeshProUGUI code;
        [SerializeField] internal TextMeshProUGUI title;
        [SerializeField] internal TextMeshProUGUI desc;

        [Tooltip("버튼 색 (글래스 틴트): 문장(말하기) 레벨")]
        [SerializeField] internal Color speakingColor;
        [Tooltip("버튼 색 (글래스 틴트): 대화 레벨")]
        [SerializeField] internal Color dialogueColor;

        public void Bind(LevelInfo level, bool dialogue, Action onClick)
        {
            name = "Level_" + level.level;
            button.targetGraphic.color = dialogue ? dialogueColor : speakingColor;
            code.text = level.level;
            title.text = level.title;
            desc.text = level.desc;
            button.onClick.AddListener(() => onClick());
        }
    }
}
