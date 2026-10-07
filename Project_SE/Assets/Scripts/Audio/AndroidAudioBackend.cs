#if UNITY_ANDROID
using UnityEngine;

namespace EnglishApp.Audio
{
    /// <summary>
    /// Android 네이티브 플러그인(Assets/Plugins/Android/EnglishAudio.androidlib) 호출.
    /// 실제 재생은 포그라운드 서비스가 하므로 화면이 꺼져도 계속된다.
    /// </summary>
    public class AndroidAudioBackend : IAudioBackend
    {
        private const string BridgeClass = "com.didrnrgus.englishspeaking.audio.AudioBridge";
        private readonly AndroidJavaClass bridge;

        public AndroidAudioBackend()
        {
            bridge = new AndroidJavaClass(BridgeClass);
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                bridge.CallStatic("init", activity);
            }
        }

        public void Play(PlaybackRequest request) => bridge.CallStatic("play", JsonUtility.ToJson(request));
        public void Pause() => bridge.CallStatic("pause");
        public void Resume() => bridge.CallStatic("resume");
        public void Stop() => bridge.CallStatic("stop");
        public void SeekStep(int step) => bridge.CallStatic("seekStep", step);
        public void SetRepeat(bool repeat) => bridge.CallStatic("setRepeat", repeat);

        public void UpdateNode(int index, PlaybackNode node)
        {
            bridge.CallStatic("updateNode", index, node.volume, node.speed, node.gap);
        }

        public PlaybackState GetState()
        {
            string json = bridge.CallStatic<string>("getState");
            if (string.IsNullOrEmpty(json)) return PlaybackState.Idle;
            return JsonUtility.FromJson<PlaybackState>(json);
        }

        public void Tick(float deltaTime)
        {
        }

        /// <summary>Android 13+ 알림 권한 (잠금화면/알림창 재생 컨트롤 표시용).</summary>
        public static void RequestNotificationPermission()
        {
            const string permission = "android.permission.POST_NOTIFICATIONS";
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (version.GetStatic<int>("SDK_INT") < 33) return;
            }
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
                UnityEngine.Android.Permission.RequestUserPermission(permission);
        }
    }
}
#endif
