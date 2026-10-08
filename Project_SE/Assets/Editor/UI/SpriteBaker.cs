using System;
using System.IO;
using EnglishApp.UI;
using UnityEditor;
using UnityEngine;

namespace EnglishApp.EditorTools
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
        Sun,
        Moon,
    }

    /// <summary>
    /// UI 스프라이트를 PNG 로 굽는다 (둥근 사각형 9-slice, 원, 아이콘, 배경 블롭 — 밝은 / 어두운 모드).
    /// SDF 로 안티앨리어싱해서 그린다. 이미 있는 PNG 는 덮어쓰지 않는다 (직접 바꾼 그림 유지).
    /// </summary>
    public static class SpriteBaker
    {
        public const string Folder = "Assets/UI/Sprites";
        private const int RoundSize = 128;
        private const int IconSize = 96;
        private const int BgWidth = 540;
        private const int BgHeight = 960;

        public static string RoundedPath => Folder + "/Rounded.png";
        public static string CirclePath => Folder + "/Circle.png";
        public static string BackgroundPath => Folder + "/Background.png";
        public static string BackgroundDarkPath => Folder + "/Background_Dark.png";
        public static string IconPath(IconKind kind) => Folder + "/Icon_" + kind + ".png";

        public static void BakeAll()
        {
            Directory.CreateDirectory(Folder);
            float b = RoundSize * 0.5f;
            Write(RoundedPath, () => Draw(RoundSize, p =>
            {
                // 픽셀 단위 rounded box
                float half = RoundSize * 0.5f;
                float r = UIKit.RoundedSpriteRadius;
                var q = new Vector2(Mathf.Abs(p.x - half), Mathf.Abs(p.y - half)) - new Vector2(half - r, half - r);
                float outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude;
                float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0);
                return outside + inside - r;
            }, pixelSpace: true), new Vector4(b, b, b, b));
            Write(CirclePath, () => Draw(RoundSize, p => (p - new Vector2(RoundSize * 0.5f, RoundSize * 0.5f)).magnitude - (RoundSize * 0.5f - 1f),
                pixelSpace: true), Vector4.zero);
            foreach (IconKind kind in Enum.GetValues(typeof(IconKind)))
                Write(IconPath(kind), () => Draw(IconSize, IconSdf(kind), pixelSpace: false), Vector4.zero);
            Write(BackgroundPath, () => DrawBackground(Theme.Hex("#FFF4E3"), LightBlobs), Vector4.zero);
            Write(BackgroundDarkPath, () => DrawBackground(Theme.Hex("#16131F"), DarkBlobs), Vector4.zero);
        }

        public static Sprite Rounded => Load(RoundedPath);
        public static Sprite Circle => Load(CirclePath);
        public static Sprite Background => Load(BackgroundPath);
        public static Sprite BackgroundDark => Load(BackgroundDarkPath);
        public static Sprite Icon(IconKind kind) => Load(IconPath(kind));

        private static Sprite Load(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) throw new Exception("[UI] 스프라이트가 없습니다: " + path);
            return s;
        }

        private static void Write(string path, Func<Texture2D> draw, Vector4 border)
        {
            if (!File.Exists(path))
            {
                var tex = draw();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType == TextureImporterType.Sprite) return; // 이미 설정됨 (직접 바꾼 설정 유지)
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------------------------

        /// <summary>
        /// sdf: 음수 = 안쪽. pixelSpace=false 면 좌표는 -1~1 (위가 +y), 거리도 그 단위.
        /// </summary>
        private static Texture2D Draw(int size, Func<Vector2, float> sdf, bool pixelSpace)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
            tex.Apply(false);
            return tex;
        }

        // 블롭: (중심 x, y (0~1, 위가 1), 반경 (폭 기준), 색, 세기)
        private static readonly (Vector2, float, Color, float)[] LightBlobs =
        {
            (new Vector2(0.10f, 0.92f), 0.70f, Theme.Hex("#FFB37A"), 0.85f),
            (new Vector2(0.95f, 0.78f), 0.62f, Theme.Hex("#8EC2FF"), 0.75f),
            (new Vector2(0.20f, 0.45f), 0.58f, Theme.Hex("#8FE3C4"), 0.65f),
            (new Vector2(0.90f, 0.30f), 0.66f, Theme.Hex("#FFD27A"), 0.70f),
            (new Vector2(0.45f, 0.05f), 0.70f, Theme.Hex("#FF9C7A"), 0.60f),
            (new Vector2(0.55f, 0.62f), 0.40f, Theme.Hex("#FFFFFF"), 0.50f),
        };

        // 어두운 모드: 같은 자리·같은 색 계열을 깊고 진하게
        private static readonly (Vector2, float, Color, float)[] DarkBlobs =
        {
            (new Vector2(0.10f, 0.92f), 0.70f, Theme.Hex("#8A3A1E"), 0.80f),
            (new Vector2(0.95f, 0.78f), 0.62f, Theme.Hex("#1F4E9C"), 0.80f),
            (new Vector2(0.20f, 0.45f), 0.58f, Theme.Hex("#17675A"), 0.70f),
            (new Vector2(0.90f, 0.30f), 0.66f, Theme.Hex("#6E4C14"), 0.65f),
            (new Vector2(0.45f, 0.05f), 0.70f, Theme.Hex("#7A2C3C"), 0.60f),
            (new Vector2(0.55f, 0.62f), 0.40f, Theme.Hex("#3A3358"), 0.50f),
        };

        /// <summary>글래스 뒤에 깔 배경: 바탕색에 앱 색(오렌지·블루·그린·노랑) 블롭을 부드럽게.</summary>
        private static Texture2D DrawBackground(Color baseColor, (Vector2, float, Color, float)[] blobs)
        {
            var tex = new Texture2D(BgWidth, BgHeight, TextureFormat.RGBA32, false);
            var pixels = new Color32[BgWidth * BgHeight];
            for (int y = 0; y < BgHeight; y++)
            for (int x = 0; x < BgWidth; x++)
            {
                var uv = new Vector2((x + 0.5f) / BgWidth, (y + 0.5f) / BgHeight);
                var c = baseColor;
                foreach (var (center, radius, color, strength) in blobs)
                {
                    // 화면 비율(9:16)을 고려한 거리 (폭 기준)
                    var d = new Vector2(uv.x - center.x, (uv.y - center.y) * BgHeight / BgWidth);
                    float t = Mathf.Clamp01(1f - d.magnitude / radius);
                    t = t * t * (3f - 2f * t); // smoothstep
                    c = Color.Lerp(c, color, t * strength);
                }
                c.a = 1f;
                pixels[y * BgWidth + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
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
                case IconKind.Sun:
                    return p =>
                    {
                        float d = SdCircle(p, Vector2.zero, 0.3f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            d = Mathf.Min(d, Segment(p, dir * 0.5f, dir * 0.7f) - 0.075f);
                        }
                        return d;
                    };
                case IconKind.Moon:
                    // 큰 원에서 오른쪽 위로 비킨 원을 빼서 초승달
                    return p => Mathf.Max(SdCircle(p, new Vector2(-0.04f, -0.02f), 0.62f), -SdCircle(p, new Vector2(0.3f, 0.28f), 0.5f));
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
