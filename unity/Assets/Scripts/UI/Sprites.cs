using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnglishApp.UI
{
    public enum IconKind
    {
        Gear,
        Back,
        ChevronLeft,
        ChevronRight,
        ChevronDown,
        ChevronUp,
        Play,
        Pause,
        Stop,
        Drag,
        Plus,
        Close,
        Repeat,
    }

    /// <summary>
    /// 런타임에 그리는 스프라이트 (둥근 사각형 9-slice, 원, 아이콘).
    /// 이미지 에셋 없이 해상도와 상관없이 깔끔하게 보이도록 SDF 로 안티앨리어싱해서 그린다.
    /// </summary>
    public static class Sprites
    {
        private const int RoundSize = 128;
        private const float RoundRadius = 62f; // px
        private const int IconSize = 96;

        private static Sprite rounded;
        private static Sprite circle;
        private static readonly Dictionary<IconKind, Sprite> icons = new Dictionary<IconKind, Sprite>();

        /// <summary>둥근 사각형 (Image.type = Sliced, 반경은 UIKit.SetRadius 로).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                var tex = Draw(RoundSize, p =>
                {
                    // 픽셀 단위 rounded box
                    float half = RoundSize * 0.5f;
                    var q = new Vector2(Mathf.Abs(p.x - half), Mathf.Abs(p.y - half)) - new Vector2(half - RoundRadius, half - RoundRadius);
                    float outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0);
                    return outside + inside - RoundRadius;
                }, pixelSpace: true);
                float b = RoundSize * 0.5f;
                rounded = Sprite.Create(tex, new Rect(0, 0, RoundSize, RoundSize), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(b, b, b, b));
                rounded.name = "Rounded";
                return rounded;
            }
        }

        /// <summary>9-slice 에서 반경 radius(캔버스 단위)를 내려면 Image.pixelsPerUnitMultiplier 를 이 값으로.</summary>
        public static float RoundedMultiplier(float radius)
        {
            return RoundRadius / Mathf.Max(1f, radius);
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                var tex = Draw(RoundSize, p => (p - new Vector2(RoundSize * 0.5f, RoundSize * 0.5f)).magnitude - (RoundSize * 0.5f - 1f), pixelSpace: true);
                circle = Sprite.Create(tex, new Rect(0, 0, RoundSize, RoundSize), new Vector2(0.5f, 0.5f), 100f);
                circle.name = "Circle";
                return circle;
            }
        }

        public static Sprite Icon(IconKind kind)
        {
            if (icons.TryGetValue(kind, out var s) && s != null) return s;
            Func<Vector2, float> sdf = IconSdf(kind);
            var tex = Draw(IconSize, sdf, pixelSpace: false);
            s = Sprite.Create(tex, new Rect(0, 0, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f);
            s.name = "Icon_" + kind;
            icons[kind] = s;
            return s;
        }

        // ------------------------------------------------------------------------------------

        /// <summary>
        /// sdf: 음수 = 안쪽. pixelSpace=false 면 좌표는 -1~1 (위가 +y), 거리도 그 단위.
        /// </summary>
        private static Texture2D Draw(int size, Func<Vector2, float> sdf, bool pixelSpace)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Generated",
            };
            var pixels = new Color32[size * size];
            float toPixel = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d;
                if (pixelSpace)
                {
                    d = sdf(new Vector2(x + 0.5f, y + 0.5f));
                }
                else
                {
                    var p = new Vector2((x + 0.5f) / toPixel - 1f, (y + 0.5f) / toPixel - 1f);
                    d = sdf(p) * toPixel;
                }
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Func<Vector2, float> IconSdf(IconKind kind)
        {
            const float stroke = 0.11f;
            switch (kind)
            {
                case IconKind.Gear:
                    return p =>
                    {
                        float d = SdCircle(p, Vector2.zero, 0.58f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            var c = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.68f;
                            d = Mathf.Min(d, Box(p, c, new Vector2(0.17f, 0.13f), a) - 0.03f);
                        }
                        return Mathf.Max(d, -SdCircle(p, Vector2.zero, 0.25f));
                    };
                case IconKind.Back:
                case IconKind.ChevronLeft:
                    return p => Polyline(p, stroke, new Vector2(0.22f, 0.58f), new Vector2(-0.32f, 0f), new Vector2(0.22f, -0.58f));
                case IconKind.ChevronRight:
                    return p => Polyline(p, stroke, new Vector2(-0.22f, 0.58f), new Vector2(0.32f, 0f), new Vector2(-0.22f, -0.58f));
                case IconKind.ChevronDown:
                    return p => Polyline(p, stroke, new Vector2(-0.58f, 0.22f), new Vector2(0f, -0.32f), new Vector2(0.58f, 0.22f));
                case IconKind.ChevronUp:
                    return p => Polyline(p, stroke, new Vector2(-0.58f, -0.22f), new Vector2(0f, 0.32f), new Vector2(0.58f, -0.22f));
                case IconKind.Play:
                    return p => Triangle(p, new Vector2(-0.4f, 0.62f), new Vector2(-0.4f, -0.62f), new Vector2(0.66f, 0f)) - 0.06f;
                case IconKind.Pause:
                    return p => Mathf.Min(Box(p, new Vector2(-0.28f, 0f), new Vector2(0.12f, 0.56f), 0f),
                        Box(p, new Vector2(0.28f, 0f), new Vector2(0.12f, 0.56f), 0f)) - 0.05f;
                case IconKind.Stop:
                    return p => Box(p, Vector2.zero, new Vector2(0.45f, 0.45f), 0f) - 0.1f;
                case IconKind.Drag:
                    return p => Mathf.Min(Segment(p, new Vector2(-0.58f, 0.38f), new Vector2(0.58f, 0.38f)),
                        Mathf.Min(Segment(p, new Vector2(-0.58f, 0f), new Vector2(0.58f, 0f)),
                            Segment(p, new Vector2(-0.58f, -0.38f), new Vector2(0.58f, -0.38f)))) - 0.085f;
                case IconKind.Plus:
                    return p => Mathf.Min(Segment(p, new Vector2(-0.58f, 0f), new Vector2(0.58f, 0f)),
                        Segment(p, new Vector2(0f, -0.58f), new Vector2(0f, 0.58f))) - stroke;
                case IconKind.Close:
                    return p => Mathf.Min(Segment(p, new Vector2(-0.48f, -0.48f), new Vector2(0.48f, 0.48f)),
                        Segment(p, new Vector2(-0.48f, 0.48f), new Vector2(0.48f, -0.48f))) - stroke;
                case IconKind.Repeat:
                    return p =>
                    {
                        // 위쪽 화살표 줄 + 아래쪽 화살표 줄
                        float top = Mathf.Min(Polyline(p, 0.09f, new Vector2(-0.6f, -0.05f), new Vector2(-0.6f, 0.38f), new Vector2(0.5f, 0.38f)),
                            Triangle(p, new Vector2(0.38f, 0.62f), new Vector2(0.38f, 0.14f), new Vector2(0.72f, 0.38f)));
                        float bottom = Mathf.Min(Polyline(p, 0.09f, new Vector2(0.6f, 0.05f), new Vector2(0.6f, -0.38f), new Vector2(-0.5f, -0.38f)),
                            Triangle(p, new Vector2(-0.38f, -0.62f), new Vector2(-0.38f, -0.14f), new Vector2(-0.72f, -0.38f)));
                        return Mathf.Min(top, bottom);
                    };
                default:
                    return p => SdCircle(p, Vector2.zero, 0.5f);
            }
        }

        private static float SdCircle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        private static float Box(Vector2 p, Vector2 c, Vector2 half, float angle)
        {
            var d = p - c;
            float cs = Mathf.Cos(-angle), sn = Mathf.Sin(-angle);
            var q = new Vector2(Mathf.Abs(d.x * cs - d.y * sn), Mathf.Abs(d.x * sn + d.y * cs)) - half;
            float outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude;
            return outside + Mathf.Min(Mathf.Max(q.x, q.y), 0);
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        private static float Polyline(Vector2 p, float thickness, params Vector2[] pts)
        {
            float d = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, Segment(p, pts[i], pts[i + 1]));
            return d - thickness;
        }

        /// <summary>삼각형 signed distance (꼭짓점 순서 무관).</summary>
        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d = Mathf.Min(Segment(p, a, b), Mathf.Min(Segment(p, b, c), Segment(p, c, a)));
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
