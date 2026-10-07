using EnglishApp.Audio;
using EnglishApp.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EnglishApp
{
    /// <summary>
    /// 앱 시작점. 씬에 아무것도 없어도 실행 시 자동으로 만들어진다 (RuntimeInitializeOnLoadMethod).
    /// 캔버스·이벤트 시스템·서비스(콘텐츠, 캐시, 설정, 재생)를 만들고 메인 화면을 띄운다.
    ///
    /// 계층:
    ///   Canvas
    ///   ├─ Background            (화면 전체)
    ///   ├─ SafeArea              (Screen.safeArea 안쪽, Android 내비게이션 바 위 — 모든 UI 는 여기 안에)
    ///   │   ├─ AdSlot            (맨 아래 배너 광고 전용 띠 — BannerAdSlot)
    ///   │   └─ Content           (광고 띠 위쪽)
    ///   │       ├─ Screens       (메인 / 레벨 / 유닛)
    ///   │       └─ MiniPlayer    (다른 화면에서 전체 듣기 중일 때)
    ///   ├─ Overlay               (설정 팝업: 어둡게 덮는 배경은 전체, 패널은 SafeArea 안쪽. 열려 있는 동안 배너는 숨김)
    ///   └─ ToastLayer            (SafeArea 안쪽, 광고 띠 위)
    /// </summary>
    public class AppRoot : MonoBehaviour
    {
        public static AppRoot I { get; private set; }

        public AppSettings Settings { get; private set; }
        public ContentService Content { get; private set; }
        public AudioCache Cache { get; private set; }
        public AudioPlayer Player { get; private set; }
        public Navigator Nav { get; private set; }
        public BannerAdSlot Ads { get; private set; }

        private CanvasScaler scaler;
        private RectTransform overlayLayer;
        private Toast toast;
        private SettingsPopup settingsPopup;
        private float saveTimer;

        private const string SettingsAdBlocker = "settings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (I != null) return;
            new GameObject("AppRoot").AddComponent<AppRoot>();
        }

        private void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);

            // 세로 모드만
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Application.targetFrameRate = 60;
            Application.runInBackground = true; // 에디터/PC. Android 화면 꺼짐 재생은 네이티브 서비스가 담당

            Settings = AppSettings.Load();
            Content = new ContentService();
            Cache = new AudioCache(this);
            Player = gameObject.AddComponent<AudioPlayer>();
            Player.Init(Settings, Cache);
            Player.Failed += ShowToast;

            EnsureCamera();
            EnsureEventSystem();
            var screensLayer = BuildCanvas();

            Nav = new Navigator(screensLayer, this);
            Nav.Push<MainScreen>();
        }

        private void Start()
        {
            StartCoroutine(Content.LoadIndex());
        }

        // ---- 화면 이동 --------------------------------------------------------------------

        public void OpenLevel(LevelInfo level)
        {
            Nav.Push<LevelScreen>(s => s.Level = level);
        }

        public void OpenUnit(UnitInfo unit)
        {
            if (Nav.PopTo<UnitScreen>(s => s.UnitId == unit.unit)) return;
            Nav.Push<UnitScreen>(s => s.Info = unit);
        }

        /// <summary>설정 팝업. tab 0 = 개별 듣기, 1 = 전체 듣기.</summary>
        public void OpenSettings(int tab)
        {
            if (settingsPopup != null)
            {
                settingsPopup.SelectTab(tab);
                return;
            }
            settingsPopup = SettingsPopup.Open(overlayLayer, tab);
            Ads.Block(SettingsAdBlocker);
        }

        public bool IsSettingsOpen => settingsPopup != null;

        public void ShowToast(string message)
        {
            toast.Show(message);
        }

        // ---- 만들기 -----------------------------------------------------------------------

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Theme.Bg;
                cam.cullingMask = 0;
                DontDestroyOnLoad(cam.gameObject);
            }
            // 에디터/PC 대체 재생(AudioSource)용
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(go);
        }

        private RectTransform BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform)) { layer = 5 };
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            UpdateScalerMatch();
            go.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)go.transform;

            UIKit.Image(root, "Background", Theme.Bg).rectTransform.Fill();

            var safe = UIKit.Rect("SafeArea", root).Fill();
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Ads = BannerAdSlot.Create(safe);
            var content = UIKit.Rect("Content", safe).Fill();
            Ads.AddInsetTarget(content);
            var screens = UIKit.Rect("Screens", content).Fill();
            MiniPlayerBar.Create(content);

            overlayLayer = UIKit.Rect("Overlay", root).Fill();

            var toastLayer = UIKit.Rect("ToastLayer", root).Fill();
            toastLayer.gameObject.AddComponent<SafeAreaFitter>();
            var toastArea = UIKit.Rect("ToastArea", toastLayer).Fill();
            Ads.AddInsetTarget(toastArea);
            toast = Toast.Create(toastArea);
            return screens;
        }

        /// <summary>폰(길쭉한 화면)은 가로 1080 기준, 태블릿처럼 덜 길쭉하면 세로 1920 기준.</summary>
        private void UpdateScalerMatch()
        {
            if (scaler == null || Screen.width <= 0) return;
            float aspect = (float)Screen.height / Screen.width;
            scaler.matchWidthOrHeight = aspect < 1.7f ? 1f : 0f;
        }

        // ---- 루프 -------------------------------------------------------------------------

        private void Update()
        {
            UpdateScalerMatch();

            if (BackPressed()) HandleBack();

            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer > 2f)
            {
                saveTimer = 0f;
                Settings.SaveIfDirty();
            }
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void HandleBack()
        {
            if (settingsPopup != null)
            {
                settingsPopup.Close();
                return;
            }
            if (Nav.Top != null && Nav.Top.OnBack()) return;
            if (Nav.Count > 1)
            {
                Nav.Pop();
                return;
            }
            MoveTaskToBack();
        }

        /// <summary>첫 화면에서 뒤로: 앱을 끄지 않고 뒤로 보낸다 (재생 유지).</summary>
        private static void MoveTaskToBack()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                activity.Call<bool>("moveTaskToBack", true);
            }
#endif
        }

        internal void OnSettingsClosed(SettingsPopup popup)
        {
            if (settingsPopup == popup) settingsPopup = null;
            if (settingsPopup == null) Ads.Unblock(SettingsAdBlocker);
            Settings.SaveIfDirty();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) Settings?.SaveIfDirty();
        }

        private void OnApplicationQuit()
        {
            Settings?.SaveIfDirty();
        }
    }
}
