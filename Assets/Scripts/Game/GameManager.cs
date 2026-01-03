using System.Collections;
using Speedrun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
// Using InputSystem

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("Death UI")]
        [Tooltip("The parent Panel object containing the black background and text.")]
        public GameObject deathScreenObject; 
    
        [Tooltip("The CanvasGroup component on the Death Screen Panel for fading.")]
        public CanvasGroup deathScreenCanvasGroup;
    
        [Tooltip("How long it takes to fade to black.")]
        public float fadeDuration = 1.5f;

        [Header("Game Settings")]
        [Tooltip("If the player's Y position drops below this, they die.")]
        public float fallThreshold = -20f; 

        private bool _isDead;
        private GameObject _player;

        void Awake()
        {
            // Singleton pattern to access this easily from EnemyAI
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            // Ensure UI is hidden at start
            if (deathScreenObject != null)
            {
                deathScreenObject.SetActive(false);
                if (deathScreenCanvasGroup != null) deathScreenCanvasGroup.alpha = 0f;
            }
        }

        void Update()
        {
            if (_isDead)
            {
                HandleDeathInput();
            }
            else
            {
                CheckPlayerFall();
            }
        }

        private void CheckPlayerFall()
        {
            // Find player if we lost reference, like after a respawn or somethign
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player");
                return;
            }

            // Check if player fell out of the world
            if (_player.transform.position.y < fallThreshold)
            {
                TriggerDeath();
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void TriggerDeath()
        {
            if (_isDead) return; // Prevent double triggering
            _isDead = true;
            
            if (SpeedrunManager.Instance != null)
            {
                SpeedrunManager.Instance.StopTimer();
            }

            Debug.Log("Player has died.");

            //  Show cursor so player can interact if needed
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Start Fade Animation
            if (deathScreenObject != null)
            {
                StartCoroutine(FadeInDeathScreen());
            }
            else
            {
                // Fallback if no UI is assigned, just pause immediately
                Time.timeScale = 0f;
            }
        }

        private IEnumerator FadeInDeathScreen()
        {
            deathScreenObject.SetActive(true);
            float timer = 0f;
        
            while (timer < fadeDuration)
            {
                // Use unscaledDeltaTime so the fade works even if we pause the game logic later
                timer += Time.unscaledDeltaTime; 
                if (deathScreenCanvasGroup != null)
                {
                    deathScreenCanvasGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
                }
                yield return null;
            }

            if (deathScreenCanvasGroup != null) deathScreenCanvasGroup.alpha = 1f;

            // Pause the game physics/logic after the fade completes
            Time.timeScale = 0f; 
        }

        private void HandleDeathInput()
        {
            if (Keyboard.current == null) return;

            // [R] to Restart
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }
        
            // [E] to Exit
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                ExitGame();
            }
        }

        public void RestartGame()
        {
            Time.timeScale = 1f; // Unpause time before reloading
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ExitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}