using System;
using UnityEngine;

namespace VoxelWorld.Core.World
{
    /// <summary>Identifies one 16x16 column-chunk of the world grid.</summary>
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int X { get; }
        public int Z { get; }

        public ChunkCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Z);
        public override string ToString() => $"Chunk({X},{Z})";

        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
    }

    /// <summary>
    /// Coordinate math shared by the data grid (core) and the view layer. All layout
    /// assumptions (chunk size, block indexing) live here so the two can never drift.
    /// </summary>
    public static class ChunkMath
    {
        /// <summary>Floor division that behaves correctly for negative coordinates.</summary>
        public static int FloorDiv(int value, int divisor)
        {
            return value >= 0 ? value / divisor : (value - divisor + 1) / divisor;
        }

        public static ChunkCoord ToChunkCoord(Vector3Int blockPos, int chunkSize)
        {
            return new ChunkCoord(FloorDiv(blockPos.x, chunkSize), FloorDiv(blockPos.z, chunkSize));
        }

        public static ChunkCoord ToChunkCoord(Vector3 worldPos, int chunkSize)
        {
            return ToChunkCoord(new Vector3Int(
                Mathf.FloorToInt(worldPos.x),
                0,
                Mathf.FloorToInt(worldPos.z)), chunkSize);
        }

        public static Vector3Int ToWorldBlockPos(ChunkCoord chunk, int localX, int localY, int localZ, int chunkSize)
        {
            return new Vector3Int(chunk.X * chunkSize + localX, localY, chunk.Z * chunkSize + localZ);
        }

        /// <summary>Index of (x,y,z) inside a chunk's flat array. Layout: X fastest, then Z, then Y.</summary>
        public static int BlockIndex(int localX, int localY, int localZ, int chunkSize)
        {
            return localX + localZ * chunkSize + localY * chunkSize * chunkSize;
        }

        /// <summary>Chebyshev (square-ring) distance between two chunks, used for streaming radii.</summary>
        public static int ChebyshevDistance(ChunkCoord a, ChunkCoord b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
        }
    }
}
