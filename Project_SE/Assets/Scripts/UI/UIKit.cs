using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// UGUI 도우미. 화면 모양은 프리팹(Assets/UI/Prefabs)에 있고, 코드는 배치·프리팹 생성만 돕는다.
    /// 프리팹은 에디터 메뉴 Tools > English App > Build UI Prefabs 가 처음 한 번 만든다 (UIPrefabBuilder).
    /// </summary>
    public static class UIKit
    {
        public const int UILayer = 5;

        /// <summary>둥근 사각형 스프라이트(Assets/UI/Sprites/Rounded.png)의 모서리 반경 (px).</summary>
        public const float RoundedSpriteRadius = 62f;

        // ---- 프리팹 -----------------------------------------------------------------------

        /// <summary>프리팹을 만들고 글자 폰트를 런타임 폰트로 바꾼다 (Fonts.ApplyTo).</summary>
        public static T Spawn<T>(T prefab, Transform parent) where T : Component
        {
            var obj = Object.Instantiate(prefab, parent, false);
            obj.name = prefab.name;
            Fonts.ApplyTo(obj.gameObject);
            return obj;
        }

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

        /// <summary>둥근 사각형 9-slice (radius: 캔버스 단위).</summary>
        public static void SetRadius(Image img, Sprite rounded, float radius)
        {
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = RoundedSpriteRadius / Mathf.Max(1f, radius);
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

        public static RectOffset Pad(int left, int right, int top, int bottom) => new RectOffset(left, right, top, bottom);
        public static RectOffset Pad(int all) => new RectOffset(all, all, all, all);

        public static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }

        /// <summary>자식 중 하나를 없앤다. Destroy 는 프레임 끝에 일어나므로 바로 레이아웃에서 빼 둔다.</summary>
        public static void DestroyNow(Component c)
        {
            if (c == null) return;
            c.gameObject.SetActive(false);
            Object.Destroy(c.gameObject);
        }
    }
}
