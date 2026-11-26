using Dino.Survival;
using UnityEngine;

namespace Dino.Player
{
    /// <summary>
    /// A bite / tail swipe in front of the dino. Cartoon combat: the target loses health and faints at zero.
    /// Also plays the friendly Roar emote.
    /// </summary>
    [RequireComponent(typeof(DinoActor))]
    public class DinoAttack : MonoBehaviour
    {
        public float staminaCost = 6f;
        float _cooldown;
        DinoActor _actor;
        readonly Collider[] _hits = new Collider[16];

        public float CooldownRemaining => Mathf.Max(0f, _cooldown);

        void Awake() => _actor = GetComponent<DinoActor>();

        void Update()
        {
            if (_actor.Species == null) return;
            _cooldown -= Time.deltaTime;
            var input = _actor.Motor.Input;
            if (_actor.Stats.IsFainted || (_actor.Effects && _actor.Effects.IsStunned)) return;

            if (input.Call) _actor.Animator.SafeTrigger("Call");
            if (input.Attack && _cooldown <= 0f) TryAttack();
        }

        public bool TryAttack()
        {
            if (_cooldown > 0f || !_actor.Stats.TryUseStamina(staminaCost)) return false;
            _cooldown = _actor.Species.attackCooldown;
            _actor.Animator.SafeTrigger("Attack");

            float reach = Mathf.Max(0.8f, _actor.Species.reach * _actor.Scale);
            float radius = Mathf.Max(0.5f, _actor.Controller.radius * 1.2f);
            Vector3 center = transform.position + Vector3.up * _actor.Height * 0.5f + transform.forward * (_actor.Controller.radius + reach * 0.6f);
            int n = Physics.OverlapSphereNonAlloc(center, radius + reach * 0.5f, _hits, ~0, QueryTriggerInteraction.Ignore);
            float damage = _actor.Species.attackDamage * _actor.Stats.DamageMultiplier;
            bool hitAny = false;
            for (int i = 0; i < n; i++)
            {
                var target = _hits[i].GetComponentInParent<IDamageable>();
                if (target == null || ReferenceEquals(target, _actor.Stats) || target.IsFainted) continue;
                target.TakeDamage(new DamageInfo(damage, gameObject, _hits[i].ClosestPoint(center)));
                hitAny = true;
                break; // one target per bite
            }
            return hitAny;
        }
    }
}
