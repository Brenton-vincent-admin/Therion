using UnityEditor;
using UnityEngine;

namespace Dino.Species.Editor
{
    /// <summary>
    /// Generates the shared skin pattern masks (stripes, spots, bands, speckles, tiger, giraffe,
    /// leopard, starry) as one Texture2DArray, procedurally, so they cost nothing and have no license.
    /// Slice i = SkinPattern (i + 1). Assign the result to the DinoSkin material's Pattern Masks.
    /// </summary>
    public static class PatternMaskGenerator
    {
        public const string OutputPath = "Assets/_Game/Art/Textures/SkinPatternMasks.asset";
        const int Size = 256;

        [MenuItem("Dino/Species/Generate Skin Pattern Masks")]
        public static void Generate()
        {
            int count = SkinPalette.PatternCount - 1; // no slice for None
            var array = new Texture2DArray(Size, Size, count, TextureFormat.R8, true, true)
            {
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "SkinPatternMasks",
            };
            var pixels = new Color32[Size * Size];
            for (int slice = 0; slice < count; slice++)
            {
                var pattern = (SkinPattern)(slice + 1);
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float v = Sample(pattern, x / (float)Size, y / (float)Size);
                    byte b = (byte)(Mathf.Clamp01(v) * 255f);
                    pixels[y * Size + x] = new Color32(b, b, b, 255);
                }
                array.SetPixels32(pixels, slice, 0);
            }
            array.Apply(true, false);

            System.IO.Directory.CreateDirectory("Assets/_Game/Art/Textures");
            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(OutputPath);
            if (existing != null) { EditorUtility.CopySerialized(array, existing); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(array, OutputPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Species] Wrote {count} pattern masks to {OutputPath}");
        }

        // All patterns tile seamlessly over 0..1.
        static float Sample(SkinPattern p, float u, float v)
        {
            const float Tau = Mathf.PI * 2f;
            switch (p)
            {
                case SkinPattern.Stripes:
                    return Smooth(Mathf.Sin((u * 6f + Mathf.Sin(v * Tau) * 0.15f) * Tau), 0.3f);
                case SkinPattern.Bands:
                    return Smooth(Mathf.Sin(v * 3f * Tau), 0.55f);
                case SkinPattern.Tiger:
                {
                    float wobble = Mathf.Sin(v * Tau * 2f) * 0.08f + Noise(u * 4f, v * 4f, 4) * 0.12f;
                    return Smooth(Mathf.Sin((u * 5f + wobble * 5f) * Tau), 0.6f) * Smooth(Noise(u * 2f, v * 8f, 2), -0.1f);
                }
                case SkinPattern.Spots:      return Cells(u, v, 6, 0.28f, 0.0f, 1);
                case SkinPattern.Speckles:   return Cells(u, v, 18, 0.18f, 0.0f, 2);
                case SkinPattern.Leopard:    return Cells(u, v, 7, 0.36f, 0.2f, 3);   // rings
                case SkinPattern.Giraffe:    return 1f - CellEdges(u, v, 5, 0.06f, 4);
                case SkinPattern.Starry:     return Cells(u, v, 12, 0.10f, 0.0f, 5) + Cells(u, v, 5, 0.05f, 0f, 6);
                default: return 0f;
            }
        }

        static float Smooth(float s, float threshold) => Mathf.SmoothStep(0f, 1f, (s - threshold) * 4f + 0.5f);

        static Vector2 Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            float a = (h & 0xFFFF) / 65535f, b = ((h >> 16) & 0xFFFF) / 65535f;
            return new Vector2(a, b);
        }

        /// <summary>Distance-to-nearest-cell-point pattern on a wrapping grid. ring > 0 makes hollow rings.</summary>
        static float Cells(float u, float v, int n, float radius, float ring, int seed)
        {
            float gx = u * n, gy = v * n;
            int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
            float best = 10f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                var j = Hash(((x % n) + n) % n, ((y % n) + n) % n, seed) * 0.8f + new Vector2(0.1f, 0.1f);
                float d = Vector2.Distance(new Vector2(gx, gy), new Vector2(x + j.x, y + j.y));
                best = Mathf.Min(best, d);
            }
            float spot = 1f - Mathf.SmoothStep(radius * 0.8f, radius, best);
            if (ring > 0f) spot *= Mathf.SmoothStep(ring * 0.8f, ring, best);
            return spot;
        }

        /// <summary>Thin lines between cells (giraffe network).</summary>
        static float CellEdges(float u, float v, int n, float width, int seed)
        {
            float gx = u * n, gy = v * n;
            int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
            float f1 = 10f, f2 = 10f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                var j = Hash(((x % n) + n) % n, ((y % n) + n) % n, seed) * 0.8f + new Vector2(0.1f, 0.1f);
                float d = Vector2.Distance(new Vector2(gx, gy), new Vector2(x + j.x, y + j.y));
                if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
            }
            return 1f - Mathf.SmoothStep(0f, width * n * 0.1f + 0.05f, f2 - f1);
        }

        /// <summary>Cheap tileable value noise.</summary>
        static float Noise(float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float H(int a, int b) => Hash(((a % period) + period) % period, ((b % period) + period) % period, 99).x * 2f - 1f;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), sx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), sx), sy);
        }
    }
}
