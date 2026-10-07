using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>한 페이지에 예문 하나. 좌우로 밀어서 넘긴다.</summary>
    public class PagerView : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SwipeRatio = 0.18f;

        private RectTransform viewport;
        private RectTransform strip;
        private readonly List<RectTransform> pages = new List<RectTransform>();
        private int current;
        private bool dragging;
        private float dragOffset;
        private float position;
        private Vector2 pressLocal;

        public int Current => current;
        public int Count => pages.Count;

        /// <summary>(페이지, 사용자가 넘겼는지)</summary>
        public event Action<int, bool> PageChanged;

        public static PagerView Create(Transform parent, string name)
        {
            var rt = UIKit.Rect(name, parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            rt.gameObject.AddComponent<RectMask2D>();
            var pager = rt.gameObject.AddComponent<PagerView>();
            pager.viewport = rt;
            pager.strip = UIKit.Rect("Strip", rt);
            pager.strip.anchorMin = Vector2.zero;
            pager.strip.anchorMax = Vector2.one;
            pager.strip.pivot = new Vector2(0, 0.5f);
            pager.strip.offsetMin = Vector2.zero;
            pager.strip.offsetMax = Vector2.zero;
            return pager;
        }

        public RectTransform AddPage()
        {
            var p = UIKit.Rect("Page" + (pages.Count + 1), strip);
            p.anchorMin = new Vector2(0, 0);
            p.anchorMax = new Vector2(0, 1);
            p.pivot = new Vector2(0, 0.5f);
            pages.Add(p);
            LayoutPages();
            return p;
        }

        public void GoTo(int page, bool byUser, bool instant = false)
        {
            if (pages.Count == 0) return;
            page = Mathf.Clamp(page, 0, pages.Count - 1);
            bool changed = page != current;
            current = page;
            if (instant) position = -current * Width;
            if (changed) PageChanged?.Invoke(current, byUser);
        }

        private float Width => viewport.rect.width;

        private void LayoutPages()
        {
            float w = Width;
            for (int i = 0; i < pages.Count; i++)
            {
                var p = pages[i];
                p.sizeDelta = new Vector2(w, 0);
                p.anchoredPosition = new Vector2(i * w, 0);
            }
        }

        private void LateUpdate()
        {
            LayoutPages();
            float target = -current * Width;
            if (!dragging) position = Mathf.Lerp(position, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            if (Mathf.Abs(position - target) < 0.5f && !dragging) position = target;
            strip.anchoredPosition = new Vector2(position + (dragging ? dragOffset : 0f), 0);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            dragOffset = 0;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.pressPosition, eventData.pressEventCamera, out pressLocal);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out var local);
            float d = local.x - pressLocal.x;
            bool atEdge = (current == 0 && d > 0) || (current == pages.Count - 1 && d < 0);
            dragOffset = atEdge ? d * 0.35f : d;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            position += dragOffset;
            float threshold = Width * SwipeRatio;
            int target = current;
            if (dragOffset < -threshold) target = current + 1;
            else if (dragOffset > threshold) target = current - 1;
            dragOffset = 0;
            GoTo(target, true);
        }
    }
}
