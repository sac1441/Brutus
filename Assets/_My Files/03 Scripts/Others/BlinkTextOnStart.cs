using System.Collections;
using UnityEngine;
using TMPro;
using TarodevController;

/// <summary>
/// Blinks a TextMeshProUGUI element for a set duration at the start of the game
/// (e.g. "TAP TO JUMP" prompt), then settles into a steady visible state --
/// UNLESS the player jumps first, in which case it disappears immediately
/// regardless of the timer. Once the player has ever jumped, this stays hidden
/// permanently -- not just for this scene/session but across future app
/// launches too (backed by PlayerPrefs, since a static field alone would only
/// survive a scene reload, not the app being closed and reopened).
/// Attach this to the GameObject holding the TextMeshProUGUI component.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class BlinkTextOnStart : MonoBehaviour
{
    private const string HasJumpedBeforePrefKey = "Brutus.HasJumpedBefore";
    [Header("Blink Settings")]
    [Tooltip("How long (seconds) the blinking should last after the game starts.")]
    [SerializeField] private float blinkDuration = 2f;

    [Tooltip("How many times per second the text toggles visibility.")]
    [SerializeField] private float blinkSpeed = 8f;

    [Tooltip("Should the text end up visible (true) or hidden (false) once blinking stops?")]
    [SerializeField] private bool visibleAtEnd = true;

    [Tooltip("Optional: use fading (alpha lerp) instead of a hard on/off toggle.")]
    [SerializeField] private bool smoothFade = false;

    private TextMeshProUGUI _text;
    private PlayerController _player;

    private void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // Already jumped in some past session (or earlier this one) -- stay hidden
        // permanently, skip the blink entirely, and don't bother subscribing.
        if (PlayerPrefs.GetInt(HasJumpedBeforePrefKey, 0) == 1)
        {
            SetAlpha(_text.color, 0f);
            gameObject.SetActive(false);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(BlinkRoutine());

        _player = FindFirstObjectByType<PlayerController>();
        if (_player != null)
            _player.Jumped += HideOnFirstJump;
    }

    private void OnDisable()
    {
        if (_player != null)
            _player.Jumped -= HideOnFirstJump;
    }

    /// <summary>
    /// The player's very first jump, ever -- hide immediately regardless of the
    /// blink timer, persist that fact so it never shows again (this session,
    /// future scene reloads, or future app launches), and unsubscribe.
    /// </summary>
    private void HideOnFirstJump()
    {
        if (_player != null)
            _player.Jumped -= HideOnFirstJump;

        PlayerPrefs.SetInt(HasJumpedBeforePrefKey, 1);
        PlayerPrefs.Save();

        StopAllCoroutines();
        SetAlpha(_text.color, 0f);
        gameObject.SetActive(false);
    }

    private IEnumerator BlinkRoutine()
    {
        float elapsed = 0f;
        Color baseColor = _text.color;

        while (elapsed < blinkDuration)
        {
            if (smoothFade)
            {
                // Smooth sine-wave fade between 0 and 1 alpha
                float alpha = (Mathf.Sin(elapsed * blinkSpeed) + 1f) * 0.5f;
                SetAlpha(baseColor, alpha);
            }
            else
            {
                // Hard on/off toggle
                bool show = Mathf.FloorToInt(elapsed * blinkSpeed) % 2 == 0;
                SetAlpha(baseColor, show ? 1f : 0f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Settle into final state
        SetAlpha(baseColor, visibleAtEnd ? 1f : 0f);
    }

    private void SetAlpha(Color baseColor, float alpha)
    {
        Color c = baseColor;
        c.a = alpha;
        _text.color = c;
    }
}