using UnityEngine;
using JumpUp.Core;

namespace JumpUp.CameraSystem
{
    /// <summary>
    /// Controls vertical camera tracking for the endless jumper.
    /// The camera strictly ascends, tracking the player's highest altitude, and never moves down.
    /// Provides screen boundary dimensions and bottom-screen death detection.
    /// Compatible with Unity 6.3.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    [DefaultExecutionOrder(-50)]
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        [Header("Target Tracking")]
        [SerializeField] private Transform target;
        [Tooltip("Vertical offset from screen center so the player stays in the lower half of the screen.")]
        [SerializeField] private float verticalOffset = 1.5f;
        [Tooltip("Smooth time for vertical camera dampening.")]
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Death Boundary")]
        [Tooltip("Distance below the camera viewport bottom before triggering game over.")]
        [SerializeField] private float deathThreshold = 1.0f;

        private UnityEngine.Camera cam;
        private float currentVelocityY;
        private float highestCameraY;
        private bool isTracking = true;

        public UnityEngine.Camera Cam => cam;
        public Transform Target => target;

        public float OrthographicSize => cam != null ? cam.orthographicSize : 5f;
        public float AspectRatio => cam != null ? cam.aspect : (9f / 16f);

        public float HalfHeight => OrthographicSize;
        public float HalfWidth => HalfHeight * AspectRatio;

        public float LeftBoundX => transform.position.x - HalfWidth;
        public float RightBoundX => transform.position.x + HalfWidth;
        public float TopBoundY => transform.position.y + HalfHeight;
        public float BottomBoundY => transform.position.y - HalfHeight;
        public float DeathY => BottomBoundY - deathThreshold;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            cam = GetComponent<UnityEngine.Camera>();
            highestCameraY = transform.position.y;
        }

        private void Start()
        {
            if (target != null)
            {
                // Align initial camera position so player is placed lower in the view
                float targetY = target.position.y - verticalOffset;
                if (targetY > highestCameraY)
                {
                    highestCameraY = targetY;
                }
                Vector3 pos = transform.position;
                pos.y = highestCameraY;
                transform.position = pos;
            }
        }

        private void OnEnable()
        {
            GameManager.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            GameManager.OnGameOver -= HandleGameOver;
        }

        private void LateUpdate()
        {
            if (!isTracking || target == null) return;

            // Check if player has fallen below the camera death threshold
            if (target.position.y < DeathY)
            {
                GameManager.Instance?.TriggerGameOver();
                return;
            }

            // Only track upwards
            float desiredTargetY = target.position.y - verticalOffset;
            if (desiredTargetY > highestCameraY)
            {
                highestCameraY = desiredTargetY;
            }

            Vector3 currentPos = transform.position;
            float newY = Mathf.SmoothDamp(currentPos.y, highestCameraY, ref currentVelocityY, smoothTime);
            transform.position = new Vector3(currentPos.x, newY, currentPos.z);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                float targetY = target.position.y - verticalOffset;
                if (targetY > highestCameraY)
                {
                    highestCameraY = targetY;
                }
            }
        }

        private void HandleGameOver()
        {
            isTracking = false;
        }

        private void OnDrawGizmos()
        {
            if (cam == null) cam = GetComponent<UnityEngine.Camera>();
            if (cam == null) return;

            Gizmos.color = Color.red;
            float deathLineY = BottomBoundY - deathThreshold;
            Gizmos.DrawLine(new Vector3(LeftBoundX - 2f, deathLineY, 0f), new Vector3(RightBoundX + 2f, deathLineY, 0f));

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(transform.position.x, transform.position.y, 0f), new Vector3(HalfWidth * 2f, HalfHeight * 2f, 0.1f));
        }
    }
}
