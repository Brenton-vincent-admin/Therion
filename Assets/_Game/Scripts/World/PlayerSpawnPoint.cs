using Dino.Species;
using UnityEngine;

namespace Dino.World
{
    /// <summary>Where a new or respawning player dino can appear. Place several around the map.</summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Biome id, one of: meadow, riverbanks, forest, swamp, highlands, lake.")]
        public string biomeId = "meadow";
        public SizeClass minSize = SizeClass.Tiny;
        public SizeClass maxSize = SizeClass.Huge;
        public float radius = 3f;

        public bool Allows(SizeClass s) => s >= minSize && s <= maxSize;

        /// <summary>A random point within the radius, snapped to the ground.</summary>
        public Vector3 GetSpawnPosition()
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = transform.position + new Vector3(r.x, 0, r.y);
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                p = hit.point;
            return p;
        }

        void OnEnable() => WorldRegistry.Register(this);
        void OnDisable() => WorldRegistry.Unregister(this);

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * radius);
        }
    }
}
