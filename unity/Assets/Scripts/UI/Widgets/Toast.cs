using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>화면 아래쪽 잠깐 뜨는 안내 문구.</summary>
    public class Toast : MonoBehaviour
    {
        private CanvasGroup group;
        private TextMeshProUGUI text;
        private Coroutine routine;

        public static Toast Create(Transform parent)
        {
            var rt = UIKit.Rect("Toast", parent);
            rt.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 260), new Vector2(900, 120));
            var t = rt.gameObject.AddComponent<Toast>();
            t.group = rt.gameObject.AddComponent<CanvasGroup>();
            t.group.alpha = 0;
            t.group.blocksRaycasts = false;
            t.group.interactable = false;
            var bg = UIKit.Rounded(rt, "Bg", new Color(0.18f, 0.16f, 0.24f, 0.92f), 60);
            bg.rectTransform.Fill();
            t.text = UIKit.Text(rt, "Text", "", 36, Color.white);
            t.text.rectTransform.Fill(40, 10, 40, 10);
            t.text.AutoSize(24, 36);
            return t;
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
