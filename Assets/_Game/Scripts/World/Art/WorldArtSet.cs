using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dino.World
{
    [Serializable]
    public class PropArt
    {
        public Generation.WorldPropKind kind;
        [Tooltip("Prefabs to pick from at random. Leave empty to use the generated placeholder.")]
        public List<GameObject> prefabs = new List<GameObject>();
    }

    /// <summary>
    /// Which art the Island Builder uses. Every slot is optional: an empty slot falls back to a generated
    /// placeholder, so the island always builds. To make the island look realistic, fill the slots with
    /// free assets (see Assets/_Game/World/README.md) and press Build again.
    /// </summary>
    [CreateAssetMenu(menuName = "Dino/World/World Art Set", fileName = "WorldArtSet")]
    public class WorldArtSet : ScriptableObject
    {
        [Header("Ground (terrain layers)")]
        [Tooltip("Meadow and forest grass.")]
        public TerrainLayer grass;
        [Tooltip("Forest floor (leaf litter). Optional; costs a second terrain pass on phones if all 5 are set.")]
        public TerrainLayer forestFloor;
        [Tooltip("Beaches, river banks.")]
        public TerrainLayer sand;
        [Tooltip("Swamp mud, wet banks.")]
        public TerrainLayer mud;
        [Tooltip("Cliffs, highlands, volcano.")]
        public TerrainLayer rock;

        [Header("Vegetation and props")]
        public List<PropArt> props = new List<PropArt>();
        [Tooltip("Grass tuft for terrain detail (mesh prefab). Empty = generated tuft.")]
        public GameObject grassDetail;

        [Header("Water and sky")]
        public Material water;
        public Material ocean;
        public Material skybox;

        public GameObject PickPrefab(Generation.WorldPropKind kind, int index)
        {
            foreach (var p in props)
            {
                if (p.kind != kind || p.prefabs == null) continue;
                int count = 0;
                foreach (var go in p.prefabs) if (go) count++;
                if (count == 0) return null;
                int pick = Mathf.Abs(index) % count;
                foreach (var go in p.prefabs)
                {
                    if (!go) continue;
                    if (pick-- == 0) return go;
                }
            }
            return null;
        }

        public bool HasPrefabs(Generation.WorldPropKind kind) => PickPrefab(kind, 0) != null;
    }
}
