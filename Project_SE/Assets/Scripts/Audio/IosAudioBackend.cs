#if UNITY_IOS
using System.Runtime.InteropServices;
using UnityEngine;

namespace EnglishApp.Audio
{
    /// <summary>
    /// iOS 네이티브 플러그인(Assets/Plugins/iOS/EnglishAudio/EsaAudio.mm) 호출.
    /// 실제 재생은 AVAudioPlayer + 백그라운드 오디오 모드가 하므로 화면이 꺼져도 계속된다.
    /// </summary>
    public class IosAudioBackend : IAudioBackend
    {
        [DllImport("__Internal")] private static extern void EsaAudio_Play(string json);
        [DllImport("__Internal")] private static extern void EsaAudio_Pause();
        [DllImport("__Internal")] private static extern void EsaAudio_Resume();
        [DllImport("__Internal")] private static extern void EsaAudio_Stop();
        [DllImport("__Internal")] private static extern void EsaAudio_SeekStep(int step);
        [DllImport("__Internal")] private static extern void EsaAudio_SetRepeat([MarshalAs(UnmanagedType.I1)] bool repeat);
        [DllImport("__Internal")] private static extern void EsaAudio_UpdateNode(int index, float volume, float speed, float gap);
        [DllImport("__Internal")] private static extern string EsaAudio_GetState();

        public void Play(PlaybackRequest request) => EsaAudio_Play(JsonUtility.ToJson(request));
        public void Pause() => EsaAudio_Pause();
        public void Resume() => EsaAudio_Resume();
        public void Stop() => EsaAudio_Stop();
        public void SeekStep(int step) => EsaAudio_SeekStep(step);
        public void SetRepeat(bool repeat) => EsaAudio_SetRepeat(repeat);
        public void UpdateNode(int index, PlaybackNode node) => EsaAudio_UpdateNode(index, node.volume, node.speed, node.gap);

        public PlaybackState GetState()
        {
            string json = EsaAudio_GetState();
            if (string.IsNullOrEmpty(json)) return PlaybackState.Idle;
            return JsonUtility.FromJson<PlaybackState>(json);
        }

        public void Tick(float deltaTime)
        {
        }
    }
}
#endif
