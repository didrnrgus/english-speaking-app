using UnityEngine;
using UnityEngine.EventSystems;

namespace EnglishApp.UI
{
    /// <summary>시퀀스 노드의 손잡이(≡). 드래그를 목록(SequenceListView)에 전달한다.</summary>
    public class SequenceDragHandle : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public SequenceNodeRow row;

        // 손잡이는 누르자마자 끌리도록 (스크롤과 구분)
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;
        public void OnBeginDrag(PointerEventData eventData) => row.List.BeginDrag(row, eventData);
        public void OnDrag(PointerEventData eventData) => row.List.Drag(row, eventData);
        public void OnEndDrag(PointerEventData eventData) => row.List.EndDrag(row, eventData);
    }
}
