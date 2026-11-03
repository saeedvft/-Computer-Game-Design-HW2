using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// Main menu, pause menu, game over screen and the in-game HUD (speed, fuel bar, lives,
    /// score, high score). Panels are resolved by path and driven purely by GameManager events.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] string hudPath = "HUD";
        [SerializeField] string menuPath = "Menu";
        [SerializeField] string pausePath = "Pause";
        [SerializeField] string gameOverPath = "GameOver";

        [Header("HUD readouts")]
        [SerializeField] string speedPath = "HUD/SpeedPanel/SpeedValue";
        [SerializeField] string scorePath = "HUD/ScorePanel/ScoreValue";
        [SerializeField] string bestPath = "HUD/ScorePanel/BestValue";
        [SerializeField] string fuelFillPath = "HUD/FuelPanel/Track/Fill";
        [SerializeField] string livesPath = "HUD/LifePanel/Lives";
        [SerializeField] string damageFlashPath = "HUD/DamageFlash";

        [Header("Menu")]
        [SerializeField] string menuBestPath = "Menu/Panel/BestValue";
        [SerializeField] string startButtonPath = "Menu/Panel/StartButton";
        [SerializeField] string quitButtonPath = "Menu/Panel/QuitButton";

        [Header("Pause")]
        [SerializeField] string resumeButtonPath = "Pause/Panel/ResumeButton";
        [SerializeField] string pauseRestartButtonPath = "Pause/Panel/RestartButton";
        [SerializeField] string pauseMenuButtonPath = "Pause/Panel/MenuButton";

        [Header("Game over")]
        [SerializeField] string overScorePath = "GameOver/Panel/ScoreValue";
        [SerializeField] string overBestPath = "GameOver/Panel/BestValue";
        [SerializeField] string overReasonPath = "GameOver/Panel/ReasonValue";
        [SerializeField] string overRecordPath = "GameOver/Panel/NewRecord";
        [SerializeField] string overRestartButtonPath = "GameOver/Panel/RestartButton";
        [SerializeField] string overMenuButtonPath = "GameOver/Panel/MenuButton";

        [SerializeField] Color damageFlashColor = new Color(1f, 0.2f, 0.16f, 1f);
        [SerializeField] Color lowFuelColor = new Color(0.95f, 0.28f, 0.2f, 1f);
        [SerializeField] Color highFuelColor = new Color(0.25f, 0.95f, 0.65f, 1f);

        GameObject hud;
        GameObject menu;
        GameObject pause;
        GameObject gameOver;

        TMP_Text speedText;
        TMP_Text scoreText;
        TMP_Text bestText;
        TMP_Text menuBestText;
        TMP_Text overScoreText;
        TMP_Text overBestText;
        TMP_Text overReasonText;
        TMP_Text overRecordText;

        Image fuelFill;
        Image damageFlash;
        Transform livesRoot;

        Button startButton;
        Button quitButton;
        Button resumeButton;
        Button restartButton;
        Button toMenuButton;
        Button overRestartButton;
        Button overMenuButton;

        float damageFlashAmount;

        // The panel's default button, re-asserted until the EventSystem accepts a selection.
        GameObject defaultSelection;

        void Start()
        {
            ResolvePanels();
            ResolveWidgets();
            BindButtons();

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StateChanged += ApplyState;
                gm.LivesChanged += _ => RefreshHud();
                gm.FuelChanged += _ => RefreshHud();
                gm.PlayerHit += _ => damageFlashAmount = 0.55f;
                gm.RunStarted += () => damageFlashAmount = 0f;
                ApplyState(gm.State);
            }

            InputManager input = InputManager.Instance;
            if (input != null)
            {
                input.PausePressed += HandlePause;
                input.RestartPressed += HandleRestart;
            }

            RefreshHud();
        }

        void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StateChanged -= ApplyState;
            }

            InputManager input = InputManager.Instance;
            if (input != null)
            {
                input.PausePressed -= HandlePause;
                input.RestartPressed -= HandleRestart;
            }
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            if (gm.IsPlaying)
            {
                RefreshHud();
            }

            if (damageFlashAmount > 0f)
            {
                damageFlashAmount = Mathf.Max(0f, damageFlashAmount - Time.unscaledDeltaTime * 1.6f);
            }

            if (damageFlash != null)
            {
                Color flash = damageFlashColor;
                flash.a = damageFlashAmount;
                damageFlash.color = flash;
            }

            EnsureSelection();
        }

        /// <summary>
        /// The EventSystem only accepts a selection once its input module is running, which can be
        /// a frame after the panel appeared. Until then keep pointing at the panel's default button
        /// so keyboard and gamepad navigation always have somewhere to start.
        /// </summary>
        void EnsureSelection()
        {
            EventSystem system = EventSystem.current;
            if (system == null || defaultSelection == null || !defaultSelection.activeInHierarchy)
            {
                return;
            }

            if (system.currentSelectedGameObject == null)
            {
                system.SetSelectedGameObject(defaultSelection);
            }
        }

        void HandlePause()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            if (gm.State == GameState.Playing || gm.State == GameState.Paused)
            {
                AudioManager.Instance.Play(Sfx.UiClick, 0.5f);
                gm.TogglePause();
            }
        }

        void HandleRestart()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State == GameState.Menu)
            {
                return;
            }

            AudioManager.Instance.Play(Sfx.UiClick, 0.6f);
            gm.StartRun();
        }

        void ResolvePanels()
        {
            hud = FindObject(hudPath);
            menu = FindObject(menuPath);
            pause = FindObject(pausePath);
            gameOver = FindObject(gameOverPath);
        }

        void ResolveWidgets()
        {
            speedText = FindText(speedPath);
            scoreText = FindText(scorePath);
            bestText = FindText(bestPath);
            menuBestText = FindText(menuBestPath);
            overScoreText = FindText(overScorePath);
            overBestText = FindText(overBestPath);
            overReasonText = FindText(overReasonPath);
            overRecordText = FindText(overRecordPath);

            fuelFill = FindComponent<Image>(fuelFillPath);
            damageFlash = FindComponent<Image>(damageFlashPath);
            livesRoot = transform.Find(livesPath);

            startButton = FindComponent<Button>(startButtonPath);
            quitButton = FindComponent<Button>(quitButtonPath);
            resumeButton = FindComponent<Button>(resumeButtonPath);
            restartButton = FindComponent<Button>(pauseRestartButtonPath);
            toMenuButton = FindComponent<Button>(pauseMenuButtonPath);
            overRestartButton = FindComponent<Button>(overRestartButtonPath);
            overMenuButton = FindComponent<Button>(overMenuButtonPath);
        }

        void BindButtons()
        {
            Attach(startButton, StartRun);
            Attach(quitButton, Quit);
            Attach(resumeButton, Resume);
            Attach(restartButton, StartRun);
            Attach(toMenuButton, ReturnToMenu);
            Attach(overRestartButton, StartRun);
            Attach(overMenuButton, ReturnToMenu);
        }

        void Attach(Button button, System.Action action)
        {
            if (button != null)
            {
                button.onClick.AddListener(() => Delegate(action));
            }
        }

        void ApplyState(GameState state)
        {
            SetActive(hud, state != GameState.Menu);
            SetActive(menu, state == GameState.Menu);
            SetActive(pause, state == GameState.Paused);
            SetActive(gameOver, state == GameState.GameOver);

            switch (state)
            {
                case GameState.Menu:
                    Select(startButton != null ? startButton.gameObject : null);
                    if (menuBestText != null)
                    {
                        menuBestText.text = $"Best  {GameManager.Instance.HighScore}";
                    }

                    break;
                case GameState.Playing:
                    Select(null);
                    break;
                case GameState.Paused:
                    Select(resumeButton != null ? resumeButton.gameObject : null);
                    break;
                case GameState.GameOver:
                    Select(overRestartButton != null ? overRestartButton.gameObject : null);
                    ShowGameOver();
                    break;
            }

            RefreshHud();
        }

        void ShowGameOver()
        {
            GameManager gm = GameManager.Instance;

            if (overScoreText != null)
            {
                overScoreText.text = $"Score  {gm.Score}";
            }

            if (overBestText != null)
            {
                overBestText.text = $"Best  {gm.HighScore}";
            }

            if (overReasonText != null)
            {
                overReasonText.text = gm.Reason == GameOverReason.OutOfFuel
                    ? "The tank ran dry."
                    : "The car is wrecked.";
            }

            if (overRecordText != null)
            {
                overRecordText.gameObject.SetActive(gm.NewRecord);
            }
        }

        void RefreshHud()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            if (speedText != null)
            {
                speedText.text = Mathf.RoundToInt(gm.Speed * 3.6f).ToString();
            }

            if (scoreText != null)
            {
                scoreText.text = gm.Score.ToString("N0");
            }

            if (bestText != null)
            {
                bestText.text = $"Best {gm.HighScore:N0}";
            }

            if (fuelFill != null)
            {
                fuelFill.fillAmount = gm.Fuel01;
                fuelFill.color = Color.Lerp(lowFuelColor, highFuelColor, gm.Fuel01);
            }

            if (livesRoot != null)
            {
                for (int i = 0; i < livesRoot.childCount; i++)
                {
                    livesRoot.GetChild(i).gameObject.SetActive(i < gm.Lives);
                }
            }
        }

        void StartRun()
        {
            GameManager.Instance.StartRun();
        }

        void Resume()
        {
            GameManager.Instance.Resume();
        }

        void ReturnToMenu()
        {
            GameManager.Instance.ReturnToMenu();
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Delegate(System.Action action)
        {
            AudioManager.Instance.Play(Sfx.UiClick, 0.6f);
            action?.Invoke();
        }

        void Select(GameObject target)
        {
            defaultSelection = target;

            EventSystem system = EventSystem.current;
            if (system == null)
            {
                return;
            }

            system.firstSelectedGameObject = target;

            // Push the selection through the EventSystem so keyboard navigation highlights the
            // button immediately, even before the module has ticked once.
            system.SetSelectedGameObject(target);
        }

        static void SetActive(GameObject go, bool value)
        {
            if (go != null)
            {
                go.SetActive(value);
            }
        }

        GameObject FindObject(string path)
        {
            Transform found = transform.Find(path);
            if (found == null)
            {
                Debug.LogError($"UI panel '{path}' not found on {name}.", this);
                return null;
            }

            return found.gameObject;
        }

        TMP_Text FindText(string path)
        {
            return FindComponent<TMP_Text>(path);
        }

        /// <summary>A path that does not resolve is a silent HUD outage otherwise, so it is reported here.</summary>
        T FindComponent<T>(string path) where T : Component
        {
            Transform found = transform.Find(path);
            if (found == null)
            {
                Debug.LogError($"UI component '{path}' ({typeof(T).Name}) not found on {name}.", this);
                return null;
            }

            return found.GetComponent<T>();
        }
    }
}
