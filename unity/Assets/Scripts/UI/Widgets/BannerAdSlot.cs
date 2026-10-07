using System;
using System.Collections.Generic;
using EnglishApp.Ads;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>
    /// 하단 배너 광고 자리 (모든 화면 공통, 배너는 앱 전체에 하나).
    ///
    ///   SafeArea
    ///   ├─ Content            ← 화면·미니 플레이어·토스트. 아래를 광고 자리 높이만큼 줄인다
    ///   └─ AdSlot (이것)      ← 광고 전용 띠. 앱 버튼은 여기에 두지 않는다
    ///        ├ 위 여백 12dp + 구분선  (앱 버튼과 광고 사이 간격 → 실수 클릭 방지)
    ///        ├ 배너 (적응형, 보통 50~90dp, 화면 폭)
    ///        └ 아래 여백 4dp          (그 아래는 내비게이션 바·제스처 바 — SafeAreaFitter 가 이미 피함)
    ///
    /// 정책 (무효 노출·무효 클릭 방지):
    /// - 자리를 처음부터 잡아 둔다 → 광고가 늦게 떠도 화면이 밀리지 않는다 (밀린 버튼을 누르다 광고 클릭 X)
    /// - 네이티브 배너는 이 띠 안쪽 좌표에 정확히 놓는다 → 앱 UI 와 절대 겹치지 않음
    /// - 설정 팝업처럼 화면을 덮는 동안은 숨기고 새로고침도 멈춘다 (안 보이는 노출 X)
    /// - 새로고침은 SDK·대시보드 설정에 맡긴다 (코드에서 억지로 다시 불러오지 않음)
    /// - 짧은 시간에 여러 번 클릭되면 한동안 배너를 내린다 (AdConfig.ClickGuard*)
    /// </summary>
    public class BannerAdSlot : MonoBehaviour
    {
        private const float TopGapDp = 12f;
        private const float BottomGapDp = 4f;
        private const float LayoutSettleSeconds = 0.5f;
        private const float RecreateMinInterval = 10f;

        private RectTransform rt;
        private RectTransform bannerArea;
        private Canvas canvas;
        private readonly List<RectTransform> insetTargets = new List<RectTransform>();
        private readonly HashSet<string> blockers = new HashSet<string>();
        private readonly Vector3[] corners = new Vector3[4];

        private bool active;   // 이 빌드·기기에서 광고 자리를 쓰는가
        private bool native;   // 실제 SDK 를 부르는가 (Android 기기 + 패키지 + 키)
        private Vector2 sizeDp;
        private float appliedHeight = -1f;
        private float layoutChangedAt;

        private enum InitState { None, Running, Ready }
        private InitState initState;
        private float nextInitAt;
        private float initRetryDelay = 30f;

        private bool created;
        private bool loaded;
        private bool shown;
        private bool refreshPaused;
        private Vector2 createdPosDp;
        private float createdAt = -999f;
        private float posMismatchSince = -1f;
        private float nextLoadAt = -1f;
        private float loadRetryDelay = 15f;

        private ClickGuard guard;

        /// <summary>광고 자리 높이 (캔버스 단위). 광고를 안 쓰면 0.</summary>
        public float Height => Mathf.Max(0f, appliedHeight);

        public static BannerAdSlot Create(RectTransform safeArea)
        {
            var bg = UIKit.Image(safeArea, "AdSlot", Theme.AdSlotBg);
            bg.raycastTarget = true; // 배너 둘레 여백을 눌러도 아래 화면으로 터치가 새지 않게
            var slot = bg.gameObject.AddComponent<BannerAdSlot>();
            slot.rt = bg.rectTransform;
            slot.rt.BottomStrip(0);

            var line = UIKit.Image(bg.transform, "Line", Theme.Line);
            line.rectTransform.TopStrip(3);

            slot.bannerArea = UIKit.Rect("Banner", bg.transform);
            slot.Setup();
            return slot;
        }

        /// <summary>광고 자리만큼 아래를 비울 영역 (화면, 토스트 등).</summary>
        public void AddInsetTarget(RectTransform target)
        {
            insetTargets.Add(target);
            target.offsetMin = new Vector2(target.offsetMin.x, Height);
        }

        /// <summary>화면을 덮는 UI(팝업 등)가 열려 있는 동안 배너를 숨긴다. 자리는 그대로 둔다 (화면이 안 밀리게).</summary>
        public void Block(string key) => blockers.Add(key);

        public void Unblock(string key) => blockers.Remove(key);

        // ---- 준비 ---------------------------------------------------------------------------

        private void Setup()
        {
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            guard = ClickGuard.Load();

            native = AdConfig.BannerEnabled && LevelPlayBanner.Supported && AdConfig.HasAndroidKeys;
            active = native || (AdConfig.BannerEnabled && Application.isEditor && AdConfig.EditorPreview);

            if (!AdConfig.BannerEnabled)
                Debug.Log("[Ads] 배너 꺼짐 (AdConfig.BannerEnabled)");
            else if (!Application.isEditor && !LevelPlayBanner.Supported)
                Debug.LogWarning("[Ads] Ads Mediation(LevelPlay) 패키지가 없어 배너를 띄우지 않습니다 → docs/ADS.md");
            else if (!Application.isEditor && !AdConfig.HasAndroidKeys)
                Debug.LogWarning("[Ads] AdConfig 에 App Key / 배너 Ad Unit ID 가 없어 배너를 띄우지 않습니다 → docs/ADS.md");

            DeviceScreen.Poll();
            sizeDp = EstimateAdaptiveSizeDp();

            if (active && !native) BuildEditorPreview();
            if (!native) return;

            LevelPlayBanner.InitFinished += OnInitFinished;
            LevelPlayBanner.Loaded += OnLoaded;
            LevelPlayBanner.LoadFailed += OnLoadFailed;
            LevelPlayBanner.Clicked += OnClicked;
        }

        private void OnDestroy()
        {
            if (!native) return;
            LevelPlayBanner.InitFinished -= OnInitFinished;
            LevelPlayBanner.Loaded -= OnLoaded;
            LevelPlayBanner.LoadFailed -= OnLoadFailed;
            LevelPlayBanner.Clicked -= OnClicked;
            DestroyBanner();
        }

        /// <summary>
        /// SDK 값을 받기 전 적응형 배너 높이 추정 (폭 x 50/320, 50~90dp).
        /// 실제 값은 초기화 직후(광고가 뜨기 전에) 한 번 받아서 맞춘다.
        /// </summary>
        private static Vector2 EstimateAdaptiveSizeDp()
        {
            float widthDp = DeviceScreen.UnityPxToDp(Screen.safeArea.width > 0 ? Screen.safeArea.width : Screen.width);
            if (widthDp <= 0f) widthDp = 360f;
            float heightDp = Mathf.Clamp(Mathf.Round(widthDp * 50f / 320f), 50f, 90f);
            return new Vector2(Mathf.Floor(widthDp), heightDp);
        }

        private void BuildEditorPreview()
        {
            var box = UIKit.Image(bannerArea, "EditorPreview", new Color(0.55f, 0.53f, 0.6f, 0.35f));
            box.rectTransform.Fill();
            var label = UIKit.Text(box.transform, "Label", "배너 광고 자리 (에디터 미리보기)", 30, Theme.TextSub).SingleLine().AutoSize(18, 30);
            label.rectTransform.Fill(20, 4, 20, 4);
        }

        // ---- 루프 ---------------------------------------------------------------------------

        private void LateUpdate()
        {
            LevelPlayBanner.Pump();
            DeviceScreen.Poll();
            UpdateLayout();
            if (!native) return;

            float now = Time.unscaledTime;
            if (initState == InitState.None && now >= nextInitAt)
            {
                initState = InitState.Running;
                LevelPlayBanner.Init(AdConfig.AndroidAppKey, Debug.isDebugBuild);
            }
            if (initState == InitState.Ready) UpdateBanner(now);
            UpdateVisibility();
        }

        private void UpdateLayout()
        {
            float height = 0f;
            float unitsPerDp = 0f;
            if (active)
            {
                float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                unitsPerDp = DeviceScreen.DpToUnityPx(1f) / scale;
                height = (TopGapDp + sizeDp.y + BottomGapDp) * unitsPerDp;
            }

            if (Mathf.Abs(height - appliedHeight) > 0.5f)
            {
                appliedHeight = height;
                layoutChangedAt = Time.unscaledTime;
                rt.BottomStrip(height);
                foreach (var target in insetTargets)
                    if (target != null) target.offsetMin = new Vector2(target.offsetMin.x, height);
            }
            if (active) bannerArea.BottomStrip(sizeDp.y * unitsPerDp, BottomGapDp * unitsPerDp);
        }

        /// <summary>배너 왼쪽 위 좌표 (dp, 앱 화면 왼쪽 위 기준). 띠 가운데에 배너 폭만큼.</summary>
        private Vector2 BannerPositionDp()
        {
            bannerArea.GetWorldCorners(corners); // Screen Space Overlay → 화면 픽셀 (왼쪽 아래 원점)
            float centerX = (corners[0].x + corners[2].x) * 0.5f;
            float top = corners[1].y;
            float xDp = DeviceScreen.UnityPxToDp(centerX) - sizeDp.x * 0.5f;
            float yDp = DeviceScreen.UnityPxToDp(Screen.height - top);
            if (AdConfig.BannerOriginIsScreen) yDp += DeviceScreen.ContentTopDp;
            return new Vector2(Mathf.Max(0f, Mathf.Round(xDp)), Mathf.Round(yDp));
        }

        private void UpdateBanner(float now)
        {
            if (guard.IsSuspended)
            {
                if (created)
                {
                    Debug.Log("[Ads] 짧은 시간에 클릭이 많아 배너를 잠시 내립니다 (" + AdConfig.ClickGuardPauseHours + "시간)");
                    DestroyBanner();
                }
                return;
            }

            // 레이아웃(캔버스 크기, Safe Area)이 자리 잡은 뒤에 만든다
            if (now - layoutChangedAt < LayoutSettleSeconds) return;

            var pos = BannerPositionDp();
            if (!created)
            {
                CreateBanner(pos, now);
                return;
            }

            // 화면 크기·내비게이션 바가 바뀌어 자리가 옮겨졌으면 (드묾) 다시 만든다
            if ((pos - createdPosDp).sqrMagnitude > 4f)
            {
                if (posMismatchSince < 0f) posMismatchSince = now;
                if (now - posMismatchSince > 1f && now - createdAt > RecreateMinInterval)
                {
                    DestroyBanner();
                    CreateBanner(pos, now);
                }
            }
            else
            {
                posMismatchSince = -1f;
            }

            if (!loaded && nextLoadAt > 0f && now >= nextLoadAt)
            {
                nextLoadAt = -1f;
                LevelPlayBanner.Load();
            }
        }

        private void CreateBanner(Vector2 posDp, float now)
        {
            LevelPlayBanner.Create(AdConfig.AndroidBannerAdUnitId, posDp);
            created = true;
            loaded = false;
            shown = false;
            refreshPaused = false;
            createdPosDp = posDp;
            createdAt = now;
            posMismatchSince = -1f;
            loadRetryDelay = 15f;
            nextLoadAt = -1f;
            LevelPlayBanner.Load();
        }

        private void DestroyBanner()
        {
            if (!created) return;
            LevelPlayBanner.Destroy();
            created = false;
            loaded = false;
            shown = false;
            refreshPaused = false;
            nextLoadAt = -1f;
        }

        private void UpdateVisibility()
        {
            bool want = created && loaded && blockers.Count == 0 && !guard.IsSuspended;
            if (want != shown)
            {
                shown = want;
                if (want) LevelPlayBanner.Show();
                else LevelPlayBanner.Hide();
            }

            bool pause = created && loaded && !want;
            if (pause != refreshPaused)
            {
                refreshPaused = pause;
                if (pause) LevelPlayBanner.PauseRefresh();
                else LevelPlayBanner.ResumeRefresh();
            }
        }

        // ---- SDK 콜백 -----------------------------------------------------------------------

        private void OnInitFinished(bool ok, string error)
        {
            if (ok)
            {
                initState = InitState.Ready;
                initRetryDelay = 30f;
                var size = LevelPlayBanner.GetAdaptiveSizeDp();
                if (size.x > 0f && size.y > 0f)
                {
                    // 단위 확인: dp 가 아니라 px 로 오면 dp 로 바꾼다 (적응형 배너 높이는 최대 90dp 안팎)
                    if (size.y > 150f) size /= DeviceScreen.Density;
                    sizeDp = size;
                }
                else
                {
                    sizeDp = new Vector2(320f, 50f); // 고정 배너 (LevelPlayAdSize.BANNER)
                }
                Debug.Log("[Ads] LevelPlay 초기화 완료, 배너 " + sizeDp.x + "x" + sizeDp.y + "dp");
                return;
            }

            initState = InitState.None;
            nextInitAt = Time.unscaledTime + initRetryDelay;
            Debug.LogWarning("[Ads] LevelPlay 초기화 실패 (" + error + "), " + initRetryDelay + "초 뒤 다시 시도");
            initRetryDelay = Mathf.Min(initRetryDelay * 2f, 600f);
        }

        private void OnLoaded()
        {
            loaded = true;
            loadRetryDelay = 15f;
            nextLoadAt = -1f;
        }

        private void OnLoadFailed(string error)
        {
            // 한 번 받은 뒤의 실패(새로고침 실패)는 SDK 가 이전 광고를 유지하고 알아서 다시 시도한다
            if (loaded) return;
            Debug.Log("[Ads] 배너 불러오기 실패 (" + error + "), " + loadRetryDelay + "초 뒤 다시 시도");
            nextLoadAt = Time.unscaledTime + loadRetryDelay;
            loadRetryDelay = Mathf.Min(loadRetryDelay * 2f, 300f);
        }

        private void OnClicked()
        {
            guard.RecordClick();
        }

        // ---- 무효 클릭 보호 -----------------------------------------------------------------

        /// <summary>최근 클릭 시각을 저장해 두고, 창 안에서 N번 이상이면 정해진 시간 동안 배너를 내린다.</summary>
        private class ClickGuard
        {
            private const string PrefsKey = "ads_click_guard_v1";

            private readonly List<long> clicks = new List<long>();
            private long suspendedUntil;

            private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            private static long Window => AdConfig.ClickGuardWindowMinutes * 60L;
            private static long Pause => AdConfig.ClickGuardPauseHours * 3600L;

            public bool IsSuspended => Now < suspendedUntil;

            public static ClickGuard Load()
            {
                var g = new ClickGuard();
                try
                {
                    var parts = PlayerPrefs.GetString(PrefsKey, "").Split('|');
                    if (parts.Length == 2)
                    {
                        long.TryParse(parts[0], out g.suspendedUntil);
                        foreach (var s in parts[1].Split(','))
                            if (long.TryParse(s, out var t)) g.clicks.Add(t);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ads] 클릭 기록 읽기 실패: " + e.Message);
                }
                // 기기 시계를 되돌려 끝없이 막히는 일이 없게
                long now = Now;
                if (g.suspendedUntil > now + Pause) g.suspendedUntil = now + Pause;
                return g;
            }

            public void RecordClick()
            {
                long now = Now;
                clicks.RemoveAll(t => t <= now - Window || t > now);
                clicks.Add(now);
                if (clicks.Count >= AdConfig.ClickGuardCount)
                {
                    suspendedUntil = now + Pause;
                    clicks.Clear();
                }
                Save();
            }

            private void Save()
            {
                PlayerPrefs.SetString(PrefsKey, suspendedUntil + "|" + string.Join(",", clicks));
                PlayerPrefs.Save();
            }
        }
    }
}
