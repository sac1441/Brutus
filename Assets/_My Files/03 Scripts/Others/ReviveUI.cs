using System.Collections;
using TarodevController;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReviveUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject revivePanel;
    [SerializeField] private EggShopUI eggShopUI;

    [Header("Egg Revive")]
    [SerializeField] private int eggReviveCost = 30;
    [SerializeField] private Button eggButton;
    [SerializeField] private Text eggCostText;

    [Header("Ad Revive")]
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Text watchAdButtonText; // optional, e.g. "Watch Ad to Revive"

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Ad Input Guard")]
    [Tooltip("Ignore panel button clicks for this many real-time seconds after the ad closes. Prevents the dismiss-tap from the native ad overlay leaking through as a click on Close/Egg/Watch-Ad.")]
    [SerializeField] private float adCloseInputGuard = 0.35f;

    [Header("Ad Revive Limit")]
    [Tooltip("The ad button is offered on deaths 1..this number in a run. From the next death on it is disabled, however the earlier revives were paid for.")]
    [SerializeField] private int maxAdRevives = 3;
    private int _deathCount;   // deaths this run (resets when the scene reloads)

    [Header("Test Mode")]
    [Tooltip("TESTING ONLY: on death, skip the revive panel and revive Brutus immediately. Turn OFF before release.")]
    [SerializeField] private bool testModeAutoRevive = false;
    [Tooltip("Real-time pause before the automatic revive.")]
    [SerializeField] private float testModeReviveDelay = 0.3f;

    private void Awake()
    {
        if (testModeAutoRevive || TestMode.Enabled) Debug.LogWarning("[ReviveUI] TEST MODE is ON - deaths auto-revive without the panel. Turn it off before release.");
        if (revivePanel != null) revivePanel.SetActive(false);
        eggButton.onClick.AddListener(OnEggReviveClicked);
        closeButton.onClick.AddListener(OnCloseClicked);

        if (watchAdButton != null)
            watchAdButton.onClick.AddListener(OnWatchAdClicked);
    }

    private void OnEnable()
    {
        if (playerController != null) playerController.Died += Show;

        // AdManager initializes asynchronously (MobileAds.Initialize), so its Instance
        // may not exist yet when this runs - retry each frame until it does, instead of
        // silently failing to subscribe forever.
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnAdClosed += HandleAdClosed;
        }
        else
        {
            StartCoroutine(SubscribeToAdManagerWhenReady());
        }
    }

    private IEnumerator SubscribeToAdManagerWhenReady()
    {
        while (AdManager.Instance == null)
            yield return null;

        AdManager.Instance.OnAdClosed += HandleAdClosed;
    }

    private void OnDisable()
    {
        if (playerController != null) playerController.Died -= Show;
        if (AdManager.Instance != null) AdManager.Instance.OnAdClosed -= HandleAdClosed;
    }

    private void HandleAdClosed()
    {
        // Ad has actually closed - this is the ONLY place the ad-earned revive is applied.
        if (_pendingRevive)
        {
            _pendingRevive = false;
            Debug.Log("[ReviveUI] Ad closed with pending reward - reviving player now.");
            Hide();
            _inputMasked = false; // panel is gone, no more UI clicks to guard
            playerController.Revive();
            // Revive() owns InputLocked from here (locks during rewind, unlocks when done).
            // Do NOT start the unmask timer or it will unlock mid-rewind.
            return;
        }

        // Ad closed WITHOUT a reward - panel stays up. Short safety buffer before
        // unmasking so the dismiss-tap can't bleed into Close/Egg on the same frame.
        if (_reenableInputRoutine != null) StopCoroutine(_reenableInputRoutine);
        _reenableInputRoutine = StartCoroutine(ReenableInputAfterDelay());
    }

    private Coroutine _reenableInputRoutine;

    /// <summary>True while an ad is showing (or in the brief buffer just after it closes).
    /// Panel button clicks are ignored while this is true, and the player's jump input
    /// is masked via playerController.InputLocked.</summary>
    private bool _inputMasked;

    private void MaskGameInput()
    {
        _inputMasked = true;
        if (playerController != null) playerController.InputLocked = true;
    }

    private void UnmaskGameInput()
    {
        _inputMasked = false;
        if (playerController != null) playerController.InputLocked = false;
    }

    private IEnumerator ReenableInputAfterDelay()
    {
        // Real-time wait since Time.timeScale may be 0 while the revive panel is up.
        yield return new WaitForSecondsRealtime(adCloseInputGuard);
        UnmaskGameInput();
        _reenableInputRoutine = null;
        RefreshAdButton();
    }

    private void Show()
    {
        _deathCount++;
        Debug.Log("[ReviveUI] Death #" + _deathCount + " - ad revive " + (_deathCount <= maxAdRevives ? "offered" : "disabled"));

        if (testModeAutoRevive || TestMode.Enabled)   // Inspector box, or the hidden on-device switch
        {
            StartCoroutine(TestModeRevive());
            return;
        }
        if (eggCostText != null) eggCostText.text = eggReviveCost.ToString();
        eggButton.interactable = true; // always clickable now - either revives or opens the shop

        RefreshAdButton();
        if (revivePanel != null) revivePanel.SetActive(true);

        // The next rewarded ad may still be loading (it reloads after every ad shown).
        // Keep re-checking while the panel is open so the button turns on as soon as it's ready.
        if (_adRefreshRoutine != null) StopCoroutine(_adRefreshRoutine);
        _adRefreshRoutine = StartCoroutine(RefreshAdButtonWhileOpen());
    }

    private Coroutine _adRefreshRoutine;

    private IEnumerator TestModeRevive()
    {
        yield return new WaitForSecondsRealtime(testModeReviveDelay);   // game is paused on death
        Debug.Log("[ReviveUI] TEST MODE: auto-revive after death #" + _deathCount);
        playerController.Revive();
    }

    private IEnumerator RefreshAdButtonWhileOpen()
    {
        var wait = new WaitForSecondsRealtime(0.25f);   // real time: game is paused on the death screen
        while (revivePanel != null && revivePanel.activeInHierarchy)
        {
            RefreshAdButton();
            yield return wait;
        }
        _adRefreshRoutine = null;
    }

    private void RefreshAdButton()
    {
        if (watchAdButton == null) return;
        if (_inputMasked || _pendingRevive) return;   // an ad is showing / just finished - leave it alone

        bool hasChances = _deathCount <= maxAdRevives;
        bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady;
        if (hasChances && !adReady && AdManager.Instance != null && !AdManager.Instance.IsLoading)
            AdManager.Instance.LoadRewardedAd();   // nothing in flight - ask for one

        watchAdButton.interactable = adReady && hasChances;
        if (watchAdButtonText != null)
        {
            if (!hasChances) watchAdButtonText.text = "No Revives Left";
            else if (!adReady) watchAdButtonText.text = "Loading ad...";
            else watchAdButtonText.text = "Watch Ad to Revive";
        }
    }

    private void Hide()
    {
        if (_adRefreshRoutine != null) { StopCoroutine(_adRefreshRoutine); _adRefreshRoutine = null; }
        if (revivePanel != null) revivePanel.SetActive(false);
    }

    // ---------- Egg revive ----------
    private void OnEggReviveClicked()
    {
        if (_inputMasked) return; // ad showing / just closed - ignore possible phantom click

        Debug.Log($"[ReviveUI] Egg button clicked. EggCount={playerController.EggCount}, eggReviveCost={eggReviveCost}, eggShopUI assigned={eggShopUI != null}");
        if (playerController.EggCount < eggReviveCost)
        {
            Debug.Log("[ReviveUI] Not enough eggs - calling eggShopUI.Open(revivePanel)");
            eggShopUI.Open(revivePanel);
            return;
        }
        Debug.Log("[ReviveUI] Enough eggs - spending and reviving");
        if (!playerController.TrySpendEggs(eggReviveCost)) return;
        Hide();
        playerController.Revive();
    }

    // ---------- Ad revive ----------
    private bool _pendingRevive;

    private void OnWatchAdClicked()
    {
        if (AdManager.Instance == null || !AdManager.Instance.IsRewardedAdReady)
        {
            Debug.LogWarning("[ReviveUI] Watch ad clicked but no ad is ready.");
            return;
        }

        // Disable immediately so the player can't double-tap while the ad is showing.
        watchAdButton.interactable = false;

        // Mask both UI clicks and jump input for the duration of the ad.
        MaskGameInput();

        // NOTE: Time.timeScale is currently 0 (paused from Die()). The ad itself renders
        // in its own native overlay so this doesn't block it from displaying/playing.
        AdManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                // Reward is typically granted mid-video, before the player has even
                // tapped to close the ad. Don't revive yet - just mark it pending.
                // The actual revive happens in HandleAdClosed, once the ad is fully gone.
                Debug.Log("[ReviveUI] Ad reward earned - revive will apply once the ad closes.");
                _pendingRevive = true;
            },
            onFailed: () =>
            {
                Debug.LogWarning("[ReviveUI] Ad failed to show or was skipped early - no revive granted.");
                _pendingRevive = false;
                // The ad never opened, but input was masked before trying - release it so
                // Close / Egg work again. The refresh loop re-enables the ad button once one loads.
                UnmaskGameInput();
                RefreshAdButton();
            }
        );
    }

    // ---------- Close (restart from beginning) ----------
    private void OnCloseClicked()
    {
        if (_inputMasked) return; // ad showing / just closed - ignore possible phantom click

        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
    }

    private void OnDestroy()
    {
        eggButton.onClick.RemoveAllListeners();
        closeButton.onClick.RemoveAllListeners();
        if (watchAdButton != null) watchAdButton.onClick.RemoveAllListeners();
    }
}