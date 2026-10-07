using TMPro;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>화면 하나. 루트 RectTransform 에 붙어 있고, Build() 에서 자기 UI 를 만든다.</summary>
    public abstract class BaseScreen : MonoBehaviour
    {
        protected AppRoot App => AppRoot.I;
        public RectTransform Root => (RectTransform)transform;

        public abstract void Build();

        /// <summary>맨 위 화면이 됐을 때 (처음 + 위 화면이 닫혔을 때).</summary>
        public virtual void OnShow()
        {
        }

        /// <summary>뒤로 가기를 직접 처리했으면 true.</summary>
        public virtual bool OnBack() => false;

        /// <summary>위쪽 바: [뒤로] 제목 [설정].</summary>
        protected TextMeshProUGUI BuildTopBar(string title, bool showBack)
        {
            var bar = UIKit.Rect("TopBar", Root).TopStrip(Theme.TopBarHeight);
            if (showBack)
            {
                var back = UIKit.IconButton(bar, "Back", IconKind.Back, Theme.Text, Color.clear, 112, 52, () => App.Nav.Pop(), out _);
                ((RectTransform)back.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(112, 112));
            }
            var gear = UIKit.IconButton(bar, "Settings", IconKind.Gear, Theme.Text, Color.clear, 112, 64, () => App.OpenSettings(0), out _);
            ((RectTransform)gear.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(112, 112));

            var text = UIKit.Text(bar, "Title", title, 50, Theme.Text).SingleLine().AutoSize(32, 50);
            text.rectTransform.Fill(150, 10, 150, 10);
            return text;
        }
    }
}
