using System;
using System.Collections.Generic;
using VoxelWorld.Core.Blocks;

namespace VoxelWorld.Core.Inventory
{
    /// <summary>Player cube storage: mined cubes accumulate here and are consumed by placement.</summary>
    public interface IInventory
    {
        int GetCount(BlockKind kind);

        void Add(BlockKind kind, int amount = 1);

        /// <summary>Removes cubes if enough are stored; returns false when short.</summary>
        bool TryConsume(BlockKind kind, int amount = 1);

        /// <summary>Fired after every add/consume with the new absolute count.</summary>
        event Action<BlockKind, int> CountChanged;

        /// <summary>Current counts, used for serialization.</summary>
        IReadOnlyDictionary<BlockKind, int> Counts { get; }

        /// <summary>Replaces all counts (used when loading a save).</summary>
        void Restore(IReadOnlyDictionary<BlockKind, int> counts);
    }
}
