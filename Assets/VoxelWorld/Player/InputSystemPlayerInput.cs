#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelWorld.Player
{
    /// <summary>
    /// Input built entirely in code (no .inputactions asset to drift out of sync):
    /// a "Gameplay" map disabled while paused and a "System" map that stays live.
    /// </summary>
    public sealed class InputSystemPlayerInput : IPlayerInput
    {
        private readonly InputActionAsset _asset = new InputActionAsset();
        private readonly InputActionMap _gameplay;
        private readonly InputActionMap _system;

        private readonly InputAction _move;
        private readonly InputAction _look;
        private readonly InputAction _jump;
        private readonly InputAction _sprint;
        private readonly InputAction _mine;
        private readonly InputAction _place;
        private readonly InputAction _scroll;
        private int _scrollSteps;

        public event Action PlacePressed;
        public event Action<int> SlotSelected;
        public event Action PausePressed;
        public event Action SaveRequested;
        public event Action LoadRequested;

        public InputSystemPlayerInput()
        {
            _gameplay = _asset.AddActionMap("Gameplay");
            _system = _asset.AddActionMap("System");

            _move = _gameplay.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _look = _gameplay.AddAction("Look", InputActionType.Value);
            _look.AddBinding("<Mouse>/delta");

            _jump = _gameplay.AddAction("Jump", InputActionType.Button);
            _jump.AddBinding("<Keyboard>/space");

            _sprint = _gameplay.AddAction("Sprint", InputActionType.Button);
            _sprint.AddBinding("<Keyboard>/leftShift");

            _mine = _gameplay.AddAction("Mine", InputActionType.Button);
            _mine.AddBinding("<Mouse>/leftButton");

            _place = _gameplay.AddAction("Place", InputActionType.Button);
            _place.AddBinding("<Mouse>/rightButton");
            _place.performed += _ => PlacePressed?.Invoke();

            _scroll = _gameplay.AddAction("Scroll", InputActionType.Value);
            _scroll.AddBinding("<Mouse>/scroll");
            _scroll.performed += ctx => _scrollSteps += Mathf.RoundToInt(ctx.ReadValue<Vector2>().y);

            for (var i = 0; i < 4; i++)
            {
                var slot = i;
                var action = _gameplay.AddAction($"Slot{i + 1}", InputActionType.Button);
                action.AddBinding($"<Keyboard>/digit{i + 1}");
                action.performed += _ => SlotSelected?.Invoke(slot);
            }

            var pause = _system.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.performed += _ => PausePressed?.Invoke();

            var save = _system.AddAction("Save", InputActionType.Button);
            save.AddBinding("<Keyboard>/f5");
            save.performed += _ => SaveRequested?.Invoke();

            var load = _system.AddAction("Load", InputActionType.Button);
            load.AddBinding("<Keyboard>/f9");
            load.performed += _ => LoadRequested?.Invoke();

            SetGameplayEnabled(true);
            _system.Enable();
        }

        public Vector2 MoveAxis => _move.ReadValue<Vector2>();
        public Vector2 LookDelta => _look.ReadValue<Vector2>();
        public bool IsSprintHeld => _sprint.IsPressed();
        public bool IsJumpHeld => _jump.IsPressed();
        public bool IsMineHeld => _mine.IsPressed();

        public int ConsumeScrollSteps()
        {
            var steps = _scrollSteps;
            _scrollSteps = 0;
            return steps;
        }

        public void Pump()
        {
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled)
            {
                _gameplay.Enable();
            }
            else
            {
                _gameplay.Disable();
            }
        }

        public void Dispose()
{
    // InputActionAsset is a ScriptableObject and does not implement
    // IDisposable; release it through the Unity object lifetime instead.
    if (Application.isPlaying)
    {
        UnityEngine.Object.Destroy(_asset);
    }
    else
    {
        UnityEngine.Object.DestroyImmediate(_asset);
    }
}
    }
}
#endif
