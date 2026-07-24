using System;
using TMPro;
using UnityEngine;
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
        private int eggCount = 0;
        [SerializeField] private TextMeshProUGUI flowerScoreText;
        private int flowerCount = 0;
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

        private Vector2 _lastSafePosition;
        private bool _isDead;

        // Horizontal-stack death detection (falling between platforms while
        // the camera follows the player, so viewport bounds won't catch it)
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

        // Airborne taps set this so the jump still fires on the next landing/coyote —
        // never expires, never dropped, never stacks into multiple jumps.
        private bool _jumpQueued = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
            _time = 0;
            _startFrame = Time.frameCount;
            _lastSafePosition = transform.position;

            _jumpQueued = false;
            _bufferedJumpUsable = false;
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeJumpWasPressed = float.MinValue;
            _frameLeftGrounded = float.MinValue;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            GatherInput();
        }

        private void GatherInput()
        {
            if (_time < INPUT_DELAY) return;
            if (Time.frameCount <= _startFrame + 1) return;

            bool jumpPressed = false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                    jumpPressed = true;
            }

            if (Input.GetMouseButtonDown(0))
                jumpPressed = true;

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
                _timeJumpWasPressed = _time;

                // Fire instantly if we're allowed to right now - no waiting for FixedUpdate.
                if (_grounded || CanUseCoyote)
                {
                    ExecuteJump();
                }
                else
                {
                    // Airborne - guarantee this click produces a jump the instant we land.
                    // No expiry window: however long the flight takes, it still fires.
                    _jumpQueued = true;
                }
            }
        }

        private void FixedUpdate()
        {
            CheckCollisions();
            HandleJump();
            HandleDirection();
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
            }
            else if (collision.gameObject.CompareTag("Right Wall"))
            {
                _moveDirection = -1;
                SpriteRenderer.flipX = true;
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

        private void CheckCollisions()
        {
            Physics2D.queriesStartInColliders = false;

            RaycastHit2D groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);
            bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);

            if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

            if (!_grounded && groundHit && _frameVelocity.y <= 0)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;

                // Respawn point = middle of the platform we just landed on.
                // Colliders live on individual Floor_Tile children, so the platform's
                // actual center is the tile's PARENT transform position, not the tile's
                // own collider bounds (same convention used for camera X targeting).
                _lastSafePosition = groundHit.collider != null && groundHit.collider.transform.parent != null
                    ? new Vector2(groundHit.collider.transform.parent.position.x, transform.position.y)
                    : (Vector2)transform.position;

                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
            }
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }

            if (_grounded)
            {
                airTime = 0f;
            }
            else
            {
                airTime += Time.timeScale;
            }

            //player fell off screen - show the revive screen instead of a hard reset
            if (airTime > 30f && !_isDead)
            {
                Die();
            }

            //horizontal stacks: camera follows the player so the airTime/viewport check
            //above won't catch falling between platforms - use a floor-Y threshold instead
            if (!_isDead && _currentStackMode == CameraFollow.Mode.Horizontal
                && _floorY > float.MinValue && transform.position.y < _floorY - 3f)
            {
                Die();
            }

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }

        private void Die()
        {
            _isDead = true;
            airTime = 0f;
            Time.timeScale = 0f;
            Died?.Invoke();
        }

        /// <summary>
        /// Attempts to spend eggs. Returns true and deducts the amount if the player has enough.
        /// </summary>
        public bool TrySpendEggs(int amount)
        {
            if (eggCount < amount) return false;

            eggCount -= amount;
            eggScoreText.text = eggCount.ToString();
            return true;
        }

        /// <summary>
        /// Revives the player at the last platform they were grounded on and resumes the game.
        /// </summary>
        public void Revive()
        {
            _isDead = false;
            _rb.linearVelocity = Vector2.zero;
            _frameVelocity = Vector2.zero;
            transform.position = _lastSafePosition;
            _grounded = false;
            airTime = 0f;
            Time.timeScale = 1f;
        }

        private bool _bufferedJumpUsable;
        private bool _endedJumpEarly;
        private bool _coyoteUsable;
        private float _timeJumpWasPressed;

        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

        private void HandleJump()
        {
            // Grounded clicks already fired instantly in GatherInput. This only
            // handles airborne taps waiting for the next landing/coyote window,
            // and it never expires — the click is never silently dropped.
            if (!_jumpQueued) return;

            if (_grounded || CanUseCoyote)
                ExecuteJump();
        }

        private void ExecuteJump()
        {
            _grounded = false; // airborne the instant we jump - don't wait for physics to confirm it
            _jumpQueued = false;
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _coyoteUsable = false;
            _frameVelocity.y = _stats.JumpPower;
            Jumped?.Invoke();
        }

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