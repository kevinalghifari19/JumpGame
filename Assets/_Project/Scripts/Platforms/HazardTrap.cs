using UnityEngine;
using JumpUp.Core;
using JumpUp.Player;

namespace JumpUp.Platforms
{
    /// <summary>
    /// Represents a hazardous obstacle (such as a spike or hazard mine) that triggers Game Over upon contact.
    /// Integrated with the PlatformSpawner pooling system.
    /// Compatible with Unity 6.3.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HazardTrap : MonoBehaviour
    {
        [Header("Trap Settings")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Collider2D col;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true; // By default trigger so player doesn't get physically stuck on spikes
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            CheckPlayerContact(other.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            CheckPlayerContact(collision.gameObject);
        }

        private void CheckPlayerContact(GameObject target)
        {
            PlayerController player = target.GetComponent<PlayerController>();
            if (player != null)
            {
                // Trigger existing Game Over system
                if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
                {
                    GameManager.Instance.TriggerGameOver();
                }
            }
        }

        public void Configure(Vector3 position)
        {
            transform.position = position;
            if (col != null) col.enabled = true;
            gameObject.SetActive(true);
        }

        public void ResetTrap()
        {
            if (col != null) col.enabled = true;
            gameObject.SetActive(false);
        }
    }
}
