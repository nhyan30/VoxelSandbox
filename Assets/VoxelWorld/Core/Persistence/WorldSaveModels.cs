using System;
using System.Collections.Generic;

namespace VoxelWorld.Core.Persistence
{
    /// <summary>One player edit stored in a save file (flat ints because JsonUtility is minimal).</summary>
    [Serializable]
    public sealed class BlockDeltaRecord
    {
        public int x;
        public int y;
        public int z;
        public int kind;
    }

    /// <summary>One hotbar stack in a save file.</summary>
    [Serializable]
    public sealed class InventoryRecord
    {
        public int kind;
        public int count;
    }

    /// <summary>Player transform snapshot for a save file.</summary>
    [Serializable]
    public sealed class PlayerRecord
    {
        public float px;
        public float py;
        public float pz;
        public float yaw;
        public float pitch;
    }

    /// <summary>Root save document: seed + sparse deltas + player state. Bumping <see cref="version"/> invalidates old files.</summary>
    [Serializable]
    public sealed class WorldSaveData
    {
        public int version = 1;
        public int seed;
        public string savedAtUtc = "";
        public PlayerRecord player = new PlayerRecord();
        public List<BlockDeltaRecord> deltas = new List<BlockDeltaRecord>();
        public List<InventoryRecord> inventory = new List<InventoryRecord>();
    }

    /// <summary>Save/load boundary; the persistence assembly provides the JSON-on-disk implementation.</summary>
    public interface IWorldPersistence
    {
        void Save(WorldSaveData data);

        /// <summary>Loads the save; returns false when missing or corrupted.</summary>
        bool TryLoad(out WorldSaveData data);

        bool HasSave { get; }

        void Delete();
    }
}
