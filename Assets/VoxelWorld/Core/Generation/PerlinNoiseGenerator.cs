using System;

namespace VoxelWorld.Core.Generation
{
    /// <summary>
    /// Classic improved Perlin noise with a seedable permutation table. Deterministic
    /// for a given seed, which the save system relies on (regenerate terrain, replay deltas).
    /// </summary>
    public sealed class PerlinNoiseGenerator : INoiseGenerator
    {
        private const int TableSize = 256;

        private readonly byte[] _permutation = new byte[TableSize * 2];

        public PerlinNoiseGenerator(int seed)
        {
            var baseTable = new byte[TableSize];
            for (var i = 0; i < TableSize; i++)
            {
                baseTable[i] = (byte)i;
            }

            // Fisher-Yates shuffle driven by a seeded PRNG keeps world generation reproducible.
            var random = new Random(seed);
            for (var i = TableSize - 1; i > 0; i--)
            {
                var swap = random.Next(i + 1);
                (baseTable[i], baseTable[swap]) = (baseTable[swap], baseTable[i]);
            }

            for (var i = 0; i < TableSize * 2; i++)
            {
                _permutation[i] = baseTable[i & (TableSize - 1)];
            }
        }

        public float Sample2D(float x, float y)
        {
            var xi = (int)Math.Floor(x) & (TableSize - 1);
            var yi = (int)Math.Floor(y) & (TableSize - 1);
            var xf = x - (float)Math.Floor(x);
            var yf = y - (float)Math.Floor(y);

            var u = Fade(xf);
            var v = Fade(yf);

            var aa = _permutation[_permutation[xi] + yi];
            var ab = _permutation[_permutation[xi] + yi + 1];
            var ba = _permutation[_permutation[xi + 1] + yi];
            var bb = _permutation[_permutation[xi + 1] + yi + 1];

            var x1 = MathfLerp(GradientDot(aa, xf, yf), GradientDot(ba, xf - 1f, yf), u);
            var x2 = MathfLerp(GradientDot(ab, xf, yf - 1f), GradientDot(bb, xf - 1f, yf - 1f), u);
            return MathfLerp(x1, x2, v); // [-1, 1]
        }

        public float Fbm(float x, float y, int octaves, float persistence, float lacunarity)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var sum = 0f;
            var maxAmplitude = 0f;

            for (var i = 0; i < octaves; i++)
            {
                sum += Sample2D(x * frequency, y * frequency) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return sum / maxAmplitude;
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float GradientDot(int hash, float dx, float dy)
        {
            switch (hash & 7)
            {
                case 0: return dx + dy;
                case 1: return -dx + dy;
                case 2: return dx - dy;
                case 3: return -dx - dy;
                case 4: return dx;
                case 5: return -dx;
                case 6: return dy;
                default: return -dy;
            }
        }

        private static float MathfLerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
