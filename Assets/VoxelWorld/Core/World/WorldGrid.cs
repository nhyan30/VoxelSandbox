using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Generation;

namespace VoxelWorld.Core.World
{
    /// <summary>
    /// Concrete voxel grid. Chunk data (one flat array per chunk) is generated lazily
    /// from the injected terrain generator and kept in memory for the session, so
    /// streaming view unload/reload never loses modifications. Player edits are stored
    /// as a sparse delta dictionary which doubles as the save payload.
    /// </summary>
    public sealed class WorldGrid : IWorldGrid
    {
        private readonly int _chunkSize;
        private readonly int _chunksX;
        private readonly int _chunksZ;
        private readonly int _worldHeight;
        private readonly Func<int, ITerrainGenerator> _terrainFactory;

        private ITerrainGenerator _terrain;
        private readonly Dictionary<ChunkCoord, BlockKind[]> _chunks =
            new Dictionary<ChunkCoord, BlockKind[]>();
        private readonly Dictionary<Vector3Int, BlockKind> _deltas =
            new Dictionary<Vector3Int, BlockKind>();

        public Vector3Int Size => new Vector3Int(_chunksX * _chunkSize, _worldHeight, _chunksZ * _chunkSize);
        public int Seed { get; private set; }
        public IReadOnlyDictionary<Vector3Int, BlockKind> Deltas => _deltas;
        public event Action<Vector3Int, BlockKind, BlockKind> BlockChanged;

        /// <param name="terrainFactory">Creates a deterministic generator for a given seed (supports <see cref="Reset"/>).</param>
        public WorldGrid(int chunksX, int chunksZ, int chunkSize, int worldHeight,
            Func<int, ITerrainGenerator> terrainFactory, int initialSeed)
        {
            _chunksX = chunksX;
            _chunksZ = chunksZ;
            _chunkSize = chunkSize;
            _worldHeight = worldHeight;
            _terrainFactory = terrainFactory;
            Reset(initialSeed);
        }

        public bool InBounds(Vector3Int pos)
        {
            return pos.x >= 0 && pos.x < Size.x
                && pos.y >= 0 && pos.y < Size.y
                && pos.z >= 0 && pos.z < Size.z;
        }

        public bool InBuildBounds(Vector3Int pos)
        {
            return pos.x >= 0 && pos.x < Size.x
                && pos.y >= 0 && pos.y < Size.y
                && pos.z >= 0 && pos.z < Size.z;
        }

        public BlockKind GetBlock(Vector3Int pos)
        {
            if (pos.y < 0)
            {
                return BlockKind.Bedrock; // below the world behaves as solid rock
            }
            if (!InBounds(pos))
            {
                return BlockKind.Air;
            }

            var chunk = EnsureChunkData(ChunkMath.ToChunkCoord(pos, _chunkSize));
            return chunk[ChunkMath.BlockIndex(pos.x % _chunkSize, pos.y, pos.z % _chunkSize, _chunkSize)];
        }

        public bool TrySetBlock(Vector3Int pos, BlockKind kind)
        {
            if (!InBounds(pos))
            {
                return false;
            }

            var oldKind = GetBlock(pos);
            if (oldKind == kind)
            {
                return false;
            }

            Write(pos, kind);
            _deltas[pos] = kind;
            BlockChanged?.Invoke(pos, oldKind, kind);
            return true;
        }

        public void ApplyDelta(Vector3Int pos, BlockKind kind)
        {
            if (!InBounds(pos) || GetBlock(pos) == kind)
            {
                return;
            }

            Write(pos, kind);
            _deltas[pos] = kind;
        }

        public void Reset(int seed)
        {
            Seed = seed;
            _terrain = _terrainFactory(seed);
            _chunks.Clear();
            _deltas.Clear();
        }

        public int SurfaceHeightAt(int x, int z)
        {
            x = Mathf.Clamp(x, 0, Size.x - 1);
            z = Mathf.Clamp(z, 0, Size.z - 1);
            return Mathf.Clamp(_terrain.HeightAt(x, z), 0, _worldHeight - 1);
        }

        private void Write(Vector3Int pos, BlockKind kind)
        {
            var chunk = EnsureChunkData(ChunkMath.ToChunkCoord(pos, _chunkSize));
            chunk[ChunkMath.BlockIndex(pos.x % _chunkSize, pos.y, pos.z % _chunkSize, _chunkSize)] = kind;
        }

        /// <summary>Generates and caches the chunk array if missing, then returns it.</summary>
        public BlockKind[] EnsureChunkData(ChunkCoord coord)
        {
            if (_chunks.TryGetValue(coord, out var existing))
            {
                return existing;
            }

            var cells = new BlockKind[_chunkSize * _chunkSize * _worldHeight];
            for (var y = 0; y < _worldHeight; y++)
            {
                for (var z = 0; z < _chunkSize; z++)
                {
                    for (var x = 0; x < _chunkSize; x++)
                    {
                        cells[ChunkMath.BlockIndex(x, y, z, _chunkSize)] =
                            _terrain.GenerateBlock(coord.X * _chunkSize + x, y, coord.Z * _chunkSize + z);
                    }
                }
            }

            _chunks[coord] = cells;
            return cells;
        }

        public bool HasChunkData(ChunkCoord coord) => _chunks.ContainsKey(coord);
    }
}
