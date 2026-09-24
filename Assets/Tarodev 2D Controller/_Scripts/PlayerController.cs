using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TarodevController
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private ScriptableStats _stats;
        [SerializeField] private SpriteRenderer SpriteRenderer;
        [SerializeField] private TextMeshProUGUI eggScoreText;
        [SerializeField] private CameraFollow CameraFollow;
        [SerializeField] private ScoreManager scoreManager;
        private static int eggCount = 0;
        [SerializeField] private TextMeshProUGUI flowerScoreText;
        private static int flowerCount = 0;
        private Rigidbody2D _rb;
        private CapsuleCollider2D _col;
        private FrameInput _frameInput;
        private Vector2 _frameVelocity;
        private bool _cachedQueryStartInColliders;

        public Vector2 FrameInput => _frameInput.Move;
        public event Action<bool, float> GroundedChanged;
        public event Action Jumped;
        public event Action Died;

        public int EggCount => eggCount;

        public bool InputLocked { get; set; }

        private Vector2 _lastSafePosition;
        private Vector2 _lastWalledPosition;
        private float _lastWalledCameraX;
        private bool _hasWalledCheckpoint;
        private bool _isDead;

        private float _floorY = float.MinValue;
        private CameraFollow.Mode _currentStackMode = CameraFollow.Mode.Vertical;

        public void SetFloorY(float floorY, CameraFollow.Mode mode)
        {
            _floorY = floorY;
            _currentStackMode = mode;
        }

        private float _time;
        private bool _grounded;

        private const float INPUT_DELAY = 0.2f;
        private int _startFrame;
        private int _lastJumpFrame = -1;

        private int _queuedJumps = 0;
        private const int MAX_QUEUED_JUMPS = 1;

        [Header("Jump Feel (visual only - never touches physics)")]
        [SerializeField] private float jumpStretchAmount = 0.35f;
        [SerializeField] private float landSquashAmount = 0.35f;
        [SerializeField] private float squashStretchDuration = 0.12f;

        private Coroutine _squashRoutine;
        private Vector3 _spriteBaseScale = Vector3.one;

        [Header("Camera Realignment (raycast to Left Wall / Right Wall on landing)")]
        [SerializeField] private float wallCastDistance = 30f;
        private int _wallLayerMask;

        [Header("Wall Detection (raycast backup)")]
        [SerializeField] private float wallDetectDistance = 0.5f;

        [Header("Death Thresholds")]
        [Tooltip("Die when this far below the highest platform reached. Platforms are 3 apart, so 12 = four platforms of recovery room.")]
        [SerializeField] private float fallDeathDistance = 12f;
        [Tooltip("Die when this far past the leftmost/rightmost platform edge of the current stack.")]
        [SerializeField] private float sideDeathMargin = 5f;

        private float _highestLandedY = float.MinValue;
        private float _stackMinX;
        private float _stackMaxX;
        private bool _stackBoundsValid = false;

        [Tooltip("Die if Brutus barely moves sideways for this long. He should always be running.")]
        [SerializeField] private float stuckDeathTime = 0.35f;
        private float _stuckTimer;

        [Header("Revive Settings")]
        [SerializeField] private float rewindSeconds = 3f;
        [SerializeField] private float rewindAnimDuration = 3f;
        [SerializeField] private float reviveFreezeDuration = 1f;

        // --- Position recording buffer ---
        private struct PositionRecord
        {
            public float time;
            public Vector2 position;
            public float cameraX;
            public bool grounded;   // standing on a platform this frame
        }

        private const int BUFFER_SIZE = 180;
        private PositionRecord[] _positionBuffer = new PositionRecord[BUFFER_SIZE];
        private int _bufferIndex = 0;
        private int _bufferCount = 0;

        private Vector2 _deathRewindPosition;
        private float _deathRewindCameraX;

        [Header("Rocket revive")]
        [Tooltip("Revive flies Brutus up to the next white milestone floor above where he died (like High Risers' rocket). Off = rewind to where he last stood.")]
        [SerializeField] private bool rocketRevive = true;
        [Tooltip("Flight speed in units per second (the flight lasts 0.8-1.8 s whatever the distance).")]
        [SerializeField] private float rocketSpeed = 22f;
        [Tooltip("Sideways bulge of the flight path, for a rocket-like arc.")]
        [SerializeField] private float rocketArc = 2.5f;
        [Tooltip("Add one point per floor the rocket skips.")]
        [SerializeField] private bool rocketAddsScore = true;
        private Vector2 _deathPosition;
        private float _deathRewindTime;

        [Tooltip("Never respawn on a spot Brutus stood on in the last N seconds before dying (e.g. the edge he ran off).")]
        [SerializeField] private float rewindMinAgeSeconds = 0.75f;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
            _wallLayerMask = 1 << 7;
            _time = 0;
            _startFrame = Time.frameCount;
            _lastSafePosition = transform.position;
            _lastWalledPosition = transform.position;
            _lastWalledCameraX = transform.position.x;
            _hasWalledCheckpoint = false;

            _queuedJumps = 0;
            _bufferedJumpUsable = false;
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeJumpWasPressed = float.MinValue;
            _frameLeftGrounded = float.MinValue;

            if (SpriteRenderer != null) _spriteBaseScale = SpriteRenderer.transform.localScale;

            if (eggScoreText != null) eggScoreText.text = eggCount.ToString();
            if (flowerScoreText != null) flowerScoreText.text = flowerCount.ToString();
        }

        private void Update()
        {
            _time += Time.deltaTime;
            GatherInput();
            RecordPosition();
        }

        private void RecordPosition()
        {
            if (_isDead) return;

            _positionBuffer[_bufferIndex] = new PositionRecord
            {
                time = _time,
                position = transform.position,
                cameraX = CameraFollow != null ? CameraFollow.transform.position.x : transform.position.x,
                grounded = _grounded
            };

            _bufferIndex = (_bufferIndex + 1) % BUFFER_SIZE;
            if (_bufferCount < BUFFER_SIZE) _bufferCount++;
        }

        private void FindRewindPosition()
        {
            float targetTime = _time - rewindSeconds;
            float newestAllowed = _time - rewindMinAgeSeconds;

            // Fallback: last checkpoint / last safe platform
            _deathRewindPosition = _hasWalledCheckpoint ? _lastWalledPosition : _lastSafePosition;
            _deathRewindCameraX = _hasWalledCheckpoint ? _lastWalledCameraX : _deathRewindPosition.x;
            _deathRewindTime = targetTime;

            // Only ever respawn where Brutus was STANDING (never mid-jump).
            // 1st choice: newest grounded frame at or before the target time.
            // 2nd choice: oldest grounded frame after it, but not from the last moments before death.
            int before = -1, after = -1;
            for (int i = 0; i < _bufferCount; i++)
            {
                int idx = (_bufferIndex - 1 - i + BUFFER_SIZE) % BUFFER_SIZE;   // newest -> oldest
                var r = _positionBuffer[idx];
                if (!r.grounded) continue;
                if (r.time <= targetTime) { before = idx; break; }
                if (r.time <= newestAllowed) after = idx;                           // keeps the oldest such frame
            }
            int pick = before >= 0 ? before : after;
            if (pick >= 0)
            {
                _deathRewindPosition = _positionBuffer[pick].position;
                _deathRewindCameraX = _positionBuffer[pick].cameraX;
                _deathRewindTime = _positionBuffer[pick].time;
            }

            _deathRewindPosition = SnapToGround(_deathRewindPosition);
            Debug.Log($"[Rewind] Respawn at {_deathRewindPosition} (grounded, {(_time - _deathRewindTime):F1}s before death{(pick < 0 ? ", fallback" : "")})");
        }

        /// <summary>Drop a point straight down onto the platform below it so Brutus stands on it.</summary>
        private Vector2 SnapToGround(Vector2 pos)
        {
            float feet = _col != null ? transform.position.y - _col.bounds.min.y : 0.5f;
            foreach (var h in Physics2D.RaycastAll(pos + Vector2.up * 0.5f, Vector2.down, 8f))
            {
                if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(transform)) continue;
                return new Vector2(pos.x, h.point.y + feet + 0.02f);
            }
            return pos;   // nothing below: keep as is
        }

        private List<Vector2> BuildRewindPath()
        {
            float targetTime = _deathRewindTime;
            var raw = new List<Vector2>();
            raw.Add((Vector2)transform.position);

            for (int i = 0; i < _bufferCount; i++)
            {
                int idx = (_bufferIndex - 1 - i + BUFFER_SIZE) % BUFFER_SIZE;
                raw.Add(_positionBuffer[idx].position);
                if (_positionBuffer[idx].time <= targetTime) break;
            }

            var path = new List<Vector2>();
            if (raw.Count <= 40)
            {
                path = raw;
            }
            else
            {
                float step = (float)(raw.Count - 1) / 39f;
                for (int i = 0; i < 40; i++)
                {
                    int idx2 = Mathf.RoundToInt(i * step);
                    path.Add(raw[idx2]);
                }
            }

            if (path.Count > 0) path[path.Count - 1] = _deathRewindPosition;
            return path;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private void GatherInput()
        {
            if (_time < INPUT_DELAY) return;
            if (Time.frameCount <= _startFrame + 1) return;
            if (InputLocked) return;

            bool jumpPressed = false;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                jumpPressed = true;

            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                    jumpPressed = true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log("[CLICK] frame=" + Time.frameCount + " t=" + _time.ToString("F3"));
                jumpPressed = true;
            }

            _frameInput = new FrameInput
            {
                JumpDown = jumpPressed,
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
            };

            if (_stats.SnapInput)
            {
                _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.x);
                _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.y);
            }

            if (_frameInput.JumpDown)
            {
                // Every click jumps immediately — no ground check, no buffer
                _timeJumpWasPressed = _time;
                ExecuteJump();
            }
        }

        private void FixedUpdate()
        {
            CheckCollisions();
            HandleJump();
            HandleDirection();
            HandleWallDetection();
            HandleGravity();
            ApplyMovement();
        }

        private float _frameLeftGrounded = float.MinValue;
        private float airTime = 0f;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Left Wall"))
            {
                _moveDirection = 1;
                SpriteRenderer.flipX = false;
                if (CameraFollow != null) CameraFollow.SetMoveDirection(_moveDirection);
            }
            else if (collision.gameObject.CompareTag("Right Wall"))
            {
                _moveDirection = -1;
                SpriteRenderer.flipX = true;
                if (CameraFollow != null) CameraFollow.SetMoveDirection(_moveDirection);
            }

        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.GetComponent<Flower>())
            {
                collision.gameObject.GetComponent<ICollectable>()?.Collect();
                flowerCount++;
                flowerScoreText.text = flowerCount.ToString();
            }
            else if (collision.gameObject.GetComponent<Egg>())
            {
                collision.gameObject.GetComponent<ICollectable>()?.Collect();
                eggCount++;
                eggScoreText.text = eggCount.ToString();
            }
        }

        private void UpdateStackBounds(Collider2D groundCollider)
        {
            if (groundCollider == null) return;
            Transform platform = groundCollider.transform.parent;
            if (platform == null) return;
            Transform stack = platform.parent;
            if (stack == null) return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            bool any = false;

            foreach (Transform child in stack)
            {
                if (!child.name.StartsWith("Platform")) continue;
                var rends = child.GetComponentsInChildren<Renderer>(false);
                foreach (var r in rends)
                {
                    if (r.bounds.min.x < minX) minX = r.bounds.min.x;
                    if (r.bounds.max.x > maxX) maxX = r.bounds.max.x;
                    any = true;
                }
            }

            if (any)
            {
                _stackMinX = minX;
                _stackMaxX = maxX;
                _stackBoundsValid = true;
            }
        }

        private void TrySaveCheckpointOnLanding(Collider2D groundCollider)
        {
            if (groundCollider == null) return;

            Transform platform = groundCollider.transform.parent;
            if (platform == null) return;

            if (platform.name.TrimEnd().EndsWith("M"))
            {
                _lastWalledPosition = transform.position;
                _lastWalledCameraX = CameraFollow != null
                    ? CameraFollow.transform.position.x
                    : transform.position.x;
                _hasWalledCheckpoint = true;
                Debug.Log($"[Checkpoint] Saved at {transform.position} on '{platform.name}'");
            }
        }

        private void CheckCollisions()
        {
            Physics2D.queriesStartInColliders = false;

            RaycastHit2D groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer & ~_stats.WallLayer);

            if (!_grounded && groundHit && _frameVelocity.y <= 0)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;

                _lastSafePosition = groundHit.collider != null && groundHit.collider.transform.parent != null
                    ? new Vector2(groundHit.collider.transform.parent.position.x, transform.position.y)
                    : (Vector2)transform.position;

                RepositionCameraForPlatform();

                TrySaveCheckpointOnLanding(groundHit.collider);

                if (transform.position.y > _highestLandedY) _highestLandedY = transform.position.y;

                UpdateStackBounds(groundHit.collider);

                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));

                if (CameraFollow != null) CameraFollow.Nudge(Mathf.Abs(_frameVelocity.y));

                PlaySquashStretch(1f + landSquashAmount, 1f - landSquashAmount);
            }
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }

            if (!_isDead)
            {
                // Fell too far below the highest platform reached
                if (_highestLandedY > float.MinValue && transform.position.y < _highestLandedY - fallDeathDistance)
                {
                    Debug.Log("[Death] Fell " + (_highestLandedY - transform.position.y).ToString("F1") + " below highest platform");
                    Die();
                }
                else if (_stackBoundsValid
                    && (transform.position.x < _stackMinX - sideDeathMargin || transform.position.x > _stackMaxX + sideDeathMargin))
                {
                    Debug.Log("[Death] Flew off side. x=" + transform.position.x.ToString("F1") + " bounds=[" + _stackMinX.ToString("F1") + ", " + _stackMaxX.ToString("F1") + "]");
                    Die();
                }
            }

            // Stuck watchdog: Brutus always runs, so near-zero sideways speed means he's pinned somewhere
            if (!_isDead && !InputLocked)
            {
                if (Mathf.Abs(_rb.linearVelocity.x) < 1f) _stuckTimer += Time.fixedDeltaTime;
                else _stuckTimer = 0f;

                if (_stuckTimer > stuckDeathTime)
                {
                    Debug.Log("[Death] Stuck at x=" + transform.position.x.ToString("F1") + " y=" + transform.position.y.ToString("F1"));
                    _stuckTimer = 0f;
                    Die();
                }
            }

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }

        private void HandleWallDetection()
        {
            Vector2 direction = _moveDirection > 0 ? Vector2.right : Vector2.left;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, wallDetectDistance, _wallLayerMask);

            if (hit.collider == null) return;

            if (hit.collider.CompareTag("Left Wall") && _moveDirection == -1)
            {
                _moveDirection = 1;
                SpriteRenderer.flipX = false;
                if (CameraFollow != null) CameraFollow.SetMoveDirection(_moveDirection);
            }
            else if (hit.collider.CompareTag("Right Wall") && _moveDirection == 1)
            {
                _moveDirection = -1;
                SpriteRenderer.flipX = true;
                if (CameraFollow != null) CameraFollow.SetMoveDirection(_moveDirection);
            }
        }

        [Header("Camera lock")]
        [Tooltip("On vertical stacks the camera stays centred on the stack sideways (no left/right drift).")]
        [SerializeField] private bool lockCameraXOnVerticalStacks = true;
        [Tooltip("A stack must fit on screen with this much room on each side to be locked.")]
        [SerializeField] private float lockEdgeMargin = 0.2f;

        private Transform _stackContainer;
        private readonly Dictionary<Transform, Vector2> _stackCentreCache = new Dictionary<Transform, Vector2>();

        /// <summary>Centre x and width of the stack that owns the given platform piece.</summary>
        private bool TryGetStackCentre(Transform piece, out float centreX, out float width)
        {
            centreX = 0f; width = 0f;
            if (_stackContainer == null)
            {
                foreach (var tr in FindObjectsByType<StackExitTrigger>(FindObjectsSortMode.None))
                    if (tr.Stack != null && tr.Stack.parent != null) { _stackContainer = tr.Stack.parent; break; }
                if (_stackContainer == null) return false;
            }
            Transform s = piece;
            while (s != null && s.parent != _stackContainer) s = s.parent;
            if (s == null) return false;

            Vector2 cw;
            if (!_stackCentreCache.TryGetValue(s, out cw))
            {
                bool any = false; Bounds b = new Bounds();
                foreach (var c in s.GetComponentsInChildren<Collider2D>(false))
                {
                    if (c.isTrigger) continue;
                    if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
                }
                if (!any) return false;
                cw = new Vector2(b.center.x, b.size.x);
                _stackCentreCache[s] = cw;
            }
            centreX = cw.x; width = cw.y;
            return true;
        }

        private void RepositionCameraForPlatform()
        {
            if (CameraFollow == null) return;
            if (_currentStackMode != CameraFollow.Mode.Vertical && !IsOnVerticalPlatform()) return;

            var cam = CameraFollow.GetComponent<Camera>();
            float viewHalfW = (cam != null && cam.orthographic) ? cam.orthographicSize * cam.aspect : 11f;
            const float pad = 0.5f;

            // Platform Brutus just landed on
            RaycastHit2D g = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0,
                Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer & ~_stats.WallLayer);
            Collider2D groundCol = g ? g.collider : null;

            // Vertical stacks / scaffolds: lock the camera sideways to the centre of the stack Brutus is on,
            // so running to and fro across staggered platforms never slides the camera left and right.
            // Stacks wider than the screen fall through to the framing logic below.
            if (lockCameraXOnVerticalStacks && _currentStackMode == CameraFollow.Mode.Vertical && groundCol != null)
            {
                float stackCx, stackW;
                if (TryGetStackCentre(groundCol.transform, out stackCx, out stackW) && stackW <= 2f * viewHalfW - 2f * lockEdgeMargin)
                {
                    CameraFollow.SetTargetX(stackCx);
                    return;
                }
            }

            float curMin = 0f, curMax = 0f;
            bool hasCur = groundCol != null && groundCol.transform.parent != null
                          && GetPlatformExtent(groundCol.transform.parent, out curMin, out curMax);

            // The next platform above - this is the one that must stay in view
            Transform above = FindPlatformAbove(groundCol);
            float upMin = 0f, upMax = 0f;
            bool hasUp = above != null && GetPlatformExtent(above, out upMin, out upMax);

            // Only frame on a platform above if it belongs to the SAME stack. The next stack's first
            // platform can sit just above the top of this one; framing on it drags the camera away
            // from Brutus. The CameraTrigger handles that hand-off when he actually enters it.
            if (hasUp && (groundCol == null || groundCol.transform.parent == null || above.parent != groundCol.transform.parent.parent))
                hasUp = false;

            if (hasUp)
            {
                // Already framed: if the platform above and Brutus are fully inside the view the camera
                // is settling on, leave it alone. Re-targeting here caused a small drift on every landing.
                float camX = CameraFollow.TargetX;
                float viewL = camX - viewHalfW + pad, viewR = camX + viewHalfW - pad;
                float px = transform.position.x;
                if (upMin >= viewL && upMax <= viewR && px >= viewL && px <= viewR)
                    return;

                float lo = hasCur ? Mathf.Min(curMin, upMin) : upMin;
                float hi = hasCur ? Mathf.Max(curMax, upMax) : upMax;
                float usable = (viewHalfW - pad) * 2f;
                float targetX;

                if (hi - lo <= usable)
                {
                    // Both platforms fit - center on the pair
                    targetX = (lo + hi) * 0.5f;
                }
                else if (upMax - upMin <= usable)
                {
                    // Can't fit both - keep the platform above fully visible, lean toward Brutus
                    targetX = Mathf.Clamp(transform.position.x, upMax + pad - viewHalfW, upMin - pad + viewHalfW);
                }
                else
                {
                    // Platform above is wider than the screen - center on it
                    targetX = (upMin + upMax) * 0.5f;
                }

                // Safety: never frame so that Brutus leaves the screen
                targetX = Mathf.Clamp(targetX, transform.position.x - (viewHalfW - pad), transform.position.x + (viewHalfW - pad));
                CameraFollow.SetTargetX(targetX);
                return;
            }

            // Fallback: no platform above found - center between the walls like before
            RaycastHit2D leftHit = Physics2D.Raycast(transform.position, Vector2.left, wallCastDistance, _wallLayerMask);
            RaycastHit2D rightHit = Physics2D.Raycast(transform.position, Vector2.right, wallCastDistance, _wallLayerMask);

            bool foundLeft = leftHit.collider != null && leftHit.collider.CompareTag("Left Wall");
            bool foundRight = rightHit.collider != null && rightHit.collider.CompareTag("Right Wall");

            if (foundLeft && foundRight)
            {
                float midX = (leftHit.point.x + rightHit.point.x) / 2f;
                CameraFollow.SetTargetX(midX);
            }
        }

        /// <summary>Lowest floor platform above the one Brutus is standing on.</summary>
        private Transform FindPlatformAbove(Collider2D current)
        {
            float baseY = current != null ? current.bounds.max.y : _col.bounds.min.y;
            Collider2D[] hits = Physics2D.OverlapAreaAll(
                new Vector2(transform.position.x - 30f, baseY + 0.5f),
                new Vector2(transform.position.x + 30f, baseY + 5f));

            Transform best = null;
            float bestY = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.isTrigger || h.transform.parent == null || !h.name.Contains("Floor")) continue;
                if (current != null && h.transform.parent == current.transform.parent) continue;
                float top = h.bounds.max.y;
                if (top < bestY) { bestY = top; best = h.transform.parent; }
            }
            return best;
        }

        /// <summary>Horizontal extent of a platform's active floor colliders.</summary>
        private bool GetPlatformExtent(Transform platform, out float minX, out float maxX)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            foreach (var c in platform.GetComponentsInChildren<Collider2D>(false))
            {
                if (c.isTrigger || !c.name.Contains("Floor")) continue;
                if (c.bounds.min.x < minX) minX = c.bounds.min.x;
                if (c.bounds.max.x > maxX) maxX = c.bounds.max.x;
            }
            return minX < maxX;
        }

        private bool IsOnVerticalPlatform()
        {
            RaycastHit2D groundHit = Physics2D.CapsuleCast(
                _col.bounds.center, _col.size, _col.direction, 0,
                Vector2.down, _stats.GrounderDistance,
                ~_stats.PlayerLayer & ~_stats.WallLayer);

            if (!groundHit || groundHit.collider == null) return false;

            Transform current = groundHit.collider.transform;
            for (int i = 0; i < 5; i++)
            {
                if (current == null) break;
                if (current.CompareTag("Vertical_v")) return true;
                current = current.parent;
            }

            return false;
        }

        // ---------- Death ----------

        private void Die()
        {
            if (_isDead) return;

            if (scoreManager != null && scoreManager.CurrentScore < 20)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(0);
                return;
            }

            _deathPosition = transform.position;
            FindRewindPosition();

            _isDead = true;
            airTime = 0f;

            Time.timeScale = 0f;
            InputLocked = true;

            _rb.linearVelocity = Vector2.zero;

            Died?.Invoke();
        }

        // ---------- Revive ----------

        public bool TrySpendEggs(int amount)
        {
            if (eggCount < amount) return false;

            eggCount -= amount;
            eggScoreText.text = eggCount.ToString();
            return true;
        }

        public void Revive()
        {
            _isDead = false;
            _rb.linearVelocity = Vector2.zero;
            _frameVelocity = Vector2.zero;
            _grounded = false;
            airTime = 0f;
            float progressY = Mathf.Max(_highestLandedY, _deathPosition.y);   // how far he had climbed
            _highestLandedY = _deathRewindPosition.y;
            _stuckTimer = 0f;

            if (rocketRevive && FindNextWhiteMilestone(progressY, out var landAt, out var landCamX))
            {
                int skipped = Mathf.Max(0, Mathf.RoundToInt((landAt.y - progressY) / 3f));
                Debug.Log($"[Revive] Rocket to white milestone at {landAt} (+{skipped} floors)");
                StartCoroutine(RocketReviveRoutine(_deathRewindPosition, landAt, landCamX, skipped));
                return;
            }

            Debug.Log($"[Revive] Rewinding to {_deathRewindPosition}");

            StartCoroutine(ReviveRewindRoutine());
        }

        /// <summary>Find the stack containing pos and put the camera (and floor rule) into that stack's mode.</summary>
        private void RestoreCameraModeFor(Vector2 pos)
        {
            var triggers = FindObjectsByType<StackExitTrigger>(FindObjectsSortMode.None);
            if (triggers.Length == 0) return;

            // 1) Exact: the stack that owns the platform right under the respawn point.
            Transform container = triggers[0].Stack != null ? triggers[0].Stack.parent : null;
            foreach (var h in Physics2D.RaycastAll(pos + Vector2.up * 0.2f, Vector2.down, 4f))
            {
                if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(transform)) continue;
                Transform s = h.collider.transform;
                while (s != null && s.parent != container) s = s.parent;
                if (s == null) break;
                StackExitTrigger own = null;
                foreach (var t in triggers) if (t.Stack == s) { own = t; break; }
                var m = own != null ? own.TriggerMode : CameraFollow.Mode.Vertical;   // scaffolds have no trigger: vertical
                CameraFollow.SetMode(m, own != null ? own.StackTargetX : s.position.x);
                SetFloorY(m == CameraFollow.Mode.Horizontal && own != null ? own.transform.position.y : float.MinValue, m);
                Debug.Log($"[Revive] Respawn on stack '{s.name}' -> camera mode {m}");
                return;
            }

            // 2) Fallback (nothing under him): nearest stack by bounds.
            StackExitTrigger best = null;
            float bestDist = float.MaxValue;
            foreach (var t in triggers)
            {
                var stack = t.Stack;
                if (stack == null || !stack.gameObject.activeInHierarchy) continue;
                bool any = false;
                Bounds b = default;
                foreach (var c in stack.GetComponentsInChildren<Collider2D>(false))
                {
                    if (c.isTrigger) continue;
                    if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
                }
                if (!any) continue;
                b.Expand(new Vector3(1f, 1.5f, 0f));
                float d = b.Contains(new Vector3(pos.x, pos.y, b.center.z)) ? 0f : Vector2.Distance(pos, b.ClosestPoint(new Vector3(pos.x, pos.y, b.center.z)));
                if (d < bestDist) { bestDist = d; best = t; }
            }
            if (best == null) return;
            var mode = best.TriggerMode;
            CameraFollow.SetMode(mode, best.StackTargetX);
            SetFloorY(mode == CameraFollow.Mode.Horizontal ? best.transform.position.y : float.MinValue, mode);
            Debug.Log($"[Revive] Respawn is in stack '{best.Stack.name}' -> camera mode {mode}");
        }

        /// <summary>
        /// Nearest white milestone floor (the light band at the top of vertical stacks) whose floor is above aboveY.
        /// Returns a standing spot in the middle of that floor and the camera x to frame it.
        /// </summary>
        private bool FindNextWhiteMilestone(float aboveY, out Vector2 spot, out float camX)
        {
            spot = Vector2.zero; camX = 0f;
            float bestTop = float.MaxValue; Transform bestPlat = null;
            foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.sprite == null || sr.sprite.name != "Square" || !sr.enabled) continue;
                var c = sr.color;
                if (!(c.r > 0.9f && c.g > 0.9f && c.b > 0.9f && c.a < 0.6f)) continue;
                Transform plat = sr.transform;
                while (plat != null && !plat.name.StartsWith("Platform")) plat = plat.parent;
                if (plat == null) continue;
                float top = float.MinValue;
                foreach (var col in plat.GetComponentsInChildren<Collider2D>(false))
                    if (!col.isTrigger && (col.name.StartsWith("Floor_Tile") || col.name.StartsWith("Up Floor"))) top = Mathf.Max(top, col.bounds.max.y);
                if (top == float.MinValue || top <= aboveY + 0.5f) continue;
                if (top < bestTop) { bestTop = top; bestPlat = plat; }
            }
            if (bestPlat == null) return false;

            // middle of the floor, on an actual tile
            float lo = float.MaxValue, hi = float.MinValue;
            var tiles = new List<Collider2D>();
            foreach (var col in bestPlat.GetComponentsInChildren<Collider2D>(false))
                if (!col.isTrigger && (col.name.StartsWith("Floor_Tile") || col.name.StartsWith("Up Floor"))) { tiles.Add(col); lo = Mathf.Min(lo, col.bounds.min.x); hi = Mathf.Max(hi, col.bounds.max.x); }
            float x = (lo + hi) * 0.5f;
            Collider2D under = null; float best = float.MaxValue;
            foreach (var col in tiles) { float d = Mathf.Abs(col.bounds.center.x - x); if (d < best) { best = d; under = col; } }
            if (under != null && (x < under.bounds.min.x || x > under.bounds.max.x)) x = under.bounds.center.x;
            float feet = _col != null ? transform.position.y - _col.bounds.min.y : 0.5f;
            spot = new Vector2(x, bestTop + feet + 0.02f);

            camX = x;
            if (TryGetStackCentre(bestPlat, out float cx, out float w))
            {
                var cam = CameraFollow != null ? CameraFollow.GetComponent<Camera>() : null;
                float halfW = cam != null && cam.orthographic ? cam.orthographicSize * cam.aspect : 11f;
                if (w <= 2f * halfW - 2f * lockEdgeMargin) camX = cx;
            }
            return true;
        }

        /// <summary>
        /// Put Brutus standing on a floor (feet at floorTop) and make everything consistent with it:
        /// fall-death height, respawn point, rewind buffer, camera mode and camera position.
        /// Used by checkpoint starts.
        /// </summary>
        public void PlaceAt(float x, float floorTop)
        {
            float feet = _col != null ? transform.position.y - _col.bounds.min.y : 0.5f;
            var pos = new Vector2(x, floorTop + feet + 0.02f);
            transform.position = pos;
            _rb.position = pos;
            _rb.linearVelocity = Vector2.zero;
            _frameVelocity = Vector2.zero;
            _highestLandedY = pos.y;
            _lastSafePosition = pos;
            _deathRewindPosition = pos;
            _hasWalledCheckpoint = false;
            _bufferCount = 0; _bufferIndex = 0;
            Physics2D.SyncTransforms();
            if (CameraFollow != null)
            {
                RestoreCameraModeFor(pos);
                float camX = pos.x;
                foreach (var h in Physics2D.RaycastAll(pos, Vector2.down, 3f))
                {
                    if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(transform)) continue;
                    var cam = CameraFollow.GetComponent<Camera>();
                    float halfW = cam != null && cam.orthographic ? cam.orthographicSize * cam.aspect : 11f;
                    if (TryGetStackCentre(h.collider.transform, out float cx, out float w) && w <= 2f * halfW - 2f * lockEdgeMargin) camX = cx;
                    break;
                }
                CameraFollow.SnapTo(camX, pos.y + CameraFollow.verticalLead);
            }
        }

        /// <summary>Rocket-style revive: launch puff, curved eased flight with a trail, landing puff, resume.</summary>
        private IEnumerator RocketReviveRoutine(Vector2 from, Vector2 to, float camX, int floorsSkipped)
        {
            Time.timeScale = 0f;
            InputLocked = true;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            transform.position = from;

            var trail = GetComponentInChildren<TrailRenderer>(true);
            if (trail != null) { trail.gameObject.SetActive(true); trail.Clear(); trail.emitting = true; }
            PlayUnscaled("Launch Particles");

            float dist = Vector2.Distance(from, to);
            float duration = Mathf.Clamp(dist / Mathf.Max(1f, rocketSpeed), 0.8f, 1.8f);
            float side = Mathf.Sign(to.x - from.x); if (side == 0f) side = 1f;
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(side * rocketArc, dist * 0.15f);   // arc + overshoot height
            float camVelX = 0f, camVelY = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float e = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;   // ease in-out cubic
                Vector2 a = Vector2.Lerp(from, ctrl, e), b = Vector2.Lerp(ctrl, to, e);
                Vector2 pos = Vector2.Lerp(a, b, e);
                transform.position = pos;
                if (CameraFollow != null)
                {
                    float tx = Mathf.Lerp(pos.x, camX, e), ty = pos.y + CameraFollow.verticalLead;
                    float nx = Mathf.SmoothDamp(CameraFollow.transform.position.x, tx, ref camVelX, 0.15f, Mathf.Infinity, Time.unscaledDeltaTime);
                    float ny = Mathf.SmoothDamp(CameraFollow.transform.position.y, ty, ref camVelY, 0.15f, Mathf.Infinity, Time.unscaledDeltaTime);
                    CameraFollow.transform.position = new Vector3(nx, ny, CameraFollow.transform.position.z);
                }
                yield return null;
            }
            transform.position = to;
            if (trail != null) trail.emitting = false;
            PlayUnscaled("Land Particles");
            Haptics.Tick(25, 140);

            if (CameraFollow != null)
            {
                RestoreCameraModeFor(to);
                CameraFollow.SnapTo(camX, to.y + CameraFollow.verticalLead);
            }
            _deathRewindPosition = to;
            _lastSafePosition = to;
            _highestLandedY = to.y;
            _bufferCount = 0;
            _bufferIndex = 0;
            if (rocketAddsScore && floorsSkipped > 0)
            {
                // look it up directly: the scoreManager slot may be empty in a scene (it is also what gates
                // the under-20 restart rule, so we deliberately don't assign it here)
                var sm = scoreManager != null ? scoreManager : FindFirstObjectByType<ScoreManager>();
                if (sm != null) sm.AddPoints(floorsSkipped);
            }

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
            yield return new WaitForSecondsRealtime(reviveFreezeDuration);

            if (trail != null) trail.gameObject.SetActive(false);
            Time.timeScale = 1f;
            InputLocked = false;
            Debug.Log("[Revive] Rocket landed - resuming");
        }

        private void PlayUnscaled(string childName)
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name != childName) continue;
                var main = ps.main; main.useUnscaledTime = true;   // game is paused during the revive
                ps.gameObject.SetActive(true);
                ps.Play(true);
            }
        }

        private IEnumerator ReviveRewindRoutine()
        {
            Time.timeScale = 0f;
            InputLocked = true;

            _rb.bodyType = RigidbodyType2D.Kinematic;

            List<Vector2> path = BuildRewindPath();

            float elapsed = 0f;
            Vector2 prevPos = transform.position;

            // SmoothDamp state for camera during rewind
            float camVelX = 0f;
            float camVelY = 0f;
            float camSmoothTime = 0.3f;

            while (elapsed < rewindAnimDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / rewindAnimDuration);

                // Quintic ease in-out
                float eased;
                if (t < 0.5f)
                    eased = 16f * t * t * t * t * t;
                else
                {
                    float f = (2f * t) - 2f;
                    eased = 0.5f * f * f * f * f * f + 1f;
                }

                // Map to spline
                float pathT = eased * (path.Count - 1);
                int seg = Mathf.FloorToInt(pathT);
                float segT = pathT - seg;

                int i0 = Mathf.Max(seg - 1, 0);
                int i1 = Mathf.Clamp(seg, 0, path.Count - 1);
                int i2 = Mathf.Clamp(seg + 1, 0, path.Count - 1);
                int i3 = Mathf.Clamp(seg + 2, 0, path.Count - 1);

                Vector2 pos = CatmullRom(path[i0], path[i1], path[i2], path[i3], segT);

                // Dampen toward spline position
                pos = Vector2.Lerp(prevPos, pos, 0.5f);
                prevPos = pos;

                transform.position = pos;

                // Camera follows with SmoothDamp for buttery smooth movement
                if (CameraFollow != null)
                {
                    float targetCamX = pos.x;
                    float targetCamY = pos.y + CameraFollow.verticalLead;

                    float newCamX = Mathf.SmoothDamp(
                        CameraFollow.transform.position.x, targetCamX,
                        ref camVelX, camSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

                    float newCamY = Mathf.SmoothDamp(
                        CameraFollow.transform.position.y, targetCamY,
                        ref camVelY, camSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

                    CameraFollow.transform.position = new Vector3(newCamX, newCamY, CameraFollow.transform.position.z);
                }

                yield return null;
            }

            transform.position = _deathRewindPosition;

            if (CameraFollow != null)
            {
                // The rewind moves Brutus BACKWARD, possibly into the previous stack. Stack triggers only
                // fire going forward, so set the camera mode for the stack he actually respawns in.
                RestoreCameraModeFor(_deathRewindPosition);
                CameraFollow.SnapTo(_deathRewindCameraX, _deathRewindPosition.y + CameraFollow.verticalLead);
            }

            _bufferCount = 0;
            _bufferIndex = 0;

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;

            yield return new WaitForSecondsRealtime(reviveFreezeDuration);

            Time.timeScale = 1f;
            InputLocked = false;

            Debug.Log("[Revive] Rewind complete — resuming");
        }

        // ---------- Jump ----------

        private bool _bufferedJumpUsable;
        private bool _endedJumpEarly;
        private bool _coyoteUsable;
        private float _timeJumpWasPressed;

        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

        private void HandleJump()
        {
            if (_queuedJumps <= 0) return;


            if (_grounded || CanUseCoyote)
            {
                _queuedJumps = 0;
                ExecuteJump();
            }
        }

        /// <summary>Same as a player's tap (used by the recording autopilot).</summary>
        public void TapJump()
        {
            if (InputLocked || _isDead) return;
            _timeJumpWasPressed = _time;
            ExecuteJump();
        }

        /// <summary>True while standing on something.</summary>
        public bool IsGrounded => _grounded;

        private void ExecuteJump()
        {
            _grounded = false;
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _coyoteUsable = false;
            _frameVelocity.y = _stats.JumpPower;
            Jumped?.Invoke();
            Haptics.Tick(12, 60);

            PlaySquashStretch(1f - jumpStretchAmount, 1f + jumpStretchAmount);
        }

        // ---------- Movement ----------

        private int _moveDirection = 1;

        private void HandleDirection()
        {
            float targetSpeed = _moveDirection * _stats.MaxSpeed;
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, targetSpeed, _stats.Acceleration * Time.fixedDeltaTime);
        }

        private void HandleGravity()
        {
            if (_grounded && _frameVelocity.y <= 0f)
            {
                _frameVelocity.y = _stats.GroundingForce;
            }
            else
            {
                var inAirGravity = _stats.FallAcceleration;
                _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -_stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
            }
        }

        private void ApplyMovement() => _rb.linearVelocity = _frameVelocity;

        // ---------- Squash & stretch ----------

        private void PlaySquashStretch(float xScale, float yScale)
        {
            if (SpriteRenderer == null) return;
            if (_squashRoutine != null) StopCoroutine(_squashRoutine);
            _squashRoutine = StartCoroutine(SquashStretchRoutine(
                new Vector3(_spriteBaseScale.x * xScale, _spriteBaseScale.y * yScale, _spriteBaseScale.z)));
        }

        private IEnumerator SquashStretchRoutine(Vector3 startScale)
        {
            var t = SpriteRenderer.transform;
            float elapsed = 0f;
            while (elapsed < squashStretchDuration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / squashStretchDuration;
                float eased = EaseOutBack(p);
                t.localScale = Vector3.LerpUnclamped(startScale, _spriteBaseScale, eased);
                yield return null;
            }
            t.localScale = _spriteBaseScale;
            _squashRoutine = null;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_stats == null) Debug.LogWarning("Please assign a ScriptableStats asset to the Player Controller's Stats slot", this);
        }
#endif
    }

    public struct FrameInput
    {
        public bool JumpDown;
        public Vector2 Move;
    }

    public interface IPlayerController
    {
        public event Action<bool, float> GroundedChanged;
        public event Action Jumped;
        public Vector2 FrameInput { get; }
    }
}