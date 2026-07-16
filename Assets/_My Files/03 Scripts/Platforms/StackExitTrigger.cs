using UnityEngine;
using TarodevController;

public class StackExitTrigger : MonoBehaviour
{
    [SerializeField] private CameraFollow.Mode triggerMode;

    private CameraFollow _cam;
    private PlayerController _player;

    private void Awake()
    {
        _cam = Camera.main.GetComponent<CameraFollow>();
        _player = FindFirstObjectByType<PlayerController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        float targetX;

        if (triggerMode == CameraFollow.Mode.Vertical)
        {
            // Use current camera X to avoid snap/jitter on mode entry
            targetX = Camera.main.transform.position.x;
        }
        else
        {
            targetX = transform.parent.position.x;
        }

        _cam?.SetMode(triggerMode, targetX);

        if (triggerMode == CameraFollow.Mode.Horizontal)
            _player?.SetFloorY(transform.position.y, CameraFollow.Mode.Horizontal);
        else
            _player?.SetFloorY(float.MinValue, CameraFollow.Mode.Vertical);
    }
}