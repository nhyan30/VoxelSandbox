#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using UnityEngine;

namespace VoxelWorld.Player
{
    /// <summary>
    /// Fallback used only when the new Input System backend is inactive (e.g. the very
    /// first editor session, before Unity applies the input-handling setting and any
    /// required restart). Keeps the project playable in every configuration.
    /// </summary>
    public sealed class LegacyPlayerInput : IPlayerInput
    {
        private bool _gameplayEnabled = true;
        private float _scrollResidual;

        public event Action PlacePressed;
        public event Action<int> SlotSelected;
        public event Action PausePressed;
        public event Action SaveRequested;
        public event Action LoadRequested;

        public Vector2 MoveAxis => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        public Vector2 LookDelta => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        public bool IsSprintHeld => Input.GetKey(KeyCode.LeftShift);
        public bool IsJumpHeld => Input.GetKey(KeyCode.Space);
        public bool IsMineHeld => Input.GetMouseButton(0);

        public int ConsumeScrollSteps()
        {
            var steps = (int)_scrollResidual; // truncates toward zero
            _scrollResidual -= steps;
            return steps;
        }

        public void Pump()
        {
            if (_gameplayEnabled)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    PlacePressed?.Invoke();
                }

                for (var i = 0; i < 4; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        SlotSelected?.Invoke(i);
                    }
                }

                _scrollResidual += Input.mouseScrollDelta.y;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                PausePressed?.Invoke();
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                SaveRequested?.Invoke();
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                LoadRequested?.Invoke();
            }
        }

        public void SetGameplayEnabled(bool enabled)
        {
            _gameplayEnabled = enabled;
        }

        public void Dispose()
        {
        }
    }
}
#endif
