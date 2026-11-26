using System;
using Dino.Survival;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// Put on the player dino. Tracks which biome it is in, applies the biome's thirst multiplier
    /// (Red Highlands heat) to SurvivalStats, and announces biome changes for UI, music and the Field Guide.
    /// </summary>
    public class BiomeTracker : MonoBehaviour
    {
        [Tooltip("Seconds between checks. Biomes are big, so a few times a second is plenty.")]
        public float interval = 0.5f;

        public string CurrentBiomeId { get; private set; }
        public BiomeInfo CurrentBiome { get; private set; }

        /// <summary>(tracker, previous biome id, new biome id). Previous is null on the first check.</summary>
        public static event Action<BiomeTracker, string, string> BiomeChanged;

        SurvivalStats _stats;
        float _next;

        void Awake() => _stats = GetComponent<SurvivalStats>();

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + interval;
            var map = BiomeMap.Active;
            if (map == null) return;

            string id = map.IdAt(transform.position);
            if (id != CurrentBiomeId)
            {
                string previous = CurrentBiomeId;
                CurrentBiomeId = id;
                CurrentBiome = map.Get(id);
                BiomeChanged?.Invoke(this, previous, id);
            }
            if (_stats) _stats.WaterDrainMultiplier = CurrentBiome != null ? CurrentBiome.thirstMultiplier : 1f;
        }

        void OnDisable()
        {
            if (_stats) _stats.WaterDrainMultiplier = 1f;
        }
    }
}
