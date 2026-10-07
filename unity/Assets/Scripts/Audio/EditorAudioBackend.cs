using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace EnglishApp.Audio
{
    /// <summary>
    /// 에디터·PC 확인용 대체 재생 (AudioSource). 네이티브 SequenceEngine 과 같은 규칙으로 동작한다.
    /// 빠르기는 pitch 로 바꾸므로 음높이도 같이 바뀐다 — 실제 품질은 기기에서 확인 (docs/PLAN.md 6-4).
    /// </summary>
    public class EditorAudioBackend : IAudioBackend
    {
        private enum Phase { Idle, Loading, Clip, Gap }

        private readonly MonoBehaviour host;
        private readonly AudioSource source;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        private PlaybackRequest request;
        private Phase phase = Phase.Idle;
        private int step = -1;
        private bool paused;
        private bool active;
        private bool ended;
        private float gapElapsed;
        private int loadToken;

        public EditorAudioBackend(MonoBehaviour host)
        {
            this.host = host;
            source = host.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
        }

        public void Play(PlaybackRequest req)
        {
            request = req;
            active = true;
            ended = false;
            paused = false;
            GoTo(Mathf.Clamp(req.startStep, 0, req.steps.Count - 1));
        }

        public void Pause()
        {
            if (!active) return;
            paused = true;
            source.Pause();
        }

        public void Resume()
        {
            if (request == null || request.steps.Count == 0) return;
            if (!active || ended)
            {
                active = true;
                ended = false;
                paused = false;
                GoTo(0);
                return;
            }
            paused = false;
            if (phase == Phase.Clip) source.UnPause();
        }

        public void Stop()
        {
            active = false;
            ended = false;
            paused = false;
            phase = Phase.Idle;
            step = -1;
            loadToken++;
            source.Stop();
        }

        public void SeekStep(int index)
        {
            if (request == null || request.steps.Count == 0) return;
            active = true;
            ended = false;
            paused = false;
            GoTo(Mathf.Clamp(index, 0, request.steps.Count - 1));
        }

        public void SetRepeat(bool repeat)
        {
            if (request != null) request.repeat = repeat;
        }

        public void UpdateNode(int index, PlaybackNode node)
        {
            if (request == null || index < 0 || index >= request.nodes.Count) return;
            request.nodes[index] = node;
            if (step >= 0 && request.steps[step].node == index && phase == Phase.Clip)
            {
                source.volume = node.volume;
                source.pitch = node.speed;
            }
            // 텀은 Tick 에서 매번 현재 값과 비교하므로 바로 반영된다
        }

        public PlaybackState GetState()
        {
            return new PlaybackState
            {
                id = request != null ? request.id : 0,
                active = active,
                playing = active && !paused,
                ended = ended,
                step = step,
                gap = phase == Phase.Gap,
            };
        }

        public void Tick(float dt)
        {
            if (!active || paused || request == null) return;
            switch (phase)
            {
                case Phase.Clip:
                    if (!source.isPlaying) StartGap();
                    break;
                case Phase.Gap:
                    gapElapsed += dt;
                    if (gapElapsed >= CurrentNode.gap) Next();
                    break;
            }
        }

        private PlaybackNode CurrentNode => request.nodes[request.steps[step].node];

        private void GoTo(int index)
        {
            step = index;
            phase = Phase.Loading;
            source.Stop();
            int token = ++loadToken;
            host.StartCoroutine(LoadAndPlay(request.steps[index], token));
        }

        private void StartGap()
        {
            phase = Phase.Gap;
            gapElapsed = 0f;
        }

        private void Next()
        {
            int next = step + 1;
            if (next >= request.steps.Count)
            {
                if (!request.repeat)
                {
                    active = false;
                    ended = true;
                    phase = Phase.Idle;
                    return;
                }
                next = 0;
            }
            GoTo(next);
        }

        private IEnumerator LoadAndPlay(PlaybackStep s, int token)
        {
            AudioClip clip;
            if (!clips.TryGetValue(s.path, out clip) || clip == null)
            {
                using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(s.path).AbsoluteUri, AudioType.MPEG))
                {
                    yield return req.SendWebRequest();
                    if (token != loadToken) yield break;
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        clip = DownloadHandlerAudioClip.GetContent(req);
                        clips[s.path] = clip;
                    }
                    else
                    {
                        Debug.LogWarning("[EditorAudio] 로드 실패: " + s.path + " " + req.error);
                    }
                }
            }
            if (token != loadToken) yield break;

            if (clip == null)
            {
                StartGap();
                yield break;
            }

            var node = request.nodes[s.node];
            source.clip = clip;
            source.volume = node.volume;
            source.pitch = node.speed;
            source.Play();
            if (paused) source.Pause();
            phase = Phase.Clip;
        }
    }
}
