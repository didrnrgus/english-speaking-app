using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace EnglishApp
{
    /// <summary>
    /// 예문 JSON 로드. 원격(GitHub Pages) → 캐시 → 번들(StreamingAssets/content) 순서 (docs/PLAN.md 4-4).
    /// 원격에서 받은 파일은 검사를 통과해야 캐시에 저장한다.
    /// </summary>
    public class ContentService
    {
        public const string RemoteBase = "https://didrnrgus.github.io/project_speaking_english/";
        private const int TimeoutSec = 8;

        public ContentIndex Index { get; private set; }
        public bool IsIndexLoaded => Index != null;
        public event Action IndexLoaded;

        private readonly Dictionary<string, UnitData> units = new Dictionary<string, UnitData>();
        private readonly HashSet<string> remoteChecked = new HashSet<string>();

        private static string CacheDir => Path.Combine(Application.persistentDataPath, "content");
        private static string BundleDir => Path.Combine(Application.streamingAssetsPath, "content");

        public IEnumerator LoadIndex()
        {
            ContentIndex result = null;
            ContentIndex parsed;

            string remote = null;
            yield return GetText(RemoteBase + "index.json", TimeoutSec, t => remote = t);
            if (TryParseIndex(remote, out parsed))
            {
                result = parsed;
                WriteCache("index.json", remote);
            }

            if (result == null && TryParseIndex(ReadCache("index.json"), out parsed)) result = parsed;

            if (result == null)
            {
                string bundled = null;
                yield return GetText(ToUrl(Path.Combine(BundleDir, "index.json")), TimeoutSec, t => bundled = t);
                if (TryParseIndex(bundled, out parsed)) result = parsed;
            }

            if (result == null)
            {
                Debug.LogError("[Content] index.json 을 읽지 못했습니다.");
                result = new ContentIndex();
            }

            Index = result;
            IndexLoaded?.Invoke();
        }

        public List<LevelInfo> LevelsOf(string track)
        {
            var list = new List<LevelInfo>();
            if (Index == null) return list;
            foreach (var l in Index.levels)
                if (l.track == track) list.Add(l);
            return list;
        }

        public LevelInfo FindLevel(string level)
        {
            if (Index == null) return null;
            foreach (var l in Index.levels)
                if (l.level == level) return l;
            return null;
        }

        public List<UnitInfo> UnitsOf(string level)
        {
            var list = new List<UnitInfo>();
            if (Index == null) return list;
            foreach (var u in Index.units)
                if (u.level == level) list.Add(u);
            return list;
        }

        public UnitInfo FindUnit(string unit)
        {
            if (Index == null) return null;
            foreach (var u in Index.units)
                if (u.unit == unit) return u;
            return null;
        }

        public UnitData GetLoadedUnit(string unit)
        {
            units.TryGetValue(unit, out var data);
            return data;
        }

        /// <summary>유닛 파일 로드. 세션당 원격 확인은 1번만 하고, 이후에는 메모리 값을 쓴다.</summary>
        public IEnumerator LoadUnit(UnitInfo info, Action<UnitData> done)
        {
            UnitData loaded;
            if (units.TryGetValue(info.unit, out loaded) && remoteChecked.Contains(info.unit))
            {
                done(loaded);
                yield break;
            }

            UnitData result = null;
            UnitData cached;
            UnitData parsed;
            TryParseUnit(ReadCache(info.file), info, out cached);

            string remote = null;
            yield return GetText(RemoteBase + info.file, TimeoutSec, t => remote = t);
            if (TryParseUnit(remote, info, out parsed))
            {
                remoteChecked.Add(info.unit);
                result = parsed;
                if (cached == null || cached.version != parsed.version) WriteCache(info.file, remote);
            }

            if (result == null) result = cached;

            if (result == null)
            {
                string bundled = null;
                yield return GetText(ToUrl(Path.Combine(BundleDir, info.file)), TimeoutSec, t => bundled = t);
                TryParseUnit(bundled, info, out result);
            }

            if (result != null) units[info.unit] = result;
            else Debug.LogError("[Content] 유닛을 읽지 못했습니다: " + info.unit);
            done(result);
        }

        // ------------------------------------------------------------------------------------

        private static bool TryParseIndex(string json, out ContentIndex index)
        {
            index = null;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<ContentIndex>(json);
                if (parsed == null || parsed.levels == null || parsed.levels.Count == 0 || parsed.units == null || parsed.units.Count == 0)
                    return false;
                index = parsed;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Content] index 파싱 실패: " + e.Message);
                return false;
            }
        }

        private static bool TryParseUnit(string json, UnitInfo info, out UnitData unit)
        {
            unit = null;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<UnitData>(json);
                if (parsed == null) return false;
                if (parsed.unit != info.unit)
                {
                    Debug.LogWarning($"[Content] {info.file}: unit 불일치 {parsed.unit}");
                    return false;
                }
                if (!parsed.IsValid(out var error))
                {
                    Debug.LogWarning($"[Content] {info.file}: {error}");
                    return false;
                }
                if (string.IsNullOrEmpty(parsed.title)) parsed.title = info.title;
                unit = parsed;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Content] {info.file} 파싱 실패: {e.Message}");
                return false;
            }
        }

        private static string ReadCache(string relative)
        {
            try
            {
                string path = Path.Combine(CacheDir, relative);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Content] 캐시 읽기 실패: " + e.Message);
                return null;
            }
        }

        private static void WriteCache(string relative, string text)
        {
            try
            {
                string path = Path.Combine(CacheDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, text);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Content] 캐시 저장 실패: " + e.Message);
            }
        }

        /// <summary>파일 경로를 UnityWebRequest 용 URL 로 (Android StreamingAssets 는 이미 jar:file:// URL).</summary>
        public static string ToUrl(string path)
        {
            if (path.Contains("://")) return path;
            return new Uri(path).AbsoluteUri;
        }

        public static IEnumerator GetText(string url, int timeoutSec, Action<string> done)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = timeoutSec;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) done(req.downloadHandler.text);
                else done(null);
            }
        }
    }
}
