using System;
using UnityEngine;

namespace HW2
{
    public enum GameState { Menu, Playing, Paused, GameOver }

    public enum GameOverReason { None, OutOfLives, OutOfFuel }

    /// <summary>
    /// Singleton game manager: owns the state machine (Menu / Playing / Paused / GameOver),
    /// the run session data (score, lives, fuel, distance, speed) and the high score record.
    /// </summary>
    public sealed class GameManager : Singleton<GameManager>
    {
        public const string HighScorePrefKey = "HW2.HighScore";

        [SerializeField] string highScoreKey = HighScorePrefKey;

        GameSettings settings;

        public event Action<GameState> StateChanged;
        public event Action RunStarted;
        public event Action<GameOverReason> RunEnded;
        public event Action<int> LivesChanged;
        public event Action<float> FuelChanged;
        public event Action<Obstacle> PlayerHit;

        float bonusScore;

        public GameState State { get; private set; } = GameState.Menu;
        public bool IsPlaying => State == GameState.Playing;
        public int Lives { get; private set; }
        public float Fuel { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public int HighScore { get; private set; }
        public GameOverReason Reason { get; private set; }
        public bool NewRecord { get; private set; }
        public int Score => Mathf.FloorToInt(Distance * settings.scorePerMeter) + Mathf.FloorToInt(bonusScore);
        public float Difficulty => Mathf.Clamp01(Distance / settings.difficultyRampDistance);
        public float Speed01 => Mathf.InverseLerp(settings.baseSpeed, settings.maxSpeed, Speed);
        public float Fuel01 => settings.fuelMax > 0f ? Fuel / settings.fuelMax : 0f;

        protected override void Awake()
        {
            base.Awake();

            settings = GameSettings.Instance;
            HighScore = PlayerPrefs.GetInt(highScoreKey, 0);
            Lives = settings.maxLives;
            Fuel = settings.fuelMax;
            Speed = settings.baseSpeed;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            float dt = Time.deltaTime;

            Speed = Mathf.MoveTowards(Speed, settings.TargetSpeed(Distance), settings.speedAcceleration * dt);
            Distance += Speed * dt;

            float burn = settings.fuelBurnPerSecond + settings.fuelBurnSpeedCoefficient * Speed;
            Fuel = Mathf.Max(0f, Fuel - burn * dt);
            FuelChanged?.Invoke(Fuel);

            if (Fuel <= 0f)
            {
                EndRun(GameOverReason.OutOfFuel);
            }
        }

        public void StartRun()
        {
            Distance = 0f;
            bonusScore = 0f;
            Speed = settings.baseSpeed;
            Fuel = settings.fuelMax;
            Lives = settings.maxLives;
            Reason = GameOverReason.None;
            NewRecord = false;
            Time.timeScale = 1f;

            LivesChanged?.Invoke(Lives);
            FuelChanged?.Invoke(Fuel);

            SetState(GameState.Playing);
            RunStarted?.Invoke();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                Time.timeScale = 0f;
                SetState(GameState.Paused);
            }
            else if (State == GameState.Paused)
            {
                Resume();
            }
        }

        public void Resume()
        {
            if (State != GameState.Paused)
            {
                return;
            }

            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SetState(GameState.Menu);
        }

        public void AddScore(int points)
        {
            if (!IsPlaying)
            {
                return;
            }

            bonusScore += points;
        }

        /// <summary>Collisions bleed off speed, which the difficulty ramp then recovers from.</summary>
        public void DipSpeed(float amount)
        {
            if (!IsPlaying)
            {
                return;
            }

            Speed = Mathf.Max(settings.baseSpeed * 0.6f, Speed - amount);
        }

        public void Refuel(float amount)
        {
            if (!IsPlaying)
            {
                return;
            }

            Fuel = Mathf.Clamp(Fuel + amount, 0f, settings.fuelMax);
            FuelChanged?.Invoke(Fuel);
        }

        public void RegisterHit(Obstacle obstacle)
        {
            if (!IsPlaying)
            {
                return;
            }

            Lives = Mathf.Max(0, Lives - 1);
            LivesChanged?.Invoke(Lives);
            PlayerHit?.Invoke(obstacle);

            if (Lives <= 0)
            {
                EndRun(GameOverReason.OutOfLives);
            }
        }

        void EndRun(GameOverReason reason)
        {
            if (State == GameState.GameOver)
            {
                return;
            }

            Reason = reason;
            SaveHighScore();
            SetState(GameState.GameOver);
            RunEnded?.Invoke(reason);
        }

        void SaveHighScore()
        {
            if (Score <= HighScore)
            {
                return;
            }

            HighScore = Score;
            NewRecord = true;
            PlayerPrefs.SetInt(highScoreKey, HighScore);
            PlayerPrefs.Save();
        }

        void SetState(GameState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
