using System;
using UnityEngine;

namespace JumpUp.Core
{
    /// <summary>
    /// Tracks height-based score, combo bonuses, and persistent high score.
    /// Compatible with Unity 6.3.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        private const string HighScoreKey = "JUMP_UP_HIGH_SCORE";

        [Header("Settings")]
        [Tooltip("Points awarded per vertical unit climbed.")]
        [SerializeField] private float pointsPerUnit = 10f;

        [Header("Runtime State")]
        [SerializeField] private int currentScore;
        [SerializeField] private int highScore;
        [SerializeField] private float highestY;
        [SerializeField] private float startY;
        [SerializeField] private int bonusScore;

        public int CurrentScore => currentScore;
        public int HighScore => highScore;
        public bool IsNewHighScore => currentScore > highScore;

        public static event Action<int> OnScoreUpdated;
        public static event Action<int> OnHighScoreUpdated;
        public static event Action OnNewHighScoreAchieved;

        private bool newHighScoreTriggered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LoadHighScore();
        }

        private void OnEnable()
        {
            GameManager.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            GameManager.OnGameOver -= HandleGameOver;
        }

        /// <summary>
        /// Initializes the baseline Y position for scoring.
        /// </summary>
        public void InitializeStartingHeight(float initialY)
        {
            startY = initialY;
            highestY = initialY;
            currentScore = 0;
            bonusScore = 0;
            newHighScoreTriggered = false;
            OnScoreUpdated?.Invoke(currentScore);
        }

        /// <summary>
        /// Updates the score based on the player's peak height.
        /// Score only increases monotonically as the player ascends.
        /// </summary>
        public void UpdateHeight(float playerY)
        {
            if (playerY > highestY)
            {
                highestY = playerY;
                int heightPoints = Mathf.FloorToInt((highestY - startY) * pointsPerUnit);
                if (heightPoints < 0) heightPoints = 0;

                int calculatedTotal = heightPoints + bonusScore;
                if (calculatedTotal > currentScore)
                {
                    currentScore = calculatedTotal;
                    OnScoreUpdated?.Invoke(currentScore);

                    if (currentScore > highScore)
                    {
                        if (!newHighScoreTriggered && highScore > 0)
                        {
                            newHighScoreTriggered = true;
                            OnNewHighScoreAchieved?.Invoke();
                        }
                        highScore = currentScore;
                        OnHighScoreUpdated?.Invoke(highScore);
                    }
                }
            }
        }

        /// <summary>
        /// Adds bonus points (e.g., from collecting items or bouncing on springs).
        /// </summary>
        public void AddBonusScore(int bonus)
        {
            if (bonus <= 0) return;
            bonusScore += bonus;
            currentScore += bonus;
            OnScoreUpdated?.Invoke(currentScore);

            if (currentScore > highScore)
            {
                highScore = currentScore;
                OnHighScoreUpdated?.Invoke(highScore);
            }
        }

        private void HandleGameOver()
        {
            SaveHighScore();
        }

        private void LoadHighScore()
        {
            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            OnHighScoreUpdated?.Invoke(highScore);
        }

        private void SaveHighScore()
        {
            if (currentScore > PlayerPrefs.GetInt(HighScoreKey, 0))
            {
                PlayerPrefs.SetInt(HighScoreKey, currentScore);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Resets the persistent high score (useful for testing and settings menus).
        /// </summary>
        public void ResetHighScore()
        {
            PlayerPrefs.DeleteKey(HighScoreKey);
            highScore = 0;
            OnHighScoreUpdated?.Invoke(0);
        }
    }
}
