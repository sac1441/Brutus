using System;
using System.Collections;
using GoogleMobileAds.Api;
using UnityEngine;

/// <summary>
/// Singleton wrapper around the AdMob Rewarded Ad API.
/// Handles init, loading, showing, and auto-reloading the next ad.
/// Attach this to a single persistent GameObject (e.g. an empty "AdManager" in your first scene)
/// and mark it DontDestroyOnLoad so it survives scene reloads (SceneManager.LoadScene(1) etc).
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    [Header("Ad Unit IDs")]
    [Tooltip("Use Google's TEST ad unit ID during development. Replace with your real AdMob rewarded ad unit ID before release.")]
    [SerializeField] private string androidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917"; // Google test ID
    [SerializeField] private string iosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313"; // Google test ID

    private RewardedAd _rewardedAd;
    private bool _isInitialized;
    private bool _isLoading;
    private int _failedLoads;
    private Coroutine _retryRoutine;

    /// <summary>True while a rewarded ad request is in flight.</summary>
    public bool IsLoading => _isLoading;

    /// <summary>
    /// Fired the moment the native ad overlay closes (user tapped its close/skip control).
    /// UI that sits underneath should ignore clicks for a short window after this fires,
    /// since the dismiss-tap can bleed through into Unity's EventSystem on the same frame.
    /// </summary>
    public event Action OnAdClosed;

    /// <summary>Time.unscaledTime of the last ad close. Use this to guard against phantom clicks.</summary>
    public float LastAdCloseTimeUnscaled { get; private set; } = -999f;

    private string AdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return androidRewardedAdUnitId;
#elif UNITY_IOS
            return iosRewardedAdUnitId;
#else
            return androidRewardedAdUnitId;
#endif
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Deliver ad callbacks on Unity's main thread so UI/scene code in them is safe.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        MobileAds.Initialize(initStatus =>
        {
            _isInitialized = true;
            Debug.Log("[AdManager] Mobile Ads SDK initialized.");
            LoadRewardedAd();
        });
    }

    /// <summary>
    /// Loads a rewarded ad into memory so it's ready to show instantly when needed.
    /// Safe to call repeatedly - if an ad is already loaded or loading, this just returns.
    /// </summary>
    public void LoadRewardedAd()
    {
        if (!_isInitialized || _isLoading) return;
        if (IsRewardedAdReady) return;

        // Clean up any previous ad instance before loading a new one.
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        Debug.Log("[AdManager] Loading rewarded ad...");
        _isLoading = true;
        var adRequest = new AdRequest();

        RewardedAd.Load(AdUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            _isLoading = false;
            if (error != null || ad == null)
            {
                _failedLoads++;
                float wait = Mathf.Min(30f, 2f * Mathf.Pow(2f, _failedLoads - 1));
                Debug.LogWarning($"[AdManager] Rewarded ad failed to load ({_failedLoads}x): {error}. Retrying in {wait}s.");
                if (_retryRoutine != null) StopCoroutine(_retryRoutine);
                _retryRoutine = StartCoroutine(RetryLoadAfter(wait));
                return;
            }

            _failedLoads = 0;
            Debug.Log("[AdManager] Rewarded ad loaded successfully.");
            _rewardedAd = ad;
            RegisterEventHandlers(_rewardedAd);
        });
    }

    private IEnumerator RetryLoadAfter(float seconds)
    {
        // Real time: the death screen pauses the game with timeScale = 0.
        yield return new WaitForSecondsRealtime(seconds);
        _retryRoutine = null;
        LoadRewardedAd();
    }

    /// <summary>
    /// True if a rewarded ad is loaded and ready to show right now.
    /// </summary>
    public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.CanShowAd();

    /// <summary>
    /// Shows the rewarded ad if ready. Calls onRewarded ONLY if the player watched to completion
    /// and earned the reward. Calls onFailed if no ad is ready or something went wrong.
    /// </summary>
    public void ShowRewardedAd(Action onRewarded, Action onFailed)
    {
        if (!IsRewardedAdReady)
        {
            Debug.LogWarning("[AdManager] Tried to show rewarded ad but none is ready.");
            onFailed?.Invoke();
            LoadRewardedAd(); // try to get one ready for next time
            return;
        }

        _rewardedAd.Show((Reward reward) =>
        {
            Debug.Log($"[AdManager] Reward earned: {reward.Amount} {reward.Type}");
            onRewarded?.Invoke();
        });
    }

    private void RegisterEventHandlers(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdManager] Rewarded ad closed. Loading next one.");
            LastAdCloseTimeUnscaled = Time.unscaledTime;
            OnAdClosed?.Invoke();
            _rewardedAd?.Destroy();
            _rewardedAd = null;
            LoadRewardedAd();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogWarning($"[AdManager] Rewarded ad failed to show: {error}");
            LastAdCloseTimeUnscaled = Time.unscaledTime;
            OnAdClosed?.Invoke(); // treat a failed show as "closed" so listeners re-enable input
            _rewardedAd?.Destroy();
            _rewardedAd = null;
            LoadRewardedAd();
        };
    }
}