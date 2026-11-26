using System.Collections.Generic;
using UnityEngine;

namespace Dino.World.Generation
{
    public enum WaterKind : byte { None = 0, Sea = 1, Lake = 2, River = 3, Pond = 4, Swamp = 5, Puddle = 6 }

    public enum WorldPropKind
    {
        // food
        BerryBush, LeafShrub, Fern, TallLeafTree, BugMound, FishSpot,
        // decor
        Tree, Pine, SwampTree, Rock, Log,
        // gameplay
        Nest, Puddle, GreatTree,
    }

    public class RiverPath
    {
        public string name;
        /// <summary>Smoothed centre line, normalised 0..1.</summary>
        public Vector2[] points;
        /// <summary>Distance along the river, 0 at source and 1 at mouth, per point.</summary>
        public float[] along;
        /// <summary>Water surface height (metres above sea) per point.</summary>
        public float[] level;
        /// <summary>Channel half width in metres per point.</summary>
        public float[] halfWidth;
        public bool hasWaterfall;
        public Vector2 waterfallRange;
    }

    public struct PondInfo
    {
        public string name;
        public Vector2 center; // normalised
        public float radius;   // metres
        public float level;    // metres above sea
    }

    public struct WorldProp
    {
        public WorldPropKind kind;
        /// <summary>World position (x, ground height, z) with sea level at y = 0.</summary>
        public Vector3 position;
        public byte biome;
        public float yaw;
        public float scale;
        public bool isMain;
    }

    /// <summary>
    /// The generated island as plain arrays. Grids are row-major: index = z * Resolution + x, z = north.
    /// Heights are metres relative to sea level.
    /// </summary>
    public class IslandData
    {
        public int Resolution;
        public float Size;
        public float[] Heights;
        public byte[] Biomes;
        /// <summary>Water surface height per sample, NaN where there is no water body.</summary>
        public float[] WaterLevel;
        public WaterKind[] Water;
        public readonly List<RiverPath> Rivers = new List<RiverPath>();
        public readonly List<PondInfo> Ponds = new List<PondInfo>();
        public readonly List<WorldProp> Props = new List<WorldProp>();
        public float WorstWaterDistance, WorstNestDistance;

        public float Cell => Size / (Resolution - 1);
        public int Index(int x, int z) => z * Resolution + x;

        public bool IsWater(int i) => Water[i] != WaterKind.None && Heights[i] < WaterLevel[i];
        public bool IsDrinkable(int i) => IsWater(i) && Water[i] != WaterKind.Sea;

        public Vector3 ToWorld(int x, int z) => new Vector3(x * Cell, Heights[Index(x, z)], z * Cell);

        public float SampleHeight(float worldX, float worldZ) => Sample(Heights, worldX / Size, worldZ / Size);

        /// <summary>Bilinear sample of a grid at normalised coordinates.</summary>
        public float Sample(float[] grid, float u, float v)
        {
            int n = Resolution - 1;
            float x = Mathf.Clamp(u * n, 0, n), y = Mathf.Clamp(v * n, 0, n);
            int x0 = Mathf.Min((int)x, n - 1), y0 = Mathf.Min((int)y, n - 1);
            float fx = x - x0, fy = y - y0;
            return grid[Index(x0, y0)] * (1 - fx) * (1 - fy) + grid[Index(x0 + 1, y0)] * fx * (1 - fy)
                 + grid[Index(x0, y0 + 1)] * (1 - fx) * fy + grid[Index(x0 + 1, y0 + 1)] * fx * fy;
        }

        public byte BiomeAt(float worldX, float worldZ)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(worldX / Cell), 0, Resolution - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(worldZ / Cell), 0, Resolution - 1);
            return Biomes[Index(x, z)];
        }

        /// <summary>Slope in degrees at a sample, from central differences.</summary>
        public float SlopeAt(int x, int z)
        {
            int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, Resolution - 1);
            int z0 = Mathf.Max(z - 1, 0), z1 = Mathf.Min(z + 1, Resolution - 1);
            float gx = (Heights[Index(x1, z)] - Heights[Index(x0, z)]) / ((x1 - x0) * Cell);
            float gz = (Heights[Index(x, z1)] - Heights[Index(x, z0)]) / ((z1 - z0) * Cell);
            return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
        }
    }
}
