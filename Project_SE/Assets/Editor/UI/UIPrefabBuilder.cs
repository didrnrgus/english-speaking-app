using System;
using System.IO;
using EnglishApp.UI;
using LeTai.Asset.TranslucentImage;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static EnglishApp.EditorTools.UIBuild;

namespace EnglishApp.EditorTools
{
    /// <summary>
    /// UI 프리팹 생성기 (메뉴: Tools > English App > Build UI Prefabs).
    /// 예전에 코드로 만들던 화면을 같은 배치로 프리팹으로 굽는다. 한 번 만든 뒤에는 프리팹이 원본이다 —
    /// 직접 열어서 고치면 되고, 이 메뉴를 다시 누르면 코드 기본값으로 덮어쓴다 (확인 창이 뜸).
    ///
    /// 만드는 것:
    ///   Assets/UI/Sprites/*.png          둥근 사각형·원·아이콘·배경 (이미 있으면 그대로 둠)
    ///   Assets/UI/Materials/Glass.mat    글래스 머티리얼 (Paraform 데모 Light 복사, 이미 있으면 그대로 둠)
    ///   Assets/UI/GlassBlur.asset        배경 블러 설정 (이미 있으면 그대로 둠)
    ///   Assets/UI/Themes/Light|Dark.asset 밝은 / 어두운 모드 팔레트 (이미 있으면 그대로 둠)
    ///   Assets/UI/Prefabs/*.prefab       화면·팝업·반복 항목
    ///   Assets/Resources/UI/UIPrefabs.asset   앱이 쓰는 프리팹 목록
    ///
    /// 배치 모드: Unity -batchmode -quit -projectPath Project_SE -executeMethod EnglishApp.EditorTools.UIPrefabBuilder.Build
    /// </summary>
    public static class UIPrefabBuilder
    {
        public const string Root = "Assets/UI";
        public const string PrefabFolder = Root + "/Prefabs";
        public const string GlassMaterialPath = Root + "/Materials/Glass.mat";
        public const string BlurConfigPath = Root + "/GlassBlur.asset";
        public const string LightPalettePath = Root + "/Themes/Light.asset";
        public const string DarkPalettePath = Root + "/Themes/Dark.asset";
        public const string RegistryPath = "Assets/Resources/" + UIPrefabs.ResourcePath + ".asset";

        private const string DemoGlassMaterial = "Assets/Le Tai's Asset/TranslucentImageParaform/Demo/Materials/Paraform_Demo_Light.mat";
        private const string DefaultGlassMaterial = "Assets/Le Tai's Asset/TranslucentImageParaform/Material/Default-Paraform.mat";

        private const float TopBarHeight = Theme.TopBarHeight;
        private const float Gutter = Theme.Gutter;
        private const float UnitControlsHeight = 490f;

        private static Scene stage;

        [MenuItem("Tools/English App/Build UI Prefabs")]
        private static void BuildFromMenu()
        {
            if (File.Exists(RegistryPath) &&
                !EditorUtility.DisplayDialog("UI 프리팹 만들기",
                    PrefabFolder + " 의 프리팹을 코드 기본값으로 다시 만듭니다.\n프리팹에서 직접 고친 내용은 사라집니다. 계속할까요?",
                    "다시 만들기", "취소"))
                return;
            Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Themes");
            Theme.Init(null, null); // 프리팹은 밝은 모드 색으로 만든다
            Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath));

            SpriteBaker.BakeAll();
            UIBuild.GlassMaterial = EnsureGlassMaterial();
            UIBuild.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectSetup.FontAssetPath);
            Require(UIBuild.Font, "폰트 템플릿 " + ProjectSetup.FontAssetPath + " (Tools > English App > Run Project Setup)");
            var blur = EnsureBlurConfig();
            var lightPalette = EnsurePalette(LightPalettePath, false);
            var darkPalette = EnsurePalette(DarkPalettePath, true);

            stage = EditorSceneManager.NewPreviewScene();
            try
            {
                // 작은 것부터 (큰 프리팹이 중첩해서 쓴다)
                var topBar = Save(BuildTopBar());
                var slider = Save(BuildLabeledSlider());
                var nowPlaying = Save(BuildNowPlayingCard());
                var levelButton = Save(BuildLevelButton());
                var unitCard = Save(BuildUnitCard());
                var chip = Save(BuildSequenceChip());
                var turnA = Save(BuildDialogueTurn(true));
                var turnB = Save(BuildDialogueTurn(false));
                var speakingPage = Save(BuildSpeakingPage());
                var dialoguePage = Save(BuildDialoguePage(turnA, turnB));
                var nodeRow = Save(BuildSequenceNodeRow(slider));

                var mainScreen = Save(BuildMainScreen(levelButton));
                var levelScreen = Save(BuildLevelScreen(topBar, unitCard));
                var unitScreen = Save(BuildUnitScreen(topBar, speakingPage, dialoguePage, chip));
                var settings = Save(BuildSettingsPopup(slider, nowPlaying, nodeRow));
                var appCanvas = Save(BuildAppCanvas());

                var registry = AssetDatabase.LoadAssetAtPath<UIPrefabs>(RegistryPath);
                if (registry == null)
                {
                    registry = ScriptableObject.CreateInstance<UIPrefabs>();
                    AssetDatabase.CreateAsset(registry, RegistryPath);
                }
                registry.appCanvas = appCanvas;
                registry.screens = new BaseScreen[] { mainScreen, levelScreen, unitScreen };
                registry.settingsPopup = settings;
                registry.blurConfig = blur;
                registry.lightPalette = lightPalette;
                registry.darkPalette = darkPalette;
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
                Debug.Log("[UI] UI 프리팹을 만들었습니다 → " + PrefabFolder + " (목록: " + RegistryPath + ")");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        // ---- 에셋 ---------------------------------------------------------------------------

        private static Material EnsureGlassMaterial()
        {
            if (!File.Exists(GlassMaterialPath))
            {
                var src = File.Exists(DemoGlassMaterial) ? DemoGlassMaterial : DefaultGlassMaterial;
                if (!AssetDatabase.CopyAsset(src, GlassMaterialPath))
                    throw new Exception("[UI] 글래스 머티리얼을 복사하지 못했습니다: " + src);
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            Require(mat, GlassMaterialPath);
            return mat;
        }

        private static BlurConfig EnsureBlurConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScalableBlurConfig>(BlurConfigPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<ScalableBlurConfig>();
            config.Mode = ScalableBlurConfig.BlurMode.Performance;
            config.ReferenceResolution = new Vector2(1080, 1920);
            config.MatchWidthOrHeight = 0f;
            config.Strength = 12f;
            AssetDatabase.CreateAsset(config, BlurConfigPath);
            return config;
        }

        /// <summary>화면 모드 팔레트. 밝은 모드의 글래스 톤은 GlassStyle 프리셋과 같게 둔다.</summary>
        private static ThemePalette EnsurePalette(string path, bool dark)
        {
            var palette = AssetDatabase.LoadAssetAtPath<ThemePalette>(path);
            if (palette != null) return palette;
            palette = ScriptableObject.CreateInstance<ThemePalette>();
            if (dark)
            {
                palette.colors = ThemePalette.DarkColors();
                palette.background = SpriteBaker.BackgroundDark;
                // 글래스 색은 배경에 곱해진다 → 어두운 유리는 틴트를 밝게 두고 밝기(음수)로 어둡게
                palette.glassCard = new GlassTone(Theme.Hex("#D6CFEA"), 0.8f, -0.45f);
                palette.glassClear = new GlassTone(Theme.Hex("#E2DCF2"), 0.7f, -0.35f);
                palette.glassPanel = new GlassTone(Theme.Hex("#CFC8E4"), 0.88f, -0.62f);
            }
            else
            {
                palette.colors = ThemePalette.LightColors();
                palette.background = SpriteBaker.Background;
                palette.glassCard = Tone(palette.colors.surface, GlassStyle.Card);
                palette.glassClear = Tone(Color.white, GlassStyle.Clear);
                palette.glassPanel = Tone(palette.colors.bg, GlassStyle.Panel);
            }
            AssetDatabase.CreateAsset(palette, path);
            return palette;
        }

        private static GlassTone Tone(Color color, GlassStyle style) => new GlassTone(color, style.opacity, style.brightness);

        /// <summary>임시 씬에 프리팹 루트를 만든다.</summary>
        private static RectTransform NewRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UIKit.UILayer };
            SceneManager.MoveGameObjectToScene(go, stage);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1080, 1920);
            return rt;
        }

        private static T Save<T>(T component) where T : Component
        {
            var go = component.gameObject;
            var path = PrefabFolder + "/" + go.name + ".prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
            UnityEngine.Object.DestroyImmediate(go);
            if (!ok || asset == null) throw new Exception("[UI] 프리팹 저장 실패: " + path);
            return asset.GetComponent<T>();
        }

        private static void SetLabel(LabeledSlider slider, string label)
        {
            var text = slider.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            text.text = label;
            RecordOverride(text);
            slider.name = "Slider_" + label;
            RecordOverride(slider.gameObject);
        }

        // ---- 공용 조각 ----------------------------------------------------------------------

        /// <summary>위쪽 바: [뒤로] 제목 [설정].</summary>
        private static TopBar BuildTopBar()
        {
            var bar = NewRoot("TopBar").TopStrip(TopBarHeight);
            var view = bar.gameObject.AddComponent<TopBar>();

            view.backButton = IconButton(bar, "Back", IconKind.Back, Theme.Text, Color.white, 112, 52, GlassStyle.Clear, out _);
            ((RectTransform)view.backButton.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(112, 112));
            view.settingsButton = IconButton(bar, "Settings", IconKind.Gear, Theme.Text, Color.white, 112, 64, GlassStyle.Clear, out _);
            ((RectTransform)view.settingsButton.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(112, 112));

            view.title = Text(bar, "Title", "제목", 50, Theme.Text).SingleLine().AutoSize(32, 50);
            view.title.rectTransform.Fill(150, 10, 150, 10);
            return view;
        }

        /// <summary>"라벨 [━━━●━━] 값" 한 줄 슬라이더.</summary>
        private static LabeledSlider BuildLabeledSlider()
        {
            const float handleSize = 60f;
            var row = NewRoot("LabeledSlider");
            Layout(row, preferredHeight: 96, minHeight: 96);
            HLayout(row, 20, Pad(0, 0, 0, 0), TextAnchor.MiddleCenter, expandHeight: false);
            var view = row.gameObject.AddComponent<LabeledSlider>();

            var label = Text(row, "Label", "음량", 34, Theme.Text, TextAlignmentOptions.MidlineLeft).SingleLine();
            Layout(label, preferredHeight: 60, preferredWidth: 130);

            var sRoot = Rect("Slider", row);
            Layout(sRoot, preferredHeight: 80, flexibleWidth: 1);
            var slider = sRoot.gameObject.AddComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;

            var track = Rounded(sRoot, "Track", Theme.Track, 10);
            track.raycastTarget = true;
            Band(track.rectTransform, 20, 0);

            var fillArea = Rect("FillArea", sRoot);
            Band(fillArea, 20, handleSize * 0.5f);
            var fill = Rounded(fillArea, "Fill", Theme.Primary, 10);
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = Vector2.zero;

            // Slider 는 손잡이를 손잡이 영역 높이만큼 세로로 늘린다 → 영역을 손잡이 크기의 띠로 둬서 동그랗게
            var handleArea = Rect("HandleArea", sRoot);
            Band(handleArea, handleSize, handleSize * 0.5f);
            var handle = Circle(handleArea, "Handle", Theme.Primary);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(handleSize, 0);
            AddShadow(handle, 4);
            var dot = Circle(handle.transform, "Dot", Color.white);
            dot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;

            view.valueText = Text(row, "Value", "80%", 34, Theme.Text, TextAlignmentOptions.MidlineRight).SingleLine();
            Layout(view.valueText, preferredHeight: 60, preferredWidth: 140);

            view.slider = slider;
            view.accentGraphics = new Graphic[] { fill, handle };
            return view;
        }

        private static void Band(RectTransform rt, float height, float inset)
        {
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, -height * 0.5f);
            rt.offsetMax = new Vector2(-inset, height * 0.5f);
        }

        /// <summary>설정 팝업 맨 위 '지금 재생 중' 카드.</summary>
        private static NowPlayingCard BuildNowPlayingCard()
        {
            var card = GlassRoot("NowPlayingCard", Theme.Text, 36, GlassStyle.Dark);
            Layout(card, preferredHeight: 120);
            HLayout(card, 20, Pad(36, 20, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);
            var view = card.gameObject.AddComponent<NowPlayingCard>();
            view.text = Text(card.transform, "Text", "전체 듣기 · S1-U01 · 1/5 · 1번 영어", 30, Theme.TextOnColor, TextAlignmentOptions.MidlineLeft)
                .SingleLine().AutoSize(22, 30);
            Layout(view.text, preferredHeight: 90, flexibleWidth: 1);
            view.button = IconButton(card.transform, "PlayPause", IconKind.Pause, Theme.TextOnColor, Theme.Primary, 88, 40, GlassStyle.Tinted, out view.icon);
            Layout(view.button, preferredHeight: 88, preferredWidth: 88);
            view.playSprite = SpriteBaker.Icon(IconKind.Play);
            view.pauseSprite = SpriteBaker.Icon(IconKind.Pause);
            return view;
        }

        /// <summary>글래스 판을 프리팹 루트로 만든다 (Glass 는 부모 아래에 만들므로 루트로 올림).</summary>
        private static TranslucentImage GlassRoot(string name, Color color, float radius, GlassStyle style)
        {
            var temp = NewRoot("_temp");
            var glass = Glass(temp, name, color, radius, style);
            glass.transform.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(glass.gameObject, stage);
            UnityEngine.Object.DestroyImmediate(temp.gameObject);
            return glass;
        }

        // ---- 메인 화면 ----------------------------------------------------------------------

        /// <summary>레벨 버튼: [코드 배지] 제목 / 설명 [▶ 레벨 전체 듣기]. 색은 LevelButton 의 문장·대화 색으로 칠한다.</summary>
        private static LevelButton BuildLevelButton()
        {
            var bg = GlassRoot("LevelButton", Theme.Speaking, 40, GlassStyle.Tinted);
            var b = MakeButton(bg);
            Layout(b, preferredHeight: 156, minHeight: 156);
            HLayout(b, 18, Pad(22, 20, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);
            var view = bg.gameObject.AddComponent<LevelButton>();
            view.button = b;
            view.speakingColor = Theme.Speaking;
            view.dialogueColor = Theme.Dialogue;

            var badge = Circle(b.transform, "Badge", new Color(1, 1, 1, 0.28f));
            Layout(badge, preferredHeight: 96, preferredWidth: 96);
            view.code = Text(badge.transform, "Code", "S1", 38, Theme.TextOnColor);
            view.code.rectTransform.Fill();

            var texts = Rect("Texts", b.transform);
            Layout(texts, preferredHeight: 120, flexibleWidth: 1);
            view.title = Text(texts, "Title", "레벨 제목", 40, Theme.TextOnColor, TextAlignmentOptions.BottomLeft).SingleLine().AutoSize(26, 40);
            view.title.rectTransform.Fill(0, 6, 0, 60);
            view.desc = Text(texts, "Desc", "레벨 설명", 28, new Color(1, 1, 1, 0.9f), TextAlignmentOptions.TopLeft).SingleLine().AutoSize(20, 28);
            view.desc.rectTransform.Fill(0, 66, 0, 4);

            // 오른쪽 ▶ : 레벨 전체 듣기 (카드 본체를 누르면 레벨 화면)
            view.playButton = IconButton(b.transform, "PlayLevel", IconKind.Play, Theme.TextOnColor, new Color(1, 1, 1, 0.28f), 96, 40, GlassStyle.Clear, out view.playIcon);
            Layout(view.playButton, preferredHeight: 96, preferredWidth: 96);
            view.playSprite = SpriteBaker.Icon(IconKind.Play);
            view.pauseSprite = SpriteBaker.Icon(IconKind.Pause);
            view.stopSprite = SpriteBaker.Icon(IconKind.Stop);
            return view;
        }

        private static MainScreen BuildMainScreen(LevelButton levelButton)
        {
            var root = NewRoot("MainScreen");
            var view = root.gameObject.AddComponent<MainScreen>();
            view.levelButtonPrefab = levelButton;

            var title = Text(root, "Title", MainScreen.AppTitle, 96, Theme.Primary).SingleLine();
            title.rectTransform.TopStrip(130, 80, 150, 150);
            var subtitle = Text(root, "Subtitle", "듣고 · 따라 말하고 · 화면 끄고도 듣기", 34, Theme.TextSub).SingleLine().AutoSize(24, 34);
            subtitle.rectTransform.TopStrip(56, 222, 60, 60);

            view.settingsButton = IconButton(root, "Settings", IconKind.Gear, Theme.Text, Color.white, 112, 64, GlassStyle.Clear, out _);
            ((RectTransform)view.settingsButton.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(112, 112));
            view.sunSprite = SpriteBaker.Icon(IconKind.Sun);
            view.moonSprite = SpriteBaker.Icon(IconKind.Moon);

            // 2열 블록: 화면 높이의 66% 지점부터 아래로
            var columnsArea = Rect("Columns", root);
            columnsArea.anchorMin = new Vector2(0, 0);
            columnsArea.anchorMax = new Vector2(1, 0.66f);
            columnsArea.offsetMin = Vector2.zero;
            columnsArea.offsetMax = Vector2.zero;

            view.loadingText = Text(columnsArea, "Loading", "불러오는 중…", 40, Theme.TextSub);
            view.loadingText.rectTransform.TopStrip(120, 40);
            view.errorText = Text(columnsArea, "Error", "예문 목록을 불러오지 못했어요.\n인터넷 연결을 확인해 주세요.", 38, Theme.TextSub);
            view.errorText.rectTransform.TopStrip(200, 40, 60, 60);
            view.errorText.gameObject.SetActive(false);

            // 화면이 짧은 기기에서도 잘리지 않도록 스크롤 안에 둔다
            var scroll = VScroll(columnsArea, "Scroll", out var content, 0, Pad(44, 44, 0, 220));
            ((RectTransform)scroll.transform).Fill();
            scroll.movementType = ScrollRect.MovementType.Clamped;
            view.columns = scroll.gameObject;

            var row = Rect("Row", content);
            var hl = HLayout(row, 36, null, TextAnchor.UpperCenter, expandHeight: false);
            hl.childForceExpandWidth = true;

            view.speakingColumn = BuildColumn(row, "문장", "한 문장씩 말하기", Theme.Speaking);
            view.dialogueColumn = BuildColumn(row, "대화", "A · B 대화하기", Theme.Dialogue);

            // 오른쪽 아래 해/달 버튼 (밝은 / 어두운 모드). 목록 위에 떠 있도록 마지막에 만든다
            view.themeButton = IconButton(root, "ThemeToggle", IconKind.Moon, Theme.Text, Color.white, 120, 60, GlassStyle.Clear, out view.themeIcon);
            ((RectTransform)view.themeButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 40), new Vector2(120, 120));
            return view;
        }

        private static RectTransform BuildColumn(Transform parent, string header, string sub, Color color)
        {
            var col = Rect("Column_" + header, parent);
            VLayout(col, 22);
            Layout(col, flexibleWidth: 1);

            var head = Rect("Header", col);
            Layout(head, preferredHeight: 120);
            var h1 = Text(head, "Title", header, 56, color).SingleLine();
            h1.rectTransform.TopStrip(70);
            var h2 = Text(head, "Sub", sub, 28, Theme.TextSub).SingleLine().AutoSize(20, 28);
            h2.rectTransform.TopStrip(40, 76);
            return col;
        }

        // ---- 레벨 화면 ----------------------------------------------------------------------

        /// <summary>유닛 카드: [번호 배지] 제목 / 상태 [›].</summary>
        private static UnitCard BuildUnitCard()
        {
            var bg = GlassRoot("UnitCard", Theme.Surface, 36, GlassStyle.Card);
            var card = MakeButton(bg);
            Layout(card, preferredHeight: 150, minHeight: 150);
            HLayout(card, 24, Pad(26, 28, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);
            var view = bg.gameObject.AddComponent<UnitCard>();
            view.button = card;
            view.speakingColor = Theme.Speaking;
            view.dialogueColor = Theme.Dialogue;

            view.badge = Circle(card.transform, "Badge", Theme.Speaking);
            Layout(view.badge, preferredHeight: 92, preferredWidth: 92);
            view.number = Text(view.badge.transform, "Num", "01", 36, Theme.TextOnColor);
            view.number.rectTransform.Fill();

            var texts = Rect("Texts", card.transform);
            Layout(texts, preferredHeight: 120, flexibleWidth: 1);
            view.title = Text(texts, "Title", "유닛 제목", 42, Theme.Text, TextAlignmentOptions.BottomLeft).SingleLine().AutoSize(28, 42);
            view.title.rectTransform.Fill(0, 4, 0, 56);
            view.status = Text(texts, "Status", "예문 5개", 28, Theme.TextSub, TextAlignmentOptions.TopLeft).SingleLine();
            view.status.rectTransform.Fill(0, 70, 0, 4);

            var chevron = Icon(card.transform, IconKind.ChevronRight, Theme.TextSub, 40);
            Layout(chevron, preferredHeight: 40, preferredWidth: 40);
            return view;
        }

        private static LevelScreen BuildLevelScreen(TopBar topBar, UnitCard unitCard)
        {
            var root = NewRoot("LevelScreen");
            var view = root.gameObject.AddComponent<LevelScreen>();
            view.topBar = Nest(topBar, root);
            view.unitCardPrefab = unitCard;

            var scroll = VScroll(root, "Units", out var content, 22, Pad(40, 40, 16, 240));
            ((RectTransform)scroll.transform).Fill(0, TopBarHeight, 0, 0);
            view.list = content;

            view.descText = Text(content, "Desc", "레벨 설명 · 유닛을 골라 주세요", 32, Theme.TextSub, TextAlignmentOptions.MidlineLeft).SingleLine();
            Layout(view.descText, preferredHeight: 60);

            // 레벨 전체 듣기: [▶] S1 전체 듣기 / U05 가족부터 이어서 · 유닛 10개
            view.levelPlayButton = GlassButton(content, "PlayLevel", Theme.Primary, 40, GlassStyle.Tinted);
            Layout(view.levelPlayButton, preferredHeight: 150, minHeight: 150);
            HLayout(view.levelPlayButton, 24, Pad(30, 30, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);
            var iconBg = Circle(view.levelPlayButton.transform, "IconBg", new Color(1, 1, 1, 0.25f));
            Layout(iconBg, preferredHeight: 92, preferredWidth: 92);
            view.levelPlayIcon = Image(iconBg.transform, "Icon", Theme.TextOnColor, SpriteBaker.Icon(IconKind.Play));
            view.levelPlayIcon.preserveAspect = true;
            ((RectTransform)view.levelPlayIcon.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            var texts = Rect("Texts", view.levelPlayButton.transform);
            Layout(texts, preferredHeight: 120, flexibleWidth: 1);
            view.levelPlayLabel = Text(texts, "Label", "S1 전체 듣기", 42, Theme.TextOnColor, TextAlignmentOptions.BottomLeft).SingleLine().AutoSize(28, 42);
            view.levelPlayLabel.rectTransform.Fill(0, 4, 0, 56);
            view.levelPlaySub = Text(texts, "Sub", "유닛 10개를 처음부터 이어서", 28, new Color(1, 1, 1, 0.9f), TextAlignmentOptions.TopLeft).SingleLine().AutoSize(20, 28);
            view.levelPlaySub.rectTransform.Fill(0, 70, 0, 4);
            view.playSprite = SpriteBaker.Icon(IconKind.Play);
            view.pauseSprite = SpriteBaker.Icon(IconKind.Pause);
            view.stopSprite = SpriteBaker.Icon(IconKind.Stop);
            return view;
        }

        // ---- 유닛 화면 ----------------------------------------------------------------------

        /// <summary>문장 글자 — 누르면 그 언어로 개별 듣기.</summary>
        private static TextMeshProUGUI TappableText(Transform parent, string text, float size, Color color, TextAlignmentOptions align, out Button button)
        {
            var hit = Image(parent, "Tap", Color.clear);
            button = MakeButton(hit, 0.98f);
            VLayout(hit, 0);
            return Text(hit.transform, "Text", text, size, color, align);
        }

        /// <summary>[▶ 영어] [▶ 한국어].</summary>
        private static void LineButtons(Transform parent, SentenceLine line, float height, float width, TextAnchor align)
        {
            var row = Rect("Buttons", parent);
            // flexibleHeight 0: 가로 레이아웃은 자기를 늘어나는 요소로 알리므로, 빈 세로 공간을 가져가지 않게 막는다
            Layout(row, preferredHeight: height, minHeight: height, flexibleHeight: 0);
            HLayout(row, 20, null, align, expandHeight: true);
            float font = height * 0.4f;
            line.playEn = TextButton(row, "PlayEn", "▶ 영어", font, Theme.En, Theme.TextOnColor, height * 0.5f, GlassStyle.Tinted, out _);
            Layout(line.playEn, preferredWidth: width);
            line.playKo = TextButton(row, "PlayKo", "▶ 한국어", font, Theme.Ko, Theme.TextOnColor, height * 0.5f, GlassStyle.Tinted, out _);
            Layout(line.playKo, preferredWidth: width);
        }

        /// <summary>문장 페이지의 정사각형 [▶ 영어] [▶ 한국어] 타일 (큰 ▶ 아이콘 + 라벨).</summary>
        private static void SquareButtons(Transform parent, SentenceLine line, float size)
        {
            var row = Rect("Buttons", parent);
            Layout(row, preferredHeight: size, minHeight: size, flexibleHeight: 0);
            HLayout(row, 48, null, TextAnchor.MiddleCenter, expandHeight: false);
            line.playEn = SquareButton(row, "PlayEn", "영어", Theme.En, size);
            line.playKo = SquareButton(row, "PlayKo", "한국어", Theme.Ko, size);
        }

        private static Button SquareButton(Transform parent, string name, string label, Color color, float size)
        {
            var b = GlassButton(parent, name, color, size * 0.2f, GlassStyle.Tinted);
            Layout(b, preferredHeight: size, preferredWidth: size, minHeight: size, minWidth: size);
            VLayout(b, 10, Pad(16, 16, 0, 0), TextAnchor.MiddleCenter, expandWidth: false);
            var icon = Image(b.transform, "Icon", Theme.TextOnColor, SpriteBaker.Icon(IconKind.Play));
            icon.preserveAspect = true;
            Layout(icon, preferredHeight: size * 0.36f, preferredWidth: size * 0.36f);
            var text = Text(b.transform, "Label", label, 42, Theme.TextOnColor).SingleLine().AutoSize(28, 42);
            Layout(text, preferredHeight: 56, preferredWidth: size - 32);
            return b;
        }

        /// <summary>문장(말하기) 페이지: 카드 가운데 문장 하나 + 정사각형 ▶ 영어 / ▶ 한국어.</summary>
        private static SpeakingPage BuildSpeakingPage()
        {
            var page = NewRoot("SpeakingPage");
            var view = page.gameObject.AddComponent<SpeakingPage>();

            var card = Glass(page, "Card", Theme.Surface, 48, GlassStyle.Card);
            card.rectTransform.Fill(Gutter, 24, Gutter, 24);

            var scroll = VScroll(card.transform, "Scroll", out var content, 64, Pad(40, 40, 56, 56), directional: true);
            ((RectTransform)scroll.transform).Fill();
            content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            content.gameObject.AddComponent<FillViewportHeight>().viewport = (RectTransform)scroll.transform;
            view.scroll = scroll;

            // 재생 중이면 강조 색으로 바뀌는 배경 (평소엔 투명)
            var lineBg = Rounded(content, "Line", new Color(1, 1, 1, 0), 36);
            VLayout(lineBg, 24, Pad(28, 28, 36, 36));
            var line = lineBg.gameObject.AddComponent<SentenceLine>();
            line.background = lineBg;
            line.followTarget = lineBg.rectTransform;
            line.en = TappableText(lineBg.transform, "How are you today?", 72, Theme.Text, TextAlignmentOptions.Center, out line.enTap);
            var divider = Image(lineBg.transform, "Divider", Theme.Line);
            Layout(divider, preferredHeight: 4, minHeight: 4);
            line.ko = TappableText(lineBg.transform, "오늘 기분 어때요?", 46, Theme.TextSub, TextAlignmentOptions.Center, out line.koTap);
            view.line = line;

            SquareButtons(content, line, 240);
            return view;
        }

        /// <summary>대화 대사 한 줄: A 는 [배지][말풍선], B 는 [말풍선][배지].</summary>
        private static SentenceLine BuildDialogueTurn(bool isA)
        {
            var row = NewRoot(isA ? "DialogueTurnA" : "DialogueTurnB");
            HLayout(row, 16, null, isA ? TextAnchor.UpperLeft : TextAnchor.UpperRight, expandHeight: false);
            var line = row.gameObject.AddComponent<SentenceLine>();
            line.followTarget = row;

            if (isA) line.speaker = SpeakerBadge(row, "A", Theme.SpeakerA);

            var bubble = Glass(row, "Bubble", Theme.Surface, 36, GlassStyle.Card);
            line.background = bubble;
            Layout(bubble, flexibleWidth: 1);
            VLayout(bubble, 10, Pad(28, 28, 22, 22));
            line.en = TappableText(bubble.transform, "Hi! How was your weekend?", 40, Theme.Text, TextAlignmentOptions.TopLeft, out line.enTap);
            line.ko = TappableText(bubble.transform, "안녕! 주말 어땠어?", 32, Theme.TextSub, TextAlignmentOptions.TopLeft, out line.koTap);
            LineButtons(bubble.transform, line, 68, 170, TextAnchor.MiddleLeft);

            if (!isA) line.speaker = SpeakerBadge(row, "B", Theme.SpeakerB);
            return line;
        }

        private static TextMeshProUGUI SpeakerBadge(Transform parent, string speaker, Color color)
        {
            var badge = Circle(parent, "Speaker", color);
            Layout(badge, preferredHeight: 84, preferredWidth: 84, minHeight: 84, minWidth: 84);
            var t = Text(badge.transform, "Text", speaker, 40, Theme.TextOnColor);
            t.rectTransform.Fill();
            return t;
        }

        /// <summary>대화 페이지: 주제 + 말풍선 목록.</summary>
        private static DialoguePage BuildDialoguePage(SentenceLine turnA, SentenceLine turnB)
        {
            var page = NewRoot("DialoguePage");
            var view = page.gameObject.AddComponent<DialoguePage>();
            view.turnAPrefab = turnA;
            view.turnBPrefab = turnB;

            view.topic = Text(page, "Topic", "대화", 46, Theme.Dialogue).SingleLine();
            view.topic.rectTransform.TopStrip(84, 12, Gutter, Gutter);

            var scroll = VScroll(page, "Turns", out var content, 22, Pad(32, 32, 8, 40), directional: true);
            ((RectTransform)scroll.transform).Fill(0, 104, 0, 0);
            view.scroll = scroll;
            view.turns = content;
            return view;
        }

        /// <summary>'듣기 순서' 칩 하나 (영 / 한).</summary>
        private static SequenceChip BuildSequenceChip()
        {
            var bg = GlassRoot("SequenceChip", Theme.Surface, 32, GlassStyle.Card);
            var view = bg.gameObject.AddComponent<SequenceChip>();
            view.button = MakeButton(bg);
            Layout(bg, preferredHeight: 64, preferredWidth: 64);
            view.text = Text(bg.transform, "Text", "영", 30, Theme.En);
            view.text.rectTransform.Fill();
            return view;
        }

        private static UnitScreen BuildUnitScreen(TopBar topBar, SpeakingPage speakingPage, DialoguePage dialoguePage, SequenceChip chip)
        {
            var root = NewRoot("UnitScreen");
            var view = root.gameObject.AddComponent<UnitScreen>();
            view.topBar = Nest(topBar, root);
            view.speakingPagePrefab = speakingPage;
            view.dialoguePagePrefab = dialoguePage;
            view.chipPrefab = chip;
            view.playSprite = SpriteBaker.Icon(IconKind.Play);
            view.pauseSprite = SpriteBaker.Icon(IconKind.Pause);
            view.stopSprite = SpriteBaker.Icon(IconKind.Stop);

            view.centerText = Text(root, "Center", "불러오는 중…", 40, Theme.TextSub);
            view.centerText.rectTransform.Fill(60, 500, 60, 600);
            view.retryButton = TextButton(root, "Retry", "다시 시도", 40, Theme.Primary, Theme.TextOnColor, 50, GlassStyle.Tinted, out _);
            ((RectTransform)view.retryButton.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(360, 110));
            view.retryButton.gameObject.SetActive(false);

            // 페이지 (좌우로 넘김)
            var pagerRt = Rect("Pager", root).Fill(0, TopBarHeight, 0, UnitControlsHeight);
            var hit = pagerRt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            pagerRt.gameObject.AddComponent<RectMask2D>();
            var pager = pagerRt.gameObject.AddComponent<PagerView>();
            pager.viewport = pagerRt;
            pager.strip = Rect("Strip", pagerRt);
            pager.strip.anchorMin = Vector2.zero;
            pager.strip.anchorMax = Vector2.one;
            pager.strip.pivot = new Vector2(0, 0.5f);
            pager.strip.offsetMin = Vector2.zero;
            pager.strip.offsetMax = Vector2.zero;
            view.pager = pager;

            // 아래 컨트롤
            var controls = Rect("Controls", root).BottomStrip(UnitControlsHeight);
            VLayout(controls, 18, Pad(40, 40, 6, 36));
            view.controls = controls.gameObject;

            // 지금 어떤 전체 듣기인지 (S1 전체 듣기 중 · 유닛 3/10 / 유닛 전체 듣기 중). 비어 있어도 자리는 유지
            view.scopeText = Text(controls, "Scope", "", 32, Theme.TextSub).SingleLine().AutoSize(22, 32);
            Layout(view.scopeText, preferredHeight: 44);

            // 페이지 이동
            var nav = Rect("PageNav", controls);
            Layout(nav, preferredHeight: 100);
            HLayout(nav, 40, null, TextAnchor.MiddleCenter, expandHeight: false);
            view.prevButton = IconButton(nav, "Prev", IconKind.ChevronLeft, Theme.Text, Theme.Surface, 100, 40, GlassStyle.Card, out _);
            Layout(view.prevButton, preferredHeight: 100, preferredWidth: 100);
            view.pageText = Text(nav, "Page", "1 / 5", 44, Theme.Text).SingleLine();
            Layout(view.pageText, preferredHeight: 80, preferredWidth: 220);
            view.nextButton = IconButton(nav, "Next", IconKind.ChevronRight, Theme.Text, Theme.Surface, 100, 40, GlassStyle.Card, out _);
            Layout(view.nextButton, preferredHeight: 100, preferredWidth: 100);

            // 듣기 순서 (눌러서 설정 > 전체 듣기). 칩은 '듣기 순서' 글자 뒤에 붙는다
            view.chipsRow = Rect("Sequence", controls);
            Layout(view.chipsRow, preferredHeight: 72);
            HLayout(view.chipsRow, 10, null, TextAnchor.MiddleCenter, expandHeight: false);
            var seqLabel = Text(view.chipsRow, "Label", "듣기 순서", 28, Theme.TextSub).SingleLine();
            Layout(seqLabel, preferredHeight: 60, preferredWidth: 140);

            // 재생 버튼
            var transport = Rect("Transport", controls);
            Layout(transport, preferredHeight: 150);
            HLayout(transport, 28, null, TextAnchor.MiddleCenter, expandHeight: false);

            view.stopButton = IconButton(transport, "Stop", IconKind.Stop, Theme.Text, Theme.Surface, 120, 44, GlassStyle.Card, out _);
            Layout(view.stopButton, preferredHeight: 120, preferredWidth: 120);

            view.playButton = GlassButton(transport, "PlayAll", Theme.Primary, 70, GlassStyle.Tinted);
            Layout(view.playButton, preferredHeight: 140, preferredWidth: 540);
            HLayout(view.playButton, 18, Pad(36, 36, 0, 0), TextAnchor.MiddleCenter, expandHeight: false);
            view.playIcon = Image(view.playButton.transform, "Icon", Theme.TextOnColor, SpriteBaker.Icon(IconKind.Play));
            view.playIcon.preserveAspect = true;
            Layout(view.playIcon, preferredHeight: 56, preferredWidth: 56);
            view.playLabel = Text(view.playButton.transform, "Label", "전체 듣기", 46, Theme.TextOnColor).SingleLine().AutoSize(30, 46);
            Layout(view.playLabel, preferredHeight: 100, preferredWidth: 360);

            view.repeatButton = IconButton(transport, "Repeat", IconKind.Repeat, Theme.TextSub, Theme.Surface, 120, 56, GlassStyle.Card, out view.repeatIcon);
            Layout(view.repeatButton, preferredHeight: 120, preferredWidth: 120);
            return view;
        }

        // ---- 설정 팝업 ----------------------------------------------------------------------

        /// <summary>듣기 시퀀스의 노드 한 줄: [≡] [번호] [영어/한국어] [요약] [펼치기] [삭제] + 펼치면 슬라이더 3개.</summary>
        private static SequenceNodeRow BuildSequenceNodeRow(LabeledSlider slider)
        {
            const float headerHeight = 124f;
            var bg = GlassRoot("SequenceNodeRow", Theme.Surface, 28, GlassStyle.Card);
            VLayout(bg, 0, Pad(0, 0, 0, 0));
            var view = bg.gameObject.AddComponent<SequenceNodeRow>();
            view.background = bg;
            view.chevronDown = SpriteBaker.Icon(IconKind.ChevronDown);
            view.chevronUp = SpriteBaker.Icon(IconKind.ChevronUp);

            // ---- 머리줄 ----
            var header = Rect("Header", bg.transform);
            Layout(header, preferredHeight: headerHeight, minHeight: headerHeight);
            HLayout(header, 12, Pad(8, 16, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);

            // 손잡이 (여기를 끌어서 순서 변경)
            var handle = Image(header, "DragHandle", Color.clear);
            handle.raycastTarget = true;
            Layout(handle, preferredHeight: headerHeight, preferredWidth: 84);
            view.dragHandle = handle.gameObject.AddComponent<SequenceDragHandle>();
            Icon(handle.transform, IconKind.Drag, Theme.TextSub, 48);

            // 번호
            var num = Circle(header, "Index", Theme.Line);
            Layout(num, preferredHeight: 60, preferredWidth: 60);
            view.indexText = Text(num.transform, "Text", "1", 30, Theme.Text);
            view.indexText.rectTransform.Fill();

            // 언어 (누르면 영어 ↔ 한국어)
            view.langButton = TextButton(header, "Lang", "영어", 34, Theme.En, Theme.TextOnColor, 40, GlassStyle.Tinted, out view.langText);
            Layout(view.langButton, preferredHeight: 80, preferredWidth: 180);

            // 요약 (눌러서 펼치기)
            var summary = Image(header, "Summary", Color.clear);
            view.summaryButton = PlainButton(summary);
            Layout(summary, preferredHeight: 90, flexibleWidth: 1);
            view.summaryText = Text(summary.transform, "Text", "100%  ·  x1.0  ·  텀 0.5초", 28, Theme.TextSub, TextAlignmentOptions.MidlineLeft)
                .SingleLine().AutoSize(20, 28);
            view.summaryText.rectTransform.Fill(8, 0, 0, 0);

            view.expandButton = FlatIconButton(header, "Expand", IconKind.ChevronDown, Theme.TextSub, 76, 36, out view.expandIcon);
            Layout(view.expandButton, preferredHeight: 76, preferredWidth: 76);
            view.deleteButton = FlatIconButton(header, "Delete", IconKind.Close, Theme.Danger, 76, 32, out _);
            Layout(view.deleteButton, preferredHeight: 76, preferredWidth: 76);

            // ---- 펼친 영역: 슬라이더 3개 ----
            var details = Rect("Details", bg.transform);
            VLayout(details, 0, Pad(36, 36, 0, 24));
            view.details = details.gameObject;
            view.volumeSlider = Nest(slider, details);
            SetLabel(view.volumeSlider, "음량");
            view.speedSlider = Nest(slider, details);
            SetLabel(view.speedSlider, "빠르기");
            view.gapSlider = Nest(slider, details);
            SetLabel(view.gapSlider, "텀");
            return view;
        }

        private static SettingsPopup BuildSettingsPopup(LabeledSlider slider, NowPlayingCard nowPlaying, SequenceNodeRow nodeRow)
        {
            var root = NewRoot("SettingsPopup");
            var view = root.gameObject.AddComponent<SettingsPopup>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();

            // 바깥 (누르면 닫힘)
            var dim = Image(root, "Dim", Theme.Dim);
            dim.rectTransform.Fill();
            view.dimButton = PlainButton(dim);

            var safe = Rect("SafeArea", root).Fill();
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var panel = Glass(safe, "Panel", Theme.Bg, 48, GlassStyle.Panel);
            panel.rectTransform.Fill(24, 60, 24, 30);
            panel.raycastTarget = true;

            // 머리: 제목 + 닫기
            var header = Rect("Header", panel.transform).TopStrip(130);
            var title = Text(header, "Title", "설정", 58, Theme.Text, TextAlignmentOptions.MidlineLeft).SingleLine();
            title.rectTransform.Fill(48, 0, 150, 0);
            view.closeButton = IconButton(header, "Close", IconKind.Close, Theme.Text, Color.white, 112, 46, GlassStyle.Clear, out _);
            ((RectTransform)view.closeButton.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(112, 112));

            // 탭
            var tabs = Rounded(panel.transform, "Tabs", Theme.Track, 50);
            tabs.rectTransform.TopStrip(104, 130, 36, 36);
            var tabsLayout = HLayout(tabs, 8, Pad(8), TextAnchor.MiddleCenter, expandHeight: true);
            tabsLayout.childForceExpandWidth = true;
            string[] tabNames = { "개별 듣기", "전체 듣기" };
            view.tabButtons = new Button[2];
            view.tabTexts = new TextMeshProUGUI[2];
            for (int i = 0; i < 2; i++)
            {
                var b = FlatTextButton(tabs.transform, "Tab" + i, tabNames[i], 38, i == 0 ? Theme.Surface : Color.clear,
                    i == 0 ? Theme.Text : Theme.TextSub, 44, out view.tabTexts[i]);
                Layout(b, flexibleWidth: 1);
                view.tabButtons[i] = b;
            }

            view.nowCards = new NowPlayingCard[2];
            view.tabPages = new GameObject[2];

            // ---- 개별 듣기 ----
            {
                var scroll = PageScroll(panel.transform, "SinglePage", out var content);
                view.tabPages[0] = scroll.gameObject;
                view.nowCards[0] = Nest(nowPlaying, content);

                var card = Card(content);
                CardTitle(card, "개별 듣기");
                CardDesc(card, "예문 화면에서 문장이나 ▶ 버튼을 눌러 한 문장씩 들을 때 적용돼요.\n슬라이더를 움직이면 재생 중인 소리에 바로 적용돼요.");

                view.singleVolume = Nest(slider, card);
                SetLabel(view.singleVolume, "음량");
                view.singleSpeed = Nest(slider, card);
                SetLabel(view.singleSpeed, "빠르기");

                view.previewButton = TextButton(card, "Preview", "▶  마지막 문장 다시 듣기", 34, Theme.Primary, Theme.TextOnColor, 44, GlassStyle.Tinted, out _);
                Layout(view.previewButton, preferredHeight: 96);
            }

            // ---- 전체 듣기 ----
            {
                var scroll = PageScroll(panel.transform, "SequencePage", out var content);
                view.tabPages[1] = scroll.gameObject;
                view.nowCards[1] = Nest(nowPlaying, content);

                // 언어별 한번에 설정
                {
                    var bulk = Card(content);
                    bulk.name = "BulkCard";
                    CardTitle(bulk, "한번에 설정");
                    CardDesc(bulk, "아래 시퀀스의 영어 / 한국어 노드 전부에 같은 값을 한 번에 적용해요.\n" +
                                   "노드마다 값이 다르면 \"-\" 로 보여요.");
                    view.bulkEn = BulkSliders(bulk, slider, "En", "영어", Theme.En);
                    view.bulkKo = BulkSliders(bulk, slider, "Ko", "한국어", Theme.Ko);
                }

                var card = Card(content);
                CardTitle(card, "듣기 시퀀스");
                CardDesc(card, "전체 듣기는 예문마다(대화는 대사마다) 아래 순서대로 재생해요.\n" +
                               "≡ 손잡이를 끌어서 순서를 바꾸고, 영어/한국어를 누르면 언어가 바뀌어요.\n" +
                               "줄을 누르면 음량 · 빠르기 · 텀(다음 소리까지 쉬는 시간)을 바꿀 수 있어요.");

                var list = Rect("SequenceList", content);
                VLayout(list, 16);
                view.sequenceList = list.gameObject.AddComponent<SequenceListView>();
                view.sequenceList.scroll = scroll;
                view.sequenceList.rowPrefab = nodeRow;
                view.sequenceList.placeholderSprite = SpriteBaker.Rounded;

                var addRow = Rect("AddRow", content);
                Layout(addRow, preferredHeight: 100);
                var addLayout = HLayout(addRow, 20, null, TextAnchor.MiddleCenter, expandHeight: true);
                addLayout.childForceExpandWidth = true;
                view.addEn = TextButton(addRow, "AddEn", "+ 영어 듣기", 36, Theme.En, Theme.TextOnColor, 50, GlassStyle.Tinted, out _);
                Layout(view.addEn, flexibleWidth: 1);
                view.addKo = TextButton(addRow, "AddKo", "+ 한국어 듣기", 36, Theme.Ko, Theme.TextOnColor, 50, GlassStyle.Tinted, out _);
                Layout(view.addKo, flexibleWidth: 1);

                // 반복
                var repeatCard = Glass(content, "Repeat", Theme.Surface, 36, GlassStyle.Card);
                Layout(repeatCard, preferredHeight: 130);
                HLayout(repeatCard, 20, Pad(40, 32, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);
                var repeatText = Text(repeatCard.transform, "Text", "유닛 끝나면 처음부터 반복", 36, Theme.Text, TextAlignmentOptions.MidlineLeft)
                    .SingleLine().AutoSize(26, 36);
                Layout(repeatText, preferredHeight: 80, flexibleWidth: 1);
                view.repeatToggle = MakeButton(Rounded(repeatCard.transform, "Toggle", Theme.Track, 36));
                Layout(view.repeatToggle, preferredHeight: 72, preferredWidth: 128);
                var knob = Circle(view.repeatToggle.transform, "Knob", Color.white);
                view.repeatKnob = knob.rectTransform;
                view.repeatKnob.anchorMin = view.repeatKnob.anchorMax = new Vector2(0f, 0.5f);
                view.repeatKnob.pivot = new Vector2(0f, 0.5f);
                view.repeatKnob.anchoredPosition = new Vector2(8f, 0f);
                view.repeatKnob.sizeDelta = new Vector2(56, 56);
                AddShadow(knob, 3);

                view.resetButton = TextButton(content, "Reset", "기본 순서로 되돌리기", 32, Theme.Surface, Theme.TextSub, 44, GlassStyle.Card, out _);
                Layout(view.resetButton, preferredHeight: 96);

                scroll.gameObject.SetActive(false); // 처음엔 개별 듣기 탭
            }
            return view;
        }

        /// <summary>한번에 설정의 언어 하나: 소제목 + 음량 / 빠르기 / 텀 슬라이더.</summary>
        private static LabeledSlider[] BulkSliders(Transform card, LabeledSlider slider, string id, string langName, Color color)
        {
            var head = Text(card, id + "Title", langName, 36, color, TextAlignmentOptions.BottomLeft).SingleLine();
            Layout(head, preferredHeight: 72);
            string[] labels = { "음량", "빠르기", "텀" };
            var sliders = new LabeledSlider[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                sliders[i] = Nest(slider, card);
                SetLabel(sliders[i], labels[i]);
                sliders[i].name = id + "_" + labels[i];
                RecordOverride(sliders[i].gameObject);
            }
            return sliders;
        }

        private static ScrollRect PageScroll(Transform panel, string name, out RectTransform content)
        {
            var scroll = VScroll(panel, name, out content, 24, Pad(36, 36, 8, 60));
            ((RectTransform)scroll.transform).Fill(0, 250, 0, 24);
            return scroll;
        }

        private static RectTransform Card(Transform parent)
        {
            var card = Glass(parent, "Card", Theme.Surface, 40, GlassStyle.Card);
            VLayout(card, 16, Pad(40, 40, 36, 36));
            return card.rectTransform;
        }

        private static void CardTitle(Transform card, string text)
        {
            var t = Text(card, "Title", text, 46, Theme.Text, TextAlignmentOptions.MidlineLeft).SingleLine();
            Layout(t, preferredHeight: 64);
        }

        private static void CardDesc(Transform card, string text)
        {
            Text(card, "Desc", text, 29, Theme.TextSub, TextAlignmentOptions.TopLeft);
        }

        // ---- 앱 캔버스 ----------------------------------------------------------------------

        private static AppCanvas BuildAppCanvas()
        {
            var rootGo = new GameObject("AppCanvas") { layer = UIKit.UILayer };
            SceneManager.MoveGameObjectToScene(rootGo, stage);
            var view = rootGo.AddComponent<AppCanvas>();

            // 배경: 카메라가 그리는 캔버스 (글래스가 이걸 비춰 보인다). 카메라는 실행 시 AppRoot 가 연결
            var bgGo = new GameObject("BackgroundCanvas", typeof(RectTransform)) { layer = UIKit.UILayer };
            bgGo.transform.SetParent(rootGo.transform, false);
            var bgCanvas = bgGo.AddComponent<Canvas>();
            bgCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            bgCanvas.planeDistance = 100f;
            bgCanvas.sortingOrder = -100;
            var bgScaler = bgGo.AddComponent<CanvasScaler>();
            bgScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            bgScaler.referenceResolution = new Vector2(1080, 1920);
            var bgImage = Image(bgGo.transform, "Background", Color.white, SpriteBaker.Background);
            bgImage.rectTransform.Fill();
            Themed(bgImage, ColorRole.Background); // 모드에 따라 팔레트의 배경 그림으로
            view.backgroundCanvas = bgCanvas;

            // UI 캔버스
            var go = new GameObject("Canvas", typeof(RectTransform)) { layer = UIKit.UILayer };
            go.transform.SetParent(rootGo.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 |
                                              AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal |
                                              AdditionalCanvasShaderChannels.Tangent;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)go.transform;
            view.canvas = canvas;
            view.scaler = scaler;

            var safe = Rect("SafeArea", root).Fill();
            safe.gameObject.AddComponent<SafeAreaFitter>();
            view.ads = BuildAdSlot(safe);
            view.content = Rect("Content", safe).Fill();
            view.screens = Rect("Screens", view.content).Fill();
            view.miniPlayer = BuildMiniPlayer(view.content);

            view.overlay = Rect("Overlay", root).Fill();

            var toastLayer = Rect("ToastLayer", root).Fill();
            toastLayer.gameObject.AddComponent<SafeAreaFitter>();
            view.toastArea = Rect("ToastArea", toastLayer).Fill();
            view.toast = BuildToast(view.toastArea);
            return view;
        }

        /// <summary>하단 배너 광고 자리. 높이는 실행 시 BannerAdSlot 이 정한다 (프리팹에선 미리보기용 높이).</summary>
        private static BannerAdSlot BuildAdSlot(Transform safe)
        {
            var bg = Image(safe, "AdSlot", Theme.AdSlotBg);
            bg.raycastTarget = true; // 배너 둘레 여백을 눌러도 아래 화면으로 터치가 새지 않게
            bg.rectTransform.BottomStrip(160);
            var slot = bg.gameObject.AddComponent<BannerAdSlot>();

            var line = Image(bg.transform, "Line", Theme.Line);
            line.rectTransform.TopStrip(3);

            slot.bannerArea = Rect("Banner", bg.transform);
            slot.bannerArea.BottomStrip(140, 6);
            var box = Image(slot.bannerArea, "EditorPreview", new Color(0.55f, 0.53f, 0.6f, 0.35f));
            box.rectTransform.Fill();
            var label = Text(box.transform, "Label", "배너 광고 자리 (에디터 미리보기)", 30, Theme.TextSub).SingleLine().AutoSize(18, 30);
            label.rectTransform.Fill(20, 4, 20, 4);
            slot.editorPreview = box.gameObject;
            return slot;
        }

        /// <summary>전체 듣기 중 다른 화면에서 아래에 뜨는 작은 플레이어.</summary>
        private static MiniPlayerBar BuildMiniPlayer(Transform parent)
        {
            var bg = Glass(parent, "MiniPlayer", Theme.Text, 44, GlassStyle.Dark);
            bg.raycastTarget = true;
            bg.rectTransform.BottomStrip(150, 24, 28, 28);
            var bar = bg.gameObject.AddComponent<MiniPlayerBar>();
            bar.group = bg.gameObject.AddComponent<CanvasGroup>();
            bar.openButton = PlainButton(bg);
            bar.playSprite = SpriteBaker.Icon(IconKind.Play);
            bar.pauseSprite = SpriteBaker.Icon(IconKind.Pause);

            bar.title = Text(bg.transform, "Title", "S1-U01  유닛 제목", 34, Theme.TextOnColor, TextAlignmentOptions.BottomLeft).SingleLine().AutoSize(26, 34);
            bar.title.rectTransform.Fill(44, 18, 300, 74);
            bar.sub = Text(bg.transform, "Sub", "전체 듣기 · 1 / 5 · 영어", 28, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.TopLeft).SingleLine();
            bar.sub.rectTransform.Fill(44, 82, 300, 18);

            bar.playButton = IconButton(bg.transform, "PlayPause", IconKind.Pause, Theme.TextOnColor, Theme.Primary, 104, 46, GlassStyle.Tinted, out bar.playIcon);
            ((RectTransform)bar.playButton.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-148, 0), new Vector2(104, 104));
            bar.stopButton = IconButton(bg.transform, "Stop", IconKind.Close, Theme.TextOnColor, new Color(1, 1, 1, 0.15f), 104, 40, GlassStyle.Clear, out _);
            ((RectTransform)bar.stopButton.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(104, 104));
            return bar;
        }

        private static Toast BuildToast(Transform parent)
        {
            var rt = Rect("Toast", parent);
            rt.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 260), new Vector2(900, 120));
            var toast = rt.gameObject.AddComponent<Toast>();
            toast.group = rt.gameObject.AddComponent<CanvasGroup>();
            var bg = Glass(rt, "Bg", new Color(0.18f, 0.16f, 0.24f, 1f), 60, GlassStyle.Dark);
            bg.rectTransform.Fill();
            toast.text = Text(rt, "Text", "안내 문구", 36, Color.white).AutoSize(24, 36);
            toast.text.rectTransform.Fill(40, 10, 40, 10);
            return toast;
        }
    }
}
