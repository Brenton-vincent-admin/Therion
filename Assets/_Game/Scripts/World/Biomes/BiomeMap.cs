using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dino.World
{
    [Serializable]
    public class BiomeInfo
    {
        public string id;
        public string displayName;
        [Tooltip("Life stage that can safely enter: 0 Hatchling, 1 Juvenile, 2 Sub-adult, 3 Adult.")]
        public int opensAtStage;
        [Tooltip("Thirst drains this many times faster here (Red Highlands heat).")]
        public float thirstMultiplier = 1f;
        [Tooltip("Colour of this biome's blob on the pause-screen map.")]
        public Color mapColor = Color.green;
    }

    /// <summary>
    /// Baked biome grid for the island, so gameplay can ask "which biome am I in?" cheaply.
    /// The Island Builder creates this asset; IslandWorld sets BiomeMap.Active when the scene loads.
    /// </summary>
    [CreateAssetMenu(menuName = "Dino/World/Biome Map", fileName = "BiomeMap")]
    public class BiomeMap : ScriptableObject
    {
        [Tooltip("World X/Z of the grid's south-west corner.")]
        public Vector2 origin;
        public float worldSize = 2000f;
        public int resolution = 256;
        [HideInInspector] public byte[] codes;
        public List<BiomeInfo> biomes = new List<BiomeInfo>();
        [Tooltip("Picture of the island in biome colours for the pause-screen map.")]
        public Texture2D mapTexture;

        /// <summary>The map of the loaded island (set by IslandWorld). Null if no island is loaded.</summary>
        public static BiomeMap Active { get; set; }

        /// <summary>Biome code at a world position (BiomeIds.*Code).</summary>
        public byte CodeAt(Vector3 worldPos)
        {
            if (codes == null || codes.Length != resolution * resolution) return BiomeIds.OceanCode;
            float u = (worldPos.x - origin.x) / worldSize, v = (worldPos.z - origin.y) / worldSize;
            int x = Mathf.Clamp(Mathf.FloorToInt(u * resolution), 0, resolution - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(v * resolution), 0, resolution - 1);
            return codes[z * resolution + x];
        }

        public string IdAt(Vector3 worldPos) => BiomeIds.FromCode(CodeAt(worldPos));

        public BiomeInfo InfoAt(Vector3 worldPos) => Get(IdAt(worldPos));

        public BiomeInfo Get(string id)
        {
            for (int i = 0; i < biomes.Count; i++) if (biomes[i].id == id) return biomes[i];
            return null;
        }

        /// <summary>True if a dino at this life stage (0..3) is meant to be here yet.</summary>
        public bool IsOpenFor(Vector3 worldPos, int lifeStage)
        {
            var info = InfoAt(worldPos);
            return info == null || lifeStage >= info.opensAtStage;
        }

        /// <summary>Normalised position on the map texture (0..1), for the "your dino is here" marker.</summary>
        public Vector2 ToMapUV(Vector3 worldPos)
            => new Vector2((worldPos.x - origin.x) / worldSize, (worldPos.z - origin.y) / worldSize);

        /// <summary>The design doc's six biomes with their stage gates, thirst and map colours.</summary>
        public static List<BiomeInfo> DefaultBiomes() => new List<BiomeInfo>
        {
            new BiomeInfo { id = BiomeIds.Ocean, displayName = "Ocean", opensAtStage = 0, thirstMultiplier = 1f, mapColor = new Color32(52, 112, 170, 255) },
            new BiomeInfo { id = BiomeIds.Meadow, displayName = "Nest Meadow", opensAtStage = 0, thirstMultiplier = 1f, mapColor = new Color32(150, 205, 95, 255) },
            new BiomeInfo { id = BiomeIds.Riverbanks, displayName = "Riverbanks", opensAtStage = 0, thirstMultiplier = 0.9f, mapColor = new Color32(225, 205, 140, 255) },
            new BiomeInfo { id = BiomeIds.Forest, displayName = "Fern Forest", opensAtStage = 1, thirstMultiplier = 0.9f, mapColor = new Color32(45, 125, 60, 255) },
            new BiomeInfo { id = BiomeIds.Swamp, displayName = "Misty Swamp", opensAtStage = 2, thirstMultiplier = 0.8f, mapColor = new Color32(110, 130, 95, 255) },
            new BiomeInfo { id = BiomeIds.Highlands, displayName = "Red Highlands", opensAtStage = 2, thirstMultiplier = 1.6f, mapColor = new Color32(190, 95, 60, 255) },
            new BiomeInfo { id = BiomeIds.Lake, displayName = "Lake Basin", opensAtStage = 3, thirstMultiplier = 1f, mapColor = new Color32(120, 185, 200, 255) },
        };
    }
}
