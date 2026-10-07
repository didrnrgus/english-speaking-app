using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EnglishApp.EditorTools
{
    /// <summary>
    /// 번들 예문(오프라인 첫 실행용) 복사: 레포의 content/*.json → Assets/StreamingAssets/content/.
    /// 빌드할 때마다 자동으로 실행된다. 원본은 레포 content/ 하나뿐이고, 복사본은 git 에 올리지 않는다.
    /// </summary>
    public class ContentSync : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content"));
        private static string Destination => Path.Combine(Application.dataPath, "StreamingAssets", "content");

        public void OnPreprocessBuild(BuildReport report)
        {
            Sync(false); // 빌드 중에는 Refresh 하지 않는다 (StreamingAssets 는 파일 그대로 들어간다)
        }

        [MenuItem("Tools/English App/Sync Bundled Content")]
        public static void SyncFromMenu() => Sync(true);

        public static void Sync(bool refresh)
        {
            if (!Directory.Exists(Source))
            {
                Debug.LogWarning("[EnglishApp] 레포의 content 폴더가 없습니다: " + Source);
                return;
            }

            if (Directory.Exists(Destination))
            {
                foreach (var stale in Directory.GetFiles(Destination, "*.json", SearchOption.AllDirectories))
                {
                    var rel = stale.Substring(Destination.Length + 1);
                    if (!File.Exists(Path.Combine(Source, rel))) File.Delete(stale);
                }
            }

            int count = 0;
            foreach (var file in Directory.GetFiles(Source, "*.json", SearchOption.AllDirectories))
            {
                var rel = file.Substring(Source.Length + 1);
                var dest = Path.Combine(Destination, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest, true);
                count++;
            }
            if (refresh) AssetDatabase.Refresh();
            Debug.Log("[EnglishApp] 번들 예문 " + count + "개 복사 → Assets/StreamingAssets/content");
        }

        public static void SyncIfMissing()
        {
            if (!File.Exists(Path.Combine(Destination, "index.json"))) Sync(true);
        }
    }
}
