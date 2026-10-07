using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>
    /// RectTransform 을 Screen.safeArea 에 맞춘다 (노치·펀치홀·제스처 바 영역을 넘지 않도록).
    /// Android 는 내비게이션 바(3버튼/제스처) 자리도 피한다 — 바가 숨겨진 전체 화면이어도 (DeviceScreen.NavBarGuardPx).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private int lastScreenVersion = -1;

        private void OnEnable() => Apply();

        private void Update()
        {
            DeviceScreen.Poll();
            if (Screen.safeArea != lastSafeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height ||
                lastScreenVersion != DeviceScreen.Version)
                Apply();
        }

        private void Apply()
        {
            DeviceScreen.Poll();
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            lastScreenVersion = DeviceScreen.Version;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var area = lastSafeArea;
            float guard = DeviceScreen.NavBarGuardPx;
            if (guard > area.yMin && guard < area.yMax) area.yMin = guard; // yMax 는 그대로, 아래만 올린다

            var rt = (RectTransform)transform;
            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
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
