using UnityEngine;
using UnityEngine.EventSystems;

namespace EnglishApp.UI
{
    /// <summary>누르는 동안 살짝 작아지는 버튼 피드백.</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressedScale = 0.95f;
        private float target = 1f;

        public void OnPointerDown(PointerEventData eventData) => target = pressedScale;
        public void OnPointerUp(PointerEventData eventData) => target = 1f;
        public void OnPointerExit(PointerEventData eventData) => target = 1f;

        private void OnDisable()
        {
            target = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float s = transform.localScale.x;
            if (Mathf.Abs(s - target) < 0.001f) return;
            s = Mathf.MoveTowards(s, target, Time.unscaledDeltaTime * 1.5f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
