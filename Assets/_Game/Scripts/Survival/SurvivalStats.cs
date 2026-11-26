using System;
using Dino.Core;
using Dino.Species;
using UnityEngine;

namespace Dino.Survival
{
    /// <summary>
    /// Health, stamina, food and water for one dino (player or wild).
    /// Configure with Configure(species, stageIndex, variant). Ticks itself in Update unless
    /// SimulateLocally is false (multiplayer clients that only mirror the host's values).
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivalStats : MonoBehaviour, IDamageable
    {
        public Stat Health { get; private set; } = new Stat(100);
        public Stat Stamina { get; private set; } = new Stat(100);
        public Stat Food { get; private set; } = new Stat(100);
        public Stat Water { get; private set; } = new Stat(100);

        /// <summary>Raised once when health reaches zero. Arg: attacker or null.</summary>
        public event Action<GameObject> OnFainted;
        public event Action OnRevived;
        public event Action<DamageInfo> OnDamaged;

        [Tooltip("False on network clients that just display synced values.")]
        public bool SimulateLocally = true;
        [Tooltip("Explorer mode: no drain, no starving, no fainting from damage.")]
        public bool NoDrain;

        public bool IsFainted { get; private set; }
        /// <summary>Set by the motor each frame. Running drains stamina and a bit more food/water.</summary>
        public bool IsRunning { get; set; }

        public SpeciesDefinition Species { get; private set; }
        public float DamageMultiplier { get; private set; } = 1f;

        float _foodDrain, _waterDrain, _staminaRegen, _runCost, _healthRegen;
        float _staminaLockout; // seconds before stamina regen resumes after emptying

        const float StarveDamagePerSecond = 1.5f;

        /// <summary>Applies species + growth stage + variant. keepRatio keeps current fill % (use when growing).</summary>
        public void Configure(SpeciesDefinition species, int stageIndex, StatVariant variant, bool keepRatio)
        {
            Species = species;
            var stage = species.Stage(stageIndex);
            float m = stage.statMultiplier;
            float vh = variant?.healthMultiplier ?? 1f;
            float vs = variant?.staminaMultiplier ?? 1f;
            float vd = variant?.drainMultiplier ?? 1f;

            Health.SetMax(species.maxHealth * m * vh, keepRatio);
            Stamina.SetMax(species.maxStamina * Mathf.Lerp(0.6f, 1f, m) * vs, keepRatio);
            Food.SetMax(species.maxFood * Mathf.Lerp(0.5f, 1f, m), keepRatio);
            Water.SetMax(species.maxWater * Mathf.Lerp(0.5f, 1f, m), keepRatio);

            float capacityScale = Mathf.Lerp(0.5f, 1f, m); // smaller bellies drain proportionally slower
            _foodDrain = species.foodDrainPerMinute / 60f * vd * capacityScale;
            _waterDrain = species.waterDrainPerMinute / 60f * vd * capacityScale;
            _staminaRegen = species.staminaRegenPerSecond * vs;
            _runCost = species.runStaminaPerSecond;
            _healthRegen = species.healthRegenPerSecond * m;
            DamageMultiplier = m * (variant?.damageMultiplier ?? 1f);
        }

        /// <summary>Extra multiplier on water drain (e.g. hot highlands biome). Set by whoever knows the biome.</summary>
        public float WaterDrainMultiplier { get; set; } = 1f;

        void Update()
        {
            if (!SimulateLocally || IsFainted) return;
            Tick(Time.deltaTime);
        }

        /// <summary>Advances drain/regen by dt seconds. Public for tests and server ticking.</summary>
        public void Tick(float dt)
        {
            if (IsFainted) return;

            if (IsRunning && !Stamina.IsEmpty)
            {
                if (!NoDrain) Stamina.Add(-_runCost * dt);
                if (Stamina.IsEmpty) _staminaLockout = 1.5f;
            }
            else if (_staminaLockout > 0f) _staminaLockout -= dt;
            else Stamina.Add(_staminaRegen * dt);

            if (NoDrain) return;

            float runFactor = IsRunning ? 1.5f : 1f;
            Food.Add(-_foodDrain * runFactor * dt);
            Water.Add(-_waterDrain * WaterDrainMultiplier * runFactor * dt);

            if (Food.IsEmpty || Water.IsEmpty)
                ApplyDamage(new DamageInfo(StarveDamagePerSecond * dt, null, transform.position));
            else if (Food.Normalized > 0.5f && Water.Normalized > 0.5f)
                Health.Add(_healthRegen * dt);
        }

        /// <summary>True if there is enough stamina to start running.</summary>
        public bool CanRun => Stamina.Current > 1f && _staminaLockout <= 0f;

        public bool TryUseStamina(float amount)
        {
            if (NoDrain) return true;
            if (Stamina.Current < amount) return false;
            Stamina.Add(-amount);
            return true;
        }

        /// <summary>Adds food units (already multiplied by nutrition). Returns how much fit.</summary>
        public float Eat(float units) => Food.Add(units);

        /// <summary>Adds water units. Returns how much fit.</summary>
        public float Drink(float units) => Water.Add(units);

        public void Heal(float amount) { if (!IsFainted) Health.Add(amount); }

        public void TakeDamage(DamageInfo info)
        {
            if (IsFainted || !SimulateLocally) return;
            if (NoDrain) return; // Explorer mode: nobody gets hurt
            ApplyDamage(info);
            OnDamaged?.Invoke(info);
        }

        void ApplyDamage(DamageInfo info)
        {
            Health.Add(-info.amount);
            if (Health.IsEmpty) Faint(info.attacker);
        }

        public void Faint(GameObject attacker)
        {
            if (IsFainted) return;
            IsFainted = true;
            IsRunning = false;
            OnFainted?.Invoke(attacker);
            GameEvents.RaiseFainted(gameObject, attacker);
        }

        /// <summary>Wakes the dino up with the given fill (0..1) on every stat.</summary>
        public void Revive(float fill = 1f)
        {
            IsFainted = false;
            Health.Set(Health.Max * fill);
            Stamina.Set(Stamina.Max);
            Food.Set(Food.Max * Mathf.Max(fill, 0.5f));
            Water.Set(Water.Max * Mathf.Max(fill, 0.5f));
            OnRevived?.Invoke();
        }

        /// <summary>Restores exact values (loading a save or receiving network state).</summary>
        public void SetValues(float health, float stamina, float food, float water)
        {
            Health.Set(health); Stamina.Set(stamina); Food.Set(food); Water.Set(water);
            IsFainted = Health.IsEmpty;
        }
    }
}
