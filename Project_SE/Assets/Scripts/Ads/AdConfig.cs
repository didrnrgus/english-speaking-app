namespace EnglishApp.Ads
{
    /// <summary>
    /// 광고 설정 (Unity LevelPlay). 키는 LevelPlay 대시보드에서 받아 여기에만 넣으면 된다 → docs/ADS.md
    /// 키가 비어 있으면 기기에서는 광고 자리 자체를 만들지 않는다 (에디터는 미리보기 자리만 표시).
    /// </summary>
    public static class AdConfig
    {
        /// <summary>LevelPlay 대시보드 &gt; Apps 의 Android App Key.</summary>
        public static readonly string AndroidAppKey = "";

        /// <summary>LevelPlay 대시보드 &gt; Ad Units 의 Android 배너 Ad Unit ID.</summary>
        public static readonly string AndroidBannerAdUnitId = "";

        /// <summary>하단 배너 전체 켜기/끄기 (나중에 '광고 제거' 구매를 붙일 때 여기와 연결).</summary>
        public static readonly bool BannerEnabled = true;

        /// <summary>에디터에서 배너 자리를 회색 상자로 보여 준다 (레이아웃 확인용, SDK 는 부르지 않음).</summary>
        public static readonly bool EditorPreview = true;

        /// <summary>
        /// 배너 좌표(dp)의 기준. false = 앱 화면(content view) 왼쪽 위, true = 기기 화면 왼쪽 위.
        /// 상태 바를 숨기는 전체 화면(Unity 기본)에서는 둘이 같다. 기기에서 배너가 상태 바 높이만큼 어긋나면 바꾼다.
        /// </summary>
        public static readonly bool BannerOriginIsScreen = false;

        // ---- 무효 클릭 보호 ------------------------------------------------------------------
        // 짧은 시간에 배너를 여러 번 누르면(실수·장난·아이가 연타) 한동안 배너를 내린다.
        // 무효 클릭은 계정 정지 사유라 앱 쪽에서도 막아 둔다.

        public static readonly int ClickGuardCount = 3;
        public static readonly int ClickGuardWindowMinutes = 30;
        public static readonly int ClickGuardPauseHours = 24;

        public static bool HasAndroidKeys =>
            !string.IsNullOrWhiteSpace(AndroidAppKey) && !string.IsNullOrWhiteSpace(AndroidBannerAdUnitId);
    }
}
