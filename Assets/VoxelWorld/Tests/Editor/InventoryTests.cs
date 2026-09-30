using NUnit.Framework;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Inventory;

namespace VoxelWorld.Tests
{
    public sealed class InventoryTests
    {
        private static Inventory Create()
        {
            return new Inventory(BlockRegistry.CreateDefault(new BlockTuning()));
        }

        [Test]
        public void Add_AccumulatesCounts()
        {
            var inventory = Create();
            inventory.Add(BlockKind.Stone);
            inventory.Add(BlockKind.Stone, 2);
            Assert.AreEqual(3, inventory.GetCount(BlockKind.Stone));
        }

        [Test]
        public void TryConsume_Decrements()
        {
            var inventory = Create();
            inventory.Add(BlockKind.Grass, 2);
            Assert.IsTrue(inventory.TryConsume(BlockKind.Grass));
            Assert.AreEqual(1, inventory.GetCount(BlockKind.Grass));
        }

        [Test]
        public void TryConsume_Insufficient_FailsWithoutChange()
        {
            var inventory = Create();
            inventory.Add(BlockKind.Snow, 1);
            Assert.IsFalse(inventory.TryConsume(BlockKind.Snow, 2));
            Assert.AreEqual(1, inventory.GetCount(BlockKind.Snow));
        }

        [Test]
        public void CountChanged_FiresWithNewCount()
        {
            var inventory = Create();
            var observed = -1;
            inventory.CountChanged += (kind, count) =>
            {
                Assert.AreEqual(BlockKind.Sand, kind);
                observed = count;
            };

            inventory.Add(BlockKind.Sand, 4);
            Assert.AreEqual(4, observed);
            inventory.TryConsume(BlockKind.Sand);
            Assert.AreEqual(3, observed);
        }

        [Test]
        public void NonPlaceableKinds_CannotBeStored()
        {
            var inventory = Create();
            inventory.Add(BlockKind.Bedrock, 10); // cannot be mined, cannot be stored
            Assert.AreEqual(0, inventory.GetCount(BlockKind.Bedrock));
        }

        [Test]
        public void Restore_ReplacesCounts()
        {
            var inventory = Create();
            inventory.Add(BlockKind.Stone, 9);
            inventory.Restore(new System.Collections.Generic.Dictionary<BlockKind, int>
            {
                [BlockKind.Stone] = 2,
                [BlockKind.Snow] = 5
            });

            Assert.AreEqual(2, inventory.GetCount(BlockKind.Stone));
            Assert.AreEqual(5, inventory.GetCount(BlockKind.Snow));
        }

        [Test]
        public void NewInventory_StartsEmpty()
        {
            var inventory = Create();
            foreach (var pair in inventory.Counts)
            {
                Assert.AreEqual(0, pair.Value, $"{pair.Key} should start at zero");
            }
        }
    }
}
