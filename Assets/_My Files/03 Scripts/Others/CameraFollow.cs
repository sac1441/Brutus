using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public enum Mode { Vertical, Horizontal }

    public Transform player;
    public float smoothSpeed = 0.125f;
    public float offset = 8f;

    private Mode _currentMode = Mode.Vertical;
    private float _targetX;

    private void Awake()
    {
        _targetX = transform.position.x;
    }

    public void SetMode(Mode newMode, float targetX)
    {
        _currentMode = newMode;
        _targetX = targetX;
        Debug.Log($"Camera mode → {newMode}, targetX → {targetX}");
    }

    void LateUpdate()
    {
        if (player == null) return;

        switch (_currentMode)
        {
            case Mode.Vertical:
                float targetY = player.position.y >= offset ? player.position.y : transform.position.y;
                Vector3 target = new Vector3(
                    Mathf.Lerp(transform.position.x, _targetX, smoothSpeed * 4f),
                    targetY,
                    transform.position.z);
                transform.position = Vector3.Lerp(transform.position, target, smoothSpeed);
                break;

            case Mode.Horizontal:
                Vector3 fullTarget = new Vector3(
                    player.position.x,
                    player.position.y,
                    transform.position.z);
                transform.position = Vector3.Lerp(transform.position, fullTarget, smoothSpeed);
                break;
        }
    }

    public void SetTargetX(float x)
    {
        _targetX = x;
    }
}