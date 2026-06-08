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

        // -------------------------------------------------------
        // Horizontal stack death detection
        // -------------------------------------------------------
        private float _floorY = float.MinValue;
        private CameraFollow.Mode _currentStackMode = CameraFollow.Mode.Vertical;

        // Called by StackExitTrigger when player enters a new stack
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
        }

        private void Update()
        {
            _time += Time.deltaTime;
            GatherInput();
            CheckRespawn();
        }

        // -------------------------------------------------------
        // RESPAWN — handles both vertical and horizontal stacks
        // -------------------------------------------------------
        private void CheckRespawn()
        {
            Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

            // Vertical stack — fell below camera bottom
            bool fellOffBottom = viewportPos.y < 0f;

            // Vertical stack — drifted off sides
            bool fellOffSides = viewportPos.x < -0.1f || viewportPos.x > 1.1f;

            // Horizontal stack — camera follows player so viewport won't catch falls
            // Only triggers when falling downward (not while jumping)
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

            if (_frameInput.JumpDown && _time > INPUT_DELAY)
            {
                _jumpToConsume = true;
                _timeJumpWasPressed = _time;

                if (_grounded || CanUseCoyote)
                {
                    ExecuteJump();
                    _jumpToConsume = false;
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

            float groundCheckDistance = Mathf.Max(_stats.GrounderDistance, Mathf.Abs(_frameVelocity.y) * Time.fixedDeltaTime + _stats.GrounderDistance);
            bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, groundCheckDistance, ~_stats.PlayerLayer);

            if (!_grounded && groundHit && _frameVelocity.y <= 0)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));

                // Camera X realignment on landing
                RaycastHit2D platformHit = Physics2D.Raycast(_col.bounds.center, Vector2.down, _col.bounds.extents.y + 0.3f, ~_stats.PlayerLayer);
                if (platformHit.collider != null && CameraFollow != null)
                    CameraFollow.SetTargetX(platformHit.transform.position.x);
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

            RaycastHit2D _platformHit = Physics2D.Raycast(_col.bounds.center, Vector2.down, _col.bounds.extents.y + 0.3f, ~_stats.PlayerLayer);
            if (_platformHit.collider != null)
                Debug.Log($"Hit: {_platformHit.collider.gameObject.name} | parent: {_platformHit.transform.parent?.gameObject.name} | parentX: {_platformHit.transform.parent?.position.x}");
        }

        private bool _jumpToConsume;
        private bool _bufferedJumpUsable;
        private bool _endedJumpEarly;
        private bool _coyoteUsable;
        private float _timeJumpWasPressed;

        private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + _stats.JumpBuffer;
        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

        private void HandleJump()
        {
            if (_jumpToConsume && _time > _timeJumpWasPressed + _stats.JumpBuffer)
                _jumpToConsume = false;

            if (!_jumpToConsume && !HasBufferedJump) return;

            if (_grounded || CanUseCoyote)
            {
                ExecuteJump();
                _jumpToConsume = false;
            }
        }

        private void ExecuteJump()
        {
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