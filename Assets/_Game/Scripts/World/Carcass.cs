using Dino.Species;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// A cartoon meat pile left when a wild dino is defeated. Never shows a body or gore.
    /// Disappears when eaten or after 'lifetimeSeconds'.
    /// </summary>
    public class Carcass : MonoBehaviour, IFoodSource
    {
        public float meat = 100f;
        public float lifetimeSeconds = 300f;
        public float radius = 0.8f;
        public Transform visual;

        float _start, _age;
        Vector3 _scale = Vector3.one;

        public FoodType FoodType => FoodType.Meat;
        public float FeedHeight => 0.3f;
        public bool HasFood => meat >= 1f;
        public float Remaining => meat;

        void Awake()
        {
            _start = Mathf.Max(1f, meat);
            if (visual) _scale = visual.localScale;
        }
        void OnEnable() => WorldRegistry.Register(this);
        void OnDisable() => WorldRegistry.Unregister(this);

        public Vector3 GetFeedPoint(Vector3 from)
        {
            Vector3 c = transform.position;
            Vector3 d = from - c; d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? c + d.normalized * radius : c;
        }

        public float Bite(float units, GameObject eater)
        {
            float taken = Mathf.Min(units, meat);
            meat -= taken;
            if (visual) visual.localScale = _scale * Mathf.Lerp(0.3f, 1f, meat / _start);
            if (meat < 1f) Destroy(gameObject);
            return taken;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age > lifetimeSeconds) Destroy(gameObject);
        }

        /// <summary>Spawns a placeholder meat pile. The art thread can swap in a prefab via 'prefab'.</summary>
        public static Carcass Spawn(Vector3 position, float meatAmount, GameObject prefab = null)
        {
            GameObject go;
            if (prefab) go = Instantiate(prefab, position, Quaternion.identity);
            else
            {
                go = new GameObject("MeatPile");
                go.transform.position = position;
                var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(vis.GetComponent<Collider>());
                vis.transform.SetParent(go.transform, false);
                vis.transform.localScale = new Vector3(1.2f, 0.5f, 1f);
                vis.transform.localPosition = new Vector3(0, 0.2f, 0);
                var r = vis.GetComponent<Renderer>();
                r.sharedMaterial = Util.PlaceholderMaterials.Get(new Color(0.85f, 0.35f, 0.35f));
                go.AddComponent<Carcass>().visual = vis.transform;
            }
            var c = go.GetComponent<Carcass>();
            if (c == null) c = go.AddComponent<Carcass>();
            c.meat = meatAmount;
            c._start = Mathf.Max(1f, meatAmount);
            return c;
        }
    }
}
