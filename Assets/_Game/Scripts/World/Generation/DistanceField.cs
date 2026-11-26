using UnityEngine;

namespace Dino.World.Generation
{
    /// <summary>Two-pass chamfer distance transform on a square grid (metres from the nearest source cell).</summary>
    public static class DistanceField
    {
        public static float[] Compute(bool[] sources, int w, int h, float cell)
        {
            const float Inf = 1e9f;
            float a = cell, b = cell * 1.41421356f;
            var d = new float[w * h];
            for (int i = 0; i < d.Length; i++) d[i] = sources[i] ? 0f : Inf;

            for (int z = 0; z < h; z++)
            {
                int row = z * w;
                for (int x = 0; x < w; x++)
                {
                    float v = d[row + x];
                    if (z > 0)
                    {
                        int up = row - w + x;
                        v = Mathf.Min(v, d[up] + a);
                        if (x > 0) v = Mathf.Min(v, d[up - 1] + b);
                        if (x < w - 1) v = Mathf.Min(v, d[up + 1] + b);
                    }
                    if (x > 0) v = Mathf.Min(v, d[row + x - 1] + a);
                    d[row + x] = v;
                }
            }
            for (int z = h - 1; z >= 0; z--)
            {
                int row = z * w;
                for (int x = w - 1; x >= 0; x--)
                {
                    float v = d[row + x];
                    if (z < h - 1)
                    {
                        int dn = row + w + x;
                        v = Mathf.Min(v, d[dn] + a);
                        if (x > 0) v = Mathf.Min(v, d[dn - 1] + b);
                        if (x < w - 1) v = Mathf.Min(v, d[dn + 1] + b);
                    }
                    if (x < w - 1) v = Mathf.Min(v, d[row + x + 1] + a);
                    d[row + x] = v;
                }
            }
            return d;
        }
    }
}
