using UnityEngine;

/// <summary>
/// Opening splash: the BRUTUS title drops in with a small bounce, then gently bobs.
/// Uses unscaled time so it works even if timeScale is changed.
/// </summary>
public class OpeningTitlePop : MonoBehaviour
{
    public float startDelay = 0.15f;
    public float popDuration = 0.55f;
    public float bobAmount = 6f;     // UI units
    public float bobSpeed = 2.2f;

    private RectTransform _rt;
    private Vector2 _basePos;
    private float _t;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _basePos = _rt.anchoredPosition;
        _rt.localScale = Vector3.zero;
    }

    private void Update()
    {
        _t += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01((_t - startDelay) / popDuration);
        float s = p <= 0f ? 0f : EaseOutBack(p);
        _rt.localScale = new Vector3(s, s, 1f);
        float bob = p >= 1f ? Mathf.Sin((_t - startDelay - popDuration) * bobSpeed) * bobAmount : 0f;
        _rt.anchoredPosition = _basePos + new Vector2(0f, bob);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float a = x - 1f;
        return 1f + c3 * a * a * a + c1 * a * a;
    }
}
