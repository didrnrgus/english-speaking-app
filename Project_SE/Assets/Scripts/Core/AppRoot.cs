using EnglishApp.Audio;
using EnglishApp.UI;
using LeTai.Asset.TranslucentImage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EnglishApp
{
    /// <summary>
    /// 앱 시작점. 씬에 아무것도 없어도 실행 시 자동으로 만들어진다 (RuntimeInitializeOnLoadMethod).
    /// 카메라·이벤트 시스템·서비스(콘텐츠, 캐시, 설정, 재생)를 만들고, 캔버스 프리팹(AppCanvas)을 띄운 뒤 메인 화면을 연다.
    /// 캔버스 계층은 AppCanvas.cs, 프리팹 목록은 UIPrefabs.cs 참고.
    ///
    /// 글래스 UI: 카메라는 배경 캔버스만 그리고, TranslucentImageSource 가 그 화면을 흐리게 만든다.
    /// UI 의 글래스(TranslucentImage + Paraform)는 그 흐린 화면을 비춰 보인다.
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
        public MiniPlayerBar MiniPlayer { get; private set; }

        private UIPrefabs prefabs;
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
            Player.Init(Settings, Cache, Content);
            Player.Failed += ShowToast;

            prefabs = UIPrefabs.I;
            if (prefabs == null)
            {
                enabled = false; // 프리팹을 아직 안 만들었음 (UIPrefabs.I 가 안내 로그를 남긴다)
                return;
            }
            Theme.Init(prefabs.lightPalette, prefabs.darkPalette);
            var cam = EnsureCamera();
            Theme.Changed += () => cam.backgroundColor = Theme.Bg;
            EnsureEventSystem();
            var screensLayer = SpawnCanvas(cam);

            Nav = new Navigator(screensLayer, prefabs, this);
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

        /// <summary>레벨 전체 듣기가 다음 유닛으로 넘어갔을 때: 보고 있던 유닛 화면을 그 유닛 화면으로 바꾼다.</summary>
        public void FollowUnit(string unitId)
        {
            var info = Content.FindUnit(unitId);
            if (info == null) return;
            if (Nav.PopTo<UnitScreen>(s => s.UnitId == unitId)) return;
            if (Nav.Top is UnitScreen) Nav.Replace<UnitScreen>(s => s.Info = info);
            else Nav.Push<UnitScreen>(s => s.Info = info);
        }

        /// <summary>
        /// 레벨 전체 듣기 버튼 (메인 화면 레벨 카드의 ▶, 레벨 화면 위 버튼 공통).
        /// 이 레벨을 듣는 중이면 일시정지 / 이어 듣기, 아니면 마지막으로 듣던 유닛부터 시작.
        /// </summary>
        public void ToggleLevelPlayback(LevelInfo level)
        {
            if (Player.IsLevelFull(level.level))
            {
                if (Player.IsPreparing) Player.Stop();
                else if (Player.IsPlaying) Player.Pause();
                else if (Player.State.active) Player.Resume();
                else Player.PlayLevel(level, Player.ResumeUnitIndex(level));
                return;
            }
            Player.PlayLevel(level, Player.ResumeUnitIndex(level));
        }

        /// <summary>설정 팝업. tab 0 = 개별 듣기, 1 = 전체 듣기.</summary>
        public void OpenSettings(int tab)
        {
            if (settingsPopup != null)
            {
                settingsPopup.SelectTab(tab);
                return;
            }
            settingsPopup = SettingsPopup.Open(prefabs.settingsPopup, overlayLayer, tab);
            Ads.Block(SettingsAdBlocker);
        }

        public bool IsSettingsOpen => settingsPopup != null;

        public void ShowToast(string message)
        {
            toast.Show(message);
        }

        // ---- 만들기 -----------------------------------------------------------------------

        /// <summary>카메라는 배경 캔버스(UI 레이어)만 그린다. 글래스가 비출 흐린 화면도 여기서 만든다.</summary>
        private Camera EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                DontDestroyOnLoad(cam.gameObject);
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Bg;
            cam.cullingMask = 1 << UIKit.UILayer;
            // 에디터/PC 대체 재생(AudioSource)용
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            var source = cam.GetComponent<TranslucentImageSource>();
            if (source == null) source = cam.gameObject.AddComponent<TranslucentImageSource>();
            if (prefabs != null) source.BlurConfig = prefabs.blurConfig;
            source.Downsample = 1;
            // 배경은 움직이지 않으므로 자주 다시 흐릴 필요가 없다. 대신 화면 전체를 흐려 둔다 (움직이는 UI 뒤에 빈틈 X)
            source.SkipCulling = true;
            source.MaxUpdateRate = 10f;
            return cam;
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

        private RectTransform SpawnCanvas(Camera cam)
        {
            var app = UIKit.Spawn(prefabs.appCanvas, null);
            DontDestroyOnLoad(app.gameObject);
            app.BackgroundCanvas.worldCamera = cam;
            scaler = app.Scaler;
            UpdateScalerMatch();

            MiniPlayer = app.MiniPlayer;
            Ads = app.Ads;
            Ads.Init();
            Ads.AddInsetTarget(app.Content);
            Ads.AddInsetTarget(app.ToastArea);
            overlayLayer = app.Overlay;
            toast = app.Toast;
            return app.Screens;
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
