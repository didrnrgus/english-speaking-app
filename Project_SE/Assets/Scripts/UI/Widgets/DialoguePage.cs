using System;
using TMPro;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>대화 페이지 (DialoguePage 프리팹): 주제 + A / B 말풍선 목록.</summary>
    public class DialoguePage : UnitPage
    {
        [SerializeField] internal TextMeshProUGUI topic;
        [SerializeField] internal RectTransform turns;
        [Tooltip("A 대사 (왼쪽 배지)")]
        [SerializeField] internal SentenceLine turnAPrefab;
        [Tooltip("B 대사 (오른쪽 배지)")]
        [SerializeField] internal SentenceLine turnBPrefab;

        public override void Bind(ItemData item, Action<int, Lang> play)
        {
            if (!string.IsNullOrEmpty(item.topic)) topic.text = item.topic;

            var lines = item.Lines;
            for (int l = 0; l < lines.Count; l++)
            {
                var data = lines[l];
                int index = l;
                var turn = UIKit.Spawn(data.speaker != "B" ? turnAPrefab : turnBPrefab, turns);
                turn.name = "Turn" + (l + 1);
                turn.Bind(data.speaker, data.en, data.ko, lang => play(index, lang));
                Lines.Add(turn);
            }
        }
    }
}
