using UnityEngine;

namespace EnglishApp.Audio
{
    /// <summary>
    /// 출력 증폭 (모든 소리 x2). 설정 화면의 음량 100% 가 실제로는 원본의 200% 로 나온다.
    /// 같은 값을 네이티브에서도 쓴다: Android GainAudioProcessor.GAIN, iOS EsaAudio.mm kOutputGain.
    /// </summary>
    public static class AudioOutput
    {
        public const float Gain = 2f;

        /// <summary>
        /// 1 을 넘는 부분을 부드럽게 눌러 깨짐(클리핑)을 줄인다. |x| ≤ Knee 는 그대로.
        /// Android GainAudioProcessor.softClip 과 같은 식.
        /// </summary>
        public const float Knee = 0.9f;

        public static float SoftClip(float x)
        {
            float a = x < 0f ? -x : x;
            if (a <= Knee) return x;
            float y = Knee + (1f - Knee) * (float)System.Math.Tanh((a - Knee) / (1f - Knee));
            return x < 0f ? -y : y;
        }
    }

    /// <summary>에디터·PC 백엔드용: 같은 GameObject 의 AudioSource 출력을 AudioOutput.Gain 배로 키운다.</summary>
    public class AudioGainFilter : MonoBehaviour
    {
        private void OnAudioFilterRead(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i++) data[i] = AudioOutput.SoftClip(data[i] * AudioOutput.Gain);
        }
    }
}
