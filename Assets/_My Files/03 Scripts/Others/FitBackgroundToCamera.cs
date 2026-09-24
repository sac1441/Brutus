using UnityEngine;

/// <summary>
/// Scales a SpriteRenderer so it always covers the full view of an orthographic camera,
/// keeping the image's aspect ratio (crops the overflow instead of stretching).
/// Put it on a child of the camera so it follows it.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class FitBackgroundToCamera : MonoBehaviour
{
    public Camera targetCamera;
    [Tooltip("Distance in front of the camera.")]
    public float depth = 10f;

    private SpriteRenderer _sr;
    private float _lastAspect, _lastSize;

    private void OnEnable() { _sr = GetComponent<SpriteRenderer>(); Fit(); }

    private void LateUpdate()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;
        if (!Mathf.Approximately(cam.aspect, _lastAspect) || !Mathf.Approximately(cam.orthographicSize, _lastSize)) Fit();
    }

    public void Fit()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || _sr == null || _sr.sprite == null) return;
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        Vector2 size = _sr.sprite.bounds.size;
        float s = Mathf.Max(viewW / size.x, viewH / size.y);   // cover
        transform.localScale = new Vector3(s, s, 1f);
        transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, cam.transform.position.z + depth);
        _lastAspect = cam.aspect; _lastSize = cam.orthographicSize;
    }
}
