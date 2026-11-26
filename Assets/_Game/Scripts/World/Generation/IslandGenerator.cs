using System.Collections.Generic;
using UnityEngine;
using static Dino.World.Generation.IslandNoise;

namespace Dino.World.Generation
{
    /// <summary>
    /// Builds the island heightmap, biome map and water from an IslandSpec. Pure data, no scene objects,
    /// so it runs in edit-mode tests. The same steps live in Tools/IslandPreview/island.py for previews.
    /// </summary>
    public static class IslandGenerator
    {
        public static IslandData Generate(IslandSpec spec)
        {
            int res = spec.heightmapResolution;
            int n = res * res;
            var data = new IslandData
            {
                Resolution = res,
                Size = spec.size,
                Heights = new float[n],
                Biomes = new byte[n],
                WaterLevel = new float[n],
                Water = new WaterKind[n],
            };
            var land = new float[n];
            var lakeW = new float[n];
            var riverBand = new bool[n];
            int seed = spec.seed;
            float inv = 1f / (res - 1);

            // Biome seed groups as flat arrays for the hot loop.
            int groups = spec.biomes.Count;
            var groupCode = new byte[groups];
            var groupScale = new float[groups];
            var groupSeeds = new Vector2[groups][];
            for (int g = 0; g < groups; g++)
            {
                groupCode[g] = BiomeIds.ToCode(spec.biomes[g].biomeId);
                groupScale[g] = spec.biomes[g].scale;
                groupSeeds[g] = spec.biomes[g].seeds.ToArray();
            }
            var dist = new float[groups];

            // Steps 1-6: coast, blended biome heights, volcano, lake, coast falloff.
            for (int z = 0; z < res; z++)
            {
                float v = z * inv;
                for (int x = 0; x < res; x++)
                {
                    float u = x * inv;
                    int i = z * res + x;

                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                    float coastR = spec.coastRadius + spec.coastWobble * Signed(u * spec.coastFrequency, v * spec.coastFrequency, seed + 1, 5);
                    land[i] = SmoothStep(0f, spec.coastBlend, coastR - r);

                    float wu = u + spec.biomeWarp * Signed(u * spec.biomeWarpFrequency, v * spec.biomeWarpFrequency, seed + 7);
                    float wv = v + spec.biomeWarp * Signed(u * spec.biomeWarpFrequency, v * spec.biomeWarpFrequency, seed + 9);
                    float dMin = float.MaxValue; int gMin = 0;
                    for (int g = 0; g < groups; g++)
                    {
                        float best = float.MaxValue;
                        var sd = groupSeeds[g];
                        for (int k = 0; k < sd.Length; k++)
                        {
                            float dx = wu - sd[k].x, dy = wv - sd[k].y;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < best) best = d;
                        }
                        dist[g] = best * groupScale[g];
                        if (dist[g] < dMin) { dMin = dist[g]; gMin = g; }
                    }
                    float wSum = 0f, h = 0f;
                    for (int g = 0; g < groups; g++)
                    {
                        float w = Mathf.Exp(-(dist[g] - dMin) / spec.biomeBlend);
                        if (w < 1e-4f) continue;
                        wSum += w;
                        h += w * BaseHeight(groupCode[g], u, v, seed);
                    }
                    h /= wSum;
                    data.Biomes[i] = groupCode[gMin];

                    float dvx = u - spec.volcanoCenter.x, dvy = v - spec.volcanoCenter.y;
                    float dv = Mathf.Sqrt(dvx * dvx + dvy * dvy);
                    h += Mathf.Pow(Mathf.Clamp01(1f - dv / spec.volcanoRadius), 1.6f) * spec.volcanoHeight;
                    h -= Mathf.Pow(Mathf.Clamp01(1f - dv / spec.craterRadius), 0.8f) * spec.craterDepth;

                    float lx = (u - spec.lakeCenter.x) / spec.lakeRadii.x, ly = (v - spec.lakeCenter.y) / spec.lakeRadii.y;
                    float lw = lx * lx + ly * ly + 0.35f * (Fbm(u * 10f, v * 10f, seed + 29) - 0.5f);
                    lakeW[i] = lw;
                    float lakeFloor = spec.lakeLevel - spec.lakeDepth * Mathf.Pow(Mathf.Clamp01(1f - lw), 0.6f) - 0.6f;
                    if (lw < 1f) h = Mathf.Min(h, lakeFloor);
                    else h = Mathf.Min(h, Mathf.Max(spec.lakeLevel + 0.4f + (lw - 1f) * 30f, lakeFloor));

                    h = spec.seabed + (h - spec.seabed) * land[i];
                    data.Heights[i] = h;

                    if (lw < 1.15f) { data.WaterLevel[i] = spec.lakeLevel; data.Water[i] = WaterKind.Lake; }
                    else { data.WaterLevel[i] = float.NaN; data.Water[i] = WaterKind.None; }
                }
            }

            var raw = (float[])data.Heights.Clone();

            // Step 7: rivers and creeks.
            for (int ri = 0; ri < spec.rivers.Count; ri++)
                CarveRiver(spec, data, spec.rivers[ri], ri, raw, land, riverBand);

            // Step 8: ponds and hot springs, levelled to the ground they sit on.
            foreach (var pond in spec.ponds)
            {
                float lvl = data.Sample(data.Heights, pond.center.x, pond.center.y) - 0.4f;
                CarveBowl(data, pond.center, pond.radius, lvl, 1.4f, WaterKind.Pond);
                data.Ponds.Add(new PondInfo { name = pond.name, center = pond.center, radius = pond.radius, level = lvl });
            }

            // Step 9: final biome ids and the rest of the water.
            for (int i = 0; i < n; i++)
            {
                bool inLake = lakeW[i] < 1.25f;
                if (inLake) data.Biomes[i] = BiomeIds.LakeCode;
                if (riverBand[i] && !inLake && data.Biomes[i] != BiomeIds.SwampCode) data.Biomes[i] = BiomeIds.RiverbanksCode;
                if (data.Heights[i] < -0.5f) data.Biomes[i] = BiomeIds.OceanCode;

                if (data.Water[i] == WaterKind.None)
                {
                    if (data.Biomes[i] == BiomeIds.SwampCode) { data.WaterLevel[i] = spec.swampLevel; data.Water[i] = WaterKind.Swamp; }
                    else if (data.Heights[i] < 0f) { data.WaterLevel[i] = 0f; data.Water[i] = WaterKind.Sea; }
                }
            }
            return data;
        }

        static float BaseHeight(byte biome, float u, float v, int seed)
        {
            switch (biome)
            {
                case BiomeIds.MeadowCode: return 10f + 5f * Fbm(u * 9f, v * 9f, seed + 11);
                case BiomeIds.ForestCode: return 14f + 16f * Fbm(u * 6f, v * 6f, seed + 13);
                case BiomeIds.SwampCode: return 0.5f + 2f * Fbm(u * 14f, v * 14f, seed + 17);
                case BiomeIds.HighlandsCode:
                {
                    float hl = 45f + 30f * (1f - Mathf.Abs(2f * Fbm(u * 4f, v * 4f, seed + 19) - 1f));
                    float q = hl / 9f, fl = Mathf.Floor(q);
                    return (fl + SmoothStep(0.35f, 0.65f, q - fl)) * 9f; // ledges
                }
                case BiomeIds.LakeCode: return 40f + 8f * Fbm(u * 7f, v * 7f, seed + 23);
                default: return 10f;
            }
        }

        /// <summary>Catmull-Rom through the control points, 'per' samples per span.</summary>
        public static Vector2[] Smooth(List<Vector2> pts, int per = 24)
        {
            var p = new List<Vector2> { pts[0] };
            p.AddRange(pts);
            p.Add(pts[pts.Count - 1]);
            var outPts = new List<Vector2>();
            for (int i = 1; i < p.Count - 2; i++)
            {
                Vector2 p0 = p[i - 1], p1 = p[i], p2 = p[i + 1], p3 = p[i + 2];
                for (int k = 0; k < per; k++)
                {
                    float t = (float)k / per;
                    outPts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (-p0 + 3f * p1 - 3f * p2 + p3) * (t * t * t)));
                }
            }
            outPts.Add(pts[pts.Count - 1]);
            return outPts.ToArray();
        }

        static void CarveRiver(IslandSpec spec, IslandData data, RiverSpec rv, int ri, float[] raw, float[] land, bool[] riverBand)
        {
            int seed = spec.seed;
            var P = Smooth(rv.points);
            int m = P.Length;
            var S = new float[m];
            for (int k = 1; k < m; k++) S[k] = S[k - 1] + Vector2.Distance(P[k - 1], P[k]);
            float total = S[m - 1];
            for (int k = 1; k < m; k++) S[k] /= total;

            var L = new float[m];
            if (rv.hasWaterfall)
            {
                float a = rv.waterfallRange.x, b = rv.waterfallRange.y;
                for (int k = 0; k < m; k++)
                {
                    float s = S[k];
                    if (s < a) L[k] = rv.topLevel - (rv.topLevel - rv.waterfallTopLevel) * Mathf.Clamp01(s / a);
                    else if (s < b) L[k] = rv.waterfallTopLevel + (rv.belowFallLevel - rv.waterfallTopLevel) * SmoothStep(a, b, s);
                    else L[k] = (rv.belowFallLevel - 0.3f) * Mathf.Pow(1f - Mathf.Clamp01((s - b) / (1f - b)), 1.15f) + 0.3f;
                }
            }
            else
            {
                // Creeks follow the ground downhill: never flow uphill, never below sea level.
                float run = float.MaxValue;
                for (int k = 0; k < m; k++)
                {
                    run = Mathf.Min(run, data.Sample(raw, P[k].x, P[k].y) - 1.3f);
                    L[k] = Mathf.Max(run, 0.25f);
                }
            }

            var path = new RiverPath
            {
                name = rv.name, points = P, along = S, level = L, halfWidth = new float[m],
                hasWaterfall = rv.hasWaterfall, waterfallRange = rv.waterfallRange,
            };
            for (int k = 0; k < m; k++) path.halfWidth[k] = HalfWidth(rv, S[k], ri, seed);
            data.Rivers.Add(path);

            // Only cells near the river can be affected (the bank term rises steeply past ~120 m).
            int res = data.Resolution;
            float margin = 170f / spec.size;
            Vector2 min = P[0], max = P[0];
            foreach (var q in P) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
            int x0 = Mathf.Max(0, Mathf.FloorToInt((min.x - margin) * (res - 1)));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((max.x + margin) * (res - 1)));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((min.y - margin) * (res - 1)));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((max.y + margin) * (res - 1)));
            float inv = 1f / (res - 1);
            float fallEnd = rv.hasWaterfall ? rv.waterfallRange.y : -1f;

            for (int z = z0; z <= z1; z++)
            {
                float v = z * inv;
                for (int x = x0; x <= x1; x++)
                {
                    float u = x * inv;
                    int i = z * res + x;
                    if (land[i] <= 0.02f) continue;

                    // closest point on the polyline
                    float best = float.MaxValue, bestS = 0f;
                    for (int k = 0; k < m - 1; k++)
                    {
                        Vector2 a = P[k], ab = P[k + 1] - a;
                        float l2 = ab.sqrMagnitude;
                        if (l2 < 1e-12f) continue;
                        float t = Mathf.Clamp01(((u - a.x) * ab.x + (v - a.y) * ab.y) / l2);
                        float px = a.x + t * ab.x - u, py = a.y + t * ab.y - v;
                        float d2 = px * px + py * py;
                        if (d2 < best) { best = d2; bestS = S[k] + t * (S[k + 1] - S[k]); }
                    }
                    if (best > margin * margin) continue;
                    float dm = Mathf.Sqrt(best) * spec.size;
                    float s = bestS;
                    float lw = Interp(s, S, L);
                    float hw = HalfWidth(rv, s, ri, seed);
                    float depth = rv.depth.x + (rv.depth.y - rv.depth.x) * Fbm(s * 25f, ri * 3f + 0.5f, seed + 37);
                    float bed = lw - depth;
                    float carve;
                    if (dm < hw)
                    {
                        float t = dm / hw;
                        carve = bed + t * t * (lw + 0.4f - bed);
                    }
                    else
                    {
                        float over = Mathf.Max(dm - hw - 35f, 0f);
                        carve = lw + 0.4f + (dm - hw) * 0.22f + over * over * 0.04f;
                    }
                    if (carve < data.Heights[i]) data.Heights[i] = carve;

                    if (dm < hw + 1.5f && (data.Water[i] == WaterKind.None || lw < data.WaterLevel[i]))
                    {
                        data.WaterLevel[i] = lw;
                        data.Water[i] = WaterKind.River;
                    }
                    if (rv.hasWaterfall ? (dm < hw + 30f && s > fallEnd) : dm < hw + 12f) riverBand[i] = true;
                }
            }
        }

        static float HalfWidth(RiverSpec rv, float s, int ri, int seed)
            => rv.halfWidth.x + (rv.halfWidth.y - rv.halfWidth.x) * s + 1.5f * Signed(s * 30f, ri * 7f, seed + 31, 2);

        static float Interp(float s, float[] xs, float[] ys)
        {
            if (s <= xs[0]) return ys[0];
            int last = xs.Length - 1;
            if (s >= xs[last]) return ys[last];
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (xs[mid] <= s) lo = mid; else hi = mid;
            }
            float t = (s - xs[lo]) / Mathf.Max(1e-9f, xs[hi] - xs[lo]);
            return Mathf.Lerp(ys[lo], ys[hi], t);
        }

        /// <summary>Carves a round bowl (pond, spring, puddle) whose water surface sits at 'level'.</summary>
        public static void CarveBowl(IslandData data, Vector2 centerNorm, float radius, float level, float depth, WaterKind kind)
        {
            int res = data.Resolution;
            float cell = data.Cell;
            float cx = centerNorm.x * data.Size, cz = centerNorm.y * data.Size;
            int reach = Mathf.CeilToInt(radius * 3f / cell) + 1;
            int ix = Mathf.RoundToInt(cx / cell), iz = Mathf.RoundToInt(cz / cell);
            for (int z = Mathf.Max(0, iz - reach); z <= Mathf.Min(res - 1, iz + reach); z++)
            for (int x = Mathf.Max(0, ix - reach); x <= Mathf.Min(res - 1, ix + reach); x++)
            {
                float dx = x * cell - cx, dz = z * cell - cz;
                float dd = Mathf.Sqrt(dx * dx + dz * dz);
                if (dd >= radius * 3f) continue;
                int i = z * res + x;
                float t = dd / radius;
                float target = dd < radius
                    ? level - depth * Mathf.Clamp01(1f - t * t)
                    : level + 0.3f + Mathf.Max(dd - radius, 0f) * 0.25f;
                if (target < data.Heights[i]) data.Heights[i] = target;
                if (dd < radius + 1f) { data.WaterLevel[i] = level; data.Water[i] = kind; }
            }
        }
    }
}
