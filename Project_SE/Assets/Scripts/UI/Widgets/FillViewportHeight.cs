using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>스크롤 내용의 최소 높이를 뷰포트 높이로 → 내용이 짧으면 세로 가운데 정렬된다.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class FillViewportHeight : MonoBehaviour
    {
        public RectTransform viewport;
        private LayoutElement layout;

        private void Awake()
        {
            layout = GetComponent<LayoutElement>();
            if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
        }

        private void LateUpdate()
        {
            if (viewport == null) return;
            float h = viewport.rect.height;
            if (!Mathf.Approximately(layout.minHeight, h)) layout.minHeight = h;
        }
    }
}
