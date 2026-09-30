namespace VoxelWorld.Core.Generation
{
    /// <summary>Abstraction over procedural noise so generators stay testable.</summary>
    public interface INoiseGenerator
    {
        /// <summary>2D noise sample in the range [-1, 1].</summary>
        float Sample2D(float x, float y);

        /// <summary>Fractal Brownian motion: several octaves of <see cref="Sample2D"/> summed with falling amplitude, range roughly [-1, 1].</summary>
        float Fbm(float x, float y, int octaves, float persistence, float lacunarity);
    }
}
