using Dino.Species;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// Ready-made food source: berry bush, leafy tree, bug mound, fish spot. Put it on the plant prefab.
    /// Shrinks the optional 'foodVisual' as it is eaten and regrows over time.
    /// </summary>
    public class FoodSource : MonoBehaviour, IFoodSource
    {
        public FoodType foodType = FoodType.Berries;
        [Tooltip("Food units when full.")]
        public float capacity = 60f;
        [Tooltip("Units regrown per minute. 0 = never regrows.")]
        public float regrowPerMinute = 20f;
        [Tooltip("Height of the food above this object's pivot (high leaves need tall dinos).")]
        public float feedHeight = 0.5f;
        [Tooltip("Radius of the plant, so dinos bite at its edge, not its center.")]
        public float radius = 0.6f;
        [Tooltip("Optional child (berries, leaves) that scales with how much food is left.")]
        public Transform foodVisual;

        float _remaining;
        Vector3 _visualScale = Vector3.one;

        public FoodType FoodType => foodType;
        public float FeedHeight => feedHeight;
        public bool HasFood => _remaining >= 1f;
        public float Remaining => _remaining;

        void Awake()
        {
            _remaining = capacity;
            if (foodVisual) _visualScale = foodVisual.localScale;
        }

        void OnEnable() => WorldRegistry.Register(this);
        void OnDisable() => WorldRegistry.Unregister(this);

        public Vector3 GetFeedPoint(Vector3 from)
        {
            Vector3 c = transform.position;
            Vector3 d = from - c; d.y = 0f;
            Vector3 edge = d.sqrMagnitude > 0.0001f ? c + d.normalized * radius : c;
            edge.y = c.y + feedHeight;
            return edge;
        }

        public float Bite(float units, GameObject eater)
        {
            float taken = Mathf.Min(units, _remaining);
            _remaining -= taken;
            UpdateVisual();
            return taken;
        }

        void Update()
        {
            if (regrowPerMinute <= 0f || _remaining >= capacity) return;
            _remaining = Mathf.Min(capacity, _remaining + regrowPerMinute / 60f * Time.deltaTime);
            UpdateVisual();
        }

        void UpdateVisual()
        {
            if (!foodVisual) return;
            float t = capacity <= 0f ? 0f : _remaining / capacity;
            foodVisual.localScale = _visualScale * Mathf.Lerp(0.05f, 1f, t);
            foodVisual.gameObject.SetActive(t > 0.02f);
        }
    }
}
