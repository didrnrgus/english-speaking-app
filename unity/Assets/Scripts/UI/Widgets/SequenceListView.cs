using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 듣기 시퀀스 목록 (설정 > 전체 듣기). 손잡이(≡)를 끌어서 순서를 바꾼다.
    /// 끄는 동안 자리 표시(placeholder)가 들어갈 위치를 보여주고, 놓으면 설정에 반영한다.
    /// </summary>
    public class SequenceListView : MonoBehaviour
    {
        private const float AutoScrollEdge = 160f;
        private const float AutoScrollSpeed = 1400f;

        private AppSettings settings;
        private ScrollRect scroll;
        private RectTransform content;
        private readonly List<SequenceNodeRow> rows = new List<SequenceNodeRow>();
        private readonly HashSet<int> expanded = new HashSet<int>();

        private SequenceNodeRow dragging;
        private RectTransform placeholder;
        private float grabOffset;
        private Vector2 lastPointer;
        private Camera dragCamera;

        public bool IsDragging => dragging != null;

        public static SequenceListView Create(Transform parent, AppSettings settings, ScrollRect scroll)
        {
            var rt = UIKit.Rect("SequenceList", parent);
            UIKit.VLayout(rt, 16);
            var view = rt.gameObject.AddComponent<SequenceListView>();
            view.settings = settings;
            view.scroll = scroll;
            view.content = rt;
            view.Rebuild();
            return view;
        }

        public void Rebuild()
        {
            CancelDrag();
            foreach (var r in rows)
            {
                if (r == null) continue;
                r.gameObject.SetActive(false); // Destroy 는 프레임 끝에 일어나므로 바로 레이아웃에서 뺀다
                Destroy(r.gameObject);
            }
            rows.Clear();

            var nodes = settings.Nodes;
            var alive = new HashSet<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                alive.Add(nodes[i].uid);
                var row = SequenceNodeRow.Create(content, this, settings, i, expanded.Contains(nodes[i].uid));
                rows.Add(row);
            }
            expanded.IntersectWith(alive);
        }

        /// <summary>노드 값이 바뀌었을 때 (슬라이더 외의 경로로) 표시만 갱신.</summary>
        public void RefreshRow(int index)
        {
            if (index >= 0 && index < rows.Count) rows[index].RefreshValues();
        }

        public void SetExpanded(int uid, bool value)
        {
            if (value) expanded.Add(uid);
            else expanded.Remove(uid);
        }

        /// <summary>재생 중인 노드 표시 (-1 이면 없음).</summary>
        public void SetPlayingNode(int index)
        {
            for (int i = 0; i < rows.Count; i++) rows[i].SetPlaying(i == index);
        }

        // ---- 드래그로 순서 바꾸기 ---------------------------------------------------------

        public void BeginDrag(SequenceNodeRow row, PointerEventData e)
        {
            if (dragging != null) return;
            dragging = row;
            dragCamera = e.pressEventCamera;
            var rt = (RectTransform)row.transform;
            int index = rt.GetSiblingIndex();

            placeholder = UIKit.Rect("Placeholder", content);
            var ph = placeholder.gameObject.AddComponent<Image>();
            UIKit.SetRadius(ph, 28);
            ph.color = Theme.Line;
            ph.raycastTarget = false;
            UIKit.Layout(placeholder, preferredHeight: rt.rect.height, minHeight: rt.rect.height);
            placeholder.SetSiblingIndex(index);

            var le = row.GetComponent<LayoutElement>();
            if (le == null) le = row.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            rt.SetAsLastSibling(); // 다른 줄 위에 그린다
            row.SetDragging(true);

            lastPointer = e.position;
            grabOffset = rt.localPosition.y - PointerY(e.position);
            scroll.StopMovement();
        }

        public void Drag(SequenceNodeRow row, PointerEventData e)
        {
            if (dragging != row) return;
            lastPointer = e.position;
            UpdateDragPosition();
        }

        public void EndDrag(SequenceNodeRow row, PointerEventData e)
        {
            if (dragging != row) return;
            int from = row.Index;
            int to = placeholder.GetSiblingIndex();
            FinishDrag(to);
            if (from != to) settings.MoveNode(from, to); // → 구조 변경 이벤트 → 목록 다시 만듦
        }

        private void CancelDrag()
        {
            if (dragging == null) return;
            FinishDrag(dragging.Index);
        }

        private void FinishDrag(int siblingIndex)
        {
            var rt = (RectTransform)dragging.transform;
            rt.SetSiblingIndex(siblingIndex);
            var le = dragging.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = false;
            dragging.SetDragging(false);
            dragging = null;
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
                Destroy(placeholder.gameObject);
                placeholder = null;
            }
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        private float PointerY(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, dragCamera, out var local);
            return local.y;
        }

        private void UpdateDragPosition()
        {
            var rt = (RectTransform)dragging.transform;
            float y = PointerY(lastPointer) + grabOffset;
            rt.localPosition = new Vector3(rt.localPosition.x, y, rt.localPosition.z);

            // 끌고 있는 줄의 중심보다 위에 있는 다른 줄 개수 = 들어갈 자리
            float center = y + rt.rect.center.y;
            int target = 0;
            for (int i = 0; i < content.childCount; i++)
            {
                var child = (RectTransform)content.GetChild(i);
                if (child == rt || child == placeholder || !child.gameObject.activeSelf) continue;
                float childCenter = child.localPosition.y + child.rect.center.y;
                if (childCenter > center) target++;
            }
            if (placeholder.GetSiblingIndex() != target) placeholder.SetSiblingIndex(target);
        }

        private void Update()
        {
            if (dragging == null) return;
            // 목록 끝으로 끌면 스크롤
            var viewport = scroll.viewport;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, lastPointer, dragCamera, out var local);
            var r = viewport.rect;
            float dir = 0f;
            if (local.y > r.yMax - AutoScrollEdge) dir = -1f; // 위로
            else if (local.y < r.yMin + AutoScrollEdge) dir = 1f; // 아래로
            if (dir == 0f) return;

            var c = scroll.content;
            float maxY = Mathf.Max(0f, c.rect.height - r.height);
            var pos = c.anchoredPosition;
            float y = Mathf.Clamp(pos.y + dir * AutoScrollSpeed * Time.unscaledDeltaTime, 0f, maxY);
            if (Mathf.Approximately(y, pos.y)) return;
            c.anchoredPosition = new Vector2(pos.x, y);
            scroll.StopMovement();
            UpdateDragPosition();
        }
    }
}
