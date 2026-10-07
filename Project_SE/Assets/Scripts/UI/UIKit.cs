using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>코드로 UGUI 를 만드는 도우미. 모든 화면은 런타임에 이 함수들로 만든다.</summary>
    public static class UIKit
    {
        private const int UILayer = 5;

        // ---- RectTransform ----------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>부모를 꽉 채운다 (여백: 왼쪽, 위, 오른쪽, 아래).</summary>
        public static RectTransform Fill(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>위쪽에 붙는 가로 띠.</summary>
        public static RectTransform TopStrip(this RectTransform rt, float height, float top = 0, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>아래쪽에 붙는 가로 띠.</summary>
        public static RectTransform BottomStrip(this RectTransform rt, float height, float bottom = 0, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
            return rt;
        }

        // ---- Image ------------------------------------------------------------------------

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>둥근 사각형 (radius: 캔버스 단위).</summary>
        public static Image Rounded(Transform parent, string name, Color color, float radius)
        {
            var img = Image(parent, name, color, Sprites.Rounded);
            SetRadius(img, radius);
            return img;
        }

        public static void SetRadius(Image img, float radius)
        {
            img.sprite = Sprites.Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = Sprites.RoundedMultiplier(radius);
        }

        public static Image Icon(Transform parent, IconKind kind, Color color, float size)
        {
            var img = Image(parent, "Icon_" + kind, color, Sprites.Icon(kind));
            img.preserveAspect = true;
            img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            return img;
        }

        public static Shadow AddShadow(Graphic g, float distance = 6f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = Theme.Shadow;
            s.effectDistance = new Vector2(0, -distance);
            return s;
        }

        // ---- Text -------------------------------------------------------------------------

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Fonts.Main;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.text = text;
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

        // ---- Button -----------------------------------------------------------------------

        public static Button Button(Transform parent, string name, Color color, float radius, Action onClick)
        {
            var img = Rounded(parent, name, color, radius);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(new UnityAction(onClick));
            img.gameObject.AddComponent<PressScale>();
            return b;
        }

        /// <summary>글자 버튼. 라벨은 버튼 가운데 꽉 채움.</summary>
        public static Button TextButton(Transform parent, string name, string label, float fontSize, Color bg, Color fg, float radius,
            Action onClick, out TextMeshProUGUI text)
        {
            var b = Button(parent, name, bg, radius, onClick);
            text = Text(b.transform, "Label", label, fontSize, fg);
            text.rectTransform.Fill(16, 4, 16, 4);
            text.SingleLine();
            return b;
        }

        /// <summary>아이콘 버튼 (bg 가 투명이면 아이콘만).</summary>
        public static Button IconButton(Transform parent, string name, IconKind kind, Color iconColor, Color bg, float size, float iconSize,
            Action onClick, out Image icon)
        {
            var b = Button(parent, name, bg, size * 0.5f, onClick);
            b.GetComponent<Image>().sprite = Sprites.Circle;
            b.GetComponent<Image>().type = UnityEngine.UI.Image.Type.Simple;
            ((RectTransform)b.transform).sizeDelta = new Vector2(size, size);
            icon = Icon(b.transform, kind, iconColor, iconSize);
            return b;
        }

        // ---- Layout -----------------------------------------------------------------------

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float preferredWidth = -1,
            float flexibleWidth = -1, float flexibleHeight = -1, float minHeight = -1, float minWidth = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            le.flexibleHeight = flexibleHeight;
            le.minHeight = minHeight;
            le.minWidth = minWidth;
            return le;
        }

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

        public static RectOffset Pad(int left, int right, int top, int bottom) => new RectOffset(left, right, top, bottom);
        public static RectOffset Pad(int all) => new RectOffset(all, all, all, all);

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

        /// <summary>빈 칸 (레이아웃 여백).</summary>
        public static RectTransform Spacer(Transform parent, float height)
        {
            var rt = Rect("Spacer", parent);
            Layout(rt, preferredHeight: height, minHeight: height);
            return rt;
        }

        public static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }
    }
}
