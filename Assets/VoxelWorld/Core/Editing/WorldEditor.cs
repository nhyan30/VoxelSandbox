using System;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.World;

namespace VoxelWorld.Core.Editing
{
    /// <summary>Why a world edit was rejected (or succeeded).</summary>
    public enum WorldEditResult
    {
        Ok,
        OutOfBounds,
        NothingToMine,
        Unbreakable,
        NotEmpty
    }

    /// <summary>Mining/placement operations the player layer performs against the world.</summary>
    public interface IWorldEditor
    {
        WorldEditResult RemoveBlock(Vector3Int pos);
        WorldEditResult PlaceBlock(Vector3Int pos, BlockKind kind);
    }

    /// <summary>
    /// Pure validation + write layer between player intents and the grid. Keeping it
    /// free of Unity scene types makes the mining/placement rules unit-testable.
    /// </summary>
    public sealed class WorldEditor : IWorldEditor
    {
        private readonly IWorldGrid _grid;
        private readonly BlockRegistry _registry;

        public WorldEditor(IWorldGrid grid, BlockRegistry registry)
        {
            _grid = grid;
            _registry = registry;
        }

        public WorldEditResult RemoveBlock(Vector3Int pos)
        {
            if (!_grid.InBounds(pos))
            {
                return WorldEditResult.OutOfBounds;
            }

            var kind = _grid.GetBlock(pos);
            if (kind == BlockKind.Air)
            {
                return WorldEditResult.NothingToMine;
            }

            if (_registry.Get(kind).IsUnbreakable)
            {
                return WorldEditResult.Unbreakable;
            }

            return _grid.TrySetBlock(pos, BlockKind.Air) ? WorldEditResult.Ok : WorldEditResult.NothingToMine;
        }

        public WorldEditResult PlaceBlock(Vector3Int pos, BlockKind kind)
        {
            if (kind == BlockKind.Air || !_grid.InBuildBounds(pos))
            {
                return WorldEditResult.OutOfBounds;
            }

            return _grid.GetBlock(pos) != BlockKind.Air
                ? WorldEditResult.NotEmpty
                : (_grid.TrySetBlock(pos, kind) ? WorldEditResult.Ok : WorldEditResult.NotEmpty);
        }
    }
}
