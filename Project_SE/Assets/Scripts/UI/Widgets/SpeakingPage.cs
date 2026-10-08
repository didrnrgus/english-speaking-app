using System;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>문장(말하기) 페이지 (SpeakingPage 프리팹): 카드 가운데 문장 하나 + ▶ 영어 / ▶ 한국어.</summary>
    public class SpeakingPage : UnitPage
    {
        [SerializeField] internal SentenceLine line;

        public override void Bind(ItemData item, Action<int, Lang> play)
        {
            var data = item.Lines[0];
            line.en.fontSize = EnSize(data.en);
            line.ko.fontSize = KoSize(data.ko);
            line.Bind(null, data.en, data.ko, lang => play(0, lang));
            Lines.Add(line);
        }

        // 문장 길이에 따라 글자 크기
        private static float EnSize(string s)
        {
            int n = s != null ? s.Length : 0;
            if (n <= 24) return 72;
            if (n <= 60) return 58;
            if (n <= 140) return 48;
            return 42;
        }

        private static float KoSize(string s)
        {
            int n = s != null ? s.Length : 0;
            if (n <= 20) return 46;
            if (n <= 60) return 40;
            return 34;
        }
    }
}
