using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>레벨 화면의 유닛 카드 (UnitCard 프리팹): [번호 배지] 제목 / 상태 [›].</summary>
    public class UnitCard : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image badge;
        [SerializeField] internal TextMeshProUGUI number;
        [SerializeField] internal TextMeshProUGUI title;
        [SerializeField] internal TextMeshProUGUI status;

        [Tooltip("번호 배지 색: 문장(말하기) 레벨")]
        [SerializeField] internal Color speakingColor;
        [Tooltip("번호 배지 색: 대화 레벨")]
        [SerializeField] internal Color dialogueColor;

        public void Bind(UnitInfo unit, bool dialogue, Action onClick)
        {
            name = "Unit_" + unit.unit;
            badge.color = dialogue ? dialogueColor : speakingColor;
            number.text = unit.ShortCode.Replace("U", "");
            title.text = unit.title;
            button.onClick.AddListener(() => onClick());
        }

        public void SetStatus(string text)
        {
            status.text = text;
        }
    }
}
