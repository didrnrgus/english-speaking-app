using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnglishApp.UI
{
    /// <summary>
    /// 메인 화면.
    /// - 가운데 위: 제목
    /// - 오른쪽 위: 설정
    /// - 가운데보다 조금 위에서 아래로 2열: 왼쪽 문장(말하기) S1~S5, 오른쪽 대화 D1~D5
    /// </summary>
    public class MainScreen : BaseScreen
    {
        public const string AppTitle = "영어 말하기";

        /// <summary>2열 블록의 위쪽 끝 (화면 높이 비율).</summary>
        private const float ColumnsTop = 0.66f;

        private RectTransform columnsArea;
        private TextMeshProUGUI loadingText;

        public override void Build()
        {
            var title = UIKit.Text(Root, "Title", AppTitle, 96, Theme.Primary).SingleLine();
            title.rectTransform.TopStrip(130, 80, 150, 150);
            var subtitle = UIKit.Text(Root, "Subtitle", "듣고 · 따라 말하고 · 화면 끄고도 듣기", 34, Theme.TextSub).SingleLine().AutoSize(24, 34);
            subtitle.rectTransform.TopStrip(56, 222, 60, 60);

            var gear = UIKit.IconButton(Root, "Settings", IconKind.Gear, Theme.Text, Color.clear, 112, 64, () => App.OpenSettings(0), out _);
            ((RectTransform)gear.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(112, 112));

            columnsArea = UIKit.Rect("Columns", Root);
            columnsArea.anchorMin = new Vector2(0, 0);
            columnsArea.anchorMax = new Vector2(1, ColumnsTop);
            columnsArea.offsetMin = new Vector2(0, 0);
            columnsArea.offsetMax = new Vector2(0, 0);

            loadingText = UIKit.Text(columnsArea, "Loading", "불러오는 중…", 40, Theme.TextSub);
            loadingText.rectTransform.TopStrip(120, 40);

            if (App.Content.IsIndexLoaded) BuildColumns();
            else App.Content.IndexLoaded += BuildColumns;
        }

        private void OnDestroy()
        {
            if (AppRoot.I != null) AppRoot.I.Content.IndexLoaded -= BuildColumns;
        }

        private void BuildColumns()
        {
            App.Content.IndexLoaded -= BuildColumns;
            if (loadingText != null) Destroy(loadingText.gameObject);

            var speaking = App.Content.LevelsOf("speaking");
            var dialogue = App.Content.LevelsOf("dialogue");
            if (speaking.Count == 0 && dialogue.Count == 0)
            {
                var err = UIKit.Text(columnsArea, "Error", "예문 목록을 불러오지 못했어요.\n인터넷 연결을 확인해 주세요.", 38, Theme.TextSub);
                err.rectTransform.TopStrip(200, 40, 60, 60);
                return;
            }

            // 화면이 짧은 기기에서도 잘리지 않도록 스크롤 안에 둔다
            var scroll = UIKit.VScroll(columnsArea, "Scroll", out var content, 0, UIKit.Pad(44, 44, 0, 220));
            scroll.GetComponent<RectTransform>().Fill();
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var row = UIKit.Rect("Row", content);
            UIKit.HLayout(row, 36, null, TextAnchor.UpperCenter, expandHeight: false);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = true;

            BuildColumn(row, "문장", "한 문장씩 말하기", speaking, false);
            BuildColumn(row, "대화", "A · B 대화하기", dialogue, true);
        }

        private void BuildColumn(Transform parent, string header, string sub, System.Collections.Generic.List<LevelInfo> levels, bool dialogue)
        {
            var col = UIKit.Rect("Column_" + header, parent);
            UIKit.VLayout(col, 22);
            UIKit.Layout(col, flexibleWidth: 1);

            var head = UIKit.Rect("Header", col);
            UIKit.Layout(head, preferredHeight: 120);
            var h1 = UIKit.Text(head, "Title", header, 56, Theme.TrackColor(dialogue)).SingleLine();
            h1.rectTransform.TopStrip(70);
            var h2 = UIKit.Text(head, "Sub", sub, 28, Theme.TextSub).SingleLine().AutoSize(20, 28);
            h2.rectTransform.TopStrip(40, 76);

            foreach (var level in levels) BuildLevelButton(col, level, dialogue);
        }

        private void BuildLevelButton(Transform parent, LevelInfo level, bool dialogue)
        {
            var color = Theme.TrackColor(dialogue);
            var b = UIKit.Button(parent, "Level_" + level.level, color, 40, () => App.OpenLevel(level));
            UIKit.Layout(b, preferredHeight: 156, minHeight: 156);
            UIKit.AddShadow(b.targetGraphic, 6);
            UIKit.HLayout(b, 18, UIKit.Pad(22, 20, 0, 0), TextAnchor.MiddleLeft, expandHeight: false);

            var badge = UIKit.Image(b.transform, "Badge", new Color(1, 1, 1, 0.28f), Sprites.Circle);
            UIKit.Layout(badge, preferredHeight: 96, preferredWidth: 96);
            var code = UIKit.Text(badge.transform, "Code", level.level, 38, Theme.TextOnColor);
            code.rectTransform.Fill();

            var texts = UIKit.Rect("Texts", b.transform);
            UIKit.Layout(texts, preferredHeight: 120, flexibleWidth: 1);
            var title = UIKit.Text(texts, "Title", level.title, 40, Theme.TextOnColor, TextAlignmentOptions.BottomLeft).SingleLine().AutoSize(26, 40);
            title.rectTransform.Fill(0, 6, 0, 60);
            var desc = UIKit.Text(texts, "Desc", level.desc, 28, new Color(1, 1, 1, 0.9f), TextAlignmentOptions.TopLeft).SingleLine().AutoSize(20, 28);
            desc.rectTransform.Fill(0, 66, 0, 4);
        }
    }
}
