using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>화면 아래쪽 잠깐 뜨는 안내 문구 (AppCanvas 프리팹 안).</summary>
    public class Toast : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal TextMeshProUGUI text;

        private Coroutine routine;

        private void Awake()
        {
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        public void Show(string message, float seconds = 2.5f)
        {
            text.text = message;
            transform.SetAsLastSibling();
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run(seconds));
        }

        private IEnumerator Run(float seconds)
        {
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.15f)
            {
                group.alpha = t;
                yield return null;
            }
            group.alpha = 1;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime / 0.3f)
            {
                group.alpha = t;
                yield return null;
            }
            group.alpha = 0;
            routine = null;
        }
    }
}
