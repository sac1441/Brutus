using UnityEngine;
using TarodevController;

/// <summary>
/// Plays Brutus automatically for recording trailers. Only taps while standing, and only when the
/// jump's predicted landing (from the real jump speed and gravity) is on a floor.
///
/// Drama (for ads): close calls - some taps come at the last moment (landing right at a floor's edge,
/// or leaving an open edge at the very end) - and an optional deliberate miss after failAfter seconds,
/// so the video ends on a painful fall ("I could do better").
/// </summary>
public class Autopilot : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("Normal distance kept inside a floor's edge when landing.")]
    public float landingMargin = 0.5f;
    [Tooltip("Small human-like pause after landing before the next tap (seconds).")]
    public Vector2 reactionTime = new Vector2(0.03f, 0.09f);

    [Header("Drama")]
    [Range(0f, 1f)] public float closeCallChance = 0.5f;
    [Tooltip("Landing distance from the edge on a close call (still safe: the landing is calculated exactly).")]
    public float closeCallMargin = 0.15f;
    [Tooltip("Stop tapping after this many seconds of play (miss the next jump). Negative = never.")]
    public float failAfter = -1f;

    private Collider2D _col;
    private Rigidbody2D _rb;
    private float _waitUntil, _startTime;
    private bool _wasGrounded, _started, _closeCall;

    private const float JumpPower = 48f, Gravity = 300f;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        _col = player.GetComponent<Collider2D>();
        _rb = player.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (player == null || player.InputLocked) return;
        bool grounded = player.IsGrounded;
        if (grounded && !_wasGrounded)
        {
            _waitUntil = Time.time + Random.Range(reactionTime.x, reactionTime.y);
            _closeCall = Random.value < closeCallChance;
        }
        _wasGrounded = grounded;

        if (!_started) { if (grounded && Time.time > 0.6f) { player.TapJump(); _started = true; _startTime = Time.time; } return; }
        if (failAfter >= 0f && Time.time - _startTime > failAfter) return;
        if (!grounded || Time.time < _waitUntil) return;

        float vx = _rb.linearVelocity.x;
        float feet = _col.bounds.min.y, x = _col.bounds.center.x;
        float margin = _closeCall ? closeCallMargin : landingMargin;

        // close call on an open edge: keep running until the very edge, then jump
        if (_closeCall && DistanceToOpenEdge(x, feet, vx, out float edgeDist) && edgeDist > 0.45f) return;

        foreach (var c in Physics2D.OverlapBoxAll(new Vector2(x + vx * 0.25f, feet + 2.6f), new Vector2(7f, 2.6f), 0f))
        {
            if (!IsFloor(c)) continue;
            float dy = c.bounds.max.y - feet;
            if (dy < 1.5f || dy > 3.6f) continue;
            float disc = JumpPower * JumpPower - 2f * Gravity * dy;
            if (disc < 0f) continue;
            float t = (JumpPower + Mathf.Sqrt(disc)) / Gravity;
            float landX = x + vx * t;
            if (landX > c.bounds.min.x + margin && landX < c.bounds.max.x - margin) { player.TapJump(); return; }
        }

        float ahead = x + Mathf.Sign(vx) * 0.6f;
        bool groundAhead = false;
        foreach (var h in Physics2D.RaycastAll(new Vector2(ahead, feet + 0.2f), Vector2.down, 0.6f)) if (IsFloor(h.collider)) { groundAhead = true; break; }
        if (!groundAhead)
        {
            float t = 2f * JumpPower / Gravity, landX = x + vx * t;
            foreach (var h in Physics2D.RaycastAll(new Vector2(landX, feet + 0.4f), Vector2.down, 1.5f))
                if (IsFloor(h.collider)) { player.TapJump(); return; }
        }
    }

    /// <summary>Distance to the end of the floor ahead, if that end is open (no wall).</summary>
    private bool DistanceToOpenEdge(float x, float feet, float vx, out float dist)
    {
        dist = 0f;
        if (vx == 0f) return false;
        float dir = Mathf.Sign(vx);
        for (float d = 0.2f; d < 4f; d += 0.1f)
        {
            bool floor = false;
            foreach (var h in Physics2D.RaycastAll(new Vector2(x + dir * d, feet + 0.2f), Vector2.down, 0.5f)) if (IsFloor(h.collider)) { floor = true; break; }
            if (!floor)
            {
                foreach (var w in Physics2D.OverlapBoxAll(new Vector2(x + dir * d, feet + 0.7f), new Vector2(0.3f, 1.2f), 0f))
                    if (w != null && !w.isTrigger && w.gameObject.layer == 7) return false;   // a wall there: not an open edge
                dist = d; return true;
            }
        }
        return false;
    }

    private bool IsFloor(Collider2D c)
    {
        if (c == null || c.isTrigger || c.transform.IsChildOf(player.transform)) return false;
        return c.name.StartsWith("Floor_Tile") || c.name.StartsWith("Up Floor");
    }
}
