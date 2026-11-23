using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 메인 화면의 레벨 버튼 (LevelButton 프리팹): [코드 배지] 제목 / 설명 [▶].
    /// - 카드를 누르면 레벨 화면, 오른쪽 ▶ 를 누르면 레벨 전체 듣기 (S1 의 유닛 10개를 이어서).
    /// - 이 레벨을 듣는 중이면 설명 자리에 지금 위치(유닛·예문)가 뜨고 ▶ 가 ⏸ 로 바뀐다.
    /// </summary>
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

        [Header("레벨 전체 듣기 (없으면 숨김)")]
        [SerializeField] internal Button playButton;
        [SerializeField] internal Image playIcon;
        [SerializeField] internal Sprite playSprite;
        [SerializeField] internal Sprite pauseSprite;
        [SerializeField] internal Sprite stopSprite;

        private string baseDesc;

        public void Bind(LevelInfo level, bool dialogue, Action onClick, Action onPlay)
        {
            name = "Level_" + level.level;
            button.targetGraphic.color = dialogue ? dialogueColor : speakingColor;
            code.text = level.level;
            title.text = level.title;
            baseDesc = level.desc;
            desc.text = baseDesc;
            button.onClick.AddListener(() => onClick());
            if (playButton != null) playButton.onClick.AddListener(() => onPlay());
        }

        public enum PlayIcon
        {
            Play,
            Pause,
            Stop,
        }

        /// <summary>status 가 null 이면 원래 설명으로 돌아간다.</summary>
        public void SetPlayback(string status, PlayIcon icon)
        {
            desc.text = status ?? baseDesc;
            if (playIcon == null) return;
            var sprite = icon == PlayIcon.Pause ? pauseSprite : icon == PlayIcon.Stop ? stopSprite : playSprite;
            if (sprite != null) playIcon.sprite = sprite;
        }
    }
}
