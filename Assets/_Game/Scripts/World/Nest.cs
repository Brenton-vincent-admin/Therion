using System.Collections.Generic;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// A nest. In Survival mode, resting at a nest makes it your respawn point.
    /// Map places some; later players may build their own.
    /// </summary>
    public class Nest : MonoBehaviour
    {
        public string nestId;
        public float radius = 2.5f;

        static readonly List<Nest> _all = new List<Nest>();
        public static IReadOnlyList<Nest> All => _all;

        void OnEnable()
        {
            if (string.IsNullOrEmpty(nestId)) nestId = $"{name}_{Mathf.RoundToInt(transform.position.x)}_{Mathf.RoundToInt(transform.position.z)}";
            _all.Add(this);
        }
        void OnDisable() => _all.Remove(this);

        public static Nest FindWithin(Vector3 pos, float extra = 0f)
        {
            foreach (var n in _all)
            {
                Vector3 d = n.transform.position - pos; d.y = 0;
                if (d.magnitude <= n.radius + extra) return n;
            }
            return null;
        }

        public static Nest FindById(string id) => _all.Find(n => n.nestId == id);

        void OnDrawGizmos() { Gizmos.color = new Color(1f, 0.8f, 0.2f); Gizmos.DrawWireSphere(transform.position, radius); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all.Clear();
    }
}
