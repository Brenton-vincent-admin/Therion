using System.Collections.Generic;
using UnityEngine;

namespace Dino.World
{
    public enum LandmarkKind { Landmark = 0, Secret = 1 }

    /// <summary>
    /// A named place: big navigation landmarks (volcano, waterfall, great tree) and each biome's secret
    /// from the design doc. The Field Guide and collectibles can find them by id.
    /// </summary>
    public class Landmark : MonoBehaviour
    {
        public string landmarkId;
        public string displayName;
        public LandmarkKind kind;
        public string biomeId;
        [Tooltip("How close a dino must come to count as 'found'.")]
        public float radius = 8f;

        static readonly List<Landmark> _all = new List<Landmark>();
        public static IReadOnlyList<Landmark> All => _all;

        void OnEnable() => _all.Add(this);
        void OnDisable() => _all.Remove(this);

        public static Landmark Find(string id) => _all.Find(l => l.landmarkId == id);

        /// <summary>A landmark whose radius contains the position, or null.</summary>
        public static Landmark At(Vector3 pos)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                Vector3 d = _all[i].transform.position - pos; d.y = 0f;
                if (d.sqrMagnitude <= _all[i].radius * _all[i].radius) return _all[i];
            }
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == LandmarkKind.Secret ? new Color(1f, 0.4f, 1f) : new Color(0.3f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all.Clear();
    }
}
