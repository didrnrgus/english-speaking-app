using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EnglishApp.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace EnglishApp.EditorTools
{
    /// <summary>
    /// 프로젝트를 처음 열면 자동으로 한 번 실행되는 셋업 (메뉴: Tools > English App > Run Project Setup).
    /// 1. TextMeshPro Essential Resources 가져오기
    /// 2. 잘난체 TMP 폰트 템플릿 만들기 (동적 아틀라스, 외곽선 0.15) → Assets/Resources/Fonts/JalnanGothic SDF.asset
    /// 3. 번들 예문 복사 (레포 content/ → StreamingAssets/content)
    /// 4. Player Settings (세로 고정, 패키지명, minSdk 26, IL2CPP/ARM64) + Main 씬을 빌드 목록에
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string MarkerPath = "ProjectSettings/EnglishAppSetup.txt";
        private const string SetupVersion = "1";
        public const string FontTtfPath = "Assets/Resources/Fonts/JalnanGothicTTF.ttf";
        public const string FontAssetPath = "Assets/Resources/Fonts/JalnanGothic SDF.asset";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string TmpImportRequestedKey = "EnglishApp.TmpImportRequested";

        public const string PackageName = "com.didrnrgus.englishspeaking";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () => Run(false);
        }

        [MenuItem("Tools/English App/Run Project Setup")]
        private static void RunFromMenu() => Run(true);

        [MenuItem("Tools/English App/Rebuild Font Asset")]
        private static void RebuildFontAsset()
        {
            AssetDatabase.DeleteAsset(FontAssetPath);
            CreateFontAsset();
        }

        private static void Run(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += () => Run(force);
                return;
            }

            if (!EnsureTmpEssentials()) return; // 가져오는 중 → 끝나면 다시 실행

            if (!File.Exists(FontAssetPath)) CreateFontAsset(); // 다시 만들려면 Tools > English App > Rebuild Font Asset
            ContentSync.SyncIfMissing();

            if (force || !File.Exists(MarkerPath))
            {
                ApplyPlayerSettings();
                EnsureScene();
                File.WriteAllText(MarkerPath, SetupVersion);
                Debug.Log("[EnglishApp] 프로젝트 셋업 완료 (세로 모드, " + PackageName + ", 폰트, 씬). 다시 하려면 Tools > English App > Run Project Setup");
            }
        }

        // ---- 1. TMP Essential Resources ----------------------------------------------------

        private static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
            if (SessionState.GetBool(TmpImportRequestedKey, false)) return false;
            SessionState.SetBool(TmpImportRequestedKey, true);

            AssetDatabase.importPackageCompleted += OnPackageImported;
            var importer = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_PackageResourceImporter", false))
                .FirstOrDefault(t => t != null);
            var method = importer?.GetMethod("ImportResources", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(bool), typeof(bool), typeof(bool) }, null);
            if (method != null)
            {
                Debug.Log("[EnglishApp] TextMeshPro Essential Resources 를 가져옵니다…");
                method.Invoke(null, new object[] { true, false, false });
            }
            else
            {
                Debug.LogWarning("[EnglishApp] Window > TextMeshPro > Import TMP Essential Resources 를 실행한 뒤 Tools > English App > Run Project Setup 을 눌러 주세요.");
            }
            return false;
        }

        private static void OnPackageImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnPackageImported;
            EditorApplication.delayCall += () => Run(false);
        }

        // ---- 2. 폰트 ------------------------------------------------------------------------

        private static void CreateFontAsset()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (font == null)
            {
                Debug.LogError("[EnglishApp] 폰트 파일이 없습니다: " + FontTtfPath);
                return;
            }

            // 템플릿 아틀라스는 작게 (실제 글리프는 실행 시 만드는 런타임 에셋에 쌓인다 — Fonts.cs)
            var asset = TMP_FontAsset.CreateFontAsset(font, Fonts.SamplingPointSize, Fonts.Padding, GlyphRenderMode.SDFAA,
                256, 256, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogError("[EnglishApp] TMP 폰트 에셋을 만들지 못했습니다.");
                return;
            }
            asset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            Fonts.ApplyOutline(asset.material);

            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            var atlas = asset.atlasTextures != null && asset.atlasTextures.Length > 0 ? asset.atlasTextures[0] : null;
            if (atlas != null)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            // 빌드할 때 에디터에서 쌓인 동적 글리프는 비운다 (있는 버전에서만)
            var so = new SerializedObject(asset);
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null)
            {
                clear.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            Debug.Log("[EnglishApp] 폰트 템플릿 생성: " + FontAssetPath + " (외곽선 " + Fonts.OutlineWidth + ")");
        }

        // ---- 4. Player Settings / 씬 ------------------------------------------------------

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "didrnrgus";
            PlayerSettings.productName = MainScreen.AppTitle;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.runInBackground = true;

            // 세로 모드만
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Android
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.bundleVersionCode = 1;

            AssetDatabase.SaveAssets();
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var active = EditorSceneManager.GetActiveScene();
                if (active.isDirty)
                {
                    Debug.LogWarning("[EnglishApp] 열린 씬에 저장하지 않은 변경이 있어 Main 씬을 만들지 않았습니다. 저장 후 Tools > English App > Run Project Setup.");
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Theme.Bg;
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
