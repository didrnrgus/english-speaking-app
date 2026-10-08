using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>화면 하나. 프리팹 루트에 붙어 있고, Init() 에서 데이터를 채운다 (모양은 프리팹에).</summary>
    public abstract class BaseScreen : MonoBehaviour
    {
        [Tooltip("위쪽 바: [뒤로] 제목 [설정] (없는 화면도 있음)")]
        [SerializeField] internal TopBar topBar;

        protected AppRoot App => AppRoot.I;
        public RectTransform Root => (RectTransform)transform;

        /// <summary>Navigator 가 프리팹을 만들고 화면 데이터를 넣은 뒤 부른다.</summary>
        public abstract void Init();

        /// <summary>맨 위 화면이 됐을 때 (처음 + 위 화면이 닫혔을 때).</summary>
        public virtual void OnShow()
        {
        }

        /// <summary>뒤로 가기를 직접 처리했으면 true.</summary>
        public virtual bool OnBack() => false;

        /// <summary>위쪽 바 연결: 제목, 뒤로(화면 닫기), 설정(개별 듣기 탭).</summary>
        protected void SetupTopBar(string title)
        {
            if (topBar == null) return;
            topBar.Setup(title, () => App.Nav.Pop(), () => App.OpenSettings(0));
        }
    }
}
