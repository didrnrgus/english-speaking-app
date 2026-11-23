using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace EnglishApp
{
    /// <summary>
    /// mp3 캐시. 들을 유닛의 파일만 받는다 (GitHub Pages 트래픽 — docs/PLAN.md 10장 5번).
    /// 유닛 파일 version 이 바뀌면 그 유닛의 mp3 를 다시 받는다 (문장이 바뀌면 같은 경로의 mp3 도 바뀌므로).
    /// </summary>
    public class AudioCache
    {
        private const int Parallel = 4;
        private const int TimeoutSec = 20;

        private readonly MonoBehaviour host;
        private readonly HashSet<string> inFlight = new HashSet<string>();

        public AudioCache(MonoBehaviour host)
        {
            this.host = host;
        }

        private static string Root => Path.Combine(Application.persistentDataPath, "audio_cache");

        public string LocalPath(string relative)
        {
            return Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        public bool Has(string relative)
        {
            return File.Exists(LocalPath(relative));
        }

        private static string MarkerPath(string unit) => Path.Combine(Root, "_units", unit + ".ver");

        /// <summary>이 유닛 mp3 를 이 version 으로 모두 받아 두었는지.</summary>
        public bool IsUnitReady(UnitData unit)
        {
            if (ReadMarker(unit.unit) != unit.version) return false;
            foreach (var p in unit.AllAudioPaths())
                if (!Has(p)) return false;
            return true;
        }

        /// <summary>유닛의 mp3 를 모두 준비한다. progress(받은 개수, 전체 개수).</summary>
        public IEnumerator EnsureUnit(UnitData unit, Action<int, int> progress, Action<bool> done)
        {
            yield return EnsureUnits(new[] { unit }, progress, done);
        }

        /// <summary>
        /// 여러 유닛(레벨 전체 듣기)의 mp3 를 한 번에 준비한다. progress 는 모든 유닛을 합친 개수.
        /// 일부가 실패해도 다 받은 유닛은 완료로 기록한다 (다음에 그 유닛은 다시 받지 않음).
        /// </summary>
        public IEnumerator EnsureUnits(IList<UnitData> units, Action<int, int> progress, Action<bool> done)
        {
            var need = new List<string>();
            var needSet = new HashSet<string>();
            var perUnit = new List<List<string>>();
            foreach (var unit in units)
            {
                bool versionChanged = ReadMarker(unit.unit) != unit.version;
                var mine = new List<string>();
                foreach (var p in unit.AllAudioPaths())
                {
                    if (!versionChanged && Has(p)) continue;
                    mine.Add(p);
                    if (needSet.Add(p)) need.Add(p);
                }
                perUnit.Add(mine);
            }

            var failed = new HashSet<string>();
            if (need.Count > 0)
                yield return DownloadAll(need, progress, failed);

            for (int i = 0; i < units.Count; i++)
            {
                bool unitOk = true;
                foreach (var p in perUnit[i])
                    if (failed.Contains(p)) { unitOk = false; break; }
                if (unitOk) WriteMarker(units[i].unit, units[i].version);
            }
            done(failed.Count == 0);
        }

        /// <summary>파일 몇 개만 (개별 듣기). 이미 있으면 바로 끝난다.</summary>
        public IEnumerator EnsureFiles(IList<string> relatives, Action<bool> done)
        {
            var need = new List<string>();
            foreach (var p in relatives)
                if (!Has(p)) need.Add(p);
            var failed = new HashSet<string>();
            if (need.Count > 0) yield return DownloadAll(need, null, failed);
            done(failed.Count == 0);
        }

        /// <summary>모두 받는다. 실패한 경로는 failed 에 담는다.</summary>
        private IEnumerator DownloadAll(List<string> relatives, Action<int, int> progress, HashSet<string> failed)
        {
            int total = relatives.Count;
            int finished = 0;
            int running = 0;
            int next = 0;
            progress?.Invoke(0, total);

            while (finished < total)
            {
                while (running < Parallel && next < total)
                {
                    string rel = relatives[next++];
                    running++;
                    host.StartCoroutine(DownloadOne(rel, ok =>
                    {
                        running--;
                        finished++;
                        if (!ok) failed.Add(rel);
                        progress?.Invoke(finished, total);
                    }));
                }
                yield return null;
            }
        }

        private IEnumerator DownloadOne(string relative, Action<bool> done)
        {
            // 같은 파일을 동시에 받지 않는다 (전체 듣기 준비 중에 개별 듣기를 누른 경우 등)
            while (inFlight.Contains(relative)) yield return null;
            inFlight.Add(relative);

            string dest = LocalPath(relative);
            string tmp = dest + ".part";
            bool ok = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AudioCache] " + e.Message);
            }

            using (var req = new UnityWebRequest(ContentService.RemoteBase + relative, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true };
                req.timeout = TimeoutSec;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(tmp, dest);
                        ok = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[AudioCache] 저장 실패 " + relative + ": " + e.Message);
                    }
                }
                else
                {
                    Debug.LogWarning("[AudioCache] 다운로드 실패 " + relative + ": " + req.error);
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 무시 */ }
                }
            }

            inFlight.Remove(relative);
            done(ok);
        }

        private static int ReadMarker(string unit)
        {
            try
            {
                string path = MarkerPath(unit);
                if (!File.Exists(path)) return -1;
                return int.TryParse(File.ReadAllText(path).Trim(), out int v) ? v : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static void WriteMarker(string unit, int version)
        {
            try
            {
                string path = MarkerPath(unit);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, version.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AudioCache] " + e.Message);
            }
        }
    }
}
