using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelWorld.Core.Blocks;

namespace VoxelWorld.Core.World
{
    /// <summary>
    /// Read/write access to the voxel data of the whole world. Implementations own
    /// chunk storage and generation, expose bounds, and notify listeners about edits.
    /// </summary>
    public interface IWorldGrid
    {
        /// <summary>World extent in blocks: (sizeX, height, sizeZ).</summary>
        Vector3Int Size { get; }

        /// <summary>Seed the terrain generator currently runs with.</summary>
        int Seed { get; }

        /// <summary>True when the position lies inside the world volume [0..Size).</summary>
        bool InBounds(Vector3Int pos);

        /// <summary>
        /// True when a cube may be placed at the position: inside bounds and at or
        /// below the build ceiling. Occupancy is checked separately.
        /// </summary>
        bool InBuildBounds(Vector3Int pos);

        /// <summary>
        /// Block kind at the position. Below the world (y &lt; 0) behaves as solid
        /// bedrock; outside horizontal bounds and above the ceiling return air.
        /// Accessing a not-yet-generated chunk generates it on demand.
        /// </summary>
        BlockKind GetBlock(Vector3Int pos);

        /// <summary>
        /// Writes a block, records a delta for the save system and fires
        /// <see cref="BlockChanged"/>. Returns false when out of bounds or unchanged.
        /// </summary>
        bool TrySetBlock(Vector3Int pos, BlockKind kind);

        /// <summary>Raised after every successful write: (position, oldKind, newKind).</summary>
        event Action<Vector3Int, BlockKind, BlockKind> BlockChanged;

        /// <summary>Every edit made on top of the generated terrain, keyed by position.</summary>
        IReadOnlyDictionary<Vector3Int, BlockKind> Deltas { get; }

        /// <summary>Discards all chunk data and deltas, then re-seeds terrain generation.</summary>
        void Reset(int seed);

        /// <summary>Silently writes a delta (used when loading a save; no events fired).</summary>
        void ApplyDelta(Vector3Int pos, BlockKind kind);

        /// <summary>Generated surface height (topmost solid y) of a column, clamped to bounds.</summary>
        int SurfaceHeightAt(int x, int z);
    }
}
