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
        Full,   // 전체 듣기: 유닛 전체(또는 레벨 전체 = 레벨의 유닛을 이어서)를 듣기 시퀀스대로
    }

    /// <summary>시퀀스의 step 하나가 화면의 어디에 해당하는지.</summary>
    public class StepInfo
    {
        public int unit; // Units 안의 유닛 번호 (유닛 전체 듣기·개별 듣기는 0)
        public int page; // 유닛 안의 예문 번호 (0부터)
        public int line; // 예문 안의 줄 (대화는 턴)
        public int node; // 듣기 시퀀스 노드 번호 (개별 듣기는 0)
        public Lang lang;
    }

    /// <summary>
    /// 재생 제어. 화면은 이 클래스만 사용한다.
    /// - 전체 듣기는 두 가지: 유닛 전체 듣기(PlayFull) / 레벨 전체 듣기(PlayLevel, Level != null).
    ///   레벨 전체 듣기는 레벨의 유닛을 모두 받아 하나의 시퀀스로 네이티브에 넘긴다 (화면이 꺼져도 다음 유닛으로 이어짐).
    /// - 설정(음량·빠르기·텀)이 바뀌면 재생 중인 소리에 바로 반영한다.
    /// - 듣기 시퀀스 구조(순서·추가·삭제·언어)가 바뀌면 현재 예문부터 다시 만든다.
    /// - 백엔드 상태를 0.1초마다 읽는다 (화면이 꺼졌다 켜지면 바로 동기화).
    /// </summary>
    public class AudioPlayer : MonoBehaviour
    {
        private const float PollInterval = 0.1f;

        private AppSettings settings;
        private AudioCache cache;
        private ContentService content;
        private IAudioBackend backend;

        private readonly List<StepInfo> steps = new List<StepInfo>();
        private readonly List<int> pageFirstStep = new List<int>(); // 레벨 전체에서 이어지는 예문 번호 → 첫 step
        private readonly List<int> unitFirstPage = new List<int>(); // 유닛 번호 → 그 유닛 첫 예문의 이어지는 번호
        private readonly List<UnitInfo> unitInfos = new List<UnitInfo>(); // 레벨 전체 듣기: 레벨의 유닛 목록
        private readonly List<UnitData> units = new List<UnitData>();     // 재생할 유닛 (유닛 전체 듣기·개별 듣기는 1개)
        private int startUnit;
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

        /// <summary>레벨 전체 듣기 중인 레벨 (유닛 전체 듣기·개별 듣기는 null).</summary>
        public LevelInfo Level { get; private set; }

        public bool IsPreparing { get; private set; }
        public int PrepareDone { get; private set; }
        public int PrepareTotal { get; private set; }

        /// <summary>상태가 바뀔 때마다 (step 이동, 재생/일시정지, 준비 진행률 등).</summary>
        public event Action Changed;
        /// <summary>음성을 받지 못했을 때 (오프라인 등).</summary>
        public event Action<string> Failed;

        public void Init(AppSettings appSettings, AudioCache audioCache, ContentService contentService)
        {
            settings = appSettings;
            cache = audioCache;
            content = contentService;
#if UNITY_ANDROID && !UNITY_EDITOR
            backend = new AndroidAudioBackend();
#elif UNITY_IOS && !UNITY_EDITOR
            backend = new IosAudioBackend();
#else
            backend = new EditorAudioBackend(this);
#endif
            settings.SequenceNodeChanged += OnSequenceNodeChanged;
            settings.SequenceStructureChanged += OnSequenceStructureChanged;
            settings.RepeatChanged += OnRepeatChanged;
            settings.SingleChanged += OnSingleChanged;
        }

        // ---- 조회 -------------------------------------------------------------------------

        /// <summary>레벨 전체 듣기 중.</summary>
        public bool IsLevelMode => Mode == PlaybackMode.Full && Level != null;

        /// <summary>이 유닛을 전체 듣기 중 (유닛 전체 듣기, 또는 레벨 전체 듣기가 지금 이 유닛을 재생 중).</summary>
        public bool IsFull(string unitId) => Mode == PlaybackMode.Full && unitId != null && UnitId == unitId;

        /// <summary>이 레벨을 레벨 전체 듣기 중.</summary>
        public bool IsLevelFull(string level) => IsLevelMode && Level.level == level;

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

        /// <summary>지금 재생 중인 유닛 번호 (Units 안). 준비 중에는 시작할 유닛.</summary>
        public int UnitIndex
        {
            get
            {
                var step = CurrentStep;
                return step != null ? step.unit : startUnit;
            }
        }

        /// <summary>재생할 유닛 수 (레벨 전체 듣기는 레벨의 유닛 수).</summary>
        public int UnitCount => Math.Max(units.Count, unitInfos.Count);

        /// <summary>지금 재생 중인 유닛. 레벨 전체 듣기는 재생 위치에 따라 바뀐다 (유닛 파일을 읽기 전에는 null).</summary>
        public UnitData Unit
        {
            get
            {
                int i = UnitIndex;
                return i >= 0 && i < units.Count ? units[i] : null;
            }
        }

        /// <summary>지금 재생 중인 유닛 id (준비 중에도 알 수 있음).</summary>
        public string UnitId
        {
            get
            {
                var u = Unit;
                if (u != null) return u.unit;
                int i = UnitIndex;
                return i >= 0 && i < unitInfos.Count ? unitInfos[i].unit : null;
            }
        }

        /// <summary>지금 재생 중인 유닛 제목 (준비 중에도 알 수 있음).</summary>
        public string UnitTitle
        {
            get
            {
                var u = Unit;
                if (u != null) return u.title;
                int i = UnitIndex;
                return i >= 0 && i < unitInfos.Count ? unitInfos[i].title : null;
            }
        }

        /// <summary>레벨 전체 듣기를 시작할 유닛 번호: 마지막으로 듣던 유닛 (없으면 0).</summary>
        public int ResumeUnitIndex(LevelInfo level)
        {
            string id = settings.GetResumeUnit(level.level);
            if (id == null) return 0;
            var infos = content.UnitsOf(level.level);
            return Mathf.Max(0, infos.FindIndex(u => u.unit == id));
        }

        // ---- 명령 -------------------------------------------------------------------------

        /// <summary>유닛 전체 듣기: 유닛 전체를 startPage 예문부터 시퀀스대로.</summary>
        public void PlayFull(UnitData unit, int startPage, bool startPaused = false)
        {
            AskNotificationPermissionOnce();
            BeginPrepare(PlaybackMode.Full, unit, null, null, 0);
            prepareRoutine = StartCoroutine(DownloadAndStart(startPage, startPaused));
        }

        /// <summary>레벨 전체 듣기: 레벨의 유닛을 모두 이어서. startUnitIndex 번째 유닛의 startPage 예문부터.</summary>
        public void PlayLevel(LevelInfo level, int startUnitIndex, int startPage = 0, bool startPaused = false)
        {
            var infos = content.UnitsOf(level.level);
            if (infos.Count == 0) return;
            AskNotificationPermissionOnce();
            BeginPrepare(PlaybackMode.Full, null, level, infos, Mathf.Clamp(startUnitIndex, 0, infos.Count - 1));
            prepareRoutine = StartCoroutine(PrepareLevel(startPage, startPaused));
        }

        /// <summary>개별 듣기: 한 줄을 한 언어로.</summary>
        public void PlaySingle(UnitData unit, int page, int line, Lang lang)
        {
            lastSingleUnit = unit;
            lastSinglePage = page;
            lastSingleLine = line;
            lastSingleLang = lang;
            BeginPrepare(PlaybackMode.Single, unit, null, null, 0);
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
            ClearPlaylist();
            requestId++;
            State = PlaybackState.Idle;
            Changed?.Invoke();
        }

        /// <summary>전체 듣기 중 지금 유닛의 다른 예문으로 이동.</summary>
        public void SeekPage(int page)
        {
            if (Mode != PlaybackMode.Full || IsPreparing) return;
            int u = UnitIndex;
            if (u < 0 || u >= units.Count || u >= unitFirstPage.Count) return;
            if (page < 0 || page >= units[u].items.Count) return;
            int global = unitFirstPage[u] + page;
            if (global >= pageFirstStep.Count) return;
            int step = pageFirstStep[global];
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

        private void BeginPrepare(PlaybackMode mode, UnitData unit, LevelInfo level, List<UnitInfo> infos, int firstUnit)
        {
            if (prepareRoutine != null) StopCoroutine(prepareRoutine);
            backend.Stop();
            requestId++;
            Mode = mode;
            ClearPlaylist();
            Level = level;
            if (unit != null) units.Add(unit);
            if (infos != null) unitInfos.AddRange(infos);
            startUnit = firstUnit;
            State = PlaybackState.Idle;
            IsPreparing = true;
            PrepareDone = 0;
            PrepareTotal = 0;
            Changed?.Invoke();
        }

        private void ClearPlaylist()
        {
            Level = null;
            units.Clear();
            unitInfos.Clear();
            startUnit = 0;
            steps.Clear();
            pageFirstStep.Clear();
            unitFirstPage.Clear();
        }

        private IEnumerator PrepareLevel(int startPage, bool startPaused)
        {
            int id = requestId;

            // 유닛 파일을 동시에 읽는다 (오프라인이면 하나씩 기다리는 동안 너무 오래 걸림)
            var loaded = new UnitData[unitInfos.Count];
            int pending = loaded.Length;
            for (int i = 0; i < loaded.Length; i++)
            {
                int index = i;
                StartCoroutine(content.LoadUnit(unitInfos[i], u =>
                {
                    loaded[index] = u;
                    pending--;
                }));
            }
            while (pending > 0) yield return null;
            if (id != requestId) yield break;

            // 읽지 못한 유닛은 빼고 이어 붙인다
            string startId = unitInfos[startUnit].unit;
            var infos = new List<UnitInfo>(unitInfos);
            unitInfos.Clear();
            units.Clear();
            startUnit = 0;
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] == null) continue;
                if (infos[i].unit == startId) startUnit = units.Count;
                unitInfos.Add(infos[i]);
                units.Add(loaded[i]);
            }
            if (units.Count == 0)
            {
                prepareRoutine = null;
                FailPrepare("예문을 불러오지 못했어요. 인터넷 연결을 확인해 주세요.");
                yield break;
            }
            Changed?.Invoke();

            yield return DownloadAndStart(startPage, startPaused);
        }

        /// <summary>재생할 유닛의 mp3 를 모두 받고 시작한다 (유닛 전체 듣기 / 레벨 전체 듣기 공통).</summary>
        private IEnumerator DownloadAndStart(int startPage, bool startPaused)
        {
            int id = requestId;
            bool ok = false;
            yield return cache.EnsureUnits(units, (done, total) =>
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

            var request = BuildFullRequest(startUnit, startPage);
            IsPreparing = false;
            StartBackend(request);
            if (startPaused) Pause();
        }

        private IEnumerator PrepareSingle(UnitData unit, int page, int line, Lang lang)
        {
            int id = requestId;
            var line0 = unit.items[page].Lines[line];
            string rel = line0.audio.Get(lang);
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
                title = LineTitle(line0),
                subtitle = line0.ko,
            });
            steps.Add(new StepInfo { unit = 0, page = page, line = line, node = 0, lang = lang });
            pageFirstStep.Clear();
            unitFirstPage.Clear();
            IsPreparing = false;
            StartBackend(request);
        }

        private void FailPrepare(string message)
        {
            IsPreparing = false;
            Mode = PlaybackMode.None;
            ClearPlaylist();
            State = PlaybackState.Idle;
            Changed?.Invoke();
            Failed?.Invoke(message);
        }

        private PlaybackRequest BuildFullRequest(int firstUnit, int startPage)
        {
            var nodes = settings.Nodes;
            var request = new PlaybackRequest
            {
                id = requestId,
                // 알림·잠금화면의 아티스트 줄
                title = Level != null
                    ? Level.level + " " + Level.title + " · 전체 듣기"
                    : units[0].unit + " " + units[0].title,
                repeat = settings.Sequence.repeat,
            };
            foreach (var n in nodes) request.nodes.Add(ToPlaybackNode(n));

            steps.Clear();
            pageFirstStep.Clear();
            unitFirstPage.Clear();
            for (int u = 0; u < units.Count; u++)
            {
                var unit = units[u];
                unitFirstPage.Add(pageFirstStep.Count);
                for (int p = 0; p < unit.items.Count; p++)
                {
                    // 네이티브의 page 는 레벨 전체에서 이어지는 번호 (알림의 이전/다음 버튼이 유닛을 넘어 이동)
                    int globalPage = pageFirstStep.Count;
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
                                page = globalPage,
                                title = LineTitle(line),
                                subtitle = line.ko,
                            });
                            steps.Add(new StepInfo { unit = u, page = p, line = l, node = n, lang = nodes[n].lang });
                        }
                    }
                }
            }
            firstUnit = Mathf.Clamp(firstUnit, 0, units.Count - 1);
            startPage = Mathf.Clamp(startPage, 0, units[firstUnit].items.Count - 1);
            startUnit = firstUnit;
            request.startStep = pageFirstStep[unitFirstPage[firstUnit] + startPage];
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
            RememberResumeUnit();
            Changed?.Invoke();
        }

        /// <summary>
        /// 레벨 전체 듣기를 다음에 이어서 시작할 유닛을 기억한다.
        /// 유닛 전체 듣기로 들은 유닛도 그 레벨의 위치로 친다. 레벨을 끝까지 들었으면 다음에는 처음부터.
        /// </summary>
        private void RememberResumeUnit()
        {
            if (Mode != PlaybackMode.Full || IsPreparing) return;
            if (IsLevelMode && State.ended)
            {
                settings.SetResumeUnit(Level.level, null);
                return;
            }
            var unit = Unit;
            if (unit != null && State.active) settings.SetResumeUnit(unit.level, unit.unit);
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
            if (Mode != PlaybackMode.Full || !IsActive) return;
            int page = 0;
            var step = CurrentStep;
            if (step != null) page = step.page;
            bool paused = !IsPreparing && !State.playing;
            if (IsLevelMode)
            {
                // 레벨의 유닛 목록 기준 번호로 다시 (읽지 못해 빠진 유닛이 있을 수 있으므로 id 로 찾음)
                var level = Level;
                string id = UnitId;
                int index = Mathf.Max(0, content.UnitsOf(level.level).FindIndex(u => u.unit == id));
                PlayLevel(level, index, page, paused);
                return;
            }
            var unit = Unit;
            if (unit != null) PlayFull(unit, page, paused);
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
