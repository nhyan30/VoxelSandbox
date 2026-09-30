using NUnit.Framework;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Editing;
using VoxelWorld.Core.Generation;
using VoxelWorld.Core.World;

namespace VoxelWorld.Tests
{
    public sealed class PerlinNoiseTests
    {
        private const float Epsilon = 0.0001f;

        [Test]
        public void SameSeed_ProducesIdenticalSamples()
        {
            var a = new PerlinNoiseGenerator(1337);
            var b = new PerlinNoiseGenerator(1337);
            for (var i = 0; i < 200; i++)
            {
                var x = i * 0.137f;
                var y = i * 0.291f;
                Assert.AreEqual(a.Sample2D(x, y), b.Sample2D(x, y), Epsilon);
            }
        }

        [Test]
        public void DifferentSeeds_Diverge()
        {
            var a = new PerlinNoiseGenerator(1);
            var b = new PerlinNoiseGenerator(2);
            var differs = false;
            for (var i = 0; i < 50 && !differs; i++)
            {
                differs = Mathf.Abs(a.Sample2D(i * 0.71f, i * 0.33f) - b.Sample2D(i * 0.71f, i * 0.33f)) > Epsilon;
            }

            Assert.IsTrue(differs, "Two different seeds produced identical noise.");
        }

        [Test]
        public void Sample_StaysWithinUnitRange()
        {
            var noise = new PerlinNoiseGenerator(99);
            for (var i = 0; i < 1000; i++)
            {
                var sample = noise.Sample2D(i * 0.173f, i * 0.061f);
                Assert.That(sample, Is.InRange(-1f - Epsilon, 1f + Epsilon));
            }
        }

        [Test]
        public void Fbm_StaysWithinUnitRange()
        {
            var noise = new PerlinNoiseGenerator(5);
            for (var i = 0; i < 500; i++)
            {
                var sample = noise.Fbm(i * 0.09f, i * 0.11f, 4, 0.5f, 2f);
                Assert.That(sample, Is.InRange(-1.05f, 1.05f));
            }
        }
    }

    public sealed class ChunkCoordTests
    {
        [Test]
        public void ToChunkCoord_PartitionsBlocks()
        {
            Assert.AreEqual(new ChunkCoord(0, 0), ChunkMath.ToChunkCoord(new Vector3Int(0, 0, 0), 16));
            Assert.AreEqual(new ChunkCoord(0, 0), ChunkMath.ToChunkCoord(new Vector3Int(15, 5, 15), 16));
            Assert.AreEqual(new ChunkCoord(1, 0), ChunkMath.ToChunkCoord(new Vector3Int(16, 0, 8), 16));
            Assert.AreEqual(new ChunkCoord(-1, -1), ChunkMath.ToChunkCoord(new Vector3Int(-1, 0, -1), 16));
        }

        [Test]
        public void ChebyshevDistance_UsesSquareRings()
        {
            Assert.AreEqual(3, ChunkMath.ChebyshevDistance(new ChunkCoord(0, 0), new ChunkCoord(3, 1)));
            Assert.AreEqual(0, ChunkMath.ChebyshevDistance(new ChunkCoord(4, 4), new ChunkCoord(4, 4)));
            Assert.AreEqual(2, ChunkMath.ChebyshevDistance(new ChunkCoord(0, 0), new ChunkCoord(-2, 1)));
        }

        [Test]
        public void BlockIndex_IsUniquePerCell()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (var y = 0; y < 4; y++)
            for (var z = 0; z < 16; z++)
            for (var x = 0; x < 16; x++)
            {
                Assert.IsTrue(seen.Add(ChunkMath.BlockIndex(x, y, z, 16)), $"duplicate index at {x},{y},{z}");
            }
        }
    }

    public sealed class TerrainTests
    {
        private const int WorldHeight = 48;

        private static HeightmapTerrainGenerator CreateGenerator()
        {
            return new HeightmapTerrainGenerator(42, new TerrainProfile(), WorldHeight);
        }

        [Test]
        public void Floor_IsBedrock()
        {
            var terrain = CreateGenerator();
            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(BlockKind.Bedrock, terrain.GenerateBlock(i * 7, 0, i * 5));
            }
        }

        [Test]
        public void AboveSurface_IsAir()
        {
            var terrain = CreateGenerator();
            for (var i = 0; i < 8; i++)
            {
                var x = i * 11;
                var z = i * 17 + 3;
                var surface = terrain.HeightAt(x, z);
                Assert.AreEqual(BlockKind.Air, terrain.GenerateBlock(x, surface + 1, z));
                Assert.AreEqual(BlockKind.Air, terrain.GenerateBlock(x, WorldHeight - 1, z));
            }
        }

        [Test]
        public void Height_StaysInsideWorld()
        {
            var terrain = CreateGenerator();
            for (var x = 0; x < 48; x++)
            for (var z = 0; z < 48; z++)
            {
                var height = terrain.HeightAt(x, z);
                Assert.That(height, Is.InRange(1, WorldHeight - 2));
            }
        }

        [Test]
        public void Bands_FollowHeightRules()
        {
            var terrain = CreateGenerator();
            for (var i = 0; i < 16; i++)
            {
                var x = i * 13;
                var z = i * 7 + 1;
                var surface = terrain.HeightAt(x, z);
                var surfaceKind = terrain.GenerateBlock(x, surface, z);
                Assert.IsTrue(
                    surfaceKind == BlockKind.Grass || surfaceKind == BlockKind.Snow || surfaceKind == BlockKind.Sand,
                    $"surface must be a natural cap material but was {surfaceKind}");
                Assert.AreEqual(BlockKind.Stone, terrain.GenerateBlock(x, Mathf.Max(1, surface - 10), z));

                if (surfaceKind == BlockKind.Grass)
                {
                    Assert.AreEqual(BlockKind.Grass, terrain.GenerateBlock(x, surface - 1, z),
                        "grass surfaces keep a soil band beneath them");
                }

                if (surfaceKind == BlockKind.Sand)
                {
                    Assert.That(surface, Is.LessThanOrEqualTo(11), "sand only appears in low basins");
                }
            }
        }
    }

    public sealed class WorldGridTests
    {
        private static WorldGrid CreateGrid(int seed = 7)
        {
            return new WorldGrid(2, 2, 16, 48, s => new HeightmapTerrainGenerator(s, new TerrainProfile(), 48), seed);
        }

        [Test]
        public void GetBlock_GeneratesChunkLazily()
        {
            var grid = CreateGrid();
            Assert.IsFalse(grid.HasChunkData(new ChunkCoord(0, 0)));
            Assert.AreEqual(BlockKind.Bedrock, grid.GetBlock(new Vector3Int(5, 0, 5)));
            Assert.IsTrue(grid.HasChunkData(new ChunkCoord(0, 0)));
        }

        [Test]
        public void TrySetBlock_WritesFiresEventAndRecordsDelta()
        {
            var grid = CreateGrid();
            var pos = new Vector3Int(3, 20, 9);
            Assert.AreEqual(BlockKind.Air, grid.GetBlock(pos)); // above terrain

            var events = 0;
            grid.BlockChanged += (p, oldKind, newKind) =>
            {
                events++;
                Assert.AreEqual(pos, p);
                Assert.AreEqual(BlockKind.Air, oldKind);
                Assert.AreEqual(BlockKind.Stone, newKind);
            };

            Assert.IsTrue(grid.TrySetBlock(pos, BlockKind.Stone));
            Assert.AreEqual(1, events);
            Assert.AreEqual(BlockKind.Stone, grid.GetBlock(pos));
            Assert.IsTrue(grid.Deltas.ContainsKey(pos));
        }

        [Test]
        public void TrySetBlock_OutOfBounds_IsRejected()
        {
            var grid = CreateGrid();
            Assert.IsFalse(grid.TrySetBlock(new Vector3Int(32, 1, 3), BlockKind.Stone));
            Assert.IsFalse(grid.TrySetBlock(new Vector3Int(3, 48, 3), BlockKind.Stone));
            Assert.IsFalse(grid.TrySetBlock(new Vector3Int(-1, 1, 3), BlockKind.Stone));
        }

        [Test]
        public void TrySetBlock_SameValue_IsRejectedWithoutEvent()
        {
            var grid = CreateGrid();
            var pos = new Vector3Int(2, 0, 2); // bedrock floor
            var fired = false;
            grid.BlockChanged += (p, o, n) => fired = true;
            Assert.IsFalse(grid.TrySetBlock(pos, BlockKind.Bedrock));
            Assert.IsFalse(fired);
        }

        [Test]
        public void Reset_ClearsDeltasAndReseeds()
        {
            var grid = CreateGrid();
            grid.TrySetBlock(new Vector3Int(1, 10, 1), BlockKind.Snow);
            Assert.IsNotEmpty(grid.Deltas);

            grid.Reset(123);
            Assert.IsEmpty(grid.Deltas);
            Assert.AreEqual(123, grid.Seed);
        }

        [Test]
        public void ApplyDelta_WritesSilently()
        {
            var grid = CreateGrid();
            var pos = new Vector3Int(4, 12, 4);
            var fired = false;
            grid.BlockChanged += (p, o, n) => fired = true;

            grid.ApplyDelta(pos, BlockKind.Grass);

            Assert.AreEqual(BlockKind.Grass, grid.GetBlock(pos));
            Assert.IsTrue(grid.Deltas.ContainsKey(pos));
            Assert.IsFalse(fired, "ApplyDelta is the silent load path");
        }

        [Test]
        public void SurfaceHeightAt_ClampsToWorld()
        {
            var grid = CreateGrid();
            Assert.That(grid.SurfaceHeightAt(-100, -100), Is.InRange(0, 47));
            Assert.That(grid.SurfaceHeightAt(1000, 1000), Is.InRange(0, 47));
        }
    }

    public sealed class WorldEditorTests
    {
        private static (WorldGrid grid, WorldEditor editor) Create()
        {
            var grid = new WorldGrid(2, 2, 16, 48, s => new HeightmapTerrainGenerator(s, new TerrainProfile(), 48), 11);
            return (grid, new WorldEditor(grid, BlockRegistry.CreateDefault(new BlockTuning())));
        }

        [Test]
        public void Remove_Bedrock_IsUnbreakable()
        {
            var (grid, editor) = Create();
            Assert.AreEqual(WorldEditResult.Unbreakable, editor.RemoveBlock(new Vector3Int(6, 0, 6)));
            Assert.AreEqual(BlockKind.Bedrock, grid.GetBlock(new Vector3Int(6, 0, 6)));
        }

        [Test]
        public void Remove_Air_IsNothingToMine()
        {
            var (_, editor) = Create();
            Assert.AreEqual(WorldEditResult.NothingToMine, editor.RemoveBlock(new Vector3Int(6, 40, 6)));
        }

        [Test]
        public void Remove_Solid_SucceedsAndRecordsDelta()
        {
            var (grid, editor) = Create();
            var pos = new Vector3Int(6, 0 + 1, 6); // just above bedrock: always solid terrain
            Assert.AreEqual(WorldEditResult.Ok, editor.RemoveBlock(pos));
            Assert.AreEqual(BlockKind.Air, grid.GetBlock(pos));
            Assert.AreEqual(BlockKind.Air, grid.Deltas[pos]);
        }

        [Test]
        public void Place_InAir_Succeeds()
        {
            var (grid, editor) = Create();
            var pos = new Vector3Int(5, 30, 5);
            Assert.AreEqual(WorldEditResult.Ok, editor.PlaceBlock(pos, BlockKind.Snow));
            Assert.AreEqual(BlockKind.Snow, grid.GetBlock(pos));
        }

        [Test]
        public void Place_Occupied_IsRejected()
        {
            var (_, editor) = Create();
            var pos = new Vector3Int(5, 0, 5); // bedrock
            Assert.AreEqual(WorldEditResult.NotEmpty, editor.PlaceBlock(pos, BlockKind.Stone));
        }

        [Test]
        public void Place_AboveCeiling_IsOutOfBounds()
        {
            var (_, editor) = Create();
            Assert.AreEqual(WorldEditResult.OutOfBounds, editor.PlaceBlock(new Vector3Int(5, 48, 5), BlockKind.Stone));
        }

        [Test]
        public void Place_AirKind_IsRejected()
        {
            var (_, editor) = Create();
            Assert.AreEqual(WorldEditResult.OutOfBounds, editor.PlaceBlock(new Vector3Int(5, 30, 5), BlockKind.Air));
        }
    }
}
