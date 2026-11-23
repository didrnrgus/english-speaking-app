using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EnglishApp.EditorTools
{
    /// <summary>
    /// CI(GitHub Actions, GameCI) 빌드 진입점. .github/workflows/build-ios.yml, build-android.yml 이 -executeMethod 로 부른다.
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

        /// <summary>
        /// .github/workflows/build-android.yml 이 부른다. 서명은 ProjectSettings 의 키스토어(Keystore/kkuk7364.keystore, 별칭 yangkey)
        /// 그대로 쓰고, 비밀번호만 GameCI 가 넘기는 -androidKeystorePass / -androidKeyaliasPass 로 받는다 (키스토어 파일은 워크플로가 시크릿에서 만든다).
        /// -appBuild 를 넘기면 versionCode 로 쓴다. 단 ProjectSettings 값보다 작아지지 않게 한다 (PC 빌드 위에 덮어 설치할 때 다운그레이드 거부 방지).
        /// </summary>
        [MenuItem("Tools/English App/Build Android (APK)")]
        public static void BuildAndroid()
        {
            string path = Arg("-customBuildPath") ?? "Build/Android/EnglishSpeaking.apk";
            if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = System.IO.Path.Combine(path, "EnglishSpeaking.apk");

            if (int.TryParse(Arg("-appBuild"), out int buildNumber))
                PlayerSettings.Android.bundleVersionCode = Math.Max(PlayerSettings.Android.bundleVersionCode, buildNumber);

            string keystorePass = Arg("-androidKeystorePass");
            string keyaliasPass = Arg("-androidKeyaliasPass");
            if (!string.IsNullOrEmpty(keystorePass)) PlayerSettings.Android.keystorePass = keystorePass;
            if (!string.IsNullOrEmpty(keyaliasPass)) PlayerSettings.Android.keyaliasPass = keyaliasPass;
            else if (!string.IsNullOrEmpty(keystorePass)) PlayerSettings.Android.keyaliasPass = keystorePass;   // 별칭 비밀번호를 따로 안 줬으면 같다고 본다

            EditorUserBuildSettings.buildAppBundle = false;   // .aab 아니고 .apk
            Debug.Log($"[EnglishApp] Android 빌드: v{PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), " +
                      $"키스토어 {PlayerSettings.Android.keystoreName} / {PlayerSettings.Android.keyaliasName}");

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[EnglishApp] Android 빌드: {report.summary.result}, {report.summary.totalSize / 1024 / 1024} MB → {path}");
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
