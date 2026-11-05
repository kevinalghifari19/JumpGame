using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JumpUp.Core
{
    /// <summary>
    /// Represents the high-level states of the game.
    /// </summary>
    public enum GameState
    {
        Ready,
        Playing,
        GameOver
    }

    /// <summary>
    /// Central manager controlling game loop states, lifecycle events, and scene transitions.
    /// Compatible with Unity 6.3.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.Ready;

        public GameState CurrentState => currentState;
        public bool IsPlaying => currentState == GameState.Playing;

        [Header("Configuration")]
        [Tooltip("Target frame rate for smooth 2D gameplay.")]
        [SerializeField] private int targetFrameRate = 60;

        public static event Action<GameState> OnStateChanged;
        public static event Action OnGameStarted;
        public static event Action OnGameOver;
        public static event Action OnGameRestarted;

        private void Awake()
        {
            Time.timeScale = 1f;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Application.targetFrameRate = targetFrameRate;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            SetState(GameState.Ready);
        }

        /// <summary>
        /// Transitions to a new game state.
        /// </summary>
        public void SetState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            OnStateChanged?.Invoke(currentState);

            switch (currentState)
            {
                case GameState.Ready:
                    Time.timeScale = 1f;
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    OnGameStarted?.Invoke();
                    break;

                case GameState.GameOver:
                    OnGameOver?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Starts the game session from the Ready state.
        /// </summary>
        public void StartGame()
        {
            if (currentState == GameState.Ready)
            {
                SetState(GameState.Playing);
            }
        }

        /// <summary>
        /// Triggers player defeat and transitions to GameOver state.
        /// </summary>
        public void TriggerGameOver()
        {
            if (currentState == GameState.Playing || currentState == GameState.Ready)
            {
                SetState(GameState.GameOver);
            }
        }

        /// <summary>
        /// Restarts the game session reliably. Restores time scale to 1.0f and reloads the current scene.
        /// </summary>
        public void RestartGame()
        {
            Debug.Log("[GameManager] Restarting game session...");
            Time.timeScale = 1f;
            OnGameRestarted?.Invoke();

            Scene current = SceneManager.GetActiveScene();
            if (current.buildIndex >= 0)
            {
                SceneManager.LoadScene(current.buildIndex);
            }
            else if (!string.IsNullOrEmpty(current.name))
            {
                SceneManager.LoadScene(current.name);
            }
            else
            {
                SceneManager.LoadScene("GameScene");
            }
        }
    }
}
