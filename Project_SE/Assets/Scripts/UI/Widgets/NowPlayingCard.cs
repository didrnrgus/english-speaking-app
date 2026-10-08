using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>설정 팝업 맨 위 '지금 재생 중' 카드 (NowPlayingCard 프리팹): 재생 정보 + 재생/일시정지.</summary>
    public class NowPlayingCard : MonoBehaviour
    {
        [SerializeField] internal TextMeshProUGUI text;
        [SerializeField] internal Button button;
        [SerializeField] internal Image icon;
        [SerializeField] internal Sprite playSprite;
        [SerializeField] internal Sprite pauseSprite;

        public void Init(Action onToggle)
        {
            button.onClick.AddListener(() => onToggle());
        }

        /// <summary>info 가 null 이면 숨긴다.</summary>
        public void Show(string info, bool playing)
        {
            gameObject.SetActive(info != null);
            if (info == null) return;
            text.text = info;
            icon.sprite = playing ? pauseSprite : playSprite;
        }
    }
}
