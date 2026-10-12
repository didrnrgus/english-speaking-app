using System;
using UnityEngine;

namespace EnglishApp
{
    /// <summary>
    /// 기기 화면 정보 (Android).
    /// - Density: dp 하나가 몇 안드로이드 픽셀인지 (배너 광고 좌표·크기는 dp)
    /// - NavBarGuardPx: 앱 화면 아래쪽 중 내비게이션 바(3버튼 / 제스처 바)와 겹치는 높이 (Unity 픽셀)
    ///   바가 지금 숨겨져 있어도(전체 화면에서 밀어 올리면 잠깐 나타남) 그 자리를 계산에 넣는다
    ///   → SafeAreaFitter 가 이만큼 더 올려서 버튼·배너가 내비게이션 버튼 밑에 깔리지 않게 한다.
    /// 화면 크기가 바뀌거나 1초마다 다시 읽는다 (JNI 몇 번이라 가볍다).
    /// </summary>
    public static class DeviceScreen
    {
        private const float PollInterval = 1f;

        /// <summary>안드로이드 px / dp.</summary>
        public static float Density { get; private set; } = 2.625f;

        /// <summary>안드로이드 px / Unity Screen px (해상도 스케일링을 안 쓰면 1).</summary>
        public static float PxPerUnityPx { get; private set; } = 1f;

        /// <summary>내비게이션 바와 겹치는 아래쪽 높이 (Unity px).</summary>
        public static float NavBarGuardPx { get; private set; }

        /// <summary>앱 화면(content view)의 기기 화면 기준 위쪽 위치 (dp).</summary>
        public static float ContentTopDp { get; private set; }

        /// <summary>값이 바뀔 때마다 1씩 오른다.</summary>
        public static int Version { get; private set; }

        private static int lastFrame = -1;
        private static float nextPoll;
        private static Vector2Int lastScreen;

        public static float DpToUnityPx(float dp) => dp * Density / PxPerUnityPx;
        public static float UnityPxToDp(float px) => px * PxPerUnityPx / Density;

        /// <summary>매 프레임 불러도 된다 (프레임당 한 번, 1초 간격 또는 화면 크기 변경 때만 실제로 읽음).</summary>
        public static void Poll()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;

            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == lastScreen && Time.unscaledTime < nextPoll) return;
            lastScreen = screen;
            nextPoll = Time.unscaledTime + PollInterval;
            Read();
        }

        private static void Read()
        {
            float density = Density, pxScale = PxPerUnityPx, guard = NavBarGuardPx, contentTopDp = ContentTopDp;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                ReadAndroid(out density, out pxScale, out guard, out contentTopDp);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeviceScreen] 화면 정보 읽기 실패: " + e.Message);
            }
#else
            // 에디터·iOS: 폭 411dp 폰으로 가정 (Device Simulator 에서 실제 기기 비율과 비슷하게 보이도록)
            // iOS 는 내비게이션 바가 없고 홈 인디케이터는 Screen.safeArea 에 들어 있다
            density = Screen.width > 0 ? Screen.width / 411f : 2.625f;
            pxScale = 1f;
            guard = 0f;
            contentTopDp = 0f;
#endif
            if (density <= 0f) density = 1f;
            if (pxScale <= 0f) pxScale = 1f;

            if (Mathf.Approximately(density, Density) && Mathf.Approximately(pxScale, PxPerUnityPx) &&
                Mathf.Abs(guard - NavBarGuardPx) < 0.5f && Mathf.Abs(contentTopDp - ContentTopDp) < 0.5f)
                return;
            Density = density;
            PxPerUnityPx = pxScale;
            NavBarGuardPx = guard;
            ContentTopDp = contentTopDp;
            Version++;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private const int AndroidRIdContent = 0x01020002; // android.R.id.content

        private static void ReadAndroid(out float density, out float pxScale, out float guardUnityPx, out float contentTopDp)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resources = activity.Call<AndroidJavaObject>("getResources"))
            using (var metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics"))
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
            using (var content = decor.Call<AndroidJavaObject>("findViewById", AndroidRIdContent))
            {
                density = metrics.Get<float>("density");
                var view = content ?? decor;
                int viewHeight = view.Call<int>("getHeight");
                pxScale = viewHeight > 0 && Screen.height > 0 ? (float)viewHeight / Screen.height : 1f;

                int[] viewLoc = LocationOnScreen(view);
                int[] decorLoc = LocationOnScreen(decor);
                int decorHeight = decor.Call<int>("getHeight");
                int navInset = NavigationBarInset(decor);

                // 내비게이션 바 윗선(기기 화면 기준)보다 앱 화면이 아래로 내려가 있는 만큼이 겹치는 높이
                int navTop = decorLoc[1] + decorHeight - navInset;
                int viewBottom = viewLoc[1] + viewHeight;
                int overlap = Mathf.Max(0, viewBottom - navTop);
                guardUnityPx = navInset > 0 && decorHeight > 0 ? overlap / pxScale : 0f;
                contentTopDp = density > 0 ? viewLoc[1] / density : 0f;
            }
        }

        /// <summary>내비게이션 바 높이 (안드로이드 px). 숨겨진 상태여도 나타났을 때의 높이.</summary>
        private static int NavigationBarInset(AndroidJavaObject decor)
        {
            int sdk;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                sdk = version.GetStatic<int>("SDK_INT");

            using (var insets = decor.Call<AndroidJavaObject>("getRootWindowInsets"))
            {
                if (insets == null) return 0;
                if (sdk >= 30)
                {
                    int type;
                    using (var types = new AndroidJavaClass("android.view.WindowInsets$Type"))
                        type = types.CallStatic<int>("navigationBars");
                    using (var value = insets.Call<AndroidJavaObject>("getInsetsIgnoringVisibility", type))
                        return value != null ? value.Get<int>("bottom") : 0;
                }
                return insets.Call<int>("getStableInsetBottom");
            }
        }

        /// <summary>View.getLocationOnScreen(int[]) — 배열을 채워 돌려주는 메서드라 JNI 로 직접 부른다.</summary>
        private static int[] LocationOnScreen(AndroidJavaObject view)
        {
            IntPtr array = AndroidJNI.NewIntArray(2);
            try
            {
                IntPtr cls = AndroidJNI.GetObjectClass(view.GetRawObject());
                IntPtr method = AndroidJNI.GetMethodID(cls, "getLocationOnScreen", "([I)V");
                AndroidJNI.DeleteLocalRef(cls);
                AndroidJNI.CallVoidMethod(view.GetRawObject(), method, new[] { new jvalue { l = array } });
                IntPtr error = AndroidJNI.ExceptionOccurred();
                if (error != IntPtr.Zero)
                {
                    AndroidJNI.ExceptionClear();
                    AndroidJNI.DeleteLocalRef(error);
                    return new int[2];
                }
                var result = AndroidJNI.FromIntArray(array);
                return result != null && result.Length >= 2 ? result : new int[2];
            }
            finally
            {
                AndroidJNI.DeleteLocalRef(array);
            }
        }
#endif
    }
}
