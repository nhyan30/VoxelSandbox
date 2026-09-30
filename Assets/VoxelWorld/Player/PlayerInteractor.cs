using System;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Editing;
using VoxelWorld.Core.Inventory;
using VoxelWorld.Core.World;

namespace VoxelWorld.Player
{
    /// <summary>
    /// Aiming, mining and placement:
    /// - mining is hold-to-break with a per-block-type duration and resets when the
    ///   target changes or the button is released;
    /// - placement is a single press, always snapped to the world grid, blocked when
    ///   the cell is occupied, above the build ceiling, or would intersect the player;
    /// - a ghost preview visualizes the target cell every frame.
    /// The class is a plain object driven by the composition root (no Update of its own).
    /// </summary>
    public sealed class PlayerInteractor
    {
        private readonly Transform _head;
        private readonly IWorldGrid _grid;
        private readonly IWorldEditor _editor;
        private readonly IInventory _inventory;
        private readonly BlockRegistry _registry;
        private readonly PlacementGhost _ghost;
        private readonly int _blockMask;
        private readonly int _overlapMask;
        private readonly float _reach;

        private readonly string _bottomMessage;
        private readonly string _ceilingMessage;

        private int _selectedSlot;
        private float _mineProgress;
        private Vector3Int _mineTarget;
        private BlockKind _mineTargetKind = BlockKind.Air;
        private float _lastNoticeTime;
        private string _lastNoticeKey = "";
        private bool _paused = true;

        /// <summary>Raised whenever mining progress changes: (normalized progress, bar visible).</summary>
        public event Action<float, bool> MiningProgressChanged;

        /// <summary>Raised when the selected hotbar slot changes.</summary>
        public event Action<int> SelectionChanged;

        /// <summary>Raised for throttled player-facing hints and warnings.</summary>
        public event Action<string> NoticePosted;

        public int SelectedSlot => _selectedSlot;

        public PlayerInteractor(Transform head, IWorldGrid grid, IWorldEditor editor, IInventory inventory,
            BlockRegistry registry, PlacementGhost ghost, float reach, int blockLayer, int playerLayer,
            string bottomMessage, string ceilingMessage)
        {
            _head = head;
            _grid = grid;
            _editor = editor;
            _inventory = inventory;
            _registry = registry;
            _ghost = ghost;
            _reach = reach;
            _blockMask = 1 << Mathf.Max(0, blockLayer);
            _overlapMask = (1 << Mathf.Max(0, blockLayer)) | (1 << Mathf.Max(0, playerLayer));
            _bottomMessage = bottomMessage;
            _ceilingMessage = ceilingMessage;
        }

        /// <summary>Freezes interaction while the game is paused (hides ghost + progress).</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                ResetMining();
                _ghost.Hide();
            }
        }

        /// <summary>Selects a hotbar slot (clamped).</summary>
        public void SelectSlot(int slot)
        {
            var count = _registry.PlaceableKinds.Count;
            if (count == 0)
            {
                return;
            }

            slot = Mathf.Clamp(slot, 0, count - 1);
            if (slot == _selectedSlot)
            {
                return;
            }

            _selectedSlot = slot;
            ResetMining();
            SelectionChanged?.Invoke(slot);
        }

        /// <summary>Cycles the hotbar by wheel steps (wraps around).</summary>
        public void Scroll(int steps)
        {
            var count = _registry.PlaceableKinds.Count;
            if (count == 0 || steps == 0)
            {
                return;
            }

            var slot = ((_selectedSlot + steps) % count + count) % count;
            _selectedSlot = slot;
            ResetMining();
            SelectionChanged?.Invoke(slot);
        }

        /// <summary>Per-frame aim + mining update.</summary>
        public void Step(float dt, bool mineHeld)
        {
            if (_paused)
            {
                return;
            }

            if (!TryAcquireTarget(out var voxel, out var placePos, out var hitKind))
            {
                ResetMining();
                _ghost.Hide();
                return;
            }

            UpdateGhost(placePos);
            UpdateMining(dt, mineHeld, voxel, hitKind);
        }

        /// <summary>Single-press placement of the selected cube type.</summary>
        public void TryPlace()
        {
            if (_paused || !TryAcquireTarget(out _, out var placePos, out _))
            {
                return;
            }

            var kind = _registry.PlaceableKinds[_selectedSlot];
            if (placePos.y >= _grid.Size.y)
            {
                Notice("ceiling", _ceilingMessage);
                return;
            }

            if (!_grid.InBuildBounds(placePos))
            {
                Notice("bounds", "That spot is outside the world.");
                return;
            }

            if (_grid.GetBlock(placePos) != BlockKind.Air)
            {
                Notice("occupied", "That space is already occupied.");
                return;
            }

            if (_inventory.GetCount(kind) <= 0)
            {
                Notice("empty", $"No {_registry.Get(kind).DisplayName} cubes in your inventory. Mine some first!");
                return;
            }

            if (OverlapsSolids(placePos))
            {
                return; // silently blocked: the cell intersects the player or a cube
            }

            if (_editor.PlaceBlock(placePos, kind) == WorldEditResult.Ok)
            {
                _inventory.TryConsume(kind);
            }
        }

        private bool TryAcquireTarget(out Vector3Int voxel, out Vector3Int placePos, out BlockKind hitKind)
        {
            voxel = default;
            placePos = default;
            hitKind = BlockKind.Air;

            if (!Physics.Raycast(_head.position, _head.forward, out var hit, _reach,
                    _blockMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            voxel = VoxelInward(hit);
            placePos = VoxelOutward(hit);
            hitKind = _grid.GetBlock(voxel);
            return true;
        }

        /// <summary>The solid voxel being looked at: half a unit from the hit face into the cube.</summary>
        private static Vector3Int VoxelInward(RaycastHit hit)
        {
            var p = hit.point - hit.normal * (0.5f + 0.001f);
            return new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        }

        /// <summary>The empty voxel where a new cube would go: half a unit out of the hit face.</summary>
        private static Vector3Int VoxelOutward(RaycastHit hit)
        {
            var p = hit.point + hit.normal * (0.5f + 0.001f);
            return new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        }

        private void UpdateMining(float dt, bool mineHeld, Vector3Int voxel, BlockKind kind)
        {
            if (!mineHeld)
            {
                ResetMining();
                return;
            }

            var definition = _registry.Get(kind);
            if (kind == BlockKind.Air || definition.IsUnbreakable)
            {
                ResetMining();
                if (kind == BlockKind.Bedrock && voxel.y == 0)
                {
                    Notice("bedrock", _bottomMessage);
                }
                return;
            }

            if (voxel != _mineTarget || kind != _mineTargetKind)
            {
                _mineTarget = voxel;
                _mineTargetKind = kind;
                _mineProgress = 0f;
            }

            _mineProgress += dt / definition.MineSeconds;
            MiningProgressChanged?.Invoke(Mathf.Clamp01(_mineProgress), true);

            if (_mineProgress >= 1f)
            {
                if (_editor.RemoveBlock(voxel) == WorldEditResult.Ok)
                {
                    _inventory.Add(kind); // mined cubes go straight into the inventory
                }

                ResetMining();
            }
        }

        private void ResetMining()
        {
            if (_mineProgress > 0f)
            {
                MiningProgressChanged?.Invoke(0f, false);
            }

            _mineProgress = 0f;
            _mineTargetKind = BlockKind.Air;
        }

        private void UpdateGhost(Vector3Int placePos)
        {
            var kind = _registry.PlaceableKinds[_selectedSlot];
            var valid = _grid.InBuildBounds(placePos)
                        && placePos.y < _grid.Size.y
                        && _grid.GetBlock(placePos) == BlockKind.Air
                        && _inventory.GetCount(kind) > 0
                        && !OverlapsSolids(placePos);
            _ghost.Show(placePos, valid);
        }

        private bool OverlapsSolids(Vector3Int pos)
        {
            var center = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
            var overlaps = Physics.OverlapBox(center, Vector3.one * 0.499f, Quaternion.identity,
                _overlapMask, QueryTriggerInteraction.Ignore);
            return overlaps.Length > 0;
        }

        private void Notice(string key, string message)
        {
            if (key == _lastNoticeKey && Time.time - _lastNoticeTime < 2f)
            {
                return;
            }

            _lastNoticeKey = key;
            _lastNoticeTime = Time.time;
            NoticePosted?.Invoke(message);
        }
    }
}
