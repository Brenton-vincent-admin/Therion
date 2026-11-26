using UnityEngine;

namespace Dino.World.Generation
{
    /// <summary>
    /// Deterministic value noise with an integer hash, so the island is identical on every machine and
    /// matches the Python preview in Tools/IslandPreview/island.py.
    /// </summary>
    public static class IslandNoise
    {
        public static float Hash01(int ix, int iy, int seed)
        {
            unchecked
            {
                uint h = ((uint)ix * 73856093u) ^ ((uint)iy * 19349663u) ^ ((uint)seed * 83492791u);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        public static float Value(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash01(x0, y0, seed), b = Hash01(x0 + 1, y0, seed);
            float c = Hash01(x0, y0 + 1, seed), d = Hash01(x0 + 1, y0 + 1, seed);
            float ab = a + (b - a) * fx, cd = c + (d - c) * fx;
            return ab + (cd - ab) * fy;
        }

        /// <summary>Fractal value noise, 0..1 (mostly 0.25..0.75).</summary>
        public static float Fbm(float x, float y, int seed, int octaves = 4)
        {
            float s = 0f, amp = 0.5f, f = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                s += amp * Value(x * f, y * f, seed + o * 101);
                norm += amp; amp *= 0.5f; f *= 2f;
            }
            return s / norm;
        }

        /// <summary>Fbm re-centred to roughly -1..1.</summary>
        public static float Signed(float x, float y, int seed, int octaves = 4)
            => Mathf.Clamp((Fbm(x, y, seed, octaves) - 0.5f) * 3.5f, -1.5f, 1.5f);

        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
