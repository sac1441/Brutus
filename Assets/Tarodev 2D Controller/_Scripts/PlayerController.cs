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
        private bool _touchInputReady = false;
        private bool _validTouchInput = false; // New flag to track valid touch input
        private bool _grounded;

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
        }

        private void GatherInput()
        {
            bool jumpPressed = false;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                // Only set _touchInputReady when a touch begins, not when it ends
                if (touch.phase == TouchPhase.Began && _grounded)
                {
                    jumpPressed = true;
                    _touchInputReady = true;
                    _validTouchInput = true; // Mark touch input as valid
                }
                // Trigger jump only when the touch ends and _touchInputReady is true
                else if (_touchInputReady && touch.phase == TouchPhase.Ended)
                {
                    _touchInputReady = false; // Reset to prevent repeated jumps
                    _validTouchInput = false;
                }
                // Reset _touchInputReady and _validTouchInput if the touch is canceled
                else if (touch.phase == TouchPhase.Canceled)
                {
                    _touchInputReady = false;
                    _validTouchInput = false;
                }
            }
            else
            {
                // Reset _touchInputReady and _validTouchInput when no touches are active
                _touchInputReady = false;
                _validTouchInput = false;
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
                _jumpToConsume = true;
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

            bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);
            bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);

            if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

            if (!_grounded && groundHit && _frameVelocity.y <= 0)
            {
                //Debug.Log("Grounded");

                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
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

            //scene reset
            if (airTime > 30f)
            {
                //SceneManager.LoadSceneAsync(0);
                SceneManager.LoadScene(0);
                AdsManager.Instance.CheckForAdsTime();
            }

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
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
            // Process jump immediately after input, without delay
            if (!_validTouchInput || (!_jumpToConsume && !HasBufferedJump)) return;

            if (_grounded || CanUseCoyote)
            {
                ExecuteJump();
                _validTouchInput = false; // Reset valid touch input after jump
            }

            _jumpToConsume = false;
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
