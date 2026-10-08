using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 앱 캔버스 프리팹(AppCanvas)의 루트.
    ///
    ///   AppCanvas
    ///   ├─ BackgroundCanvas          (Screen Space - Camera: 카메라가 그린다 → 글래스가 이걸 비춰 보인다)
    ///   │   └─ Background            (배경 그림)
    ///   └─ Canvas                    (Screen Space - Overlay: 모든 UI)
    ///       ├─ SafeArea              (Screen.safeArea 안쪽, Android 내비게이션 바 위 — 모든 UI 는 여기 안에)
    ///       │   ├─ AdSlot            (맨 아래 배너 광고 전용 띠 — BannerAdSlot)
    ///       │   └─ Content           (광고 띠 위쪽)
    ///       │       ├─ Screens       (메인 / 레벨 / 유닛)
    ///       │       └─ MiniPlayer    (다른 화면에서 전체 듣기 중일 때)
    ///       ├─ Overlay               (설정 팝업: 어둡게 덮는 배경은 전체, 패널은 SafeArea 안쪽. 열려 있는 동안 배너는 숨김)
    ///       └─ ToastLayer            (SafeArea 안쪽, 광고 띠 위)
    /// </summary>
    public class AppCanvas : MonoBehaviour
    {
        [SerializeField] internal Canvas backgroundCanvas;
        [SerializeField] internal Canvas canvas;
        [SerializeField] internal CanvasScaler scaler;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal RectTransform screens;
        [SerializeField] internal RectTransform overlay;
        [SerializeField] internal RectTransform toastArea;
        [SerializeField] internal BannerAdSlot ads;
        [SerializeField] internal MiniPlayerBar miniPlayer;
        [SerializeField] internal Toast toast;

        public Canvas BackgroundCanvas => backgroundCanvas;
        public CanvasScaler Scaler => scaler;
        public RectTransform Content => content;
        public RectTransform Screens => screens;
        public RectTransform Overlay => overlay;
        public RectTransform ToastArea => toastArea;
        public BannerAdSlot Ads => ads;
        public Toast Toast => toast;
    }
}
