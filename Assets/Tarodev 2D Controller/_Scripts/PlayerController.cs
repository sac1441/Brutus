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

        private float _time;
        private bool _grounded;

        private const float INPUT_DELAY = 0.2f;
        private int _startFrame;

        private float _floorY = float.MinValue;
        private CameraFollow.Mode _currentStackMode = CameraFollow.Mode.Vertical;

        // Each tap is queued here and consumed on landing — taps are never dropped.
        private int _jumpQueue = 0;

        // Optional safety cap so mad mashing can't stack an absurd number of jumps.
        private const int MAX_JUMP_QUEUE = 3;

        public void SetFloorY(float floorY, CameraFollow.Mode mode)
        {
            _floorY = floorY;
            _currentStackMode = mode;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
            _time = 0;
            _startFrame = Time.frameCount;

            _jumpQueue = 0;
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
            CheckRespawn();
        }

        private void CheckRespawn()
        {
            Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

            bool fellOffBottom = viewportPos.y < 0f;
            bool fellOffSides = viewportPos.x < -0.1f || viewportPos.x > 1.1f;
            bool fellBetweenPlatforms = _currentStackMode == CameraFollow.Mode.Horizontal
                                        && transform.position.y < _floorY - 3f
                                        && _rb.linearVelocity.y < -1f;

            if (fellOffBottom || fellOffSides || fellBetweenPlatforms)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
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
                Move = Vector2.zero
            };

            if (_frameInput.JumpDown)
            {
                // If we're grounded and already have a jump queued, extra clicks
                // right now are the same intended jump (double/triple-click),
                // not a request for a second jump later. Don't queue them —
                // otherwise they sit and fire late on the *next* landing,
                // disconnected from this click burst.
                bool redundantWhileGrounded = _grounded && _jumpQueue > 0;

                if (!redundantWhileGrounded)
                    _jumpQueue = Mathf.Min(_jumpQueue + 1, MAX_JUMP_QUEUE);

                _timeJumpWasPressed = _time;
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

            float groundCheckDistance = Mathf.Max(_stats.GrounderDistance, Mathf.Abs(_frameVelocity.y) * Time.fixedDeltaTime + _stats.GrounderDistance);
            bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, groundCheckDistance, ~_stats.PlayerLayer);

            if (!_grounded && groundHit && _frameVelocity.y <= 0)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));

                RaycastHit2D platformHit = Physics2D.Raycast(_col.bounds.center, Vector2.down, _col.bounds.extents.y + 0.3f, ~_stats.PlayerLayer);
                if (platformHit.collider != null && CameraFollow != null)
                {
                    Transform stackRoot = platformHit.transform.parent?.parent;
                    if (stackRoot != null && stackRoot.CompareTag("Vertical_v"))
                    {
                        CameraFollow.SetTargetX(platformHit.transform.parent.position.x);
                    }
                }
            }
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }

            if (_grounded)
                airTime = 0f;
            else
                airTime += Time.timeScale;

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }

        private bool _bufferedJumpUsable;
        private bool _endedJumpEarly;
        private bool _coyoteUsable;
        private float _timeJumpWasPressed;

        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

        private void HandleJump()
        {
            // Nothing queued, nothing to do.
            if (_jumpQueue <= 0) return;

            // Fire one queued jump per landing (or coyote window). Runs once per FixedUpdate,
            // so a single jump leaves the ground before the next queued one can fire.
            if (_grounded || CanUseCoyote)
                ExecuteJump();
        }

        private void ExecuteJump()
        {
            _jumpQueue = Mathf.Max(0, _jumpQueue - 1);
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

                // Extra gravity on the way down for snappier feel
                if (_frameVelocity.y < 0)
                    inAirGravity *= 1.8f;

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