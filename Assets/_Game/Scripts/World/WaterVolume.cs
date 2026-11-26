using System.Collections.Generic;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// A lake, pond or river section. Box-shaped: the top face is the water surface.
    /// Dinos can drink at it and swim in it. Use several for a winding river.
    /// Size it with 'size' (local, centered on the transform; surface = transform.y + size.y/2).
    /// </summary>
    public class WaterVolume : MonoBehaviour, IWaterSource
    {
        public Vector3 size = new Vector3(20f, 3f, 20f);
        [Tooltip("False for swim-only water such as the ocean (no drinking seawater).")]
        public bool drinkable = true;

        static readonly List<WaterVolume> _all = new List<WaterVolume>();
        public static IReadOnlyList<WaterVolume> All => _all;

        public bool CanDrink => drinkable && isActiveAndEnabled;
        public float SurfaceY => transform.position.y + size.y * 0.5f;

        void OnEnable() { _all.Add(this); WorldRegistry.Register(this); }
        void OnDisable() { _all.Remove(this); WorldRegistry.Unregister(this); }

        public Vector3 GetDrinkPoint(Vector3 from)
        {
            Vector3 local = transform.InverseTransformPoint(from);
            local.x = Mathf.Clamp(local.x, -size.x * 0.5f, size.x * 0.5f);
            local.z = Mathf.Clamp(local.z, -size.z * 0.5f, size.z * 0.5f);
            local.y = size.y * 0.5f;
            return transform.TransformPoint(local);
        }

        public float Drink(float units, GameObject drinker) => units;

        /// <summary>True if the point is inside the volume horizontally and below the surface.</summary>
        public bool Contains(Vector3 worldPos)
        {
            Vector3 l = transform.InverseTransformPoint(worldPos);
            return Mathf.Abs(l.x) <= size.x * 0.5f && Mathf.Abs(l.z) <= size.z * 0.5f
                && l.y <= size.y * 0.5f && l.y >= -size.y * 0.5f;
        }

        /// <summary>Finds water at a point. Returns false if dry.</summary>
        public static bool TryGetWaterAt(Vector3 worldPos, out WaterVolume volume)
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Contains(worldPos)) { volume = _all[i]; return true; }
            volume = null;
            return false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, size);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => _all.Clear();
    }
}
