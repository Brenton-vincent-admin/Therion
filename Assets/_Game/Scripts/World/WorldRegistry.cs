using System.Collections.Generic;
using Dino.Species;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// Static registry of food, water and spawn points so dinos (and AI) can find them without physics layers.
    /// </summary>
    public static class WorldRegistry
    {
        static readonly List<IFoodSource> _food = new List<IFoodSource>();
        static readonly List<IWaterSource> _water = new List<IWaterSource>();
        static readonly List<PlayerSpawnPoint> _spawns = new List<PlayerSpawnPoint>();

        public static IReadOnlyList<IFoodSource> Food => _food;
        public static IReadOnlyList<IWaterSource> Water => _water;
        public static IReadOnlyList<PlayerSpawnPoint> SpawnPoints => _spawns;

        public static void Register(IFoodSource f) { if (!_food.Contains(f)) _food.Add(f); }
        public static void Unregister(IFoodSource f) => _food.Remove(f);
        public static void Register(IWaterSource w) { if (!_water.Contains(w)) _water.Add(w); }
        public static void Unregister(IWaterSource w) => _water.Remove(w);
        public static void Register(PlayerSpawnPoint s) { if (!_spawns.Contains(s)) _spawns.Add(s); }
        public static void Unregister(PlayerSpawnPoint s) => _spawns.Remove(s);

        /// <summary>
        /// Nearest food within 'radius' (horizontal) that matches 'edible', has food, and is no higher than 'maxHeight'.
        /// </summary>
        public static IFoodSource FindNearestFood(Vector3 pos, float radius, FoodType edible, float maxHeight)
        {
            IFoodSource best = null;
            float bestSq = radius * radius;
            for (int i = 0; i < _food.Count; i++)
            {
                var f = _food[i];
                if (f == null || !f.HasFood || (f.FoodType & edible) == 0 || f.FeedHeight > maxHeight) continue;
                Vector3 d = f.GetFeedPoint(pos) - pos; d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = f; }
            }
            return best;
        }

        public static IWaterSource FindNearestWater(Vector3 pos, float radius)
        {
            IWaterSource best = null;
            float bestSq = radius * radius;
            for (int i = 0; i < _water.Count; i++)
            {
                var w = _water[i];
                if (w == null || !w.CanDrink) continue;
                Vector3 p = w.GetDrinkPoint(pos);
                Vector3 d = p - pos; d.y = 0f;
                if (Mathf.Abs(p.y - pos.y) > radius + 1f) continue;
                float sq = d.sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = w; }
            }
            return best;
        }

        /// <summary>Pick a spawn point for a species size. Falls back to any point, then null.</summary>
        public static PlayerSpawnPoint PickSpawn(SizeClass size, string biomeId = null)
        {
            var candidates = new List<PlayerSpawnPoint>();
            foreach (var s in _spawns)
                if (s != null && s.Allows(size) && (string.IsNullOrEmpty(biomeId) || s.biomeId == biomeId)) candidates.Add(s);
            if (candidates.Count == 0) candidates.AddRange(_spawns);
            candidates.RemoveAll(s => s == null);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _food.Clear(); _water.Clear(); _spawns.Clear(); }
    }
}
