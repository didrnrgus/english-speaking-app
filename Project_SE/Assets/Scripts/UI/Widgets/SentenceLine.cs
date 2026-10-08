using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 예문 한 줄 (문장 페이지의 문장 / 대화 페이지의 대사 말풍선).
    /// 영어·한국어 글자를 누르거나 ▶ 버튼을 누르면 그 언어로 개별 듣기. 재생 중이면 강조한다.
    /// </summary>
    public class SentenceLine : MonoBehaviour
    {
        [Tooltip("재생 중일 때 강조 색으로 바뀌는 배경 (평소 색은 프리팹에 정한 색)")]
        [SerializeField] internal Image background;
        [Tooltip("대화: 화자 배지 글자 (A / B)")]
        [SerializeField] internal TextMeshProUGUI speaker;
        [SerializeField] internal Button enTap;
        [SerializeField] internal TextMeshProUGUI en;
        [SerializeField] internal Button koTap;
        [SerializeField] internal TextMeshProUGUI ko;
        [SerializeField] internal Button playEn;
        [SerializeField] internal Button playKo;
        [Tooltip("전체 듣기 중 이 줄이 보이도록 스크롤할 때 기준 영역")]
        [SerializeField] internal RectTransform followTarget;

        private Color baseColor;
        private Color enColor;
        private Color koColor;

        public RectTransform FollowTarget => followTarget;

        public void Bind(string speakerName, string enText, string koText, Action<Lang> play)
        {
            baseColor = background.color;
            enColor = en.color;
            koColor = ko.color;
            if (speaker != null) speaker.text = speakerName;
            en.text = enText;
            ko.text = koText;
            enTap.onClick.AddListener(() => play(Lang.En));
            koTap.onClick.AddListener(() => play(Lang.Ko));
            playEn.onClick.AddListener(() => play(Lang.En));
            playKo.onClick.AddListener(() => play(Lang.Ko));
        }

        /// <summary>playing: 이 줄을 재생 중인지, lang: 재생 중인 언어.</summary>
        public void SetPlaying(bool playing, Lang lang)
        {
            background.color = playing ? Theme.Highlight : baseColor;
            en.color = playing && lang == Lang.En ? Theme.PrimaryDark : enColor;
            ko.color = playing && lang == Lang.Ko ? Theme.PrimaryDark : koColor;
        }
    }
}
