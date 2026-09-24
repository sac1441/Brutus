using System.Collections;
using UnityEngine;

/// <summary>
/// Idle life for the wall snake: random blinks and quick tongue flicks.
/// Each snake uses its own random timing. Also hides itself if the left walls it
/// would cover are missing or moved in this stack instance (e.g. shortened platforms).
/// </summary>
[ExecuteAlways]
public class SnakeIdle : MonoBehaviour
{
    [Header("Parts")]
    public SpriteRenderer eyesClosed;
    public Transform tongue;

    [Header("Blink")]
    public Vector2 blinkInterval = new Vector2(2.5f, 5f);
    public float blinkDuration = 0.12f;

    [Header("Tongue")]
    public Vector2 tongueInterval = new Vector2(1.5f, 3.5f);
    public float tongueOut = 0.05f, tongueHold = 0.08f, tongueIn = 0.05f;
    [Range(0, 1)] public float doubleFlickChance = 0.4f;

    [Header("Wall check")]
    [Tooltip("Max sideways distance between the snake's pillar and each covered left wall.")]
    public float wallTolerance = 0.8f;

    private bool _visible = true;

    private void OnEnable()
    {
        ResetParts();
        RefreshVisibility();
        if (Application.isPlaying)
        {
            StartCoroutine(BlinkLoop());
            StartCoroutine(TongueLoop());
        }
    }

#if UNITY_EDITOR
    private void Update() { if (!Application.isPlaying) RefreshVisibility(); }
#endif

    private void ResetParts()
    {
        if (eyesClosed != null) eyesClosed.enabled = false;
        if (tongue != null) tongue.localScale = new Vector3(1f, 0f, 1f);
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blinkInterval.x, blinkInterval.y));
            if (!_visible || eyesClosed == null) continue;
            eyesClosed.enabled = true;
            yield return new WaitForSeconds(blinkDuration);
            eyesClosed.enabled = false;
        }
    }

    private IEnumerator TongueLoop()
    {
        yield return new WaitForSeconds(Random.Range(0f, tongueInterval.y));
        while (true)
        {
            if (_visible && tongue != null)
            {
                yield return Flick();
                if (Random.value < doubleFlickChance) { yield return new WaitForSeconds(0.06f); yield return Flick(); }
            }
            yield return new WaitForSeconds(Random.Range(tongueInterval.x, tongueInterval.y));
        }
    }

    private IEnumerator Flick()
    {
        for (float t = 0; t < tongueOut; t += Time.deltaTime) { SetTongue(t / tongueOut); yield return null; }
        SetTongue(1f);
        yield return new WaitForSeconds(tongueHold);
        for (float t = 0; t < tongueIn; t += Time.deltaTime) { SetTongue(1f - t / tongueIn); yield return null; }
        SetTongue(0f);
    }

    private void SetTongue(float k) { tongue.localScale = new Vector3(1f, Mathf.Clamp01(k), 1f); }

    /// <summary>Show only if every left wall in the snake's height range is present and lined up.</summary>
    public void RefreshVisibility()
    {
        var body = GetComponent<SpriteRenderer>();
        var stack = transform.parent;
        if (body == null || stack == null || body.sprite == null) return;

        float x = transform.position.x;
        Vector2 half = body.sprite.bounds.extents * (Vector2)transform.lossyScale;
        float yMin = transform.position.y - half.y, yMax = transform.position.y + half.y;

        bool ok = true; int covered = 0;
        foreach (Transform plat in stack)
        {
            if (!plat.name.StartsWith("Platform")) continue;
            float py = plat.position.y;
            if (py < yMin - 0.5f || py > yMax) continue;
            covered++;
            Transform wall = null;
            foreach (Transform c in plat) if (c.name.Trim() == "Left Wall") { wall = c; break; }
            if (wall == null || !wall.gameObject.activeInHierarchy || !plat.gameObject.activeInHierarchy) { ok = false; break; }
            float wx = 0f; int n = 0;
            foreach (var sr in wall.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (sr.sprite == null || !sr.sprite.name.StartsWith("Platform_Stone")) continue;
                wx += sr.bounds.center.x; n++;
            }
            if (n == 0 || Mathf.Abs(wx / n - x) > wallTolerance) { ok = false; break; }
        }
        if (covered == 0) ok = false;

        if (ok == _visible && body.enabled == ok) return;
        _visible = ok;
        body.enabled = ok;
        if (!ok) { if (eyesClosed != null) eyesClosed.enabled = false; if (tongue != null) tongue.localScale = new Vector3(1f, 0f, 1f); }
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) if (r != body && r != eyesClosed) r.enabled = ok;
    }

    /// <summary>True if this snake is shown and covers world height y (used to keep torches off it).</summary>
    public bool Covers(float y)
    {
        var body = GetComponent<SpriteRenderer>();
        if (body == null || !body.enabled || body.sprite == null) return false;
        float h = body.sprite.bounds.extents.y * transform.lossyScale.y;
        return y > transform.position.y - h - 1.5f && y < transform.position.y + h;
    }
}
