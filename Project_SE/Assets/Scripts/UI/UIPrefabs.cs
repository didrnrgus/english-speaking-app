using LeTai.Asset.TranslucentImage;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>
    /// 앱이 쓰는 프리팹 목록 (Assets/Resources/UI/UIPrefabs.asset).
    /// 프리팹을 바꾸려면 여기서 가리키는 프리팹(Assets/UI/Prefabs)을 열어서 고치면 된다.
    /// 화면 안의 반복 항목(레벨 버튼, 유닛 카드 등)은 각 화면 프리팹이 직접 가리킨다.
    /// </summary>
    [CreateAssetMenu(fileName = "UIPrefabs", menuName = "English App/UI Prefabs")]
    public class UIPrefabs : ScriptableObject
    {
        public const string ResourcePath = "UI/UIPrefabs";

        [Tooltip("캔버스 전체 (배경, SafeArea, 광고 띠, 미니 플레이어, 토스트)")]
        public AppCanvas appCanvas;

        [Tooltip("화면들 (메인 / 레벨 / 유닛)")]
        public BaseScreen[] screens;

        public SettingsPopup settingsPopup;

        [Tooltip("글래스 뒤 배경 블러 (카메라의 TranslucentImageSource)")]
        public BlurConfig blurConfig;

        [Tooltip("밝은 모드 팔레트")]
        public ThemePalette lightPalette;
        [Tooltip("어두운 모드 팔레트")]
        public ThemePalette darkPalette;

        private static UIPrefabs instance;

        public static UIPrefabs I
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<UIPrefabs>(ResourcePath);
                if (instance == null)
                    Debug.LogError("[UI] Resources/" + ResourcePath + " 가 없습니다. Tools > English App > Build UI Prefabs 를 실행하세요.");
                return instance;
            }
        }

        public T Screen<T>() where T : BaseScreen
        {
            foreach (var s in screens)
                if (s is T t) return t;
            Debug.LogError("[UI] UIPrefabs 에 " + typeof(T).Name + " 프리팹이 없습니다.");
            return null;
        }
    }
}
