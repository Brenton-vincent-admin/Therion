using System.Collections.Generic;
using Dino.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Dino.Species.AI
{
    /// <summary>
    /// Keeps a small population of wild dinos alive around this point (one per biome area).
    /// Spawns out of players' sight and refills a few seconds after one faints.
    /// The wild prefab needs DinoActor, SurvivalStats, DinoMotor, DinoStatusEffects, DinoStealth and WildDinoBrain.
    /// </summary>
    public class WildDinoSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public SpeciesDefinition species;
            [Min(0f)] public float weight = 1f;
            [Tooltip("Growth stages this spawner may use (0 = hatchling .. 3 = adult).")]
            public int minStage = 1, maxStage = 3;
        }

        public GameObject wildDinoPrefab;
        public List<Entry> entries = new List<Entry>();
        [Min(0)] public int maxAlive = 4;
        public float spawnRadius = 40f;
        [Tooltip("Never spawn closer than this to a player dino (m).")]
        public float minPlayerDistance = 35f;
        public float respawnDelay = 20f;

        readonly List<WildDinoBrain> _alive = new List<WildDinoBrain>();
        float _nextSpawn;

        void Update()
        {
            _alive.RemoveAll(b => b == null);
            if (_alive.Count >= maxAlive || Time.time < _nextSpawn || wildDinoPrefab == null || entries.Count == 0) return;
            _nextSpawn = Time.time + 2f;
            if (TryPickPoint(out var point)) Spawn(PickEntry(), point);
        }

        Entry PickEntry()
        {
            float total = 0f;
            foreach (var e in entries) if (e.species != null) total += e.weight;
            float r = Random.value * total;
            foreach (var e in entries)
            {
                if (e.species == null) continue;
                r -= e.weight;
                if (r <= 0f) return e;
            }
            return entries[0];
        }

        bool TryPickPoint(out Vector3 point)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 c = Random.insideUnitCircle * spawnRadius;
                Vector3 p = transform.position + new Vector3(c.x, 0f, c.y);
                if (NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas)) p = hit.position;
                else if (Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out var rh, 400f)) p = rh.point;
                if (FarFromPlayers(p)) { point = p; return true; }
            }
            point = default;
            return false;
        }

        bool FarFromPlayers(Vector3 p)
        {
            var all = DinoStealth.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].isPlayer && (all[i].transform.position - p).sqrMagnitude < minPlayerDistance * minPlayerDistance)
                    return false;
            return true;
        }

        public WildDinoBrain Spawn(Entry e, Vector3 point)
        {
            if (e == null || e.species == null) return null;
            var go = Instantiate(wildDinoPrefab, point, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.name = $"Wild_{e.species.id}";
            var actor = go.GetComponent<DinoActor>();
            int stage = Mathf.Clamp(Random.Range(e.minStage, e.maxStage + 1), 0, e.species.StageCount - 1);
            actor.Setup(e.species, stage, null);
            var stealth = go.GetComponent<DinoStealth>();
            if (stealth != null) { stealth.isPlayer = false; stealth.RefreshVariant(); }

            var skin = go.GetComponentInChildren<DinoSkin>();
            if (skin != null) skin.Apply(WildSkin(e.species));

            var brain = go.GetComponent<WildDinoBrain>();
            brain.Home = transform.position;
            brain.Despawned += b => { _alive.Remove(b); _nextSpawn = Time.time + respawnDelay; };
            _alive.Add(brain);
            return brain;
        }

        /// <summary>Wild dinos use natural-looking variations of the species' default look.</summary>
        static SkinLoadout WildSkin(SpeciesDefinition s)
        {
            var natural = new[] { SkinColor.Moss, SkinColor.Sand, SkinColor.Clay, SkinColor.Stone, SkinColor.Leaf, SkinColor.Charcoal };
            var skin = s.defaultSkin;
            if (Random.value < 0.5f) skin.baseColor = (byte)natural[Random.Range(0, natural.Length)];
            return skin;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
#endif
    }
}
