using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 세로 스크롤 안에서 가로로 끌면 부모(PagerView)에게 넘기는 ScrollRect.
    /// 예문 페이지 안의 긴 문장/대화 목록을 스크롤하면서도 좌우로 넘길 수 있다.
    /// </summary>
    public class DirectionalScrollRect : ScrollRect
    {
        private bool routeToParent;

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            ToParent<IInitializePotentialDragHandler>(h => h.OnInitializePotentialDrag(eventData));
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            var d = eventData.position - eventData.pressPosition;
            routeToParent = Mathf.Abs(d.x) > Mathf.Abs(d.y);
            if (routeToParent) ToParent<IBeginDragHandler>(h => h.OnBeginDrag(eventData));
            else base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (routeToParent) ToParent<IDragHandler>(h => h.OnDrag(eventData));
            else base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (routeToParent) ToParent<IEndDragHandler>(h => h.OnEndDrag(eventData));
            else base.OnEndDrag(eventData);
            routeToParent = false;
        }

        private void ToParent<T>(System.Action<T> action) where T : class, IEventSystemHandler
        {
            var p = transform.parent;
            while (p != null)
            {
                var handler = p.GetComponent<T>();
                if (handler != null)
                {
                    action(handler);
                    return;
                }
                p = p.parent;
            }
        }
    }
}
