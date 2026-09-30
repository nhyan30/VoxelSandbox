using System;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Generation;

namespace VoxelWorld.App
{
    /// <summary>
    /// Every tunable of the game in one serializable place — tweakable in the Inspector
    /// on the Bootstrap object, with defaults that match the task brief (12x12 chunks of
    /// 16x16x48, gray/green/white banding, streaming + pooling).
    /// </summary>
    [Serializable]
    public sealed class GameConfig
    {
        [Header("World")]
        public bool UseFixedSeed = false;
        public int FixedSeed = 12345;
        public int WorldChunksX = 12;
        public int WorldChunksZ = 12;
        public int ChunkSize = 16;
        public int WorldHeight = 48;

        [Header("Streaming & Pooling")]
        public int LoadRadius = 3;
        public int UnloadRadius = 5;
        public int ChunksBuiltPerFrame = 2;
        public int PoolPrewarm = 2000;
        public int PoolMax = 60000;
        public bool CastCubeShadows = false;

        [Header("Terrain")]
        public float BaseSurfaceHeight = 8f;
        public float SurfaceAmplitude = 26f;
        public float NoiseFrequency = 0.013f;
        public int NoiseOctaves = 4;
        public float SnowLine = 24f;
        public float SnowLineVariation = 3f;
        public int SoilDepth = 4;
        public float SandMaxSurfaceHeight = 11f;

        [Header("Mining (seconds)")]
        public float StoneMineSeconds = 1.5f;
        public float GrassMineSeconds = 0.75f;
        public float SnowMineSeconds = 0.3f;
        public float SandMineSeconds = 0.5f;

        [Header("Player")]
        public float WalkSpeed = 6f;
        public float SprintSpeed = 9.5f;
        public float JumpVelocity = 8.5f;
        public float Gravity = 24f;
        /// <summary>Mouse look gain in degrees per pixel of mouse delta.</summary>
        public float MouseSensitivity = 0.15f;
        public float Reach = 6f;
        public float EyeHeight = 1.62f;

        [Header("Visuals")]
        public Color SkyColor = new Color(0.63f, 0.79f, 0.95f);
        public Color GhostValidColor = new Color(0.45f, 1f, 0.55f);
        public Color GhostInvalidColor = new Color(1f, 0.35f, 0.3f);

        [Header("Save / Messages")]
        public string SaveFileName = "voxelsandbox_save.json";
        public string BottomMessage = "You reached the bottom of the world — bedrock cannot be mined.";
        public string CeilingMessage = "You reached the build ceiling — cannot place cubes any higher.";

        public BlockTuning CreateBlockTuning()
        {
            return new BlockTuning
            {
                StoneMineSeconds = StoneMineSeconds,
                GrassMineSeconds = GrassMineSeconds,
                SnowMineSeconds = SnowMineSeconds,
                SandMineSeconds = SandMineSeconds
            };
        }

        public TerrainProfile CreateTerrainProfile()
        {
            return new TerrainProfile
            {
                BaseSurfaceHeight = BaseSurfaceHeight,
                SurfaceAmplitude = SurfaceAmplitude,
                NoiseFrequency = NoiseFrequency,
                NoiseOctaves = NoiseOctaves,
                SnowLine = SnowLine,
                SnowLineVariation = SnowLineVariation,
                SoilDepth = SoilDepth,
                SandMaxSurfaceHeight = SandMaxSurfaceHeight
            };
        }
    }
}
