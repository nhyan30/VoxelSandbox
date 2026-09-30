using System;
using UnityEngine;

namespace VoxelWorld.Player
{
    /// <summary>
    /// Frame input for one player. Implementations exist for the new Input System
    /// (<see cref="InputSystemPlayerInput"/>) and as a legacy fallback
    /// (<see cref="LegacyPlayerInput"/>) so the project runs in any input configuration.
    /// </summary>
    public interface IPlayerInput : IDisposable
    {
        Vector2 MoveAxis { get; }
        Vector2 LookDelta { get; }
        bool IsSprintHeld { get; }
        bool IsJumpHeld { get; }
        bool IsMineHeld { get; }

        event Action PlacePressed;

        /// <summary>Hotbar slot pressed (0-based).</summary>
        event Action<int> SlotSelected;

        event Action PausePressed;
        event Action SaveRequested;
        event Action LoadRequested;

        /// <summary>Mouse-wheel notches accumulated since the last call (positive = up).</summary>
        int ConsumeScrollSteps();

        /// <summary>
        /// Flushes poll-based providers once per frame (call before reading state).
        /// Event-based implementations may leave this empty.
        /// </summary>
        void Pump();

        /// <summary>Disables gameplay actions while paused; menu/system actions stay live.</summary>
        void SetGameplayEnabled(bool enabled);
    }
}
