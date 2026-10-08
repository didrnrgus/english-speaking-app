using System;
using EnglishApp.UI;
using LeTai.Asset.TranslucentImage;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PShadow = LeTai.Paraform.Scaffold.Shadow;

namespace EnglishApp.EditorTools
{
    /// <summary>글래스(리퀴드 글래스) 모양 프리셋. 프리팹을 만든 뒤에는 각 TranslucentImage 인스펙터에서 바꾸면 된다.</summary>
    public struct GlassStyle
    {
        public float opacity;     // 색(틴트)이 얼마나 덮는지 (TranslucentImage.foregroundOpacity)
        public float brightness;  // 비치는 배경 밝기
        public float bevel;       // 테두리 굴절 폭 (반경의 80% 를 넘지 않게 줄인다)
        public float elevation;   // 바닥과의 거리 → 굴절 세기
        public float shadowAlpha;
        public float shadowOffset;
        public float shadowSoftness;
        public ColorRole? themeRole; // 흰(중립) 유리일 때 밝은 / 어두운 모드에 따라 톤을 바꾸는 역할

        /// <summary>흰 카드 (유닛 카드, 예문 카드, 말풍선, 설정 카드).</summary>
        public static readonly GlassStyle Card = new GlassStyle
            { opacity = 0.72f, brightness = 0.6f, bevel = 40, elevation = 120, shadowAlpha = 0.18f, shadowOffset = 8, shadowSoftness = 28, themeRole = ColorRole.GlassCard };

        /// <summary>색 버튼 (레벨 버튼, 전체 듣기, ▶ 영어 / 한국어).</summary>
        public static readonly GlassStyle Tinted = new GlassStyle
            { opacity = 0.92f, brightness = 0.6f, bevel = 32, elevation = 100, shadowAlpha = 0.22f, shadowOffset = 6, shadowSoftness = 24 };

        /// <summary>배경 위 동그란 아이콘 버튼 (뒤로, 설정, 이전/다음, 정지, 반복).</summary>
        public static readonly GlassStyle Clear = new GlassStyle
            { opacity = 0.45f, brightness = 0.7f, bevel = 28, elevation = 100, shadowAlpha = 0.15f, shadowOffset = 5, shadowSoftness = 20, themeRole = ColorRole.GlassClear };

        /// <summary>어두운 유리 (미니 플레이어, 지금 재생 중, 토스트).</summary>
        public static readonly GlassStyle Dark = new GlassStyle
            { opacity = 0.86f, brightness = 0f, bevel = 32, elevation = 120, shadowAlpha = 0.28f, shadowOffset = 8, shadowSoftness = 32 };

        /// <summary>설정 팝업 패널.</summary>
        public static readonly GlassStyle Panel = new GlassStyle
            { opacity = 0.9f, brightness = 0.6f, bevel = 48, elevation = 150, shadowAlpha = 0.3f, shadowOffset = 10, shadowSoftness = 40, themeRole = ColorRole.GlassPanel };
    }

    /// <summary>프리팹 생성기(UIPrefabBuilder)가 쓰는 UGUI 생성 도우미.</summary>
    public static class UIBuild
    {
        public static Material GlassMaterial;
        public static TMP_FontAsset Font;

        // ---- 기본 ---------------------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent) => UIKit.Rect(name, parent);

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            AutoTheme(img);
            return img;
        }

        // ---- 화면 모드 ----------------------------------------------------------------------

        /// <summary>색 역할을 붙인다 (밝은 / 어두운 모드에 따라 ThemeColor 가 다시 칠함).</summary>
        public static ThemeColor Themed(Graphic g, ColorRole role)
        {
            var t = g.GetComponent<ThemeColor>();
            if (t == null) t = g.gameObject.AddComponent<ThemeColor>();
            t.role = role;
            return t;
        }

        /// <summary>
        /// 밝은 모드 팔레트 색으로 만든 그림·글자에 같은 역할을 붙인다.
        /// 흰색은 붙이지 않는다 (색 버튼 위 흰 글자·아이콘, 손잡이 점 등은 두 모드 공통).
        /// </summary>
        private static void AutoTheme(Graphic g)
        {
            var c = g.color;
            if (g is TMP_Text)
            {
                if (c == Theme.Text) Themed(g, ColorRole.Text);
                else if (c == Theme.TextSub) Themed(g, ColorRole.TextSub);
                return;
            }
            if (c == Theme.Text) Themed(g, ColorRole.Text);
            else if (c == Theme.TextSub) Themed(g, ColorRole.TextSub);
            else if (c == Theme.Line) Themed(g, ColorRole.Line);
            else if (c == Theme.Track) Themed(g, ColorRole.Track);
            else if (c == Theme.AdSlotBg) Themed(g, ColorRole.AdSlotBg);
            else if (c == Theme.Bg) Themed(g, ColorRole.Bg);
        }

        /// <summary>둥근 사각형 (radius: 캔버스 단위). 글래스가 아닌 작은 장식용 (배지, 슬라이더, 강조 배경).</summary>
        public static Image Rounded(Transform parent, string name, Color color, float radius)
        {
            var img = Image(parent, name, color);
            UIKit.SetRadius(img, SpriteBaker.Rounded, radius);
            return img;
        }

        public static Image Circle(Transform parent, string name, Color color)
        {
            return Image(parent, name, color, SpriteBaker.Circle);
        }

        public static Image Icon(Transform parent, IconKind kind, Color color, float size)
        {
            var img = Image(parent, "Icon_" + kind, color, SpriteBaker.Icon(kind));
            img.preserveAspect = true;
            img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            return img;
        }

        public static UnityEngine.UI.Shadow AddShadow(Graphic g, float distance)
        {
            var s = g.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = Theme.Shadow;
            s.effectDistance = new Vector2(0, -distance);
            return s;
        }

        // ---- 글래스 -------------------------------------------------------------------------

        /// <summary>리퀴드 글래스 판 (TranslucentImage + Paraform 둥근 사각형). color 는 유리 틴트.</summary>
        public static TranslucentImage Glass(Transform parent, string name, Color color, float radius, GlassStyle style)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<TranslucentImage>();
            img.material = GlassMaterial;
            img.color = color;
            img.raycastTarget = false;
            img.foregroundOpacity = style.opacity;
            img.brightness = style.brightness;
            img.vibrancy = 1f;
            img.flatten = 0f;

            // 흰(중립) 유리만 모드에 따라 톤을 바꾼다. 색 버튼·어두운 유리는 두 모드 공통
            bool neutral = color == Color.white || color == Theme.Surface || color == Theme.Bg;
            if (style.themeRole.HasValue && neutral) Themed(img, style.themeRole.Value);

            img.paraformConfig.CornerRadii = new Vector4(radius, radius, radius, radius);
            img.paraformConfig.BevelWidth = Mathf.Min(style.bevel, radius * 0.8f);
            img.paraformConfig.Elevation = style.elevation;
            using (var scope = img.paraformConfig.EditEffects())
            {
                scope.effects.Add(new PShadow
                {
                    name = "Shadow",
                    color = new Color(0, 0, 0, style.shadowAlpha),
                    offset = new Vector2(0, -style.shadowOffset),
                    softness = style.shadowSoftness,
                });
            }

            // 인스펙터에서 네 모서리를 한 번에 바꾸도록 연결
            var so = new SerializedObject(img);
            var linked = so.FindProperty("paraformConfig.isCornersLinked");
            if (linked != null)
            {
                linked.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return img;
        }

        // ---- 글자 ---------------------------------------------------------------------------

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.text = text;
            AutoTheme(t);
            return t;
        }

        /// <summary>한 줄, 넘치면 … (제목, 라벨).</summary>
        public static TextMeshProUGUI SingleLine(this TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static TextMeshProUGUI AutoSize(this TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            return t;
        }

        // ---- 버튼 ---------------------------------------------------------------------------

        /// <summary>그림에 버튼을 붙인다 (누르면 살짝 어두워지고 작아짐).</summary>
        public static Button MakeButton(Graphic g, float pressedScale = 0.95f)
        {
            g.raycastTarget = true;
            var b = g.gameObject.AddComponent<Button>();
            b.targetGraphic = g;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            g.gameObject.AddComponent<PressScale>().pressedScale = pressedScale;
            return b;
        }

        /// <summary>눌러도 모양이 안 바뀌는 투명 버튼 (글자 누르기, 바깥 누르기).</summary>
        public static Button PlainButton(Graphic g)
        {
            g.raycastTarget = true;
            var b = g.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            return b;
        }

        public static Button GlassButton(Transform parent, string name, Color color, float radius, GlassStyle style)
        {
            return MakeButton(Glass(parent, name, color, radius, style));
        }

        /// <summary>글래스 글자 버튼. 라벨은 버튼 가운데 꽉 채움.</summary>
        public static Button TextButton(Transform parent, string name, string label, float fontSize, Color bg, Color fg, float radius,
            GlassStyle style, out TextMeshProUGUI text)
        {
            var b = GlassButton(parent, name, bg, radius, style);
            text = Text(b.transform, "Label", label, fontSize, fg);
            text.rectTransform.Fill(16, 4, 16, 4);
            text.SingleLine();
            return b;
        }

        /// <summary>글래스가 아닌 글자 버튼 (탭처럼 다른 판 위에 놓이는 것).</summary>
        public static Button FlatTextButton(Transform parent, string name, string label, float fontSize, Color bg, Color fg, float radius,
            out TextMeshProUGUI text)
        {
            var b = MakeButton(Rounded(parent, name, bg, radius));
            text = Text(b.transform, "Label", label, fontSize, fg);
            text.rectTransform.Fill(16, 4, 16, 4);
            text.SingleLine();
            return b;
        }

        /// <summary>동그란 글래스 아이콘 버튼.</summary>
        public static Button IconButton(Transform parent, string name, IconKind kind, Color iconColor, Color bg, float size, float iconSize,
            GlassStyle style, out Image icon)
        {
            var b = GlassButton(parent, name, bg, size * 0.5f, style);
            ((RectTransform)b.transform).sizeDelta = new Vector2(size, size);
            icon = Icon(b.transform, kind, iconColor, iconSize);
            return b;
        }

        /// <summary>배경 없는 아이콘 버튼 (카드 안의 펼치기·삭제).</summary>
        public static Button FlatIconButton(Transform parent, string name, IconKind kind, Color iconColor, float size, float iconSize, out Image icon)
        {
            var b = MakeButton(Circle(parent, name, Color.clear));
            ((RectTransform)b.transform).sizeDelta = new Vector2(size, size);
            icon = Icon(b.transform, kind, iconColor, iconSize);
            return b;
        }

        // ---- 레이아웃 -----------------------------------------------------------------------

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float preferredWidth = -1,
            float flexibleWidth = -1, float flexibleHeight = -1, float minHeight = -1, float minWidth = -1)
            => UIKit.Layout(c, preferredHeight, preferredWidth, flexibleWidth, flexibleHeight, minHeight, minWidth);

        public static RectOffset Pad(int left, int right, int top, int bottom) => UIKit.Pad(left, right, top, bottom);
        public static RectOffset Pad(int all) => UIKit.Pad(all);

        public static VerticalLayoutGroup VLayout(Component c, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperCenter, bool expandWidth = true)
        {
            var g = c.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = expandWidth;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup HLayout(Component c, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleCenter, bool expandHeight = true)
        {
            var g = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = expandHeight;
            return g;
        }

        public static ContentSizeFitter FitHeight(Component c)
        {
            var f = c.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        /// <summary>
        /// 세로 스크롤. content 에는 VerticalLayoutGroup + ContentSizeFitter 가 붙어 있다.
        /// directional=true 면 가로 드래그를 부모(페이지 넘김)에 넘긴다.
        /// </summary>
        public static ScrollRect VScroll(Transform parent, string name, out RectTransform content, float spacing = 0f,
            RectOffset padding = null, bool directional = false)
        {
            var root = Rect(name, parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            root.gameObject.AddComponent<RectMask2D>();
            ScrollRect sr = directional ? root.gameObject.AddComponent<DirectionalScrollRect>() : root.gameObject.AddComponent<ScrollRect>();

            content = Rect("Content", root);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VLayout(content, spacing, padding);
            FitHeight(content);

            sr.content = content;
            sr.viewport = root;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.1f;
            sr.inertia = true;
            sr.decelerationRate = 0.135f;
            sr.scrollSensitivity = 40f;
            return sr;
        }

        // ---- 중첩 프리팹 --------------------------------------------------------------------

        /// <summary>다른 프리팹을 중첩해서 넣는다 (원본 프리팹을 고치면 여기도 바뀜).</summary>
        public static T Nest<T>(T prefab, Transform parent) where T : Component
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            return go.GetComponent<T>();
        }

        /// <summary>중첩 프리팹 안의 값을 코드로 바꿨을 때 오버라이드로 저장되게 기록한다.</summary>
        public static void RecordOverride(UnityEngine.Object o)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }

        public static void Require(UnityEngine.Object o, string what)
        {
            if (o == null) throw new Exception("[UI] " + what + " 이(가) 없습니다.");
        }
    }
}
