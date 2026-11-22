using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EnglishApp.EditorTools
{
    /// <summary>
    /// CI(GitHub Actions, GameCI) 빌드 진입점. .github/workflows/build-ios.yml 이 -executeMethod 로 부른다.
    /// 씬은 Build Settings 에 켜진 것 그대로, 번들 ID·백그라운드 오디오 등은 ProjectSettings 값 그대로 쓴다 (IosBuildSettings.cs).
    /// -appBuild 를 넘기면 iOS 빌드 번호(CFBundleVersion)로 쓴다. 버전(0.1.2 등)은 ProjectSettings 의 bundleVersion.
    /// </summary>
    public static class CiBuild
    {
        [MenuItem("Tools/English App/Build iOS (Xcode project)")]
        public static void BuildIOS()
        {
            string path = Arg("-customBuildPath") ?? "Build/iOS";
            string buildNumber = Arg("-appBuild");
            if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            Debug.Log($"[EnglishApp] iOS 빌드: {report.summary.result}, {report.summary.totalSize / 1024 / 1024} MB → {path}");
            if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
