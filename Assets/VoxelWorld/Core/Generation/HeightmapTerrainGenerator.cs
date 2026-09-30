using System.Collections.Generic;
using VoxelWorld.Core.Blocks;

namespace VoxelWorld.Core.Generation
{
    /// <summary>All knobs that shape the generated landscape.</summary>
    public sealed class TerrainProfile
    {
        public float BaseSurfaceHeight = 8f;
        public float SurfaceAmplitude = 26f;
        public float NoiseFrequency = 0.013f;
        public int NoiseOctaves = 4;
        public float NoisePersistence = 0.5f;
        public float NoiseLacunarity = 2f;
        /// <summary>Surfaces at or above this height are snow-capped; varies with a second noise channel.</summary>
        public float SnowLine = 24f;
        public float SnowLineVariation = 3f;
        /// <summary>Layers of grass beneath the surface block.</summary>
        public int SoilDepth = 4;
        /// <summary>Low, flat columns become sand instead of grass.</summary>
        public float SandMaxSurfaceHeight = 11f;
        public float SandFlatnessThreshold = 0.35f;
    }

    /// <summary>
    /// Heightmap terrain: octaved Perlin noise drives a surface height per column and a
    /// material band decided by height — gray stone deep down, green grass at walkable
    /// heights, white snow on the highest peaks, plus sand in low flat basins and an
    /// unbreakable bedrock floor at y = 0. Column heights are cached for cheap re-queries.
    /// </summary>
    public sealed class HeightmapTerrainGenerator : ITerrainGenerator
    {
        private readonly TerrainProfile _profile;
        private readonly INoiseGenerator _noise;
        private readonly int _maxSurfaceHeight;
        private readonly Dictionary<long, int> _heightCache = new Dictionary<long, int>();

        public HeightmapTerrainGenerator(int seed, TerrainProfile profile, int worldHeight)
            : this(profile, new PerlinNoiseGenerator(seed), worldHeight)
        {
        }

        public HeightmapTerrainGenerator(TerrainProfile profile, INoiseGenerator noise, int worldHeight)
        {
            _profile = profile;
            _noise = noise;
            _maxSurfaceHeight = worldHeight - 2; // keep head room below the build ceiling
        }

        public int HeightAt(int x, int z)
        {
            var key = ((long)x << 32) ^ (uint)z;
            if (_heightCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var t = (_noise.Fbm(x * _profile.NoiseFrequency, z * _profile.NoiseFrequency,
                _profile.NoiseOctaves, _profile.NoisePersistence, _profile.NoiseLacunarity) + 1f) * 0.5f;
            var height = MathfRoundToInt(_profile.BaseSurfaceHeight + t * _profile.SurfaceAmplitude);
            height = Clamp(height, 1, _maxSurfaceHeight);

            _heightCache[key] = height;
            return height;
        }

        public BlockKind GenerateBlock(int x, int y, int z)
        {
            if (y == 0)
            {
                return BlockKind.Bedrock;
            }

            var surface = HeightAt(x, z);
            if (y > surface)
            {
                return BlockKind.Air;
            }

            if (y == surface)
            {
                return SurfaceMaterial(x, z, surface);
            }

            // Below the surface: a shallow soil band, then solid gray stone all the way down.
            if (y > surface - _profile.SoilDepth && SurfaceMaterial(x, z, surface) == BlockKind.Grass)
            {
                return BlockKind.Grass;
            }

            return BlockKind.Stone;
        }

        private BlockKind SurfaceMaterial(int x, int z, int surface)
        {
            var snowLine = _profile.SnowLine
                + _noise.Sample2D(x * 0.05f + 400f, z * 0.05f + 400f) * _profile.SnowLineVariation;
            if (surface >= snowLine)
            {
                return BlockKind.Snow;
            }

            if (surface <= _profile.SandMaxSurfaceHeight)
            {
                var flatness = _noise.Sample2D(x * 0.03f + 100f, z * 0.03f + 100f);
                if (flatness > _profile.SandFlatnessThreshold)
                {
                    return BlockKind.Sand;
                }
            }

            return BlockKind.Grass;
        }

        private static int MathfRoundToInt(float value) => (int)System.Math.Round(value);
        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
    }
}
