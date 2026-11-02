using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HW2
{
    /// <summary>
    /// Single owner of the generated Input System asset (Assets/Resources/GameInput.inputactions).
    /// Everything else - player, UI, menus - talks to this class instead of touching devices
    /// directly, which keeps the whole game on the new Input System.
    /// </summary>
    public sealed class InputManager : Singleton<InputManager>
    {
        GameInput actions;

        public event Action PausePressed;
        public event Action RestartPressed;

        /// <summary>Shared asset instance, also handed to the EventSystem UI input module.</summary>
        public InputActionAsset Asset => actions != null ? actions.asset : null;

        public float Steer => actions != null ? actions.Gameplay.Steer.ReadValue<float>() : 0f;

        protected override void Awake()
        {
            base.Awake();

            actions = new GameInput();
            actions.Gameplay.Pause.performed += OnPause;
            actions.Gameplay.Restart.performed += OnRestart;
        }

        void OnEnable()
        {
            actions?.Gameplay.Enable();
        }

        void OnDisable()
        {
            actions?.Gameplay.Disable();
        }

        protected override void OnDestroy()
        {
            if (actions != null)
            {
                actions.Gameplay.Pause.performed -= OnPause;
                actions.Gameplay.Restart.performed -= OnRestart;
                actions.Dispose();
                actions = null;
            }

            base.OnDestroy();
        }

        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();

        void OnRestart(InputAction.CallbackContext _) => RestartPressed?.Invoke();
    }
}
