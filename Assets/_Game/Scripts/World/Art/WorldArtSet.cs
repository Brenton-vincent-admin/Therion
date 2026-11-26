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

        [Header("Lighting matched to the sky (filled in by the Poly Haven downloader)")]
        [Tooltip("True when the values below were measured from the sky picture.")]
        public bool hasSkyLighting;
        [Tooltip("Direction pointing from the ground toward the sun in the sky picture.")]
        public Vector3 sunDirection = new Vector3(0.4f, 0.75f, -0.5f);
        public Color ambientSky = new Color(0.62f, 0.72f, 0.86f);
        public Color ambientEquator = new Color(0.52f, 0.56f, 0.52f);
        public Color ambientGround = new Color(0.30f, 0.27f, 0.22f);
        public Color fogColor = new Color(0.70f, 0.77f, 0.84f);

        [Header("Grass colour (matched to the ground texture by the downloader)")]
        public Color grassRoot = new Color(0.10f, 0.15f, 0.05f);
        public Color grassTip = new Color(0.34f, 0.46f, 0.14f);

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
