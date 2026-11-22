#if UNITY_IOS
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace EnglishApp.EditorTools
{
    /// <summary>
    /// iOS 빌드 준비 (빌드 대상이 iOS 일 때 에디터가 스크립트를 불러오면 자동, 메뉴: Tools > English App > Apply iOS Settings).
    /// iOS Build Support 모듈이 있고 빌드 대상이 iOS 일 때만 컴파일된다 (UNITY_IOS) → iOS 로 전환한 직후의 재컴파일 때 실행된다.
    /// - Player Settings: 번들 ID(Android 와 같게), 백그라운드 오디오, 자동 서명, 스크립팅 심볼(Android 와 같게)
    /// - Xcode 프로젝트 생성 직후: Info.plist 에 UIBackgroundModes=audio 보장,
    ///   UnityFramework 에 AVFoundation·MediaPlayer 링크 (EsaAudio.mm 이 사용)
    /// 서명 팀(Team)은 Xcode 의 Signing &amp; Capabilities 에서 고른다 (Apple ID 무료 계정도 기기 설치 가능) → docs/IOS.md
    /// </summary>
    [InitializeOnLoad]
    public class IosBuildSettings : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        static IosBuildSettings()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) Apply(false);
            };
        }

        [MenuItem("Tools/English App/Apply iOS Settings")]
        private static void ApplyFromMenu() => Apply(true);

        private static void Apply(bool log)
        {
            bool changed = false;

            // 번들 ID: 비어 있거나 Unity 기본값이면 Android 패키지명과 같게
            string iosId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            string androidId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if ((string.IsNullOrEmpty(iosId) || iosId.StartsWith("com.DefaultCompany") || iosId.StartsWith("com.Company."))
                && !string.IsNullOrEmpty(androidId))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, androidId);
                changed = true;
            }

            // 화면이 꺼져도 재생 (Info.plist UIBackgroundModes=audio)
            if ((PlayerSettings.iOS.backgroundModes & iOSBackgroundMode.AudioAirplayPiP) == 0)
            {
                PlayerSettings.iOS.backgroundModes |= iOSBackgroundMode.AudioAirplayPiP;
                changed = true;
            }

            if (!PlayerSettings.iOS.appleEnableAutomaticSigning)
            {
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                changed = true;
            }

            // LEVELPLAY_*, LETAI_PARAFORM 등 Android 에 있는 심볼을 iOS 에도
            var androidDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(';').Where(s => s.Length > 0);
            var iosDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS)
                .Split(';').Where(s => s.Length > 0).ToList();
            var missing = androidDefines.Where(d => !iosDefines.Contains(d)).ToList();
            if (missing.Count > 0)
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, string.Join(";", iosDefines.Concat(missing)));
                changed = true;
            }

            if (changed) AssetDatabase.SaveAssets();
            if (changed || log)
                Debug.Log("[EnglishApp] iOS 설정: 번들 ID " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) +
                          ", 백그라운드 오디오, 자동 서명 → Xcode 에서 Team 만 고르면 된다 (docs/IOS.md)");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string root = report.summary.outputPath;

            string plistPath = Path.Combine(root, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var modes = plist.root["UIBackgroundModes"]?.AsArray() ?? plist.root.CreateArray("UIBackgroundModes");
            if (!modes.values.Any(v => v.AsString() == "audio")) modes.AddString("audio");
            plist.WriteToFile(plistPath);

            string projPath = PBXProject.GetPBXProjectPath(root);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string framework = proj.GetUnityFrameworkTargetGuid();
            proj.AddFrameworkToProject(framework, "AVFoundation.framework", false);
            proj.AddFrameworkToProject(framework, "MediaPlayer.framework", false);
            proj.WriteToFile(projPath);
        }
    }
}
#endif
