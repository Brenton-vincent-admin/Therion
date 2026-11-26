using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dino.World.Generation
{
    /// <summary>
    /// Decides where nests, drinking puddles, food and decor go on a generated island, following the
    /// design doc's rules: water every 20-30 s of walking, a nest every 30-60 s and in every biome.
    /// Works on a coarse grid (placementStep samples per cell). Adds props to data.Props and carves puddles.
    /// </summary>
    public static class IslandPlacer
    {
        static readonly string[] NestOrder =
            { BiomeIds.Meadow, BiomeIds.Riverbanks, BiomeIds.Forest, BiomeIds.Swamp, BiomeIds.Highlands, BiomeIds.Lake };

        class Grid
        {
            public int W, Step;
            public float Cell;
            public float[] H, Slope;
            public bool[] Water, Drinkable, Land, Walk;
            public byte[] Biome;
        }

        public static void Place(IslandSpec spec, IslandData data)
        {
            data.Props.Clear();
            var g = BuildGrid(spec, data);

            var nests = PlaceNests(spec, data, g);
            PlacePuddles(spec, data, g);
            g = BuildGrid(spec, data); // puddles changed heights and water

            var occupied = new SpatialHash(4f);
            foreach (var p in data.Props) occupied.Add(p.position, 6f);
            PlaceGreatTree(spec, data, occupied);

            foreach (var rule in spec.food) Scatter(spec, data, g, rule, occupied, nests);
            foreach (var rule in spec.decor) Scatter(spec, data, g, rule, occupied, nests);
        }

        static Grid BuildGrid(IslandSpec spec, IslandData data)
        {
            int step = Mathf.Max(1, spec.placementStep);
            int w = (data.Resolution - 1) / step + 1;
            var g = new Grid
            {
                W = w, Step = step, Cell = data.Cell * step,
                H = new float[w * w], Slope = new float[w * w],
                Water = new bool[w * w], Drinkable = new bool[w * w], Land = new bool[w * w], Walk = new bool[w * w],
                Biome = new byte[w * w],
            };
            for (int z = 0; z < w; z++)
            for (int x = 0; x < w; x++)
            {
                int c = z * w + x;
                int fx = x * step, fz = z * step;
                int i = data.Index(fx, fz);
                g.H[c] = data.Heights[i];
                g.Slope[c] = data.SlopeAt(fx, fz);
                g.Water[c] = data.IsWater(i);
                g.Drinkable[c] = data.IsDrinkable(i);
                g.Biome[c] = data.Biomes[i];
                g.Land[c] = !g.Water[c] && g.H[c] > 0.4f;
                g.Walk[c] = g.Land[c] && g.Slope[c] < spec.maxWalkSlope && g.H[c] < spec.maxCoverageHeight;
            }
            return g;
        }

        // ---------------- nests ----------------

        static List<int> PlaceNests(IslandSpec spec, IslandData data, Grid g)
        {
            int w = g.W;
            var nearWater = DistanceField.Compute(g.Drinkable, w, w, g.Cell);
            var flat = new float[w * w];
            for (int c = 0; c < flat.Length; c++)
                flat[c] = g.Land[c] && g.Slope[c] < 12f && nearWater[c] > 8f
                    ? -g.Slope[c] - 0.02f * Mathf.Abs(nearWater[c] - 30f)
                    : float.NegativeInfinity;

            var nests = new List<int>();
            foreach (var biomeId in NestOrder)
            {
                byte code = BiomeIds.ToCode(biomeId);
                int best = -1; float bestScore = float.NegativeInfinity;
                if (biomeId == BiomeIds.Riverbanks && nests.Count > 0)
                {
                    // the riverbank nest sits as close to the meadow nest as possible
                    int m = nests[0]; int mx = m % w, mz = m / w;
                    for (int c = 0; c < flat.Length; c++)
                    {
                        if (g.Biome[c] != code || float.IsNegativeInfinity(flat[c])) continue;
                        float s = flat[c] - 0.05f * Mathf.Sqrt((c % w - mx) * (c % w - mx) + (c / w - mz) * (c / w - mz));
                        if (s > bestScore) { bestScore = s; best = c; }
                    }
                }
                else
                {
                    var group = spec.biomes.Find(b => b.biomeId == biomeId);
                    if (group == null || group.seeds.Count == 0) continue;
                    int sx = Mathf.RoundToInt(group.seeds[0].x * (w - 1)), sz = Mathf.RoundToInt(group.seeds[0].y * (w - 1));
                    best = BestNearby(flat, w, sx, sz, Mathf.CeilToInt(120f / g.Cell), c => g.Biome[c] == code);
                }
                if (best >= 0 && !float.IsNegativeInfinity(flat[best])) nests.Add(best);
            }

            var src = new bool[w * w];
            foreach (var c in nests) src[c] = true;
            var extra = Cover(src, g.Walk, w, g.Cell, spec.nestSpacing,
                far => BestNearby(flat, w, far % w, far / w, Mathf.CeilToInt(40f / g.Cell), null), out data.WorstNestDistance);
            nests.AddRange(extra);

            for (int k = 0; k < nests.Count; k++)
            {
                Vector3 pos = CellToWorld(data, g, nests[k]);
                data.Props.Add(new WorldProp
                {
                    kind = WorldPropKind.Nest, position = pos, biome = data.BiomeAt(pos.x, pos.z),
                    yaw = Hash(k, 11) * 360f, scale = 1f, isMain = k == 0,
                });
            }
            return nests;
        }

        // ---------------- puddles ----------------

        static void PlacePuddles(IslandSpec spec, IslandData data, Grid g)
        {
            int w = g.W;
            var score = new float[w * w];
            for (int c = 0; c < score.Length; c++)
                score[c] = g.Land[c] && g.Slope[c] < 15f ? -g.Slope[c] : float.NegativeInfinity;
            var puddles = Cover((bool[])g.Drinkable.Clone(), g.Walk, w, g.Cell, spec.waterSpacing,
                far => BestNearby(score, w, far % w, far / w, Mathf.CeilToInt(20f / g.Cell), null), out data.WorstWaterDistance);

            for (int k = 0; k < puddles.Count; k++)
            {
                int c = puddles[k];
                Vector3 pos = CellToWorld(data, g, c);
                float radius = 3.5f + Hash(k, 5) * 2f;
                var center = new Vector2(pos.x / data.Size, pos.z / data.Size);
                IslandGenerator.CarveBowl(data, center, radius, pos.y - 0.12f, 0.55f, WaterKind.Puddle);
                pos.y -= 0.12f; // water surface
                data.Props.Add(new WorldProp
                {
                    kind = WorldPropKind.Puddle, position = pos, biome = data.BiomeAt(pos.x, pos.z), scale = radius,
                });
            }
        }

        // ---------------- great tree ----------------

        static void PlaceGreatTree(IslandSpec spec, IslandData data, SpatialHash occupied)
        {
            float x = spec.greatTree.x * data.Size, z = spec.greatTree.y * data.Size;
            var pos = new Vector3(x, data.SampleHeight(x, z), z);
            data.Props.Add(new WorldProp { kind = WorldPropKind.GreatTree, position = pos, biome = data.BiomeAt(x, z), scale = 1f, isMain = true });
            occupied.Add(pos, 14f);
        }

        // ---------------- food and decor ----------------

        static void Scatter(IslandSpec spec, IslandData data, Grid g, FoodRule rule, SpatialHash occupied, List<int> nests)
        {
            if (rule.spacing <= 0.5f) return;
            byte code = BiomeIds.ToCode(rule.biomeId);
            bool fish = rule.kind == WorldPropKind.FishSpot;
            var rng = new System.Random(spec.seed * 7919 + (int)rule.kind * 131 + code * 17);
            var same = new SpatialHash(rule.spacing);
            float size = data.Size;
            int attempts = Mathf.CeilToInt(size * size / (rule.spacing * rule.spacing) * 2.5f);
            float personal = PersonalSpace(rule.kind);

            for (int a = 0; a < attempts; a++)
            {
                float x = (float)rng.NextDouble() * size, z = (float)rng.NextDouble() * size;
                int ix = Mathf.Clamp(Mathf.RoundToInt(x / data.Cell), 0, data.Resolution - 1);
                int iz = Mathf.Clamp(Mathf.RoundToInt(z / data.Cell), 0, data.Resolution - 1);
                int i = data.Index(ix, iz);
                if (data.Biomes[i] != code) continue;
                Vector3 pos;
                if (fish)
                {
                    // fish live in drinkable water at least 0.6 m deep, close to a shore
                    if (!data.IsDrinkable(i) || data.WaterLevel[i] - data.Heights[i] < 0.6f) continue;
                    if (!NearLand(data, ix, iz, Mathf.CeilToInt(8f / data.Cell))) continue;
                    pos = new Vector3(x, data.WaterLevel[i], z);
                }
                else
                {
                    if (data.Water[i] != WaterKind.None && data.Heights[i] < data.WaterLevel[i] + 0.3f) continue;
                    if (data.Heights[i] < 0.6f || data.SlopeAt(ix, iz) > MaxSlope(rule.kind)) continue;
                    pos = new Vector3(x, data.SampleHeight(x, z), z);
                }
                if (same.AnyWithin(pos, rule.spacing)) continue;
                if (!fish && occupied.AnyWithin(pos, personal)) continue;

                same.Add(pos, 0f);
                if (!fish) occupied.Add(pos, personal);
                data.Props.Add(new WorldProp
                {
                    kind = rule.kind, position = pos, biome = code,
                    yaw = (float)rng.NextDouble() * 360f, scale = 0.8f + (float)rng.NextDouble() * 0.45f,
                });
            }
        }

        static float PersonalSpace(WorldPropKind k)
        {
            switch (k)
            {
                case WorldPropKind.Tree: case WorldPropKind.Pine: case WorldPropKind.SwampTree: case WorldPropKind.TallLeafTree: return 3.5f;
                case WorldPropKind.Rock: case WorldPropKind.Log: return 3f;
                default: return 2.5f;
            }
        }

        static float MaxSlope(WorldPropKind k) => k == WorldPropKind.Rock ? 45f : k == WorldPropKind.Pine ? 35f : 28f;

        static bool NearLand(IslandData data, int ix, int iz, int r)
        {
            int res = data.Resolution;
            for (int dz = -r; dz <= r; dz += Mathf.Max(1, r / 3))
            for (int dx = -r; dx <= r; dx += Mathf.Max(1, r / 3))
            {
                int x = ix + dx, z = iz + dz;
                if (x < 0 || z < 0 || x >= res || z >= res) continue;
                if (!data.IsWater(data.Index(x, z))) return true;
            }
            return false;
        }

        // ---------------- helpers ----------------

        /// <summary>
        /// Greedy coverage: while some eligible cell is farther than 'target' metres from every source,
        /// put a new source near the farthest one (chosen by 'pick'). Returns the added cells.
        /// </summary>
        static List<int> Cover(bool[] sources, bool[] eligible, int w, float cell, float target, Func<int, int> pick, out float worst, int maxAdd = 80)
        {
            var added = new List<int>();
            worst = 0f;
            for (int iter = 0; iter <= maxAdd; iter++)
            {
                var d = DistanceField.Compute(sources, w, w, cell);
                int far = -1; float farD = -1f;
                for (int c = 0; c < d.Length; c++)
                    if (eligible[c] && d[c] > farD) { farD = d[c]; far = c; }
                worst = Mathf.Max(0f, farD);
                if (far < 0 || farD <= target || iter == maxAdd) break;
                int chosen = pick(far);
                if (chosen < 0 || sources[chosen]) chosen = far;
                added.Add(chosen);
                sources[chosen] = true;
            }
            return added;
        }

        /// <summary>Highest-scoring cell within 'radius' cells of (cx, cz), or -1.</summary>
        static int BestNearby(float[] score, int w, int cx, int cz, int radius, Func<int, bool> filter)
        {
            int best = -1; float bestScore = float.NegativeInfinity;
            for (int z = Mathf.Max(0, cz - radius); z <= Mathf.Min(w - 1, cz + radius); z++)
            for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(w - 1, cx + radius); x++)
            {
                int c = z * w + x;
                if (filter != null && !filter(c)) continue;
                if (score[c] > bestScore) { bestScore = score[c]; best = c; }
            }
            return best;
        }

        static Vector3 CellToWorld(IslandData data, Grid g, int c)
        {
            int fx = (c % g.W) * g.Step, fz = (c / g.W) * g.Step;
            return new Vector3(fx * data.Cell, data.Heights[data.Index(fx, fz)], fz * data.Cell);
        }

        static float Hash(int k, int salt) => IslandNoise.Hash01(k, salt, 4242);

        /// <summary>Tiny 2D spatial hash of points with a keep-out radius each.</summary>
        class SpatialHash
        {
            readonly float _cell;
            readonly Dictionary<long, List<Vector4>> _map = new Dictionary<long, List<Vector4>>();
            float _maxRadius;

            public SpatialHash(float cell) { _cell = Mathf.Max(1f, cell); }

            long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            public void Add(Vector3 p, float radius)
            {
                int x = Mathf.FloorToInt(p.x / _cell), z = Mathf.FloorToInt(p.z / _cell);
                long k = Key(x, z);
                if (!_map.TryGetValue(k, out var l)) _map[k] = l = new List<Vector4>();
                l.Add(new Vector4(p.x, p.y, p.z, radius));
                _maxRadius = Mathf.Max(_maxRadius, radius);
            }

            /// <summary>True if any stored point is closer than max(r, its own radius).</summary>
            public bool AnyWithin(Vector3 p, float r)
            {
                float reach = Mathf.Max(r, _maxRadius);
                int span = Mathf.CeilToInt(reach / _cell);
                int cx = Mathf.FloorToInt(p.x / _cell), cz = Mathf.FloorToInt(p.z / _cell);
                for (int z = cz - span; z <= cz + span; z++)
                for (int x = cx - span; x <= cx + span; x++)
                {
                    if (!_map.TryGetValue(Key(x, z), out var l)) continue;
                    foreach (var q in l)
                    {
                        float dx = q.x - p.x, dz = q.z - p.z;
                        float lim = Mathf.Max(r, q.w);
                        if (dx * dx + dz * dz < lim * lim) return true;
                    }
                }
                return false;
            }
        }
    }
}
