using UnityEngine;
using TarodevController;

public class StackExitTrigger : MonoBehaviour
{
    [SerializeField] private CameraFollow.Mode triggerMode;

    private CameraFollow _cam;
    private PlayerController _player;

    /// <summary>Camera mode for the stack this trigger belongs to.</summary>
    public CameraFollow.Mode TriggerMode => triggerMode;
    /// <summary>The stack root (trigger sits at Stack/Platform/CameraTrigger).</summary>
    public Transform Stack => transform.parent != null ? transform.parent.parent : null;
    /// <summary>The x the camera centres on for this stack.</summary>
    public float StackTargetX => transform.parent != null ? transform.parent.position.x : transform.position.x;
    /// <summary>Used by generated chunks (e.g. diagonal climbs) to set their camera mode.</summary>
    public void SetTriggerMode(CameraFollow.Mode mode) { triggerMode = mode; }

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
            // Use the stack's own X (not the camera's current X) -- stacks can now
            // sit at shifted X positions after a Horizontal section, so locking to
            // "wherever the camera currently is" no longer matches the stack being
            // entered and cuts off part of the platform.
            targetX = transform.parent.position.x;
        }
        else
        {
            targetX = transform.parent.position.x;
        }

        _cam?.SetMode(triggerMode, targetX);

        if (triggerMode == CameraFollow.Mode.Horizontal)
            _player?.SetFloorY(transform.position.y, CameraFollow.Mode.Horizontal);
        else
            _player?.SetFloorY(float.MinValue, triggerMode);   // Vertical or Diagonal
    }
}