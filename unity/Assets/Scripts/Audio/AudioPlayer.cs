using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EnglishApp.Audio
{
    public enum PlaybackMode
    {
        None,
        Single, // 개별 듣기: 문장 하나, 한 언어
        Full,   // 전체 듣기: 유닛 전체를 듣기 시퀀스대로
    }

    /// <summary>시퀀스의 step 하나가 화면의 어디에 해당하는지.</summary>
    public class StepInfo
    {
        public int page; // 예문 번호 (0부터)
        public int line; // 예문 안의 줄 (대화는 턴)
        public int node; // 듣기 시퀀스 노드 번호 (개별 듣기는 0)
        public Lang lang;
    }

    /// <summary>
    /// 재생 제어. 화면은 이 클래스만 사용한다.
    /// - 설정(음량·빠르기·텀)이 바뀌면 재생 중인 소리에 바로 반영한다.
    /// - 듣기 시퀀스 구조(순서·추가·삭제·언어)가 바뀌면 현재 예문부터 다시 만든다.
    /// - 백엔드 상태를 0.1초마다 읽는다 (화면이 꺼졌다 켜지면 바로 동기화).
    /// </summary>
    public class AudioPlayer : MonoBehaviour
    {
        private const float PollInterval = 0.1f;

        private AppSettings settings;
        private AudioCache cache;
        private IAudioBackend backend;

        private readonly List<StepInfo> steps = new List<StepInfo>();
        private readonly List<int> pageFirstStep = new List<int>();
        private int requestId;
        private float pollTimer;
        private Coroutine prepareRoutine;
        private bool permissionAsked;
        private UnitData lastSingleUnit;
        private int lastSinglePage;
        private int lastSingleLine;
        private Lang lastSingleLang;

        public PlaybackMode Mode { get; private set; }
        public PlaybackState State { get; private set; } = PlaybackState.Idle;
        public UnitData Unit { get; private set; }

        public bool IsPreparing { get; private set; }
        public int PrepareDone { get; private set; }
        public int PrepareTotal { get; private set; }

        /// <summary>상태가 바뀔 때마다 (step 이동, 재생/일시정지, 준비 진행률 등).</summary>
        public event Action Changed;
        /// <summary>음성을 받지 못했을 때 (오프라인 등).</summary>
        public event Action<string> Failed;

        public void Init(AppSettings appSettings, AudioCache audioCache)
        {
            settings = appSettings;
            cache = audioCache;
#if UNITY_ANDROID && !UNITY_EDITOR
            backend = new AndroidAudioBackend();
#else
            backend = new EditorAudioBackend(this);
#endif
            settings.SequenceNodeChanged += OnSequenceNodeChanged;
            settings.SequenceStructureChanged += OnSequenceStructureChanged;
            settings.RepeatChanged += OnRepeatChanged;
            settings.SingleChanged += OnSingleChanged;
        }

        // ---- 조회 -------------------------------------------------------------------------

        public bool IsFull(string unitId) => Mode == PlaybackMode.Full && Unit != null && Unit.unit == unitId;

        /// <summary>진행 중 (준비 중이거나, 재생/일시정지 상태). 끝났거나 멈췄으면 false.</summary>
        public bool IsActive => Mode != PlaybackMode.None && (IsPreparing || State.active);

        /// <summary>소리가 나는 중이거나 곧 날 예정 (재생 버튼을 '일시정지' 로 보여줄 때).</summary>
        public bool IsPlaying => Mode != PlaybackMode.None && (IsPreparing || (State.active && State.playing));

        public StepInfo CurrentStep
        {
            get
            {
                if (Mode == PlaybackMode.None || IsPreparing) return null;
                if (State.step < 0 || State.step >= steps.Count) return null;
                return steps[State.step];
            }
        }

        // ---- 명령 -------------------------------------------------------------------------

        /// <summary>전체 듣기: 유닛 전체를 startPage 예문부터 시퀀스대로.</summary>
        public void PlayFull(UnitData unit, int startPage, bool startPaused = false)
        {
            AskNotificationPermissionOnce();
            BeginPrepare(PlaybackMode.Full, unit);
            prepareRoutine = StartCoroutine(PrepareFull(unit, startPage, startPaused));
        }

        /// <summary>개별 듣기: 한 줄을 한 언어로.</summary>
        public void PlaySingle(UnitData unit, int page, int line, Lang lang)
        {
            lastSingleUnit = unit;
            lastSinglePage = page;
            lastSingleLine = line;
            lastSingleLang = lang;
            BeginPrepare(PlaybackMode.Single, unit);
            prepareRoutine = StartCoroutine(PrepareSingle(unit, page, line, lang));
        }

        /// <summary>설정 화면의 '미리 듣기': 마지막으로 개별 듣기 한 문장을 다시.</summary>
        public bool HasLastSingle => lastSingleUnit != null;

        public void ReplayLastSingle()
        {
            if (lastSingleUnit != null) PlaySingle(lastSingleUnit, lastSinglePage, lastSingleLine, lastSingleLang);
        }

        public void Pause()
        {
            if (!IsActive) return;
            if (IsPreparing)
            {
                Stop();
                return;
            }
            backend.Pause();
            var s = State;
            s.playing = false;
            SetState(s);
        }

        public void Resume()
        {
            if (Mode == PlaybackMode.None || IsPreparing) return;
            backend.Resume();
            var s = State;
            s.playing = true;
            s.active = true;
            s.ended = false;
            if (s.step < 0) s.step = 0;
            SetState(s);
        }

        public void Stop()
        {
            if (prepareRoutine != null) StopCoroutine(prepareRoutine);
            prepareRoutine = null;
            IsPreparing = false;
            backend.Stop();
            Mode = PlaybackMode.None;
            Unit = null;
            steps.Clear();
            pageFirstStep.Clear();
            requestId++;
            State = PlaybackState.Idle;
            Changed?.Invoke();
        }

        /// <summary>전체 듣기 중 다른 예문으로 이동.</summary>
        public void SeekPage(int page)
        {
            if (Mode != PlaybackMode.Full || IsPreparing || page < 0 || page >= pageFirstStep.Count) return;
            int step = pageFirstStep[page];
            backend.SeekStep(step);
            var s = State;
            s.step = step;
            s.active = true;
            s.playing = true;
            s.ended = false;
            s.gap = false;
            SetState(s);
        }

        // ---- 내부 -------------------------------------------------------------------------

        private void BeginPrepare(PlaybackMode mode, UnitData unit)
        {
            if (prepareRoutine != null) StopCoroutine(prepareRoutine);
            backend.Stop();
            requestId++;
            Mode = mode;
            Unit = unit;
            steps.Clear();
            pageFirstStep.Clear();
            State = PlaybackState.Idle;
            IsPreparing = true;
            PrepareDone = 0;
            PrepareTotal = 0;
            Changed?.Invoke();
        }

        private IEnumerator PrepareFull(UnitData unit, int startPage, bool startPaused)
        {
            int id = requestId;
            bool ok = false;
            yield return cache.EnsureUnit(unit, (done, total) =>
            {
                if (id != requestId) return;
                PrepareDone = done;
                PrepareTotal = total;
                Changed?.Invoke();
            }, r => ok = r);
            if (id != requestId) yield break;
            prepareRoutine = null;

            if (!ok)
            {
                FailPrepare("음성을 받지 못했어요. 인터넷 연결을 확인해 주세요.");
                yield break;
            }

            var request = BuildFullRequest(unit, startPage);
            IsPreparing = false;
            StartBackend(request);
            if (startPaused) Pause();
        }

        private IEnumerator PrepareSingle(UnitData unit, int page, int line, Lang lang)
        {
            int id = requestId;
            var content = unit.items[page].Lines[line];
            string rel = content.audio.Get(lang);
            bool ok = false;
            yield return cache.EnsureFiles(new[] { rel }, r => ok = r);
            if (id != requestId) yield break;
            prepareRoutine = null;

            if (!ok)
            {
                FailPrepare("음성을 받지 못했어요. 인터넷 연결을 확인해 주세요.");
                yield break;
            }

            var request = new PlaybackRequest
            {
                id = requestId,
                title = unit.unit + " " + unit.title,
                repeat = false,
                startStep = 0,
            };
            request.nodes.Add(SingleNode());
            request.steps.Add(new PlaybackStep
            {
                path = cache.LocalPath(rel),
                node = 0,
                page = page,
                title = LineTitle(content),
                subtitle = content.ko,
            });
            steps.Add(new StepInfo { page = page, line = line, node = 0, lang = lang });
            pageFirstStep.Clear();
            IsPreparing = false;
            StartBackend(request);
        }

        private void FailPrepare(string message)
        {
            IsPreparing = false;
            Mode = PlaybackMode.None;
            Unit = null;
            State = PlaybackState.Idle;
            Changed?.Invoke();
            Failed?.Invoke(message);
        }

        private PlaybackRequest BuildFullRequest(UnitData unit, int startPage)
        {
            var nodes = settings.Nodes;
            var request = new PlaybackRequest
            {
                id = requestId,
                title = unit.unit + " " + unit.title,
                repeat = settings.Sequence.repeat,
            };
            foreach (var n in nodes) request.nodes.Add(ToPlaybackNode(n));

            steps.Clear();
            pageFirstStep.Clear();
            for (int p = 0; p < unit.items.Count; p++)
            {
                pageFirstStep.Add(request.steps.Count);
                var lines = unit.items[p].Lines;
                for (int l = 0; l < lines.Count; l++)
                {
                    var line = lines[l];
                    for (int n = 0; n < nodes.Count; n++)
                    {
                        string rel = line.audio.Get(nodes[n].lang);
                        request.steps.Add(new PlaybackStep
                        {
                            path = cache.LocalPath(rel),
                            node = n,
                            page = p,
                            title = LineTitle(line),
                            subtitle = line.ko,
                        });
                        steps.Add(new StepInfo { page = p, line = l, node = n, lang = nodes[n].lang });
                    }
                }
            }
            startPage = Mathf.Clamp(startPage, 0, pageFirstStep.Count - 1);
            request.startStep = pageFirstStep[startPage];
            return request;
        }

        private void StartBackend(PlaybackRequest request)
        {
            backend.Play(request);
            SetState(new PlaybackState
            {
                id = request.id,
                active = true,
                playing = true,
                step = request.startStep,
            });
        }

        private static string LineTitle(ContentLine line)
        {
            return string.IsNullOrEmpty(line.speaker) ? line.en : line.speaker + ": " + line.en;
        }

        private PlaybackNode SingleNode()
        {
            return new PlaybackNode { volume = settings.Single.volume, speed = settings.Single.speed, gap = 0f };
        }

        private static PlaybackNode ToPlaybackNode(SequenceNode n)
        {
            return new PlaybackNode { volume = n.volume, speed = n.speed, gap = n.gap };
        }

        private void SetState(PlaybackState s)
        {
            if (s.SameAs(State)) return;
            State = s;
            Changed?.Invoke();
        }

        private void AskNotificationPermissionOnce()
        {
            if (permissionAsked) return;
            permissionAsked = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidAudioBackend.RequestNotificationPermission();
#endif
        }

        // ---- 설정 변경 → 재생 중인 소리에 반영 ------------------------------------------

        private void OnSequenceNodeChanged(int index)
        {
            if (Mode != PlaybackMode.Full || IsPreparing) return;
            if (index < 0 || index >= settings.Nodes.Count) return;
            backend.UpdateNode(index, ToPlaybackNode(settings.Nodes[index]));
        }

        private void OnSingleChanged()
        {
            if (Mode != PlaybackMode.Single || IsPreparing) return;
            backend.UpdateNode(0, SingleNode());
        }

        private void OnRepeatChanged()
        {
            if (Mode != PlaybackMode.Full || IsPreparing) return;
            backend.SetRepeat(settings.Sequence.repeat);
        }

        private void OnSequenceStructureChanged()
        {
            if (Mode != PlaybackMode.Full || Unit == null || !IsActive) return;
            int page = 0;
            var step = CurrentStep;
            if (step != null) page = step.page;
            bool paused = !IsPreparing && !State.playing;
            PlayFull(Unit, page, paused);
        }

        // ---- 루프 -------------------------------------------------------------------------

        private void Update()
        {
            if (backend == null) return;
            backend.Tick(Time.unscaledDeltaTime);
            pollTimer += Time.unscaledDeltaTime;
            if (pollTimer >= PollInterval) Poll();
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause) Poll();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus) Poll();
        }

        private void Poll()
        {
            pollTimer = 0f;
            if (backend == null || Mode == PlaybackMode.None || IsPreparing) return;
            PlaybackState s;
            try
            {
                s = backend.GetState();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Audio] 상태 조회 실패: " + e.Message);
                return;
            }
            if (s.id != requestId) return; // 아직 이전 요청의 상태
            SetState(s);
        }
    }
}
