using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dino.UI
{
    /// <summary>
    /// Crisp UI shapes and icons drawn into textures at startup, so the game needs no image files:
    /// rounded panels (9-sliced), and heart / bolt / leaf / drop / pause / star icons.
    /// Artists can later replace any of these with real sprites.
    /// </summary>
    public static class UiSprites
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>White rounded rectangle, 9-sliced so it stretches without squashing its corners.</summary>
        public static Sprite Rounded(int radius = 24)
        {
            string key = "rounded" + radius;
            if (_cache.TryGetValue(key, out var s) && s) return s;
            int n = radius * 2 + 8;
            float half = n * 0.5f;
            s = Draw(key, n, (x, y) =>
            {
                // Signed distance to a rounded box.
                float qx = Mathf.Abs(x - half) - (half - radius);
                float qy = Mathf.Abs(y - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                return -d;
            }, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            return s;
        }

        public static Sprite Heart => Icon("heart", (u, v) =>
        {
            // Classic implicit heart curve, (x^2 + y^2 - 1)^3 - x^2 y^3 < 0
            float x = u * 1.25f, y = v * 1.25f + 0.15f;
            float a = x * x + y * y - 1f;
            return -(a * a * a - x * x * y * y * y) * 2.5f;
        });

        public static Sprite Drop => Icon("drop", (u, v) =>
        {
            // Circle at the bottom, tapering to a point at the top.
            float cy = -0.3f, r = 0.55f;
            float circle = r - new Vector2(u, v - cy).magnitude;
            float t = Mathf.InverseLerp(cy, 0.92f, v);
            float cone = v >= cy && v <= 0.92f ? r * (1f - t) * Mathf.Lerp(1f, 0.75f, t) - Mathf.Abs(u) : -1f;
            return Mathf.Max(circle, cone);
        });

        public static Sprite Bolt => Icon("bolt", (u, v) => InPolygon(u, v, BoltPoints) ? 1f : -1f, hardEdge: true);
        static readonly Vector2[] BoltPoints =
        {
            new Vector2(0.15f, 0.95f), new Vector2(-0.5f, -0.1f), new Vector2(-0.05f, -0.1f),
            new Vector2(-0.2f, -0.95f), new Vector2(0.5f, 0.15f), new Vector2(0.05f, 0.15f), new Vector2(0.4f, 0.95f),
        };

        public static Sprite Leaf => Icon("leaf", (u, v) =>
        {
            // Two overlapping circles make a leaf shape, rotated 45 degrees.
            float x = (u + v) * 0.7071f, y = (v - u) * 0.7071f;
            float r = 1.05f, off = 0.62f;
            float d1 = r - new Vector2(x, y - off).magnitude;
            float d2 = r - new Vector2(x, y + off).magnitude;
            return Mathf.Min(d1, d2);
        });

        public static Sprite Pause => Icon("pause", (u, v) =>
        {
            float bar = 0.2f - Mathf.Abs(Mathf.Abs(u) - 0.36f);
            float height = 0.7f - Mathf.Abs(v);
            return Mathf.Min(bar, height);
        });

        public static Sprite Star => Icon("star", (u, v) => InPolygon(u, v, StarPoints) ? 1f : -1f, hardEdge: true);
        static readonly Vector2[] StarPoints = MakeStar(5, 0.95f, 0.42f);

        public static Sprite Lock => Icon("lock", (u, v) =>
        {
            float body = Mathf.Min(0.62f - Mathf.Abs(u), 0.42f - Mathf.Abs(v + 0.38f));
            float ringDist = new Vector2(u, Mathf.Max(0f, v - 0.2f)).magnitude;
            float shackle = v > 0.0f ? 0.11f - Mathf.Abs(ringDist - 0.42f) : -1f;
            return Mathf.Max(body, shackle);
        });

        // ---------- drawing ----------

        static Vector2[] MakeStar(int points, float outer, float inner)
        {
            var pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return pts;
        }

        static bool InPolygon(float x, float y, Vector2[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > y) != (p[j].y > y) &&
                    x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Icon from a shape function over u,v in -1..1 (v up). Positive = inside.</summary>
        static Sprite Icon(string key, Func<float, float, float> shape, bool hardEdge = false)
        {
            if (_cache.TryGetValue(key, out var s) && s) return s;
            const int n = 96;
            const int ss = 3; // supersampling for smooth edges
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float u = ((x + (sx + 0.5f) / ss) / n) * 2f - 1f;
                    float v = ((y + (sy + 0.5f) / ss) / n) * 2f - 1f;
                    if (shape(u * 1.08f, v * 1.08f) > 0f) hits++;
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s.name = key;
            _cache[key] = s;
            return s;
        }

        /// <summary>Sprite from a pixel-space distance function (positive inside, in pixels).</summary>
        static Sprite Draw(string key, int n, Func<float, float, float> distance, Vector4 border)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = distance(x + 0.5f, y + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            _cache[key] = s;
            return s;
        }
    }

    /// <summary>One palette for every screen, so the game looks like one piece.</summary>
    public static class UiTheme
    {
        public static readonly Color Ink = new Color(0.07f, 0.10f, 0.09f);
        public static readonly Color Panel = new Color(0.07f, 0.10f, 0.09f, 0.72f);
        public static readonly Color PanelSolid = new Color(0.10f, 0.15f, 0.13f, 0.97f);
        public static readonly Color Track = new Color(0f, 0f, 0f, 0.5f);
        public static readonly Color Text = new Color(1f, 0.98f, 0.93f);
        public static readonly Color TextDim = new Color(1f, 0.98f, 0.93f, 0.7f);
        public static readonly Color Health = new Color(0.93f, 0.30f, 0.34f);
        public static readonly Color Stamina = new Color(0.98f, 0.78f, 0.22f);
        public static readonly Color Food = new Color(0.45f, 0.80f, 0.33f);
        public static readonly Color Water = new Color(0.27f, 0.62f, 0.96f);
        public static readonly Color Xp = new Color(0.69f, 0.52f, 0.98f);
        public static readonly Color Growth = new Color(1f, 0.62f, 0.26f);
        public static readonly Color Good = new Color(0.30f, 0.68f, 0.38f);
        public static readonly Color Warn = new Color(0.80f, 0.33f, 0.30f);
        public static readonly Color Neutral = new Color(0.30f, 0.38f, 0.45f);
    }
}
