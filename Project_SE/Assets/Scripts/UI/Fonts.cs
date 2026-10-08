using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace EnglishApp.UI
{
    /// <summary>
    /// UI 폰트: 잘난체 고딕 (Assets/Resources/Fonts/JalnanGothicTTF.ttf), TextMeshPro 동적 아틀라스, 외곽선 0.15.
    ///
    /// - 템플릿 에셋 "Fonts/JalnanGothic SDF" 는 에디터 셋업(Tools > English App)이 만든다.
    ///   셰이더를 빌드에 포함시키고 외곽선 등 머티리얼 값을 정하는 용도 (에디터에서 색·두께를 바꿔도 된다).
    /// - 실행 시에는 그 템플릿 값을 복사한 런타임 폰트 에셋을 새로 만든다.
    ///   → 한글 글리프가 템플릿 에셋 파일에 쌓이지 않는다 (에디터에서 실행해도 에셋이 바뀌지 않음).
    /// </summary>
    public static class Fonts
    {
        public const string TemplateResource = "Fonts/JalnanGothic SDF";
        public const string FontResource = "Fonts/JalnanGothicTTF";
        public const int SamplingPointSize = 64;
        public const int Padding = 6;
        public const int AtlasSize = 2048;
        public const float OutlineWidth = 0.15f;
        public static readonly Color OutlineColor = Color.black;

        private static TMP_FontAsset main;
        private static TMP_FontAsset template;

        public static TMP_FontAsset Main
        {
            get
            {
                if (main == null) main = Create();
                return main;
            }
        }

        private static TMP_FontAsset Template
        {
            get
            {
                if (template == null) template = Resources.Load<TMP_FontAsset>(TemplateResource);
                return template;
            }
        }

        /// <summary>
        /// 프리팹 글자는 템플릿 에셋을 가리킨다. 실행 시에는 런타임 폰트로 바꿔서 템플릿 파일에 글리프가 쌓이지 않게 한다.
        /// (프리팹에서 다른 폰트를 고른 글자는 그대로 둔다.)
        /// </summary>
        public static void ApplyTo(GameObject root)
        {
            var runtime = Main;
            if (runtime == null || runtime == Template) return;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t.font == null || t.font == Template) t.font = runtime;
        }

        private static TMP_FontAsset Create()
        {
            var template = Template;
            var font = Resources.Load<Font>(FontResource);
            if (font == null)
            {
                Debug.LogError("[Fonts] " + FontResource + " 가 없습니다.");
                return template;
            }

            TMP_FontAsset asset = null;
            try
            {
                asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                    AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Fonts] 런타임 폰트 에셋 생성 실패: " + e.Message);
            }

            if (asset == null)
            {
                if (template == null) Debug.LogError("[Fonts] 폰트 템플릿이 없습니다. Tools > English App > Run Project Setup 을 실행하세요.");
                return template;
            }

            asset.name = "JalnanGothic (Runtime)";
            var mat = asset.material;
            if (template != null && template.material != null)
                CopyLook(template.material, mat);
            else
                ApplyOutline(mat);
            return asset;
        }

        /// <summary>외곽선 기본값 (에디터 셋업도 이 함수를 쓴다).</summary>
        public static void ApplyOutline(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", OutlineWidth);
            if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", OutlineColor);
            mat.EnableKeyword("OUTLINE_ON");
        }

        /// <summary>템플릿 머티리얼의 모양(셰이더, 외곽선 등)을 복사하고 아틀라스 관련 값은 유지한다.</summary>
        private static void CopyLook(Material from, Material to)
        {
            var tex = to.GetTexture("_MainTex");
            float w = Get(to, "_TextureWidth"), h = Get(to, "_TextureHeight"), gs = Get(to, "_GradientScale");
            float wn = Get(to, "_WeightNormal"), wb = Get(to, "_WeightBold");

            to.shader = from.shader;
            to.CopyPropertiesFromMaterial(from);
            to.shaderKeywords = from.shaderKeywords;

            to.SetTexture("_MainTex", tex);
            Set(to, "_TextureWidth", w);
            Set(to, "_TextureHeight", h);
            Set(to, "_GradientScale", gs);
            Set(to, "_WeightNormal", wn);
            Set(to, "_WeightBold", wb);
        }

        private static float Get(Material m, string prop) => m.HasProperty(prop) ? m.GetFloat(prop) : float.NaN;

        private static void Set(Material m, string prop, float v)
        {
            if (!float.IsNaN(v) && m.HasProperty(prop)) m.SetFloat(prop, v);
        }
    }
}
