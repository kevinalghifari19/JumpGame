using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;
using JumpUp.Core;

namespace JumpUp.UI
{
    /// <summary>
    /// Manages Heads-Up Display (HUD), Start/Ready screen, and Game Over popup.
    /// Supports TextMeshPro (TMP_Text) and standard UI Text.
    /// Compatible with Unity 6.3.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private GameObject inGamePanel;
        [SerializeField] private GameObject gameOverPanel;

        [Header("In-Game Score Display")]
        [SerializeField] private TMP_Text scoreTextTMP;
        [SerializeField] private Text scoreTextLegacy;

        [Header("High Score Display")]
        [SerializeField] private TMP_Text highScoreTextTMP;
        [SerializeField] private Text highScoreTextLegacy;

        [Header("Game Over Screen Elements")]
        [SerializeField] private TMP_Text finalScoreTextTMP;
        [SerializeField] private Text finalScoreTextLegacy;
        [SerializeField] private TMP_Text finalHighScoreTextTMP;
        [SerializeField] private Text finalHighScoreTextLegacy;
        [SerializeField] private GameObject newBestBadge;
        [SerializeField] private Button restartButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Ensure InputSystemUIInputModule is configured with default actions for mouse, touch, and keys
            var inputModule = FindFirstObjectByType<InputSystemUIInputModule>();
            if (inputModule != null)
            {
                inputModule.AssignDefaultActions();
            }

            // Bind Restart Button
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            ScoreManager.OnScoreUpdated += UpdateScoreDisplay;
            ScoreManager.OnHighScoreUpdated += UpdateHighScoreDisplay;
            ScoreManager.OnNewHighScoreAchieved += HandleNewHighScore;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            ScoreManager.OnScoreUpdated -= UpdateScoreDisplay;
            ScoreManager.OnHighScoreUpdated -= UpdateHighScoreDisplay;
            ScoreManager.OnNewHighScoreAchieved -= HandleNewHighScore;

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartButtonClicked);
            }
        }

        private void Start()
        {
            if (newBestBadge != null)
            {
                newBestBadge.SetActive(false);
            }

            if (ScoreManager.Instance != null)
            {
                UpdateScoreDisplay(ScoreManager.Instance.CurrentScore);
                UpdateHighScoreDisplay(ScoreManager.Instance.HighScore);
            }

            SetPanelVisibility(ready: true, inGame: false, gameOver: false);
        }

        private void Update()
        {
            // Allow restarting via Space or Enter when Game Over panel is active
            if (gameOverPanel != null && gameOverPanel.activeSelf)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                        Keyboard.current.enterKey.wasPressedThisFrame ||
                        Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                    {
                        OnRestartButtonClicked();
                    }
                }
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Ready:
                    SetPanelVisibility(ready: true, inGame: false, gameOver: false);
                    break;

                case GameState.Playing:
                    SetPanelVisibility(ready: false, inGame: true, gameOver: false);
                    break;

                case GameState.GameOver:
                    ShowGameOver();
                    break;
            }
        }

        private void SetPanelVisibility(bool ready, bool inGame, bool gameOver)
        {
            if (readyPanel != null) readyPanel.SetActive(ready);
            if (inGamePanel != null) inGamePanel.SetActive(inGame);
            if (gameOverPanel != null) gameOverPanel.SetActive(gameOver);
        }

        public void UpdateScoreDisplay(int score)
        {
            string scoreStr = score.ToString();

            if (scoreTextTMP != null) scoreTextTMP.text = scoreStr;
            if (scoreTextLegacy != null) scoreTextLegacy.text = scoreStr;
        }

        public void UpdateHighScoreDisplay(int highScore)
        {
            string highStr = $"BEST: {highScore}";

            if (highScoreTextTMP != null) highScoreTextTMP.text = highStr;
            if (highScoreTextLegacy != null) highScoreTextLegacy.text = highStr;
        }

        private void HandleNewHighScore()
        {
            if (newBestBadge != null)
            {
                newBestBadge.SetActive(true);
            }
        }

        private void ShowGameOver()
        {
            SetPanelVisibility(ready: false, inGame: false, gameOver: true);

            int finalScore = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            int finalHigh = ScoreManager.Instance != null ? ScoreManager.Instance.HighScore : 0;

            string scoreStr = $"SCORE: {finalScore}";
            string highStr = $"BEST: {finalHigh}";

            if (finalScoreTextTMP != null) finalScoreTextTMP.text = scoreStr;
            if (finalScoreTextLegacy != null) finalScoreTextLegacy.text = scoreStr;

            if (finalHighScoreTextTMP != null) finalHighScoreTextTMP.text = highStr;
            if (finalHighScoreTextLegacy != null) finalHighScoreTextLegacy.text = highStr;
        }

        /// <summary>
        /// Invoked when the PLAY AGAIN button is clicked or restart key is pressed.
        /// </summary>
        public void OnRestartButtonClicked()
        {
            Debug.Log("[UIManager] PLAY AGAIN button activated! Initiating restart...");
            Time.timeScale = 1f;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
            else
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.buildIndex >= 0)
                {
                    SceneManager.LoadScene(activeScene.buildIndex);
                }
                else if (!string.IsNullOrEmpty(activeScene.name))
                {
                    SceneManager.LoadScene(activeScene.name);
                }
                else
                {
                    SceneManager.LoadScene("GameScene");
                }
            }
        }

        public void SetupReferences(
            GameObject ready, GameObject inGame, GameObject gameOver,
            TMP_Text scoreText, TMP_Text highScoreText,
            TMP_Text finalScore, TMP_Text finalHighScore,
            GameObject bestBadge, Button restartBtn)
        {
            readyPanel = ready;
            inGamePanel = inGame;
            gameOverPanel = gameOver;
            scoreTextTMP = scoreText;
            highScoreTextTMP = highScoreText;
            finalScoreTextTMP = finalScore;
            finalHighScoreTextTMP = finalHighScore;
            newBestBadge = bestBadge;
            restartButton = restartBtn;
        }

        public void SetupLegacyReferences(
            GameObject ready, GameObject inGame, GameObject gameOver,
            Text scoreText, Text highScoreText,
            Text finalScore, Text finalHighScore,
            GameObject bestBadge, Button restartBtn)
        {
            readyPanel = ready;
            inGamePanel = inGame;
            gameOverPanel = gameOver;
            scoreTextLegacy = scoreText;
            highScoreTextLegacy = highScoreText;
            finalScoreTextLegacy = finalScore;
            finalHighScoreTextLegacy = finalHighScore;
            newBestBadge = bestBadge;
            restartButton = restartBtn;
        }
    }
}
