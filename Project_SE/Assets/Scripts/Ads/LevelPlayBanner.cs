#if LEVELPLAY_INSTALLED && UNITY_ANDROID && !UNITY_EDITOR
#define ADS_NATIVE
#endif
using System;
using System.Collections.Concurrent;
using UnityEngine;
#if ADS_NATIVE
using Unity.Services.LevelPlay;
using LevelPlaySdk = Unity.Services.LevelPlay.LevelPlay;
#endif

namespace EnglishApp.Ads
{
    /// <summary>
    /// LevelPlay SDK 를 직접 부르는 유일한 곳 (배너 하나만 쓴다).
    /// - Ads Mediation 패키지(com.unity.services.levelplay 9.x)가 있고 Android 기기일 때만 실제로 동작,
    ///   그 밖(에디터, 패키지 없음)에서는 아무것도 하지 않는다 → 패키지가 없어도 프로젝트가 컴파일된다.
    /// - SDK 콜백은 큐에 담았다가 Pump() 에서 메인 스레드로 넘긴다.
    /// - 위치·크기 단위는 dp. 위치는 앱 화면 왼쪽 위 기준 배너의 왼쪽 위.
    /// </summary>
    public static class LevelPlayBanner
    {
        public static event Action<bool, string> InitFinished;
#pragma warning disable CS0067 // SDK 가 없는 빌드에서는 부르지 않는다
        public static event Action Loaded;
        public static event Action<string> LoadFailed;
        public static event Action Clicked;
#pragma warning restore CS0067

        private static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

#if ADS_NATIVE
        private static bool initHooked;
        private static int generation; // 없앤 배너의 늦은 콜백은 버린다
        private static LevelPlayBannerAd banner;
        private static LevelPlayAdSize adaptiveSize;
#endif

        /// <summary>실제 광고를 띄울 수 있는 빌드인가 (Android 기기 + LevelPlay 패키지).</summary>
        public static bool Supported
        {
            get
            {
#if ADS_NATIVE
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>메인 스레드에서 매 프레임 호출.</summary>
        public static void Pump()
        {
            while (queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public static void Init(string appKey, bool debugBuild)
        {
#if ADS_NATIVE
            if (!initHooked)
            {
                initHooked = true;
                LevelPlaySdk.OnInitSuccess += _ => queue.Enqueue(() => InitFinished?.Invoke(true, null));
                LevelPlaySdk.OnInitFailed += e => queue.Enqueue(() => InitFinished?.Invoke(false, e != null ? e.ToString() : "unknown"));
            }
            try
            {
                if (debugBuild) LevelPlaySdk.SetMetaData("is_test_suite", "enable"); // 개발 빌드: 테스트 스위트 허용
                LevelPlaySdk.Init(appKey);
                if (debugBuild) LevelPlaySdk.ValidateIntegration(); // logcat 에 네트워크 어댑터 연동 상태
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] LevelPlay 초기화 호출 실패: " + e.Message);
                queue.Enqueue(() => InitFinished?.Invoke(false, e.Message));
            }
#else
            queue.Enqueue(() => InitFinished?.Invoke(false, "LevelPlay 를 쓸 수 없는 빌드"));
#endif
        }

        /// <summary>
        /// 적응형(adaptive) 배너 크기 (dp). 기기 화면 폭에 맞춘 폭·높이(보통 50~90dp).
        /// 같은 자리에서 고정 320x50 보다 eCPM 이 높다. 못 구하면 Vector2.zero.
        /// </summary>
        public static Vector2 GetAdaptiveSizeDp()
        {
#if ADS_NATIVE
            try
            {
                if (adaptiveSize == null) adaptiveSize = LevelPlayAdSize.CreateAdaptiveAdSize();
                if (adaptiveSize != null && adaptiveSize.Width > 0 && adaptiveSize.Height > 0)
                    return new Vector2(adaptiveSize.Width, adaptiveSize.Height);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] 적응형 배너 크기 계산 실패, 320x50 사용: " + e.Message);
            }
            adaptiveSize = null;
#endif
            return Vector2.zero;
        }

        /// <summary>배너를 만든다 (아직 화면에 안 보임). 위치는 만들 때 정해지므로 바꾸려면 Destroy 후 다시 Create.</summary>
        public static void Create(string adUnitId, Vector2 topLeftDp)
        {
#if ADS_NATIVE
            Destroy();
            var size = adaptiveSize ?? LevelPlayAdSize.BANNER;
            var config = new LevelPlayBannerAd.Config.Builder()
                .SetSize(size)
                .SetPosition(new LevelPlayBannerPosition(topLeftDp))
                .SetDisplayOnLoad(false) // 자리·가림 여부를 확인한 뒤 Show()
                .Build();
            banner = new LevelPlayBannerAd(adUnitId, config);
            int gen = ++generation;
            banner.OnAdLoaded += _ => queue.Enqueue(() => { if (gen == generation) Loaded?.Invoke(); });
            banner.OnAdLoadFailed += e =>
            {
                string message = e != null ? e.ToString() : "unknown";
                queue.Enqueue(() => { if (gen == generation) LoadFailed?.Invoke(message); });
            };
            banner.OnAdClicked += _ => queue.Enqueue(() => { if (gen == generation) Clicked?.Invoke(); });
#endif
        }

        public static void Load()
        {
#if ADS_NATIVE
            banner?.LoadAd();
#endif
        }

        public static void Show()
        {
#if ADS_NATIVE
            banner?.ShowAd();
#endif
        }

        public static void Hide()
        {
#if ADS_NATIVE
            banner?.HideAd();
#endif
        }

        /// <summary>숨겨 둔 동안 새 광고를 받지 않게 (보이지 않는 노출은 무효 노출).</summary>
        public static void PauseRefresh()
        {
#if ADS_NATIVE
            banner?.PauseAutoRefresh();
#endif
        }

        public static void ResumeRefresh()
        {
#if ADS_NATIVE
            banner?.ResumeAutoRefresh();
#endif
        }

        public static void Destroy()
        {
#if ADS_NATIVE
            if (banner == null) return;
            generation++;
            banner.DestroyAd();
            banner = null;
#endif
        }
    }
}
