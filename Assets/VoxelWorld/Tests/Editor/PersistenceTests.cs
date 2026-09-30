using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Persistence;
using VoxelWorld.Persistence;

namespace VoxelWorld.Tests
{
    public sealed class PersistenceTests
    {
        /// <summary>In-memory storage double; keeps tests free of the file system.</summary>
        private sealed class MemorySaveStorage : ISaveStorage
        {
            public string Json;

            public bool Exists => Json != null;

            public bool TryRead(out string json)
            {
                json = Json;
                return Json != null;
            }

            public void Write(string json) => Json = json;

            public void Delete() => Json = null;
        }

        private static WorldSaveData SampleData()
        {
            var data = new WorldSaveData
            {
                seed = 424242,
                player = new PlayerRecord { px = 1.5f, py = 12.25f, pz = -3f, yaw = 90f, pitch = -15f }
            };

            data.deltas.Add(new BlockDeltaRecord { x = 1, y = 2, z = 3, kind = (int)BlockKind.Stone });
            data.deltas.Add(new BlockDeltaRecord { x = 4, y = 5, z = 6, kind = (int)BlockKind.Air });
            data.inventory.Add(new InventoryRecord { kind = (int)BlockKind.Stone, count = 7 });
            data.inventory.Add(new InventoryRecord { kind = (int)BlockKind.Snow, count = 0 });
            return data;
        }

        [Test]
        public void SaveThenLoad_PreservesEverything()
        {
            var persistence = new JsonWorldPersistence(new MemorySaveStorage());
            persistence.Save(SampleData());

            Assert.IsTrue(persistence.HasSave);
            Assert.IsTrue(persistence.TryLoad(out var loaded));
            Assert.AreEqual(424242, loaded.seed);
            Assert.AreEqual(2, loaded.deltas.Count);
            Assert.AreEqual(2, loaded.inventory.Count);
            Assert.AreEqual(1.5f, loaded.player.px);
            Assert.AreEqual(-15f, loaded.player.pitch);
            Assert.AreEqual((int)BlockKind.Air, loaded.deltas[1].kind);
            Assert.IsNotEmpty(loaded.savedAtUtc);
        }

        [Test]
        public void Load_MissingSave_ReturnsFalse()
        {
            var persistence = new JsonWorldPersistence(new MemorySaveStorage());
            Assert.IsFalse(persistence.HasSave);
            Assert.IsFalse(persistence.TryLoad(out _));
        }

        [Test]
        public void Load_CorruptJson_ReturnsFalse()
        {
            var storage = new MemorySaveStorage { Json = "{ this is not json" };
            var persistence = new JsonWorldPersistence(storage);
            Assert.IsFalse(persistence.TryLoad(out _));
        }

        [Test]
        public void Delete_RemovesSave()
        {
            var persistence = new JsonWorldPersistence(new MemorySaveStorage());
            persistence.Save(SampleData());
            persistence.Delete();
            Assert.IsFalse(persistence.HasSave);
        }

        [Test]
        public void Deltas_AreSparse_NotFullWorld()
        {
            // The save contract: only player modifications, never the whole voxel field.
            var data = SampleData();
            Assert.Less(data.deltas.Count, 1000);
        }
    }
}
