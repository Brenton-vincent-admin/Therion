using System;
using System.Collections.Generic;
using Dino.Player;
using Dino.Species.AI;
using Dino.Survival;
using Dino.World;
using UnityEngine;

namespace Dino.Species
{
    /// <summary>Optional world hooks the map thread can implement for level-3 abilities.</summary>
    public interface IShoveable { void Shove(Vector3 direction, GameObject by); }   // Trike Lv3: boulders
    public interface IShakeable { void ShakeLoose(GameObject by); }                 // Brachio Lv3: fruit trees

    /// <summary>Global signals for VFX, audio and UI. Gameplay effects are already applied when these fire.</summary>
    public static class SignatureAbilityEvents
    {
        public static event Action<GameObject, SignatureAbility, int> Used;
        public static event Action<GameObject, bool> NightVisionChanged;
        public static event Action<GameObject, IReadOnlyList<IFoodSource>> FoodRevealed;

        internal static void RaiseUsed(GameObject g, SignatureAbility a, int l) => Used?.Invoke(g, a, l);
        internal static void RaiseNightVision(GameObject g, bool on) => NightVisionChanged?.Invoke(g, on);
        internal static void RaiseFoodRevealed(GameObject g, IReadOnlyList<IFoodSource> f) => FoodRevealed?.Invoke(g, f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Used = null; NightVisionChanged = null; FoodRevealed = null; }
    }

    /// <summary>
    /// Runs the species' one signature ability (design doc table, levels 1/2/3 by growth stage).
    /// Call <see cref="TryActivate"/> from the ability button. Writes to the core's DinoStatusEffects
    /// for movement (dash, speed, glide gravity) and to wild brains for scare and blind.
    /// Player-vs-player damage is off by default (design doc), so abilities only hurt wild dinos.
    /// </summary>
    [RequireComponent(typeof(DinoActor))]
    public class SignatureAbilityRunner : MonoBehaviour
    {
        public static bool PlayerVsPlayerDamage = false;

        public DinoActor Actor { get; private set; }
        public SignatureAbility Ability => Actor.Species != null ? Actor.Species.signatureAbility : SignatureAbility.None;
        public int Level => Actor.Species != null ? Actor.Species.AbilityLevel(Actor.StageIndex) : 1;
        public AbilitySpec Spec => AbilityTuning.Get(Ability, Level);

        /// <summary>0..1 for the button's cooldown ring (1 = ready).</summary>
        public float Readiness => _cooldownTotal <= 0f ? 1f : Mathf.Clamp01(1f - (_readyAt - Time.time) / _cooldownTotal);
        public bool IsReady => Time.time >= _readyAt;
        public bool IsActive => Time.time < _activeUntil;
        public bool NightVisionOn { get; private set; }

        DinoStatusEffects _fx;
        DinoStealth _stealth;
        float _readyAt, _cooldownTotal, _activeUntil, _baseSpeed = 1f, _baseGravity = 1f;
        bool _wasStealthyAtStart;
        readonly HashSet<GameObject> _hitThisUse = new HashSet<GameObject>();
        readonly List<IFoodSource> _foodBuffer = new List<IFoodSource>();
        readonly List<DinoStealth> _friendsHidden = new List<DinoStealth>();
        static readonly Collider[] _overlap = new Collider[32];

        float Scale => Actor.Species != null ? Actor.Species.Stage(Actor.StageIndex).scale : 1f;
        float Range => Spec.range * Mathf.Lerp(0.6f, 1f, Scale); // hatchlings have a smaller reach

        void Awake()
        {
            Actor = GetComponent<DinoActor>();
            _fx = GetComponent<DinoStatusEffects>(); // DinoActor requires it
            _stealth = GetComponent<DinoStealth>();
        }

        public bool TryActivate()
        {
            if (Actor.Species == null || Ability == SignatureAbility.None || !IsReady) return false;
            if (Actor.Stats != null && (Actor.Stats.IsFainted || _fx.IsStunned)) return false;

            if (Ability == SignatureAbility.NightVision) { ToggleNightVision(); return true; }
            if (Ability == SignatureAbility.Glide && !HighEnoughToGlide()) return false; // can't take off from flat ground

            var spec = Spec;
            if (Actor.Stats != null && spec.staminaCost > 0f && !Actor.Stats.TryUseStamina(spec.staminaCost)) return false;

            _cooldownTotal = spec.cooldown;
            _readyAt = Time.time + spec.cooldown;
            _activeUntil = Time.time + spec.duration;
            _hitThisUse.Clear();
            _wasStealthyAtStart = _stealth != null && _stealth.NoticeRange <= Actor.Species.noticeRadius * Actor.Species.sneakNoticeMultiplier + 0.1f;

            Begin(spec);
            Actor.Animator.SafeTrigger("Ability");
            SignatureAbilityEvents.RaiseUsed(gameObject, Ability, Level);
            return true;
        }

        void Begin(AbilitySpec spec)
        {
            var sp = Actor.Species;
            Vector3 fwd = transform.forward;
            switch (Ability)
            {
                case SignatureAbility.Call:
                    _stealth?.BreakStealth();
                    foreach (var d in DinoStealth.All)
                    {
                        if (d == _stealth || d.isPlayer || !d.IsPredator) continue;
                        if (Dist(d) <= Range) d.HighlightedUntil = Time.time + spec.duration;
                    }
                    if (Level >= 3) BoostFriends(spec.power, spec.duration);
                    break;

                case SignatureAbility.HeadButt:
                case SignatureAbility.HornCharge:
                    _fx.Dash(fwd, sp.runSpeed * Mathf.Lerp(0.7f, 1f, Scale) * 1.6f, spec.duration);
                    break;

                case SignatureAbility.Pounce:
                    _fx.Dash(fwd, sp.runSpeed * Mathf.Lerp(0.7f, 1f, Scale) * 1.8f, spec.duration);
                    break;

                case SignatureAbility.Hide:
                    if (_stealth != null) { _stealth.HideActive = true; _stealth.HideLevel = Level; }
                    if (Level >= 3)
                        foreach (var d in DinoStealth.All)
                            if (d != _stealth && d.isPlayer && Dist(d) <= spec.range) { d.HideActive = true; d.HideLevel = 2; _friendsHidden.Add(d); }
                    break;

                case SignatureAbility.TailSwipe:
                    HitAround(behind: true, damageMul: spec.power, stun: Level >= 3 ? 1f : 0f, arcDot: Level >= 2 ? -0.1f : -0.4f);
                    break;

                case SignatureAbility.HighReachStomp:
                    ScareWild(Range, spec.duration, smallerOnly: false);
                    if (Level >= 3) ForNearby<IShakeable>(Range * 0.5f, s => s.ShakeLoose(gameObject));
                    break;

                case SignatureAbility.Snatch:
                {
                    float maxH = sp.feedHeight * Scale + 0.5f;
                    var food = WorldRegistry.FindNearestFood(transform.position, sp.reach * Scale + spec.range, sp.EdibleMask, maxH);
                    if (food != null && Actor.Stats != null)
                    {
                        float taken = food.Bite(sp.biteSize * 3f, gameObject);
                        Actor.Stats.Eat(taken * sp.FoodMultiplier(food.FoodType));
                        Core.GameEvents.RaiseAte(gameObject, food.FoodType, taken);
                    }
                    _fx.Dash(fwd, sp.runSpeed * spec.power, spec.duration);
                    if (Level >= 3) DistractWild(6f, 4f); // grab it right from under a bigger dino
                    break;
                }

                case SignatureAbility.SprintBurst:
                    _baseSpeed = _fx.SpeedMultiplier;
                    _fx.SpeedMultiplier = _baseSpeed * spec.power;
                    if (Level >= 3) DistractWild(spec.range, 5f); // dust cloud confuses chasers
                    break;

                case SignatureAbility.Glide:
                    _baseGravity = _fx.GravityMultiplier;
                    _fx.GravityMultiplier = spec.power;
                    _baseSpeed = _fx.SpeedMultiplier;
                    _fx.SpeedMultiplier = _baseSpeed * 1.2f;
                    break;

                case SignatureAbility.FrillFlash:
                {
                    var target = NearestInFront(spec.range, 0.4f);
                    if (target != null)
                    {
                        var brain = target.GetComponent<WildDinoBrain>();
                        float secs = spec.duration * (Level >= 3 && _wasStealthyAtStart ? 1.5f : 1f);
                        if (brain != null)
                        {
                            brain.Blind(secs);
                            if (Level >= 3 && _wasStealthyAtStart) brain.Scare(transform.position, secs);
                        }
                    }
                    _stealth?.BreakStealth();
                    break;
                }

                case SignatureAbility.Roar:
                    _stealth?.BreakStealth(5f);
                    ScareWild(Range, spec.duration, smallerOnly: true);
                    if (Level >= 3) BoostFriends(spec.power, spec.duration);
                    break;
            }
        }

        void Update()
        {
            if (Actor == null || Actor.Species == null) return;
            // The Power button (DinoInputState.Ability, pressed this frame) from touch, keyboard or network.
            if (Actor.Motor != null && Actor.Motor.Input.Ability) TryActivate();
            bool active = IsActive;

            switch (Ability)
            {
                case SignatureAbility.HeadButt:
                case SignatureAbility.HornCharge:
                case SignatureAbility.Pounce:
                    if (active && _fx.IsDashing) DashHits();
                    break;

                case SignatureAbility.Hide:
                    if (!active && _stealth != null && _stealth.HideActive)
                    {
                        _stealth.HideActive = false;
                        foreach (var d in _friendsHidden) if (d != null) d.HideActive = false;
                        _friendsHidden.Clear();
                    }
                    // Lv1 hide ends when you leave the grass or move fast.
                    if (active && _stealth != null && (!_stealth.InCover || _stealth.CurrentStance == Stance.Running)) _activeUntil = 0f;
                    break;

                case SignatureAbility.SprintBurst:
                    if (!active && _baseSpeed > 0f && !Mathf.Approximately(_fx.SpeedMultiplier, _baseSpeed)) _fx.SpeedMultiplier = _baseSpeed;
                    break;

                case SignatureAbility.Glide:
                    if (!active && !Mathf.Approximately(_fx.GravityMultiplier, _baseGravity))
                    {
                        _fx.GravityMultiplier = _baseGravity; _fx.SpeedMultiplier = _baseSpeed;
                    }
                    break;

                case SignatureAbility.NightVision:
                    if (NightVisionOn && Level >= 2 && Time.frameCount % 30 == 0) RevealFood();
                    if (NightVisionOn && Level >= 3 && WildDinoSettings.IsNight)
                        foreach (var d in DinoStealth.All)
                            if (d != _stealth && !d.isPlayer && Dist(d) <= 20f) d.HighlightedUntil = Time.time + 1f;
                    break;
            }
        }

        // ---------------- helpers ----------------

        void DashHits()
        {
            float r = Mathf.Max(0.6f, Actor.Species.adultRadius * Scale);
            Vector3 center = transform.position + transform.forward * (r + 0.5f) + Vector3.up * r;
            int n = Physics.OverlapSphereNonAlloc(center, r + 0.4f, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var go = _overlap[i].attachedRigidbody != null ? _overlap[i].attachedRigidbody.gameObject : _overlap[i].gameObject;
                if (!CanHurt(go, out var target)) continue;

                var spec = Spec;
                float mul = spec.power;
                float stun = 0f;
                var other = go.GetComponentInParent<DinoStealth>();
                bool smaller = other != null && other.Size < Actor.Species.size;

                if (Ability == SignatureAbility.HeadButt) stun = Level >= 2 ? 2f : 1.5f;
                if (Ability == SignatureAbility.Pounce)
                {
                    if (!smaller && other != null) mul = 1f;       // you can only pin smaller prey
                    else stun = 1.5f;
                    if (Level >= 3 && _wasStealthyAtStart) mul *= 2f;
                }
                Damage(target, go, mul);

                var fx = go.GetComponentInParent<DinoStatusEffects>();
                if (fx != null)
                {
                    if (Ability == SignatureAbility.HornCharge || (Ability == SignatureAbility.HeadButt && Level >= 3))
                        fx.Dash(transform.forward, 9f, 0.35f);   // knock back
                    if (stun > 0f) StartCoroutine(StunRoutine(fx, stun));
                }
                if (Ability == SignatureAbility.HeadButt && Level >= 3)
                    go.GetComponentInParent<WildDinoBrain>()?.Scare(transform.position, 3f); // predators get knocked over and run
                _fx.CancelDash();
                break;
            }

            if (Ability == SignatureAbility.HornCharge && Level >= 3)
                ForNearby<IShoveable>(Actor.Species.adultRadius * Scale + 1.5f, s => s.Shove(transform.forward, gameObject));
        }

        void HitAround(bool behind, float damageMul, float stun, float arcDot)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, Range, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var go = _overlap[i].attachedRigidbody != null ? _overlap[i].attachedRigidbody.gameObject : _overlap[i].gameObject;
                Vector3 to = go.transform.position - transform.position; to.y = 0f;
                float dot = Vector3.Dot(transform.forward, to.normalized);
                if (behind ? dot > arcDot : dot < -arcDot) continue;
                if (!CanHurt(go, out var target)) continue;
                Damage(target, go, damageMul);
                var fx = go.GetComponentInParent<DinoStatusEffects>();
                if (fx != null && stun > 0f) StartCoroutine(StunRoutine(fx, stun));
            }
        }

        bool CanHurt(GameObject go, out IDamageable target)
        {
            target = null;
            if (go == null || go == gameObject || go.transform.IsChildOf(transform)) return false;
            if (_hitThisUse.Contains(go)) return false;
            target = go.GetComponentInParent<IDamageable>();
            if (target == null || target.IsFainted) return false;
            var other = go.GetComponentInParent<DinoStealth>();
            bool iAmPlayer = _stealth != null && _stealth.isPlayer;
            if (other != null && other.isPlayer && iAmPlayer && !PlayerVsPlayerDamage) return false;
            if (other != null && other.InSafeZone) return false; // nests are always safe
            _hitThisUse.Add(go);
            return true;
        }

        void Damage(IDamageable target, GameObject go, float multiplier)
        {
            float dmg = Actor.Species.attackDamage * multiplier * (Actor.Stats != null ? Actor.Stats.DamageMultiplier : 1f);
            target.TakeDamage(new DamageInfo(dmg, gameObject, go.transform.position));
        }

        static System.Collections.IEnumerator StunRoutine(DinoStatusEffects fx, float seconds)
        {
            fx.IsStunned = true;
            yield return new WaitForSeconds(seconds);
            if (fx != null) fx.IsStunned = false;
        }

        void ScareWild(float range, float seconds, bool smallerOnly)
        {
            foreach (var d in DinoStealth.All)
            {
                if (d == _stealth || d.isPlayer || Dist(d) > range) continue;
                if (smallerOnly && d.Size >= Actor.Species.size) continue;
                d.GetComponent<WildDinoBrain>()?.Scare(transform.position, seconds);
            }
        }

        void DistractWild(float range, float cooldown)
        {
            foreach (var d in DinoStealth.All)
                if (d != _stealth && !d.isPlayer && Dist(d) <= range)
                    d.GetComponent<WildDinoBrain>()?.LoseTarget(cooldown);
        }

        void BoostFriends(float multiplier, float seconds)
        {
            foreach (var d in DinoStealth.All)
            {
                if (!d.isPlayer || Dist(d) > 20f || d.Effects == null) continue;
                StartCoroutine(BoostRoutine(d.Effects, multiplier, seconds));
            }
        }

        static System.Collections.IEnumerator BoostRoutine(DinoStatusEffects fx, float mul, float seconds)
        {
            fx.SpeedMultiplier *= mul;
            yield return new WaitForSeconds(seconds);
            if (fx != null) fx.SpeedMultiplier /= mul;
        }

        DinoStealth NearestInFront(float range, float minDot)
        {
            DinoStealth best = null; float bestD = range;
            foreach (var d in DinoStealth.All)
            {
                if (d == _stealth || d.IsFainted) continue;
                Vector3 to = d.transform.position - transform.position; to.y = 0f;
                float dist = to.magnitude;
                if (dist > bestD || Vector3.Dot(transform.forward, to / Mathf.Max(dist, 0.001f)) < minDot) continue;
                best = d; bestD = dist;
            }
            return best;
        }

        void ForNearby<T>(float radius, Action<T> action) where T : class
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var t = _overlap[i].GetComponentInParent<T>();
                if (t != null) action(t);
            }
        }

        bool HighEnoughToGlide()
        {
            // Must be up a tree or cliff: at least 1.5 m of air below.
            return !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, Spec.range + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        }

        void ToggleNightVision()
        {
            NightVisionOn = !NightVisionOn;
            _readyAt = Time.time + Spec.cooldown;
            _cooldownTotal = Spec.cooldown;
            SignatureAbilityEvents.RaiseNightVision(gameObject, NightVisionOn);
            SignatureAbilityEvents.RaiseUsed(gameObject, Ability, Level);
            if (NightVisionOn && Level >= 2) RevealFood();
        }

        void RevealFood()
        {
            _foodBuffer.Clear();
            var all = WorldRegistry.Food;
            float r2 = Spec.range * Spec.range;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || !f.HasFood || !Actor.Species.CanEat(f.FoodType)) continue;
                if ((f.GetFeedPoint(transform.position) - transform.position).sqrMagnitude <= r2) _foodBuffer.Add(f);
            }
            SignatureAbilityEvents.RaiseFoodRevealed(gameObject, _foodBuffer);
        }

        float Dist(Component c) => Vector3.Distance(transform.position, c.transform.position);
    }
}
