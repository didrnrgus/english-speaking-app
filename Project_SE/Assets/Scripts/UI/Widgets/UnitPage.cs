using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>유닛 화면의 한 페이지 (예문 하나): SpeakingPage / DialoguePage.</summary>
    public abstract class UnitPage : MonoBehaviour
    {
        [SerializeField] internal ScrollRect scroll;

        public readonly List<SentenceLine> Lines = new List<SentenceLine>();
        public ScrollRect Scroll => scroll;

        /// <summary>전체 듣기 중 따라갈 스크롤 위치 (-1 이면 없음).</summary>
        [NonSerialized] public float ScrollTarget = -1f;

        /// <summary>play(줄, 언어): 개별 듣기.</summary>
        public abstract void Bind(ItemData item, Action<int, Lang> play);
    }
}
