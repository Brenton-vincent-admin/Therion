using UnityEngine;

namespace Dino.Survival
{
    public struct DamageInfo
    {
        public float amount;
        public GameObject attacker; // may be null (falling, starving)
        public Vector3 point;
        public DamageInfo(float amount, GameObject attacker, Vector3 point) { this.amount = amount; this.attacker = attacker; this.point = point; }
    }

    /// <summary>Anything that can be bitten. SurvivalStats implements it; wild dinos use the same component.</summary>
    public interface IDamageable
    {
        bool IsFainted { get; }
        void TakeDamage(DamageInfo info);
    }
}
