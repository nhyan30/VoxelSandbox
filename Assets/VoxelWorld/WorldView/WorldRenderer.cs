using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.World;

namespace VoxelWorld.World
{
    /// <summary>
    /// Bridges the voxel data grid and the scene: streams chunk views around the player,
    /// listens for block edits and updates only the affected cube views, hands out pooled
    /// views/materials, and builds invisible boundary walls at the world edge.
    ///
    /// NOTE ON SCALE: the task opted for simple per-cube renderers with pooling rather
    /// than combined chunk meshes. Exposure culling + pooling keep a 12x12x48 world with
    /// a view radius of 3 comfortably playable; for much larger worlds the natural next
    /// step is one combined mesh per chunk (or DOTS instancing) behind the same provider.
    /// </summary>
    public sealed class WorldRenderer : ICubeViewProvider, IDisposable
    {
        private readonly IWorldGrid _grid;
        private readonly BlockRegistry _registry;
        private readonly int _chunkSize;
        private readonly int _loadRadius;
        private readonly int _unloadRadius;
        private readonly int _chunksBuiltPerFrame;

        private readonly CubeViewPool _pool;
        private readonly Dictionary<ChunkCoord, ChunkView> _active = new Dictionary<ChunkCoord, ChunkView>();
        private readonly Dictionary<BlockKind, Material> _materials = new Dictionary<BlockKind, Material>();
        private readonly List<ChunkCoord> _buildQueue = new List<ChunkCoord>();
        private readonly Transform _root;

        private ChunkCoord _center;
        private bool _hasCenter;

        public int ActiveChunkCount => _active.Count;

        public WorldRenderer(IWorldGrid grid, BlockRegistry registry, Transform root,
            int chunkSize, int loadRadius, int unloadRadius, int chunksBuiltPerFrame,
            int poolPrewarm, int poolMax, bool castShadows, int blockLayer)
        {
            _grid = grid;
            _registry = registry;
            _root = root;
            _chunkSize = chunkSize;
            _loadRadius = loadRadius;
            _unloadRadius = unloadRadius;
            _chunksBuiltPerFrame = Mathf.Max(1, chunksBuiltPerFrame);
            _pool = new CubeViewPool(root, blockLayer, castShadows, poolPrewarm, poolMax);
            _grid.BlockChanged += OnBlockChanged;
        }

        public void Dispose()
        {
            _grid.BlockChanged -= OnBlockChanged;
        }

        /// <summary>
        /// Called every frame with the player position: unloads chunks that fell outside
        /// the keep radius and dequeues a small budget of new chunk builds per frame so
        /// streaming never hitches.
        /// </summary>
        public void StreamAround(Vector3 playerPosition)
        {
            var center = ChunkMath.ToChunkCoord(playerPosition, _chunkSize);
            if (!_hasCenter || center != _center)
            {
                _center = center;
                _hasCenter = true;
                UnloadFarChunks();
                PlanBuildQueue();
            }

            var budget = _chunksBuiltPerFrame;
            while (budget > 0 && _buildQueue.Count > 0)
            {
                var coord = _buildQueue[_buildQueue.Count - 1];
                _buildQueue.RemoveAt(_buildQueue.Count - 1);
                if (_active.ContainsKey(coord) || !IsInsideWorld(coord))
                {
                    continue;
                }

                BuildChunk(coord);
                budget--;
            }
        }

        /// <summary>Synchronously builds all chunks within <paramref name="radius"/> of a center (spawn area).</summary>
        public void BuildImmediate(ChunkCoord center, int radius)
        {
            for (var dz = -radius; dz <= radius; dz++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var coord = new ChunkCoord(center.X + dx, center.Z + dz);
                    if (IsInsideWorld(coord) && !_active.ContainsKey(coord))
                    {
                        BuildChunk(coord);
                    }
                }
            }

            _center = center;
            _hasCenter = true;
        }

        /// <summary>Drops every view (used after loading a save or resetting the world).</summary>
        public void RebuildAll()
        {
            foreach (var chunk in _active.Values)
            {
                chunk.Release(this);
            }

            _active.Clear();
            _buildQueue.Clear();
            _hasCenter = false;
        }

        /// <summary>Invisible walls so the player cannot walk off the finite world island.</summary>
        public void BuildWorldBoundaries(int boundaryLayer)
        {
            var size = _grid.Size;
            var holder = new GameObject("WorldBoundaries");
            holder.transform.SetParent(_root, false);
            holder.layer = Mathf.Max(0, boundaryLayer);

            AddWall(holder.transform, new Vector3(size.x * 0.5f, size.y * 0.5f, -1f), new Vector3(size.x + 4f, size.y + 8f, 2f));
            AddWall(holder.transform, new Vector3(size.x * 0.5f, size.y * 0.5f, size.z + 1f), new Vector3(size.x + 4f, size.y + 8f, 2f));
            AddWall(holder.transform, new Vector3(-1f, size.y * 0.5f, size.z * 0.5f), new Vector3(2f, size.y + 8f, size.z + 4f));
            AddWall(holder.transform, new Vector3(size.x + 1f, size.y * 0.5f, size.z * 0.5f), new Vector3(2f, size.y + 8f, size.z + 4f));
        }

        private void AddWall(Transform parent, Vector3 position, Vector3 size)
        {
            var wall = new GameObject("BoundaryWall");
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.layer = parent.gameObject.layer;
            var collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private void OnBlockChanged(Vector3Int pos, BlockKind oldKind, BlockKind newKind)
        {
            // Edits at a chunk border also change exposure of cells in the adjacent
            // chunk, so the four horizontal neighbours get refreshed as well. Cells
            // outside a chunk's footprint early-return, keeping this cheap.
            var coord = ChunkMath.ToChunkCoord(pos, _chunkSize);
            NotifyChunk(coord, pos);
            NotifyChunk(new ChunkCoord(coord.X + 1, coord.Z), pos);
            NotifyChunk(new ChunkCoord(coord.X - 1, coord.Z), pos);
            NotifyChunk(new ChunkCoord(coord.X, coord.Z + 1), pos);
            NotifyChunk(new ChunkCoord(coord.X, coord.Z - 1), pos);
            // Edits in unloaded chunks are stored by the grid and appear on next load.
        }

        private void NotifyChunk(ChunkCoord coord, Vector3Int pos)
        {
            if (_active.TryGetValue(coord, out var chunk))
            {
                chunk.UpdateBlock(_grid, this, pos);
            }
        }

        private void BuildChunk(ChunkCoord coord)
        {
            var view = new ChunkView(coord, _chunkSize);
            view.Rebuild(_grid, this);
            _active[coord] = view;
        }

        private bool IsInsideWorld(ChunkCoord coord)
        {
            var maxChunkX = ChunkMath.FloorDiv(_grid.Size.x - 1, _chunkSize);
            var maxChunkZ = ChunkMath.FloorDiv(_grid.Size.z - 1, _chunkSize);
            return coord.X >= 0 && coord.X <= maxChunkX && coord.Z >= 0 && coord.Z <= maxChunkZ;
        }

        private void UnloadFarChunks()
        {
            List<ChunkCoord> toUnload = null;
            foreach (var pair in _active)
            {
                if (ChunkMath.ChebyshevDistance(pair.Key, _center) > _unloadRadius)
                {
                    (toUnload ?? (toUnload = new List<ChunkCoord>())).Add(pair.Key);
                }
            }

            if (toUnload == null)
            {
                return;
            }

            foreach (var coord in toUnload)
            {
                _active[coord].Release(this);
                _active.Remove(coord);
            }
        }

        private void PlanBuildQueue()
        {
            _buildQueue.Clear();
            for (var dz = -_loadRadius; dz <= _loadRadius; dz++)
            {
                for (var dx = -_loadRadius; dx <= _loadRadius; dx++)
                {
                    var coord = new ChunkCoord(_center.X + dx, _center.Z + dz);
                    if (IsInsideWorld(coord) && !_active.ContainsKey(coord))
                    {
                        _buildQueue.Add(coord);
                    }
                }
            }

            // Nearest chunks last so they pop first (the queue is drained from the end).
            _buildQueue.Sort((a, b) =>
            {
                var da = ChunkMath.ChebyshevDistance(a, _center);
                var db = ChunkMath.ChebyshevDistance(b, _center);
                return db != da ? db.CompareTo(da) : b.GetHashCode().CompareTo(a.GetHashCode());
            });
        }

        // ---- ICubeViewProvider ----

        public CubeView Acquire(BlockKind kind)
        {
            var view = _pool.Get();
            view.Bind(kind, MaterialFor(kind));
            return view;
        }

        public void Release(CubeView view)
        {
            _pool.Return(view);
        }

        public Material MaterialFor(BlockKind kind)
        {
            if (!_materials.TryGetValue(kind, out var material))
            {
                material = MaterialFactory.CreateLit(_registry.Get(kind).Color);
                _materials[kind] = material;
            }

            return material;
        }
    }
}
