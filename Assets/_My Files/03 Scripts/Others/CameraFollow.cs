using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public enum Mode { Vertical, Horizontal, Diagonal }   // new values only ever go at the END (saved as numbers)

    public Transform player;

    [Tooltip("Smaller = snappier, larger = smoother. Typical range 0.1 - 0.4")]
    public float smoothTime = 0.2f;

    [Tooltip("Cap on camera speed (units/sec). Mathf.Infinity = uncapped.")]
    public float maxSpeed = Mathf.Infinity;

    public float offset = 8f;

    [Tooltip("Vertical headroom kept above the player in Vertical mode.")]
    public float verticalLead = 6f;

    [Tooltip("Horizontal headroom in the player's move direction so upcoming platforms are visible.")]
    public float horizontalLead = 3f;

    private Mode _currentMode = Mode.Vertical;
    private float _targetX;
    /// <summary>Where the camera is settling horizontally (vertical mode).</summary>
    public float TargetX => _targetX;

    private float _velX;
    private float _velY;

    private int _horizontalDir = 1;

    [Header("Landing Nudge")]
    [Tooltip("Max dip in world units on a hard landing. Keep tiny - he lands constantly.")]
    public float nudgeMax = 0.08f;
    [Tooltip("Landing speed that produces the full dip.")]
    public float nudgeFullImpact = 25f;
    [Tooltip("Landings slower than this don't nudge at all.")]
    public float nudgeMinImpact = 3f;
    [Tooltip("Seconds for the dip and return.")]
    public float nudgeDuration = 0.14f;

    private float _nudgeAmp;
    private float _nudgeTime = -1f;
    private float _nudgeApplied;

    [Header("Horizontal Stacks")]
    [Tooltip("Camera only ever moves left in horizontal mode (stacks progress leftward). It only moves right if Brutus would leave the screen.")]
    public bool horizontalOnlyLeft = true;
    [Tooltip("How close to the screen edge Brutus may get before the camera gives way.")]
    public float edgePad = 1.5f;
    private Camera _cam;

    private void Awake()
    {
        _targetX = transform.position.x;
        _cam = GetComponent<Camera>();
        if (horizontalOnlyLeft) _horizontalDir = -1;
    }

    public void SetMode(Mode newMode, float targetX)
    {
        if (_currentMode != newMode)
        {
            _velX = 0f;
            _velY = 0f;
        }
        _currentMode = newMode;
        _targetX = targetX;
    }

    public void SetTargetX(float x)
    {
        _targetX = x;
    }

    public void SetMoveDirection(int dir)
    {
        // Horizontal stacks progress leftward: with horizontalOnlyLeft the lead always points left
        _horizontalDir = horizontalOnlyLeft ? -1 : dir;
    }

    /// <summary>
    /// Instantly moves the camera to the given position (no smoothing).
    /// Used on revive so there is no drift from the old location.
    /// </summary>
    public void SnapTo(float x, float y)
    {
        _nudgeApplied = 0f;
        _nudgeTime = -1f;
        transform.position = new Vector3(x, y, transform.position.z);
        _targetX = x;
        _velX = 0f;
        _velY = 0f;
    }

    /// <summary>
    /// Tiny downward dip-and-return on landing. impactSpeed = downward speed at touchdown.
    /// Scales with impact, capped at nudgeMax; ignored below nudgeMinImpact.
    /// </summary>
    public void Nudge(float impactSpeed)
    {
        if (impactSpeed < nudgeMinImpact) return;
        _nudgeAmp = Mathf.Clamp01(impactSpeed / nudgeFullImpact) * nudgeMax;
        _nudgeTime = 0f;
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Remove last frame's nudge so the smoothing only ever sees the real camera position
        if (_nudgeApplied != 0f)
        {
            var p = transform.position;
            transform.position = new Vector3(p.x, p.y - _nudgeApplied, p.z);
            _nudgeApplied = 0f;
        }

        float newX = transform.position.x;
        float newY = transform.position.y;

        switch (_currentMode)
        {
            case Mode.Vertical:
                float targetY = player.position.y >= offset
                    ? player.position.y + verticalLead
                    : transform.position.y;

                newX = Mathf.SmoothDamp(transform.position.x, _targetX, ref _velX, smoothTime, maxSpeed, Time.deltaTime);
                newY = Mathf.SmoothDamp(transform.position.y, targetY, ref _velY, smoothTime, maxSpeed, Time.deltaTime);
                break;

            case Mode.Diagonal:
                // Diagonal climbs go left OR right: follow Brutus both ways, and upward like vertical stacks.
                // (The left-only rule belongs to horizontal stacks only.)
                newX = Mathf.SmoothDamp(transform.position.x, player.position.x, ref _velX, smoothTime, maxSpeed, Time.deltaTime);
                newY = Mathf.SmoothDamp(transform.position.y, player.position.y + verticalLead, ref _velY, smoothTime, maxSpeed, Time.deltaTime);
                break;

            case Mode.Horizontal:
                float leadX = player.position.x + (_horizontalDir * horizontalLead);

                if (horizontalOnlyLeft)
                {
                    // Never move right: hold position while Brutus runs right (wall bounce)...
                    leadX = Mathf.Min(leadX, transform.position.x);
                    // ...unless he would leave the right edge of the screen.
                    float halfW = (_cam != null && _cam.orthographic) ? _cam.orthographicSize * _cam.aspect : 11f;
                    float mustBeAtLeast = player.position.x - (halfW - edgePad);
                    if (leadX < mustBeAtLeast) leadX = mustBeAtLeast;
                }

                newX = Mathf.SmoothDamp(transform.position.x, leadX, ref _velX, smoothTime, maxSpeed, Time.deltaTime);
                newY = Mathf.SmoothDamp(transform.position.y, player.position.y, ref _velY, smoothTime, maxSpeed, Time.deltaTime);
                break;
        }

        // Landing nudge: a separate layer on top of the smoothed position
        float nudge = 0f;
        if (_nudgeTime >= 0f)
        {
            _nudgeTime += Time.deltaTime;
            float t = _nudgeTime / nudgeDuration;
            if (t >= 1f) _nudgeTime = -1f;
            else nudge = -_nudgeAmp * Mathf.Sin(t * Mathf.PI);
        }
        _nudgeApplied = nudge;

        transform.position = new Vector3(newX, newY + nudge, transform.position.z);
    }
}