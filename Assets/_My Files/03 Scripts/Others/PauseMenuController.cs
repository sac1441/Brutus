using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PauseMenuController : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The 'Pause' GameObject containing Panel / Panel (1) / Panel (2)")]
    [SerializeField] private GameObject pauseMenuRoot;

    [Header("Panels")]
    [SerializeField] private GameObject panelMain;      // Panel      - Pause / Settings / Play
    [SerializeField] private GameObject panelSettings;  // Panel (1)  - Sound / Notification / Credits
    [SerializeField] private GameObject panelCredits;   // Panel (2)  - Credits text

    [Header("Top bar")]
    [Tooltip("The hash/pause icon button in the top-right corner")]
    [SerializeField] private Button pauseButton;

    [Header("Panel: Main")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;

    [Header("Panel: Settings")]
    [SerializeField] private Button soundToggleButton;
    [SerializeField] private Text soundToggleLabel; // shows "SOUND ON" / "SOUND OFF"
    [SerializeField] private Button notificationToggleButton;
    [SerializeField] private Text notificationToggleLabel; // shows "NOTIFICATION ON" / "NOTIFICATION OFF"
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button settingsBackButton; // the small X, goes back to Panel (main)

    [Header("Panel: Credits")]
    [SerializeField] private Button creditsCloseButton; // goes back to Panel (1)

    private bool _soundOn = true;
    private bool _notificationOn = true;
    private readonly Stack<GameObject> _panelHistory = new Stack<GameObject>();
    private GameObject _currentPanel;

    private void Awake()
    {
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);

        pauseButton.onClick.AddListener(OpenPauseMenu);
        playButton.onClick.AddListener(ClosePauseMenu);
        settingsButton.onClick.AddListener(() => ShowPanel(panelSettings));
        settingsBackButton.onClick.AddListener(GoBack);
        creditsButton.onClick.AddListener(() => ShowPanel(panelCredits));
        creditsCloseButton.onClick.AddListener(GoBack);
        soundToggleButton.onClick.AddListener(ToggleSound);
        notificationToggleButton.onClick.AddListener(ToggleNotification);

        UpdateSoundLabel();
        UpdateNotificationLabel();
    }

    // ---------- Open / Close ----------

    private void OpenPauseMenu()
    {
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);

        _panelHistory.Clear();
        ActivateOnly(panelMain);
        _currentPanel = panelMain;

        Time.timeScale = 0f;
    }

    private void ClosePauseMenu()
    {
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    // ---------- Panel navigation ----------

    private void ShowPanel(GameObject target)
    {
        if (_currentPanel != null) _panelHistory.Push(_currentPanel);
        ActivateOnly(target);
        _currentPanel = target;
    }

    private void GoBack()
    {
        if (_panelHistory.Count == 0)
        {
            // Nothing to go back to - fall back to the main pause panel.
            ActivateOnly(panelMain);
            _currentPanel = panelMain;
            return;
        }

        GameObject previous = _panelHistory.Pop();
        ActivateOnly(previous);
        _currentPanel = previous;
    }

    private void ActivateOnly(GameObject target)
    {
        if (panelMain != null) panelMain.SetActive(panelMain == target);
        if (panelSettings != null) panelSettings.SetActive(panelSettings == target);
        if (panelCredits != null) panelCredits.SetActive(panelCredits == target);
    }

    // ---------- Sound ----------

    private void ToggleSound()
    {
        _soundOn = !_soundOn;
        AudioListener.volume = _soundOn ? 1f : 0f;
        UpdateSoundLabel();
    }

    private void UpdateSoundLabel()
    {
        if (soundToggleLabel != null)
            soundToggleLabel.text = _soundOn ? "SOUND ON" : "SOUND OFF";
    }

    // ---------- Notification ----------

    private void ToggleNotification()
    {
        _notificationOn = !_notificationOn;
        UpdateNotificationLabel();

        // Intentionally empty for now - hook actual notification
        // scheduling/cancelling logic in here later.
    }

    private void UpdateNotificationLabel()
    {
        if (notificationToggleLabel != null)
            notificationToggleLabel.text = _notificationOn ? "NOTIFICATION ON" : "NOTIFICATION OFF";
    }

    private void OnDestroy()
    {
        pauseButton.onClick.RemoveAllListeners();
        playButton.onClick.RemoveAllListeners();
        settingsButton.onClick.RemoveAllListeners();
        settingsBackButton.onClick.RemoveAllListeners();
        creditsButton.onClick.RemoveAllListeners();
        creditsCloseButton.onClick.RemoveAllListeners();
        soundToggleButton.onClick.RemoveAllListeners();
        notificationToggleButton.onClick.RemoveAllListeners();
    }
}