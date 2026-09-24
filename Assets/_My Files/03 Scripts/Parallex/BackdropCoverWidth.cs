using UnityEngine;

/// <summary>
/// Scales a camera-attached backdrop sprite (uniformly, keeping proportions) so its
/// painted area always covers the full screen width on any aspect ratio.
/// Uses the sprite's mesh (Tight mesh = the actual painted outline), not its rectangle.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class BackdropCoverWidth : MonoBehaviour
{
    public Camera targetCamera;
    [Tooltip("Extra world units of coverage beyond each screen edge.")]
    public float margin = 0.4f;

    [SerializeField, HideInInspector] private Vector3 _baseScale;
    [SerializeField, HideInInspector] private bool _hasBase;
    private float _lastAspect = -1f, _lastSize = -1f;

    private void OnEnable()
    {
        if (!_hasBase) { _baseScale = transform.localScale; _hasBase = true; }
        _lastAspect = -1f;
        Fit();
    }

    private void LateUpdate()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;
        if (!Mathf.Approximately(cam.aspect, _lastAspect) || !Mathf.Approximately(cam.orthographicSize, _lastSize)) Fit();
    }

    public void Fit()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        var sr = GetComponent<SpriteRenderer>();
        if (cam == null || sr == null || sr.sprite == null || !_hasBase) return;
        _lastAspect = cam.aspect; _lastSize = cam.orthographicSize;

        // Painted extent in local space (mesh vertices, not the padded rectangle)
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (var v in sr.sprite.vertices) { if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x; }

        // Distance from the camera centre to each painted edge, at the base scale (world units)
        float parentX = transform.parent != null ? transform.parent.lossyScale.x : 1f;
        float sx = _baseScale.x * parentX;
        float px = transform.position.x - cam.transform.position.x;   // pivot offset from camera centre
        float leftReach  = -(px + minX * sx);
        float rightReach =  px + maxX * sx;
        float need = cam.orthographicSize * cam.aspect + margin;

        float k = 1f;
        if (leftReach > 0f)  k = Mathf.Max(k, need / leftReach);
        if (rightReach > 0f) k = Mathf.Max(k, need / rightReach);
        transform.localScale = _baseScale * k;
    }
}
