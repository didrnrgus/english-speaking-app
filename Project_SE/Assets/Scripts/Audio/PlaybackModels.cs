using System;
using System.Collections.Generic;

namespace EnglishApp.Audio
{
    // 네이티브 플러그인(Android SequenceEngine.load / iOS EsaAudio.mm load)으로 보내는 JSON 과 같은 모양.

    [Serializable]
    public class PlaybackNode
    {
        /// <summary>설정 화면 값 (0~1). 실제 출력은 백엔드가 AudioOutput.Gain(x2) 를 곱한다.</summary>
        public float volume = 1f;
        public float speed = 1f;
        public float gap;
    }

    [Serializable]
    public class PlaybackStep
    {
        public string path; // 로컬 mp3 절대 경로
        public int node;
        public int page;
        public string title;
        public string subtitle;
    }

    [Serializable]
    public class PlaybackRequest
    {
        public int id;
        public string title;
        public bool repeat;
        public int startStep;
        public List<PlaybackNode> nodes = new List<PlaybackNode>();
        public List<PlaybackStep> steps = new List<PlaybackStep>();
    }

    /// <summary>네이티브 getState() JSON.</summary>
    [Serializable]
    public struct PlaybackState
    {
        public int id;
        public bool active;  // 시퀀스 진행 중 (일시정지 포함)
        public bool playing; // 소리가 나는 중 (텀 포함)
        public bool ended;   // 끝까지 재생함
        public int step;
        public bool gap;     // 텀(무음) 구간

        public static PlaybackState Idle => new PlaybackState { step = -1 };

        public bool SameAs(PlaybackState o)
        {
            return id == o.id && active == o.active && playing == o.playing && ended == o.ended && step == o.step && gap == o.gap;
        }
    }

    /// <summary>재생 백엔드: Android 는 네이티브 서비스, iOS 는 네이티브 AVAudioEngine, 에디터/PC 는 AudioSource.</summary>
    public interface IAudioBackend
    {
        void Play(PlaybackRequest request);
        void Pause();
        void Resume();
        void Stop();
        void SeekStep(int step);
        void SetRepeat(bool repeat);
        void UpdateNode(int index, PlaybackNode node);
        PlaybackState GetState();
        void Tick(float deltaTime);
    }
}
