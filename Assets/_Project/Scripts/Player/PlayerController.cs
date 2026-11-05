using System;
using UnityEngine;
using UnityEngine.InputSystem;
using JumpUp.Core;
using JumpUp.CameraSystem;
using JumpUp.Platforms;

namespace JumpUp.Player
{
    /// <summary>
    /// Handles player horizontal movement, jumping physics, screen wrap-around, and platform interactions.
    /// Uses Unity New Input System and Unity 2D Physics.
    /// Compatible with Unity 6.3.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Horizontal movement speed.")]
        [SerializeField] private float moveSpeed = 8.5f;

        [Tooltip("Default upward impulse applied when landing on a standard platform.")]
        [SerializeField] private float defaultJumpForce = 12.5f;

        [Tooltip("Horizontal acceleration smoothing factor.")]
        [SerializeField] private float acceleration = 25f;

        [Header("Screen Wrapping")]
        [Tooltip("Margin added beyond camera viewport bounds before wrapping player.")]
        [SerializeField] private float wrapMargin = 0.4f;

        [Header("Input Configuration")]
        [Tooltip("Optional Input Action Reference for movement (e.g. Player/Move). If unassigned, direct keyboard/touch inputs will be used.")]
        [SerializeField] private InputActionReference moveActionReference;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Tilt angle when moving sideways.")]
        [SerializeField] private float tiltAngle = 10f;

        [Header("Jump Settings")]
        [Tooltip("Grace period (seconds) after leaving a platform where SPACE jump is still allowed (coyote time).")]
        [SerializeField] private float coyoteTime = 0.12f;

        public static event Action<float> OnPlayerJumped;
        public static event Action OnPlayerDied;

        private Rigidbody2D rb;
        private Collider2D col;
        private float horizontalInput;
        private float currentHorizontalVelocity;
        private bool isDead;

        // Grounded tracking — platforms use isTrigger=true so we detect via OnTrigger callbacks
        private bool isGrounded;
        private float lastGroundedTime;
        private Platform currentPlatform; // The platform the player is currently standing on

        /// <summary>
        /// Unity 6.3 uses linearVelocity for Rigidbody2D.
        /// </summary>
        /// <remarks>Wrapped for forward compatibility with older Unity versions.</remarks>
        public Vector2 Velocity
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return rb.linearVelocity;
#else
                return rb.velocity;
#endif
            }
            set
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = value;
#else
                rb.velocity = value;
#endif
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // Ensure player physics configuration
            rb.gravityScale = 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void Start()
        {
            ScoreManager.Instance?.InitializeStartingHeight(transform.position.y);
        }

        private void OnEnable()
        {
            moveActionReference?.action?.Enable();
            GameManager.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            moveActionReference?.action?.Disable();
            GameManager.OnGameOver -= HandleGameOver;
        }

        private void Update()
        {
            if (isDead) return;

            // Refresh isGrounded from coyote time (OnTriggerStay2D sets lastGroundedTime each frame)
            isGrounded = (Time.time - lastGroundedTime) < coyoteTime;

            // Clear grounded flag once player is moving upward fast enough (just launched)
            if (Velocity.y > 0.5f)
            {
                isGrounded = false;
                lastGroundedTime = -100f;
                currentPlatform = null;
            }

            ReadInput();
            HandleScreenWrap();
            UpdateVisuals();

            // Notify score system of player's vertical progress
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                ScoreManager.Instance?.UpdateHeight(transform.position.y);
            }
        }

        private void FixedUpdate()
        {
            if (isDead) return;

            ApplyPlatformMovement();
            ApplyHorizontalMovement();
        }

        /// <summary>
        /// Carries the player smoothly along with the moving platform they are standing on.
        /// Transfers both horizontal and vertical displacements from Platform.FrameDelta.
        /// </summary>
        private void ApplyPlatformMovement()
        {
            if (!isGrounded || currentPlatform == null) return;

            Vector2 delta = currentPlatform.FrameDelta;
            if (delta.sqrMagnitude > 0.0000001f)
            {
                rb.position += delta;
            }
        }

        /// <summary>
        /// Reads input across New Input System: Action reference, Keyboard, Gamepad, Touch, and Mouse.
        /// </summary>
        private void ReadInput()
        {
            float inputX = 0f;

            // 1. Check InputActionReference if assigned
            if (moveActionReference != null && moveActionReference.action != null && moveActionReference.action.enabled)
            {
                Vector2 val = moveActionReference.action.ReadValue<Vector2>();
                if (Mathf.Abs(val.x) > 0.05f)
                {
                    inputX = val.x;
                }
            }

            // 2. Direct Keyboard fallback
            if (Mathf.Abs(inputX) < 0.05f && Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    inputX -= 1f;
                }
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    inputX += 1f;
                }
            }

            // 3. Direct Gamepad fallback
            if (Mathf.Abs(inputX) < 0.05f && Gamepad.current != null)
            {
                float stickX = Gamepad.current.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) > 0.15f) inputX = stickX;
                if (Gamepad.current.dpad.left.isPressed) inputX -= 1f;
                if (Gamepad.current.dpad.right.isPressed) inputX += 1f;
            }

            // 4. Touch / Mouse screen side tap for Mobile & Desktop
            if (Mathf.Abs(inputX) < 0.05f)
            {
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                {
                    Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
                    inputX = touchPos.x < (Screen.width * 0.5f) ? -1f : 1f;
                }
                else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    inputX = mousePos.x < (Screen.width * 0.5f) ? -1f : 1f;
                }
            }

            horizontalInput = Mathf.Clamp(inputX, -1f, 1f);

            // Auto-start game from Ready state upon receiving first input (horizontal or Space)
            bool isReadyState = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Ready;

            if (Mathf.Abs(horizontalInput) > 0.1f && isReadyState)
            {
                GameManager.Instance.StartGame();
            }

            // ── SPACE key jump ──────────────────────────────────────────────
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                // Auto-start from Ready state on Space press
                if (isReadyState)
                {
                    GameManager.Instance.StartGame();
                }

                // Apply jump when grounded (or within coyote time) and game is playing
                bool effectivelyGrounded = isGrounded || (Time.time - lastGroundedTime < coyoteTime);
                bool isPlaying = GameManager.Instance != null && GameManager.Instance.IsPlaying;

                if (effectivelyGrounded && isPlaying)
                {
                    // Read platform-specific jump multiplier (Spring platforms boost force)
                    float multiplier = currentPlatform != null ? currentPlatform.BounceMultiplier : 1.0f;

                    // Reset grounded flag immediately so we can't double-jump while still in trigger
                    isGrounded = false;
                    lastGroundedTime = -100f; // Exhaust coyote window
                    currentPlatform = null;

                    Bounce(multiplier);
                }
            }

            // Also support Gamepad south button (A/Cross) as jump
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                bool effectivelyGrounded = isGrounded || (Time.time - lastGroundedTime < coyoteTime);
                bool isPlaying = GameManager.Instance != null && GameManager.Instance.IsPlaying;

                if (effectivelyGrounded && isPlaying)
                {
                    float multiplier = currentPlatform != null ? currentPlatform.BounceMultiplier : 1.0f;
                    isGrounded = false;
                    lastGroundedTime = -100f;
                    currentPlatform = null;
                    Bounce(multiplier);
                }
            }
        }

        private void ApplyHorizontalMovement()
        {
            float targetVelocityX = horizontalInput * moveSpeed;
            float newVelocityX = Mathf.MoveTowards(Velocity.x, targetVelocityX, acceleration * Time.fixedDeltaTime);
            Velocity = new Vector2(newVelocityX, Velocity.y);
        }

        /// <summary>
        /// Wraps player position around left and right edges of the camera viewport.
        /// </summary>
        private void HandleScreenWrap()
        {
            float leftBound = -3f;
            float rightBound = 3f;

            if (CameraFollow.Instance != null)
            {
                leftBound = CameraFollow.Instance.LeftBoundX;
                rightBound = CameraFollow.Instance.RightBoundX;
            }
            else if (UnityEngine.Camera.main != null)
            {
                float halfWidth = UnityEngine.Camera.main.orthographicSize * UnityEngine.Camera.main.aspect;
                leftBound = UnityEngine.Camera.main.transform.position.x - halfWidth;
                rightBound = UnityEngine.Camera.main.transform.position.x + halfWidth;
            }

            Vector3 pos = transform.position;

            if (pos.x < leftBound - wrapMargin)
            {
                pos.x = rightBound + wrapMargin * 0.5f;
                transform.position = pos;
            }
            else if (pos.x > rightBound + wrapMargin)
            {
                pos.x = leftBound - wrapMargin * 0.5f;
                transform.position = pos;
            }
        }

        private void UpdateVisuals()
        {
            if (spriteRenderer == null) return;

            // Flip sprite depending on horizontal movement direction
            if (horizontalInput > 0.05f)
            {
                spriteRenderer.flipX = false;
                transform.rotation = Quaternion.Euler(0f, 0f, -tiltAngle);
            }
            else if (horizontalInput < -0.05f)
            {
                spriteRenderer.flipX = true;
                transform.rotation = Quaternion.Euler(0f, 0f, tiltAngle);
            }
            else
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 10f);
            }
        }

        /// <summary>
        /// Applies an upward jump impulse to the player.
        /// </summary>
        public void Bounce(float bounceMultiplier = 1f)
        {
            isGrounded = false;
            lastGroundedTime = -100f;
            currentPlatform = null;

            float calculatedForce = defaultJumpForce * bounceMultiplier;
            Velocity = new Vector2(Velocity.x, calculatedForce);
            OnPlayerJumped?.Invoke(calculatedForce);
        }

        private void HandleGameOver()
        {
            isDead = true;
            isGrounded = false;
            lastGroundedTime = -100f;
            currentPlatform = null;
            col.enabled = false;
            OnPlayerDied?.Invoke();
        }

        // ── Grounded detection via collision callbacks ───────────────────────
        // Platforms have solid BoxCollider2D + PlatformEffector2D (one-way).
        // Player physically lands on them — use OnCollisionEnter/Stay/Exit2D.

        private void OnCollisionStay2D(Collision2D collision)
        {
            UpdateGroundedFromCollision(collision);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            UpdateGroundedFromCollision(collision);
        }

        private void UpdateGroundedFromCollision(Collision2D collision)
        {
            if (isDead) return;
            Platform platform = collision.collider.GetComponent<Platform>();
            if (platform == null) return;

            // Only count as grounded when player is landing onto or standing on the platform
            float platformTop = platform.transform.position.y;
            if (platform.Collider != null)
            {
                platformTop = platform.Collider.bounds.max.y;
            }

            float playerBottom = transform.position.y - (col != null ? col.bounds.extents.y : 0.45f);

            // Feet must be roughly at or above the platform surface and falling or resting
            if (playerBottom >= platformTop - 0.3f && Velocity.y <= 0.5f)
            {
                lastGroundedTime = Time.time;
                isGrounded = true;
                currentPlatform = platform;
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.collider.GetComponent<Platform>() != null)
            {
                if (currentPlatform != null && collision.collider.GetComponent<Platform>() == currentPlatform)
                {
                    currentPlatform = null;
                    isGrounded = false;
                }
            }
        }

        // Keep these stubs so old trigger-based code doesn't cause compile errors
        // if Platform.cs still fires trigger events during transition.
        private void OnTriggerStay2D(Collider2D other) { }
        private void OnTriggerEnter2D(Collider2D other) { }
        private void OnTriggerExit2D(Collider2D other) { }
    }
}
