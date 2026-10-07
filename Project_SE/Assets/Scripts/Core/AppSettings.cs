using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnglishApp
{
    /// <summary>개별 듣기: 예문 화면에서 한 문장씩 들을 때.</summary>
    [Serializable]
    public class SingleListenSettings
    {
        public float volume = 1f;
        public float speed = 1f;
    }

    /// <summary>듣기 시퀀스의 노드 하나 (예: "영어 듣기, 음량 80%, x0.75, 텀 1.5초").</summary>
    [Serializable]
    public class SequenceNode
    {
        public Lang lang;
        public float volume = 1f;
        public float speed = 1f;
        public float gap = 1f; // 이 노드를 재생한 뒤 쉬는 시간(초)
        [NonSerialized] public int uid; // UI 에서 펼침 상태 유지용

        public SequenceNode Clone() => new SequenceNode { lang = lang, volume = volume, speed = speed, gap = gap };
    }

    /// <summary>전체 듣기: 예문(대화는 턴)마다 nodes 순서대로 재생한다.</summary>
    [Serializable]
    public class SequenceSettings
    {
        public List<SequenceNode> nodes = new List<SequenceNode>();
        public bool repeat;
    }

    /// <summary>설정 저장/변경 알림. PlayerPrefs 에 JSON 으로 저장한다.</summary>
    public class AppSettings
    {
        public const float VolumeMin = 0f, VolumeMax = 1f, VolumeStep = 0.01f;
        public const float SpeedMin = 0.5f, SpeedMax = 2f, SpeedStep = 0.05f;
        public const float GapMin = 0.1f, GapMax = 5f, GapStep = 0.1f;
        public const int MaxNodes = 10;

        private const string PrefsKey = "app_settings_v1";

        [Serializable]
        private class Data
        {
            public int schema = 1;
            public SingleListenSettings single = new SingleListenSettings();
            public SequenceSettings sequence = new SequenceSettings();
        }

        private Data data;
        private bool dirty;
        private int nextUid = 1;

        public SingleListenSettings Single => data.single;
        public SequenceSettings Sequence => data.sequence;
        public IReadOnlyList<SequenceNode> Nodes => data.sequence.nodes;

        /// <summary>순서·추가·삭제·언어 변경 (재생 중이면 시퀀스를 다시 만든다).</summary>
        public event Action SequenceStructureChanged;
        /// <summary>노드 음량·빠르기·텀 변경 (재생 중인 소리에 바로 적용).</summary>
        public event Action<int> SequenceNodeChanged;
        public event Action RepeatChanged;
        public event Action SingleChanged;

        public static List<SequenceNode> DefaultNodes()
        {
            return new List<SequenceNode>
            {
                new SequenceNode { lang = Lang.Ko, gap = 1f },
                new SequenceNode { lang = Lang.En, gap = 1.5f },
                new SequenceNode { lang = Lang.En, gap = 1.5f },
                new SequenceNode { lang = Lang.Ko, gap = 1f },
                new SequenceNode { lang = Lang.En, gap = 2f },
            };
        }

        public static AppSettings Load()
        {
            var s = new AppSettings();
            Data loaded = null;
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, null);
                if (!string.IsNullOrEmpty(json)) loaded = JsonUtility.FromJson<Data>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] 읽기 실패: " + e.Message);
            }
            s.data = loaded ?? new Data();
            s.Sanitize();
            return s;
        }

        private void Sanitize()
        {
            if (data.single == null) data.single = new SingleListenSettings();
            if (data.sequence == null) data.sequence = new SequenceSettings();
            if (data.sequence.nodes == null || data.sequence.nodes.Count == 0) data.sequence.nodes = DefaultNodes();
            if (data.sequence.nodes.Count > MaxNodes) data.sequence.nodes.RemoveRange(MaxNodes, data.sequence.nodes.Count - MaxNodes);

            data.single.volume = ClampVolume(data.single.volume);
            data.single.speed = ClampSpeed(data.single.speed);
            foreach (var n in data.sequence.nodes)
            {
                n.volume = ClampVolume(n.volume);
                n.speed = ClampSpeed(n.speed);
                n.gap = ClampGap(n.gap);
                if (n.lang != Lang.En && n.lang != Lang.Ko) n.lang = Lang.En;
                n.uid = nextUid++;
            }
        }

        public static float ClampVolume(float v) => Snap(Mathf.Clamp(v, VolumeMin, VolumeMax), VolumeStep);
        public static float ClampSpeed(float v) => Snap(Mathf.Clamp(v, SpeedMin, SpeedMax), SpeedStep);
        public static float ClampGap(float v) => Snap(Mathf.Clamp(v, GapMin, GapMax), GapStep);

        public static float Snap(float v, float step)
        {
            return (float)Math.Round(Math.Round(v / step) * step, 2);
        }

        // ---- 개별 듣기 -------------------------------------------------------------------

        public void SetSingle(float volume, float speed)
        {
            volume = ClampVolume(volume);
            speed = ClampSpeed(speed);
            if (Mathf.Approximately(volume, data.single.volume) && Mathf.Approximately(speed, data.single.speed)) return;
            data.single.volume = volume;
            data.single.speed = speed;
            dirty = true;
            SingleChanged?.Invoke();
        }

        // ---- 전체 듣기 시퀀스 ------------------------------------------------------------

        public void SetNodeParams(int index, float volume, float speed, float gap)
        {
            if (index < 0 || index >= data.sequence.nodes.Count) return;
            var n = data.sequence.nodes[index];
            volume = ClampVolume(volume);
            speed = ClampSpeed(speed);
            gap = ClampGap(gap);
            if (Mathf.Approximately(n.volume, volume) && Mathf.Approximately(n.speed, speed) && Mathf.Approximately(n.gap, gap)) return;
            n.volume = volume;
            n.speed = speed;
            n.gap = gap;
            dirty = true;
            SequenceNodeChanged?.Invoke(index);
        }

        public void SetNodeLang(int index, Lang lang)
        {
            if (index < 0 || index >= data.sequence.nodes.Count) return;
            if (data.sequence.nodes[index].lang == lang) return;
            data.sequence.nodes[index].lang = lang;
            StructureChanged();
        }

        public void MoveNode(int from, int to)
        {
            var nodes = data.sequence.nodes;
            if (from < 0 || from >= nodes.Count) return;
            to = Mathf.Clamp(to, 0, nodes.Count - 1);
            if (from == to) return;
            var n = nodes[from];
            nodes.RemoveAt(from);
            nodes.Insert(to, n);
            StructureChanged();
        }

        public bool CanAddNode => data.sequence.nodes.Count < MaxNodes;
        public bool CanRemoveNode => data.sequence.nodes.Count > 1;

        /// <summary>노드 추가. 같은 언어의 마지막 노드 값을 이어받는다.</summary>
        public void AddNode(Lang lang)
        {
            if (!CanAddNode) return;
            SequenceNode template = null;
            for (int i = data.sequence.nodes.Count - 1; i >= 0; i--)
                if (data.sequence.nodes[i].lang == lang) { template = data.sequence.nodes[i]; break; }
            var node = template != null ? template.Clone() : new SequenceNode { lang = lang };
            node.lang = lang;
            node.uid = nextUid++;
            data.sequence.nodes.Add(node);
            StructureChanged();
        }

        public void RemoveNode(int index)
        {
            if (!CanRemoveNode || index < 0 || index >= data.sequence.nodes.Count) return;
            data.sequence.nodes.RemoveAt(index);
            StructureChanged();
        }

        public void ResetSequence()
        {
            data.sequence.nodes = DefaultNodes();
            foreach (var n in data.sequence.nodes) n.uid = nextUid++;
            StructureChanged();
        }

        public void SetRepeat(bool repeat)
        {
            if (data.sequence.repeat == repeat) return;
            data.sequence.repeat = repeat;
            dirty = true;
            RepeatChanged?.Invoke();
        }

        private void StructureChanged()
        {
            dirty = true;
            SequenceStructureChanged?.Invoke();
        }

        // ---- 저장 ------------------------------------------------------------------------

        public void SaveIfDirty()
        {
            if (!dirty) return;
            dirty = false;
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
