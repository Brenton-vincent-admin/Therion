using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dino.World.Generation
{
    [Serializable]
    public class BiomeSeedGroup
    {
        public string biomeId;
        [Tooltip("Below 1 makes the biome grow bigger than its neighbours.")]
        public float scale = 1f;
        [Tooltip("Normalised map positions (0..1, x = east, y = north) the biome grows around.")]
        public List<Vector2> seeds = new List<Vector2>();
    }

    [Serializable]
    public class RiverSpec
    {
        public string name;
        [Tooltip("Normalised control points from source to mouth. Smoothed with a Catmull-Rom spline.")]
        public List<Vector2> points = new List<Vector2>();
        [Tooltip("Channel half width in metres at the source (x) and the mouth (y).")]
        public Vector2 halfWidth = new Vector2(4f, 6f);
        [Tooltip("Channel depth in metres: shallow fords (x) to deep spots (y).")]
        public Vector2 depth = new Vector2(1f, 2f);
        public bool hasWaterfall;
        [Tooltip("Fraction of the river's length where the waterfall starts (x) and ends (y).")]
        public Vector2 waterfallRange = new Vector2(0.075f, 0.105f);
        public float topLevel = 35.7f;
        public float waterfallTopLevel = 34f;
        public float belowFallLevel = 11f;
    }

    [Serializable]
    public class PondSpec
    {
        public string name;
        public Vector2 center;
        public float radius = 10f;
    }

    [Serializable]
    public class FoodRule
    {
        public string biomeId;
        public WorldPropKind kind;
        [Tooltip("Minimum metres between two of these in this biome (Poisson disc). 0 = none.")]
        public float spacing;
    }

    /// <summary>
    /// Every number that shapes the island. Defaults are the hand-tuned layout from the design doc
    /// (see ResetToDefaults); tweak in the inspector and press Rebuild in the Island Builder window.
    /// </summary>
    [CreateAssetMenu(menuName = "Dino/World/Island Spec", fileName = "IslandSpec")]
    public class IslandSpec : ScriptableObject
    {
        [Header("Size")]
        public int seed = 1987;
        [Tooltip("Terrain width and length in metres. The island itself is about 85% of this.")]
        public float size = 2000f;
        [Tooltip("Heightmap samples per side (2^n + 1). 1025 = 1.95 m per sample.")]
        public int heightmapResolution = 1025;
        [Tooltip("World Y of the terrain's lowest point. Sea level is world Y 0.")]
        public float terrainBottom = -20f;
        public float terrainHeight = 230f;
        public float seabed = -14f;

        [Header("Coast")]
        public float coastRadius = 0.425f;
        public float coastWobble = 0.065f;
        public float coastFrequency = 2.2f;
        public float coastBlend = 0.07f;

        [Header("Biomes")]
        public float biomeWarp = 0.07f;
        public float biomeWarpFrequency = 3f;
        [Tooltip("How softly biome heights blend into each other (normalised distance).")]
        public float biomeBlend = 0.035f;
        public List<BiomeSeedGroup> biomes = new List<BiomeSeedGroup>();

        [Header("Landmarks")]
        public Vector2 volcanoCenter = new Vector2(0.69f, 0.68f);
        public float volcanoRadius = 0.13f;
        public float volcanoHeight = 165f;
        public float craterRadius = 0.022f;
        public float craterDepth = 35f;
        public Vector2 lakeCenter = new Vector2(0.31f, 0.77f);
        public Vector2 lakeRadii = new Vector2(0.095f, 0.062f);
        public float lakeLevel = 36f;
        public float lakeDepth = 9f;
        public float swampLevel = 1.6f;
        public Vector2 greatTree = new Vector2(0.335f, 0.50f);

        [Header("Water")]
        public List<RiverSpec> rivers = new List<RiverSpec>();
        public List<PondSpec> ponds = new List<PondSpec>();

        [Header("Placement")]
        [Tooltip("Every walkable spot should be within this many metres of drinkable water (about 25 s walking).")]
        public float waterSpacing = 115f;
        [Tooltip("Every walkable spot should be within this many metres of a nest (about 45-60 s walking).")]
        public float nestSpacing = 210f;
        public float maxWalkSlope = 38f;
        [Tooltip("Ground above this height (upper volcano) doesn't need water or nests nearby.")]
        public float maxCoverageHeight = 110f;
        [Tooltip("Heightmap samples per placement cell (4 at 1025 = 7 m cells).")]
        public int placementStep = 4;
        public List<FoodRule> food = new List<FoodRule>();
        public List<FoodRule> decor = new List<FoodRule>();

        public float CellSize => size / (heightmapResolution - 1);

        void Reset() => ResetToDefaults();

        [ContextMenu("Reset To Design Defaults")]
        public void ResetToDefaults()
        {
            seed = 1987; size = 2000f; heightmapResolution = 1025;
            terrainBottom = -20f; terrainHeight = 230f; seabed = -14f;
            coastRadius = 0.425f; coastWobble = 0.065f; coastFrequency = 2.2f; coastBlend = 0.07f;
            biomeWarp = 0.07f; biomeWarpFrequency = 3f; biomeBlend = 0.035f;
            biomes = new List<BiomeSeedGroup>
            {
                Group(BiomeIds.Meadow, 0.8f, V(0.40f, 0.42f), V(0.37f, 0.32f)),
                Group(BiomeIds.Forest, 1f, V(0.17f, 0.50f), V(0.21f, 0.27f), V(0.38f, 0.14f), V(0.62f, 0.50f)),
                Group(BiomeIds.Swamp, 1f, V(0.74f, 0.28f), V(0.63f, 0.15f), V(0.84f, 0.40f)),
                Group(BiomeIds.Highlands, 1f, V(0.72f, 0.70f), V(0.58f, 0.82f), V(0.84f, 0.60f)),
                Group(BiomeIds.Lake, 1f, V(0.32f, 0.77f), V(0.17f, 0.70f), V(0.44f, 0.87f)),
            };
            volcanoCenter = V(0.69f, 0.68f); volcanoRadius = 0.13f; volcanoHeight = 165f; craterRadius = 0.022f; craterDepth = 35f;
            lakeCenter = V(0.31f, 0.77f); lakeRadii = V(0.095f, 0.062f); lakeLevel = 36f; lakeDepth = 9f;
            swampLevel = 1.6f; greatTree = V(0.335f, 0.50f);
            rivers = new List<RiverSpec>
            {
                new RiverSpec
                {
                    name = "BigRiver", halfWidth = V(7f, 12f), depth = V(1f, 2.6f),
                    hasWaterfall = true, waterfallRange = V(0.075f, 0.105f), topLevel = lakeLevel - 0.3f, waterfallTopLevel = 34f, belowFallLevel = 11f,
                    points = Pts(0.405f, 0.715f, 0.43f, 0.675f, 0.445f, 0.655f, 0.47f, 0.60f, 0.505f, 0.52f, 0.50f, 0.45f,
                                 0.485f, 0.39f, 0.52f, 0.30f, 0.535f, 0.22f, 0.515f, 0.13f, 0.50f, 0.02f),
                },
                new RiverSpec
                {
                    name = "WestCreek", halfWidth = V(4f, 6f), depth = V(1.8f, 2.2f),
                    points = Pts(0.235f, 0.665f, 0.25f, 0.61f, 0.275f, 0.52f, 0.285f, 0.44f, 0.272f, 0.36f,
                                 0.252f, 0.27f, 0.222f, 0.18f, 0.185f, 0.08f, 0.17f, 0.03f),
                },
                new RiverSpec
                {
                    name = "EastCreek", halfWidth = V(3.5f, 5f), depth = V(1f, 1.4f),
                    points = Pts(0.585f, 0.615f, 0.615f, 0.53f, 0.645f, 0.44f, 0.672f, 0.36f, 0.70f, 0.29f, 0.72f, 0.24f),
                },
            };
            ponds = new List<PondSpec>
            {
                new PondSpec { name = "MeadowPond", center = V(0.385f, 0.455f), radius = 20f },
                new PondSpec { name = "HotSpringA", center = V(0.79f, 0.575f), radius = 9f },
                new PondSpec { name = "HotSpringB", center = V(0.585f, 0.79f), radius = 8f },
            };
            waterSpacing = 115f; nestSpacing = 210f; maxWalkSlope = 38f; maxCoverageHeight = 110f; placementStep = 4;

            // Food: spacing in metres. Plant-eaters must never go hungry, so every biome has plants and bugs.
            food = new List<FoodRule>();
            AddFood(BiomeIds.Meadow, (WorldPropKind.BerryBush, 30f), (WorldPropKind.LeafShrub, 45f), (WorldPropKind.BugMound, 40f));
            AddFood(BiomeIds.Riverbanks, (WorldPropKind.BerryBush, 45f), (WorldPropKind.Fern, 30f), (WorldPropKind.BugMound, 50f), (WorldPropKind.FishSpot, 35f));
            AddFood(BiomeIds.Forest, (WorldPropKind.BerryBush, 45f), (WorldPropKind.Fern, 28f), (WorldPropKind.LeafShrub, 40f), (WorldPropKind.TallLeafTree, 60f), (WorldPropKind.BugMound, 32f));
            AddFood(BiomeIds.Swamp, (WorldPropKind.Fern, 35f), (WorldPropKind.BugMound, 30f), (WorldPropKind.FishSpot, 40f));
            AddFood(BiomeIds.Highlands, (WorldPropKind.BerryBush, 70f), (WorldPropKind.LeafShrub, 70f), (WorldPropKind.BugMound, 60f));
            AddFood(BiomeIds.Lake, (WorldPropKind.BerryBush, 38f), (WorldPropKind.LeafShrub, 38f), (WorldPropKind.TallLeafTree, 50f), (WorldPropKind.BugMound, 45f), (WorldPropKind.FishSpot, 30f));

            // Decor (terrain trees, rocks, logs). Terrain trees are instanced and cheap; spacing in metres.
            decor = new List<FoodRule>();
            AddDecor(BiomeIds.Meadow, (WorldPropKind.Tree, 38f), (WorldPropKind.Rock, 45f), (WorldPropKind.Log, 70f));
            AddDecor(BiomeIds.Riverbanks, (WorldPropKind.Tree, 26f), (WorldPropKind.Rock, 22f));
            AddDecor(BiomeIds.Forest, (WorldPropKind.Tree, 7.5f), (WorldPropKind.Rock, 35f), (WorldPropKind.Log, 30f));
            AddDecor(BiomeIds.Swamp, (WorldPropKind.SwampTree, 14f), (WorldPropKind.Log, 40f));
            AddDecor(BiomeIds.Highlands, (WorldPropKind.Pine, 30f), (WorldPropKind.Rock, 14f));
            AddDecor(BiomeIds.Lake, (WorldPropKind.Tree, 18f), (WorldPropKind.Rock, 30f));
        }

        void AddFood(string biome, params (WorldPropKind kind, float spacing)[] rules)
        {
            foreach (var r in rules) food.Add(new FoodRule { biomeId = biome, kind = r.kind, spacing = r.spacing });
        }

        void AddDecor(string biome, params (WorldPropKind kind, float spacing)[] rules)
        {
            foreach (var r in rules) decor.Add(new FoodRule { biomeId = biome, kind = r.kind, spacing = r.spacing });
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static BiomeSeedGroup Group(string id, float scale, params Vector2[] seeds)
            => new BiomeSeedGroup { biomeId = id, scale = scale, seeds = new List<Vector2>(seeds) };

        static List<Vector2> Pts(params float[] xy)
        {
            var l = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new Vector2(xy[i], xy[i + 1]));
            return l;
        }

        /// <summary>A spec with the design defaults, not saved as an asset.</summary>
        public static IslandSpec CreateDefault()
        {
            var s = CreateInstance<IslandSpec>();
            s.ResetToDefaults();
            return s;
        }
    }
}
