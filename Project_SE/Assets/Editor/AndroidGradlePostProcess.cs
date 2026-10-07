#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace EnglishApp.EditorTools
{
    /// <summary>
    /// Gradle 프로젝트 생성 직후 보정.
    /// - 오디오 플러그인(EnglishAudio.androidlib)의 compileSdk 를 unityLibrary 와 같게 맞춘다
    ///   (설치된 Android SDK 버전과 상관없이 빌드되도록)
    /// - AndroidX 사용 설정 (Media3 가 AndroidX 라이브러리)
    /// </summary>
    public class AndroidGradlePostProcess : IPostGenerateGradleAndroidProject
    {
        private const string PluginFolder = "EnglishAudio.androidlib";
        private const string CompileSdkMarker = "// @esa-compileSdk";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var root = Path.GetFullPath(Path.Combine(unityLibraryPath, ".."));
            EnsureAndroidX(Path.Combine(root, "gradle.properties"));

            int compileSdk = ReadCompileSdk(Path.Combine(unityLibraryPath, "build.gradle"));
            if (compileSdk <= 0) return;
            foreach (var dir in Directory.GetDirectories(root, PluginFolder, SearchOption.AllDirectories))
            {
                var gradle = Path.Combine(dir, "build.gradle");
                if (!File.Exists(gradle)) continue;
                var lines = File.ReadAllLines(gradle);
                bool changed = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains(CompileSdkMarker)) continue;
                    lines[i] = "    compileSdk " + compileSdk + " " + CompileSdkMarker;
                    changed = true;
                }
                if (changed) File.WriteAllLines(gradle, lines);
            }
        }

        private static int ReadCompileSdk(string gradlePath)
        {
            if (!File.Exists(gradlePath)) return -1;
            var text = File.ReadAllText(gradlePath);
            var m = Regex.Match(text, @"compileSdk(?:Version)?\s*(?:=\s*)?(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        private static void EnsureAndroidX(string propertiesPath)
        {
            var text = File.Exists(propertiesPath) ? File.ReadAllText(propertiesPath) : "";
            if (Regex.IsMatch(text, @"^\s*android\.useAndroidX\s*=\s*true", RegexOptions.Multiline)) return;
            text = Regex.Replace(text, @"^\s*android\.useAndroidX\s*=.*$", "", RegexOptions.Multiline);
            text = text.TrimEnd() + "\nandroid.useAndroidX=true\n";
            File.WriteAllText(propertiesPath, text);
            Debug.Log("[EnglishApp] gradle.properties: android.useAndroidX=true");
        }
    }
}
#endif
