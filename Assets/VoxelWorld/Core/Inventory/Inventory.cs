using System;
using System.Collections.Generic;
using VoxelWorld.Core.Blocks;

namespace VoxelWorld.Core.Inventory
{
    /// <summary>
    /// Plain C# inventory (no Unity lifetime) restricted to player-placeable block
    /// kinds; bedrock can never enter it. Counts are clamped at zero.
    /// </summary>
    public sealed class Inventory : IInventory
    {
        private readonly Dictionary<BlockKind, int> _counts = new Dictionary<BlockKind, int>();

        public event Action<BlockKind, int> CountChanged;
        public IReadOnlyDictionary<BlockKind, int> Counts => _counts;

        public Inventory(BlockRegistry registry)
        {
            foreach (var kind in registry.PlaceableKinds)
            {
                _counts[kind] = 0;
            }
        }

        public int GetCount(BlockKind kind)
        {
            return _counts.TryGetValue(kind, out var count) ? count : 0;
        }

        public void Add(BlockKind kind, int amount = 1)
        {
            if (amount <= 0 || !_counts.ContainsKey(kind))
            {
                return; // non-placeable kinds (e.g. bedrock) cannot be stored
            }

            _counts[kind] = GetCount(kind) + amount;
            CountChanged?.Invoke(kind, _counts[kind]);
        }

        public bool TryConsume(BlockKind kind, int amount = 1)
        {
            var current = GetCount(kind);
            if (amount <= 0 || current < amount)
            {
                return false;
            }

            _counts[kind] = current - amount;
            CountChanged?.Invoke(kind, _counts[kind]);
            return true;
        }

        public void Restore(IReadOnlyDictionary<BlockKind, int> counts)
        {
            foreach (var pair in counts)
            {
                if (!_counts.ContainsKey(pair.Key))
                {
                    continue;
                }

                _counts[pair.Key] = pair.Value;
                CountChanged?.Invoke(pair.Key, pair.Value);
            }
        }
    }
}
