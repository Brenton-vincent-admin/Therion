using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// Root of the island scene (created by the Island Builder). Makes the island's BiomeMap active
    /// while the scene is loaded.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class IslandWorld : MonoBehaviour
    {
        public BiomeMap biomeMap;
        [Tooltip("World Y of the sea surface.")]
        public float seaLevel = 0f;
        public Terrain terrain;

        public static IslandWorld Current { get; private set; }

        void OnEnable()
        {
            Current = this;
            if (biomeMap) BiomeMap.Active = biomeMap;
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            if (BiomeMap.Active == biomeMap) BiomeMap.Active = null;
        }

        /// <summary>Ground height at a world position (terrain only), or seaLevel if there is no terrain.</summary>
        public float GroundHeight(Vector3 worldPos)
            => terrain ? terrain.SampleHeight(worldPos) + terrain.transform.position.y : seaLevel;
    }
}
