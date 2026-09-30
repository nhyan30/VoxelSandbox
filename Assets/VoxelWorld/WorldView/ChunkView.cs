using System.Collections.Generic;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.World;

namespace VoxelWorld.World
{
    /// <summary>Supplies pooled cube views and per-kind materials to <see cref="ChunkView"/>.</summary>
    public interface ICubeViewProvider
    {
        CubeView Acquire(BlockKind kind);

        void Release(CubeView view);

        Material MaterialFor(BlockKind kind);
    }

    /// <summary>
    /// Rendered state of one chunk: a map from cell index to the cube view shown for it.
    /// A cell gets a view only while it is solid **and exposed** (at least one air
    /// neighbour), which keeps the object count proportional to the visible surface
    /// instead of the full chunk volume. Edits re-evaluate the edited cell and its six
    /// neighbours, so digging automatically reveals buried voxels and building buries them.
    /// </summary>
    public sealed class ChunkView
    {
        private readonly ChunkCoord _coord;
        private readonly int _chunkSize;
        private readonly Dictionary<int, CubeView> _views = new Dictionary<int, CubeView>();

        public ChunkCoord Coord => _coord;
        public int ViewCount => _views.Count;

        public ChunkView(ChunkCoord coord, int chunkSize)
        {
            _coord = coord;
            _chunkSize = chunkSize;
        }

        /// <summary>Spawns views for every exposed solid cell of the chunk.</summary>
        public void Rebuild(IWorldGrid grid, ICubeViewProvider provider)
        {
            for (var y = 0; y < grid.Size.y; y++)
            {
                for (var z = 0; z < _chunkSize; z++)
                {
                    for (var x = 0; x < _chunkSize; x++)
                    {
                        RefreshCell(grid, provider, x, y, z);
                    }
                }
            }
        }

        /// <summary>Re-evaluates a cell and its six neighbours after an edit.</summary>
        public void UpdateBlock(IWorldGrid grid, ICubeViewProvider provider, Vector3Int worldPos)
        {
            RefreshCell(grid, provider,
                worldPos.x - _coord.X * _chunkSize,
                worldPos.y,
                worldPos.z - _coord.Z * _chunkSize);

            foreach (var dir in NeighborOffsets)
            {
                RefreshCell(grid, provider,
                    worldPos.x + dir.x - _coord.X * _chunkSize,
                    worldPos.y + dir.y,
                    worldPos.z + dir.z - _coord.Z * _chunkSize);
            }
        }

        /// <summary>Returns every view to the pool (chunk unloads).</summary>
        public void Release(ICubeViewProvider provider)
        {
            foreach (var view in _views.Values)
            {
                provider.Release(view);
            }

            _views.Clear();
        }

        private void RefreshCell(IWorldGrid grid, ICubeViewProvider provider, int localX, int localY, int localZ)
        {
            // Cells outside this chunk's XZ footprint are owned by their own chunk view.
            if (localX < 0 || localX >= _chunkSize || localZ < 0 || localZ >= _chunkSize
                || localY < 0 || localY >= grid.Size.y)
            {
                return;
            }

            var worldPos = ChunkMath.ToWorldBlockPos(_coord, localX, localY, localZ, _chunkSize);
            var kind = grid.GetBlock(worldPos);
            var wantsView = kind != BlockKind.Air && IsExposed(grid, worldPos);
            var index = ChunkMath.BlockIndex(localX, localY, localZ, _chunkSize);

            if (_views.TryGetValue(index, out var existing))
            {
                if (!wantsView)
                {
                    provider.Release(existing);
                    _views.Remove(index);
                }
                else if (existing.Kind != kind)
                {
                    // Kind swaps (e.g. a saved delta) just rebind the pooled view.
                    existing.Bind(kind, provider.MaterialFor(kind));
                }
                return;
            }

            if (!wantsView)
            {
                return;
            }

            var view = provider.Acquire(kind);
            view.PlaceAt(worldPos);
            _views[index] = view;
        }

        /// <summary>A solid cell is exposed when any of its six neighbours is air. Below the world counts as solid.</summary>
        private static bool IsExposed(IWorldGrid grid, Vector3Int pos)
        {
            foreach (var dir in NeighborOffsets)
            {
                if (grid.GetBlock(pos + dir) == BlockKind.Air)
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Vector3Int[] NeighborOffsets =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };
    }
}
