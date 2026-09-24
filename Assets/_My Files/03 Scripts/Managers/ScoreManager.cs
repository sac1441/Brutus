using System.Collections;
using TarodevController;
using TMPro;
using UnityEngine;
public class ScoreManager : MonoBehaviour
{
    public PlayerController PlayerController;
    public TextMeshProUGUI scoreText;
    private int score = 1;

    [Header("Score Pop")]
    [Tooltip("Peak scale when the score changes.")]
    public float popScale = 1.06f;
    [Tooltip("Seconds for the whole pop.")]
    public float popDuration = 0.2f;
    private Vector3 _baseScale = Vector3.one;
    private Coroutine _pop;

    public int CurrentScore => score;

    /// <summary>Add several points at once (e.g. floors skipped by the rocket revive).</summary>
    public void AddPoints(int points)
    {
        if (points <= 0) return;
        score += points;
        scoreText.text = score.ToString();
        if (_pop != null) { StopCoroutine(_pop); scoreText.transform.localScale = _baseScale; }
        _pop = StartCoroutine(Pop());
    }

    private void AddScore()
    {
        score++;
        scoreText.text = score.ToString();
        if (_pop != null) { StopCoroutine(_pop); scoreText.transform.localScale = _baseScale; }
        _pop = StartCoroutine(Pop());
        // TEMPORARY DIAGNOSTIC -- remove once the delayed-score issue is found.
        Debug.Log($"[ScoreManager] AddScore fired -> score={score}, player Y={PlayerController.transform.position.y:F2}");
    }
    private void Start()
    {
        Debug.Log($"[ScoreManager] Start() -- subscribing to Jumped. PlayerController null? {PlayerController == null}, scoreText null? {scoreText == null}");
        if (scoreText != null) _baseScale = scoreText.transform.localScale;
        PlayerController.Jumped += AddScore;
    }
    private void OnDisable()
    {
        PlayerController.Jumped -= AddScore;
    }

    // Quick grow, then settle back with a tiny squish. Unscaled time so it plays even when paused.
    private IEnumerator Pop()
    {
        var rt = scoreText.transform;
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / popDuration);
            float k;
            if (n < 0.3f) k = Mathf.Sin((n / 0.3f) * Mathf.PI * 0.5f);   // grow
            else { float u = (n - 0.3f) / 0.7f; k = (1f - u) * Mathf.Cos(u * Mathf.PI); } // settle, slight undershoot
            rt.localScale = _baseScale * (1f + (popScale - 1f) * k);
            yield return null;
        }
        rt.localScale = _baseScale;
        _pop = null;
    }
}