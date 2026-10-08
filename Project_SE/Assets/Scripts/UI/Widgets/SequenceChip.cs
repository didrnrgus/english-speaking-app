using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>유닛 화면 아래 '듣기 순서' 칩 하나 (SequenceChip 프리팹): 영 / 한. 재생 중인 칩은 언어 색으로 채운다.</summary>
    public class SequenceChip : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal TextMeshProUGUI text;

        private Color idleColor;
        private Lang lang;

        public Button Button => button;

        public void Bind(Lang value)
        {
            idleColor = button.targetGraphic.color;
            lang = value;
            text.text = Theme.LangShort(lang);
            SetPlaying(false);
        }

        public void SetPlaying(bool playing)
        {
            var c = Theme.LangColor(lang);
            button.targetGraphic.color = playing ? c : idleColor;
            text.color = playing ? Theme.TextOnColor : c;
        }
    }
}
