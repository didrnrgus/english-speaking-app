using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>RectTransform 을 Screen.safeArea 에 맞춘다 (노치·펀치홀·제스처 바 영역을 넘지 않도록).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var rt = (RectTransform)transform;
            Vector2 min = lastSafeArea.position;
            Vector2 max = lastSafeArea.position + lastSafeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
