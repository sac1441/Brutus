using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public enum Mode { Vertical, Horizontal }

    public Transform player;

    [Tooltip("Smaller = snappier, larger = smoother. Typical range 0.1 - 0.4")]
    public float smoothTime = 0.2f;

    [Tooltip("Cap on camera speed (units/sec). Mathf.Infinity = uncapped.")]
    public float maxSpeed = Mathf.Infinity;

    public float offset = 8f;

    private Mode _currentMode = Mode.Vertical;
    private float _targetX;

    // SmoothDamp velocity state
    private float _velX;
    private float _velY;

    private void Awake()
    {
        _targetX = transform.position.x;
    }

    public void SetMode(Mode newMode, float targetX)
    {
        _currentMode = newMode;
        _targetX = targetX;
    }

    public void SetTargetX(float x)
    {
        _targetX = x;
    }

    void LateUpdate()
    {
        if (player == null) return;

        float newX = transform.position.x;
        float newY = transform.position.y;

        switch (_currentMode)
        {
            case Mode.Vertical:
                float targetY = player.position.y >= offset ? player.position.y : transform.position.y;
                newX = Mathf.SmoothDamp(transform.position.x, _targetX, ref _velX, smoothTime, maxSpeed, Time.deltaTime);
                newY = Mathf.SmoothDamp(transform.position.y, targetY, ref _velY, smoothTime, maxSpeed, Time.deltaTime);
                break;

            case Mode.Horizontal:
                newX = Mathf.SmoothDamp(transform.position.x, player.position.x, ref _velX, smoothTime, maxSpeed, Time.deltaTime);
                newY = Mathf.SmoothDamp(transform.position.y, player.position.y, ref _velY, smoothTime, maxSpeed, Time.deltaTime);
                break;
        }

        transform.position = new Vector3(newX, newY, transform.position.z);
    }
}