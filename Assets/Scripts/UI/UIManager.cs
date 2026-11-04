using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// Main menu, pause menu, game over screen and the in-game HUD (speed, fuel, lives, score, high
    /// score). Panels are resolved by path and driven purely by GameManager events.
    ///
    /// This class dispatches facts and never eases: it tells a meter what fraction to show and a screen
    /// to appear, and the components it names own the motion. That division is what keeps a tween alive
    /// while the panel it is writing to is on its way off the frame.
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
        [SerializeField] string speedBarPath = "HUD/SpeedPanel/SpeedBar/Fill";
        [SerializeField] string fuelMeterPath = "HUD/FuelPanel/Track/Fill";
        [SerializeField] string livesPath = "HUD/LifePanel/Lives";
        [SerializeField] string damageFlashPath = "HUD/DamageFlash";

        [Header("Menu")]
        [SerializeField] string menuBestPath = "Menu/BestChip/BestValueBlock/BestValue";
        [SerializeField] string startButtonPath = "Menu/StartButton";
        [SerializeField] string quitButtonPath = "Menu/QuitButton";

        [Header("Pause")]
        [SerializeField] string resumeButtonPath = "Pause/Panel/ResumeButton";
        [SerializeField] string pauseRestartButtonPath = "Pause/Panel/RestartButton";
        [SerializeField] string pauseMenuButtonPath = "Pause/Panel/MenuButton";
        [SerializeField] string pauseScorePath = "Pause/Panel/Stakes/ScoreValueBlock/ScoreValue";
        [SerializeField] string pauseBestPath = "Pause/Panel/Stakes/BestValueBlock/BestValue";

        [Header("Game over")]
        [SerializeField] string overScorePath = "GameOver/Panel/Result/ScoreValueBlock/ScoreValue";
        [SerializeField] string overBestPath = "GameOver/Panel/Result/BestValueBlock/BestValue";
        [SerializeField] string overReasonPath = "GameOver/Panel/ReasonValue";
        [SerializeField] string overRecordPath = "GameOver/Panel/RecordChip";
        [SerializeField] string overRestartButtonPath = "GameOver/Panel/RestartButton";
        [SerializeField] string overMenuButtonPath = "GameOver/Panel/MenuButton";

        [Tooltip("How hard a hit lights the screen. The rate it fades at lives on the flash itself.")]
        [SerializeField] float damageImpulse = 0.55f;

        GameObject hud;
        GameObject menu;
        GameObject pause;
        GameObject gameOver;

        TMP_Text speedText;
        TMP_Text bestText;
        TMP_Text menuBestText;
        TMP_Text pauseScoreText;
        TMP_Text pauseBestText;
        TMP_Text overBestText;
        TMP_Text overReasonText;

        ReadoutText scoreReadout;
        ReadoutText overScoreReadout;

        MeterBar speedBar;
        MeterBar fuelMeter;
        HeartMeter lives;
        ScreenFlash damageFlash;
        GameObject overRecord;

        Button startButton;
        Button quitButton;
        Button resumeButton;
        Button restartButton;
        Button toMenuButton;
        Button overRestartButton;
        Button overMenuButton;

        int writtenScore = -1;

        // The panel's default button, re-asserted until the EventSystem accepts a selection.
        GameObject defaultSelection;

        void Start()
        {
            ResolvePanels();
            ResolveWidgets();
            BindButtons();

            // Every screen is built active so that all four are visible in the editor. They go off before
            // the first state is applied, otherwise the scene opens with three screens fading out at once.
            HideAll();

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StateChanged += ApplyState;
                gm.LivesChanged += _ => RefreshHud();
                gm.FuelChanged += _ => RefreshHud();
                gm.PlayerHit += _ => damageFlash?.Punch(damageImpulse);
                gm.RunStarted += HandleRunStarted;
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
                gm.RunStarted -= HandleRunStarted;
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

        /// <summary>
        /// A new run starts clean. The flash is cut rather than left to decay, and the score is snapped
        /// rather than rolled, so the first point of this run is not counted up from the last one.
        /// </summary>
        void HandleRunStarted()
        {
            damageFlash?.Clear();
            writtenScore = -1;
            RefreshHud();
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
            bestText = FindText(bestPath);
            menuBestText = FindText(menuBestPath);
            pauseScoreText = FindText(pauseScorePath);
            pauseBestText = FindText(pauseBestPath);
            overBestText = FindText(overBestPath);
            overReasonText = FindText(overReasonPath);

            scoreReadout = FindComponent<ReadoutText>(scorePath);
            overScoreReadout = FindComponent<ReadoutText>(overScorePath);

            speedBar = FindComponent<MeterBar>(speedBarPath);
            fuelMeter = FindComponent<MeterBar>(fuelMeterPath);
            lives = FindComponent<HeartMeter>(livesPath);
            damageFlash = FindComponent<ScreenFlash>(damageFlashPath);

            overRecord = FindObject(overRecordPath);

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
            // The readouts go away with the run, not just with the menu. A pause card that quotes the
            // score next to a live score readout is two numbers arguing about the same thing.
            Show(hud, state == GameState.Playing);
            Show(menu, state == GameState.Menu);
            Show(pause, state == GameState.Paused);
            Show(gameOver, state == GameState.GameOver);

            switch (state)
            {
                case GameState.Menu:
                    Select(startButton != null ? startButton.gameObject : null);
                    if (menuBestText != null)
                    {
                        menuBestText.text = GameManager.Instance.HighScore.ToString("N0");
                    }

                    break;
                case GameState.Playing:
                    Select(null);
                    break;
                case GameState.Paused:
                    Select(resumeButton != null ? resumeButton.gameObject : null);
                    RefreshStakes();
                    break;
                case GameState.GameOver:
                    Select(overRestartButton != null ? overRestartButton.gameObject : null);
                    ShowGameOver();
                    break;
            }

            RefreshHud();
        }

        /// <summary>
        /// The score of a run that just ended is worth counting up, which is why it is written here and
        /// not in <c>RefreshHud</c>: that one runs every frame and would cut the roll short.
        /// </summary>
        void ShowGameOver()
        {
            GameManager gm = GameManager.Instance;

            if (overScoreReadout != null)
            {
                overScoreReadout.SetValue(0f);
                overScoreReadout.RollTo(gm.Score);
            }

            if (overBestText != null)
            {
                overBestText.text = gm.HighScore.ToString("N0");
            }

            if (overReasonText != null)
            {
                overReasonText.text = gm.Reason == GameOverReason.OutOfFuel
                    ? "The tank ran dry."
                    : "The car is wrecked.";
            }

            if (overRecord != null)
            {
                overRecord.SetActive(gm.NewRecord);
            }
        }

        /// <summary>What a paused player is playing for, read out on the pause card.</summary>
        void RefreshStakes()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            if (pauseScoreText != null)
            {
                pauseScoreText.text = gm.Score.ToString("N0");
            }

            if (pauseBestText != null)
            {
                pauseBestText.text = gm.HighScore.ToString("N0");
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

            if (speedBar != null)
            {
                speedBar.SetTarget(gm.Speed01);
            }

            // Only on a change: the readout owns its own motion, and being told the same number every
            // frame would leave it nothing to roll between.
            if (scoreReadout != null && gm.Score != writtenScore)
            {
                writtenScore = gm.Score;
                scoreReadout.SetValue(writtenScore);
            }

            if (bestText != null)
            {
                bestText.text = $"Best {gm.HighScore:N0}";
            }

            if (fuelMeter != null)
            {
                fuelMeter.SetTarget(gm.Fuel01);
            }

            if (lives != null)
            {
                lives.SetLives(gm.Lives);
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

        void HideAll()
        {
            StartHidden(hud);
            StartHidden(menu);
            StartHidden(pause);
            StartHidden(gameOver);
        }

        /// <summary>
        /// A screen arrives through its transition, or simply switches if the scene was built without
        /// one: the HUD still works, it just has nothing to animate.
        /// </summary>
        static void Show(GameObject panel, bool value)
        {
            PanelTransition fx = Transition(panel);

            if (fx != null)
            {
                fx.SetVisible(value);
            }
            else if (panel != null)
            {
                panel.SetActive(value);
            }
        }

        static void StartHidden(GameObject panel)
        {
            PanelTransition fx = Transition(panel);

            if (fx != null)
            {
                fx.StartHidden();
            }
            else if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        static PanelTransition Transition(GameObject panel) =>
            panel != null ? panel.GetComponent<PanelTransition>() : null;

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
