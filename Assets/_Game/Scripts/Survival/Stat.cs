using System;
using UnityEngine;

namespace Dino.Survival
{
    /// <summary>A clamped 0..Max value with a change event. Plain C# so it is easy to test and to sync over the network.</summary>
    [Serializable]
    public class Stat
    {
        [SerializeField] float current;
        [SerializeField] float max;

        public event Action<Stat> Changed;

        public Stat(float max) { this.max = Mathf.Max(0.01f, max); current = this.max; }

        public float Current => current;
        public float Max => max;
        public float Normalized => max <= 0f ? 0f : current / max;
        public bool IsEmpty => current <= 0f;
        public bool IsFull => current >= max;

        /// <summary>Sets the value (clamped). Returns the actual change.</summary>
        public float Set(float value)
        {
            float before = current;
            current = Mathf.Clamp(value, 0f, max);
            if (!Mathf.Approximately(before, current)) Changed?.Invoke(this);
            return current - before;
        }

        public float Add(float amount) => Set(current + amount);

        /// <summary>Change Max. keepRatio = true keeps the same fill percentage (used when growing).</summary>
        public void SetMax(float newMax, bool keepRatio)
        {
            newMax = Mathf.Max(0.01f, newMax);
            float ratio = Normalized;
            max = newMax;
            current = keepRatio ? ratio * max : Mathf.Min(current, max);
            Changed?.Invoke(this);
        }
    }
}
