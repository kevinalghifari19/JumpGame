using System.Collections;
using UnityEngine;
using JumpUp.Player;
using JumpUp.CameraSystem;

namespace JumpUp.Platforms
{
    public enum PlatformType
    {
        Standard,
        Moving,
        Fragile,
        Spring
    }

    /// <summary>
    /// Represents an interactable platform in the endless jumper.
    /// Handles player landing detection, movement oscillation, fragile breaking, and spring boosts.
    /// Auto-bounce is DISABLED — the player only jumps when SPACE is pressed.
    /// Compatible with Unity 6.3.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Platform : MonoBehaviour
    {
        [Header("Platform Attributes")]
        [SerializeField] private PlatformType platformType = PlatformType.Standard;
        [SerializeField] private float bounceMultiplier = 1.0f;

        [Header("Moving Platform Settings")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float movePadding = 0.5f;

        [Header("Fragile Platform Settings")]
        [SerializeField] private float breakDelay = 0.05f;

        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Collider2D col;
        private int moveDirection = 1;
        private bool isBroken;
        private bool hasLandingFired; // Guard: landing effects fire only once per landing event
        private Vector3 initialScale;
        private Vector3 lastFixedPosition;

        public PlatformType Type => platformType;
        public bool IsBroken => isBroken;
        public Collider2D Collider => col;

        /// <summary>
        /// Frame-by-frame physical delta displacement computed during FixedUpdate.
        /// Read by PlayerController to carry the player horizontally and vertically.
        /// </summary>
        public Vector2 FrameDelta { get; private set; }

        /// <summary>
        /// Current velocity of the platform in units per second.
        /// </summary>
        public Vector2 PlatformVelocity { get; private set; }

        /// <summary>
        /// The jump force multiplier this platform provides.
        /// Spring platforms return 1.9x; all others return 1.0x.
        /// PlayerController reads this when SPACE is pressed while grounded here.
        /// </summary>
        public float BounceMultiplier => bounceMultiplier;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = false;
                col.usedByEffector = true;
            }

            PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
            if (effector == null)
            {
                effector = gameObject.AddComponent<PlatformEffector2D>();
            }

            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 170f;
            effector.rotationalOffset = 0f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            initialScale = transform.localScale;
            lastFixedPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (platformType == PlatformType.Moving && !isBroken)
            {
                HandleHorizontalMovement();
            }

            Vector3 currentPos = transform.position;
            FrameDelta = (Vector2)(currentPos - lastFixedPosition);
            PlatformVelocity = Time.fixedDeltaTime > 0f ? (FrameDelta / Time.fixedDeltaTime) : Vector2.zero;
            lastFixedPosition = currentPos;
        }

        private void HandleHorizontalMovement()
        {
            float leftBound = -2.5f;
            float rightBound = 2.5f;

            if (CameraFollow.Instance != null)
            {
                leftBound = CameraFollow.Instance.LeftBoundX + movePadding;
                rightBound = CameraFollow.Instance.RightBoundX - movePadding;
            }

            transform.Translate(Vector3.right * (moveDirection * moveSpeed * Time.fixedDeltaTime));

            if (transform.position.x >= rightBound)
            {
                moveDirection = -1;
            }
            else if (transform.position.x <= leftBound)
            {
                moveDirection = 1;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerController player = collision.collider.GetComponent<PlayerController>();
            if (player == null) return;

            // Player approaching from above (falling/landing) → reset guard so squash/break can fire
            // Player approaching from below (jumping through) → PlatformEffector2D should ignore it,
            // but guard is NOT reset here to avoid spurious triggers when grazing the platform edge.
            if (player.Velocity.y <= 0.1f)
            {
                hasLandingFired = false;
            }

            CheckPlayerLanding(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            CheckPlayerLanding(collision.collider);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.collider.GetComponent<PlayerController>() != null)
            {
                hasLandingFired = false; // Player left — reset for next landing
            }
        }

        private void CheckPlayerLanding(Collider2D targetCollider)
        {
            if (isBroken) return;
            if (hasLandingFired) return; // Already processed this landing event

            PlayerController player = targetCollider.GetComponent<PlayerController>();
            if (player == null) return;

            // Player must be falling downward to land on a platform
            if (player.Velocity.y <= 0.1f)
            {
                // Ensure player's feet are above the platform center
                float platformTop = transform.position.y + (col != null ? col.bounds.extents.y : 0.1f);
                if (player.transform.position.y >= platformTop - 0.25f)
                {
                    hasLandingFired = true;
                    OnPlayerBounce(player);
                }
            }
        }

        private void OnPlayerBounce(PlayerController player)
        {
            switch (platformType)
            {
                case PlatformType.Standard:
                    // No auto-jump. Visual squash only — player jumps via SPACE.
                    StartCoroutine(BounceSquashEffect());
                    break;

                case PlatformType.Moving:
                    // No auto-jump. Visual squash only — player jumps via SPACE.
                    StartCoroutine(BounceSquashEffect());
                    break;

                case PlatformType.Spring:
                    // No auto-jump. Spring multiplier is read by PlayerController on SPACE press.
                    // Visual squash gives landing feedback.
                    StartCoroutine(BounceSquashEffect());
                    break;

                case PlatformType.Fragile:
                    // Fragile platforms still break on landing (no jump applied).
                    StartCoroutine(BreakSequence());
                    break;
            }
        }

        private IEnumerator BounceSquashEffect()
        {
            Vector3 squashed = new Vector3(initialScale.x * 1.15f, initialScale.y * 0.7f, initialScale.z);
            transform.localScale = squashed;
            yield return new WaitForSeconds(0.08f);
            transform.localScale = initialScale;
        }

        private IEnumerator BreakSequence()
        {
            isBroken = true;
            yield return new WaitForSeconds(breakDelay);

            if (col != null) col.enabled = false;

            // Fade or crumble visual
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 0.2f;
                spriteRenderer.color = c;
            }

            // Deactivate completely after brief crumble
            yield return new WaitForSeconds(0.2f);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Configures platform properties when retrieved from the object pool.
        /// </summary>
        public void Configure(PlatformType type, Vector3 position)
        {
            platformType = type;
            transform.position = position;
            lastFixedPosition = position;
            FrameDelta = Vector2.zero;
            PlatformVelocity = Vector2.zero;
            transform.localScale = initialScale;
            isBroken = false;
            hasLandingFired = false;
            gameObject.SetActive(true);

            if (col != null) col.enabled = true;

            // Set default multiplier and visual hints based on type
            switch (platformType)
            {
                case PlatformType.Standard:
                    bounceMultiplier = 1.0f;
                    SetVisualColor(new Color(0.35f, 0.85f, 0.35f)); // Vibrant Green
                    break;

                case PlatformType.Moving:
                    bounceMultiplier = 1.0f;
                    moveDirection = Random.value > 0.5f ? 1 : -1;
                    moveSpeed = Random.Range(2.0f, 3.5f);
                    SetVisualColor(new Color(0.3f, 0.65f, 1.0f)); // Blue
                    break;

                case PlatformType.Fragile:
                    bounceMultiplier = 1.0f;
                    SetVisualColor(new Color(0.9f, 0.4f, 0.3f)); // Red/Brown Fragile
                    break;

                case PlatformType.Spring:
                    bounceMultiplier = 1.9f;
                    SetVisualColor(new Color(1.0f, 0.85f, 0.2f)); // Gold / Spring
                    break;
            }
        }

        private void SetVisualColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// Resets the platform state for object pooling reuse.
        /// </summary>
        public void ResetPlatform()
        {
            isBroken = false;
            hasLandingFired = false;
            FrameDelta = Vector2.zero;
            PlatformVelocity = Vector2.zero;
            transform.localScale = initialScale;
            if (col != null) col.enabled = true;
            gameObject.SetActive(false);
        }
    }
}
