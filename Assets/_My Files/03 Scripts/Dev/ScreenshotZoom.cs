using UnityEngine;
using TarodevController;

/// <summary>
/// Store-screenshot helper only (added at runtime by StoreScreenshotCapture, never in the scene).
/// Zooms the main camera in and keeps Brutus in the lower-middle of the frame so he reads clearly.
/// Runs after CameraFollow each frame.
/// </summary>
[DefaultExecutionOrder(10000)]
public class ScreenshotZoom : MonoBehaviour
{
    public float zoom = 0.55f;          // fraction of the normal view height
    public float playerScreenY = 0.38f; // where Brutus sits vertically (0 bottom, 1 top)

    private Camera _cam;
    private Transform _player;
    private float _baseSize = -1f;
    private Vector3 _smooth, _vel;

    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_player == null) { var p = FindFirstObjectByType<PlayerController>(); if (p) _player = p.transform; }
        if (_cam == null || _player == null) return;
        if (_baseSize < 0f) _baseSize = _cam.orthographicSize;

        float size = _baseSize * zoom;
        _cam.orthographicSize = size;
        var target = new Vector3(_player.position.x, _player.position.y + (0.5f - playerScreenY) * 2f * size, _cam.transform.position.z);
        if (_smooth == Vector3.zero) _smooth = target;
        _smooth = Vector3.SmoothDamp(_smooth, target, ref _vel, 0.15f);
        _cam.transform.position = _smooth;
    }
}
