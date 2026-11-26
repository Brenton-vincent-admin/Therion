using Dino.Core;
using Dino.Input;
using Dino.Player;
using Dino.Survival;
using Dino.World;
using UnityEngine;
using UnityEngine.AI;

namespace Dino.Species.AI
{
    /// <summary>
    /// Simple, readable AI for wild dinos. It drives the same DinoMotor as the player by writing a
    /// world-space DinoInputState each frame, so wild and player dinos move and fight the same way.
    /// Every think tick it builds a <see cref="WildSnapshot"/> and asks <see cref="WildDinoDecisions"/>
    /// what to do. Kid-friendly rules: predators give up quickly, never attack inside nests, and
    /// fainted wild dinos turn into a cartoon meat pile.
    /// </summary>
    [RequireComponent(typeof(DinoActor))]
    [RequireComponent(typeof(DinoStealth))]
    public class WildDinoBrain : MonoBehaviour
    {
        [Tooltip("Seconds between decisions. Spread out so many dinos don't think on the same frame.")]
        public float thinkInterval = 0.3f;
        [Tooltip("How far it looks for food and water (m).")]
        public float searchRadius = 60f;
        [Tooltip("How far it wanders from home (m).")]
        public float homeRadius = 40f;
        [Tooltip("Meat left behind when it faints, per size step.")]
        public float meatPerSizeStep = 40f;
        [Tooltip("Seconds before the fainted body is replaced by the meat pile.")]
        public float faintToMeatSeconds = 1.5f;

        public WildState State => _decision.state;
        public Vector3 Home { get; set; }

        /// <summary>Raised when this wild dino has fainted and been removed (spawner refills).</summary>
        public event System.Action<WildDinoBrain> Despawned;

        DinoActor _actor;
        DinoStealth _self;
        DinoStatusEffects _fx;
        WildDecision _decision;
        DinoStealth _target;
        float _nextThink, _chaseTime, _giveUpCooldown, _scaredUntil, _blindUntil, _warnStarted;
        Vector3 _wanderPoint, _fleeFrom;
        DinoStealth _lastAttacker; float _lastAttackedAt;
        IFoodSource _food; IWaterSource _water; IFoodSource _carcass;
        float _nextBite;
        NavMeshPath _path;
        Vector3[] _corners = new Vector3[16]; int _cornerCount, _cornerIndex; float _nextRepath;
        readonly DinoStealth[] _seenRefs = new DinoStealth[16];
        readonly SeenDino[] _seen = new SeenDino[16];
        bool _fainted;

        SpeciesDefinition Species => _actor.Species;
        SurvivalStats Stats => _actor.Stats;

        void Awake()
        {
            _actor = GetComponent<DinoActor>();
            _self = GetComponent<DinoStealth>();
            _fx = GetComponent<DinoStatusEffects>();
            _self.isPlayer = false;
            _path = new NavMeshPath();
        }

        void Start()
        {
            if (Home == Vector3.zero) Home = transform.position;
            _wanderPoint = transform.position;
            _nextThink = Time.time + Random.value * thinkInterval;
            if (Stats != null)
            {
                Stats.OnDamaged += OnDamaged;
                Stats.OnFainted += OnFainted;
            }
        }

        void OnDestroy()
        {
            if (Stats != null) { Stats.OnDamaged -= OnDamaged; Stats.OnFainted -= OnFainted; }
        }

        // ---------------- external pokes from abilities ----------------

        /// <summary>Roar / stomp: run away from 'from' for a while.</summary>
        public void Scare(Vector3 from, float seconds)
        {
            _fleeFrom = from; _scaredUntil = Time.time + seconds; _target = null; _nextThink = 0f;
        }

        /// <summary>Frill flash: can't see anyone for a while (it stumbles about).</summary>
        public void Blind(float seconds) { _blindUntil = Time.time + seconds; _target = null; _nextThink = 0f; }

        /// <summary>Snatch level 3 / dust cloud: lose track of the current target.</summary>
        public void LoseTarget(float cooldown)
        {
            _target = null; _chaseTime = 0f; _giveUpCooldown = Mathf.Max(_giveUpCooldown, cooldown);
        }

        // ---------------- main loop ----------------

        void Update()
        {
            if (_fainted || Species == null || Stats == null || _actor.Motor == null) return;
            if (_giveUpCooldown > 0f) _giveUpCooldown -= Time.deltaTime;

            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + thinkInterval;
                Think();
            }
            Act(Time.deltaTime);
        }

        void Think()
        {
            bool blind = Time.time < _blindUntil;
            int count = blind ? 0 : Perceive();

            var snap = new WildSnapshot
            {
                diet = Species.diet, size = Species.size, temperament = Species.temperament,
                food = Stats.Food.Normalized, water = Stats.Water.Normalized,
                health = Stats.Health.Normalized, stamina = Stats.Stamina.Normalized,
                isNight = WildDinoSettings.IsNight, friendlyPredators = WildDinoSettings.FriendlyPredators,
                hardcore = WildDinoSettings.Hardcore, chaseSeconds = _chaseTime, giveUpCooldown = _giveUpCooldown,
                lastAttackerIndex = -1, seen = _seen, seenCount = count,
            };

            if (_lastAttacker != null && Time.time - _lastAttackedAt < 6f && !_lastAttacker.IsFainted)
            {
                int idx = System.Array.IndexOf(_seenRefs, _lastAttacker, 0, count);
                if (idx < 0 && count < _seen.Length) { idx = count; Fill(count, _lastAttacker); snap.seenCount = ++count; }
                if (idx >= 0)
                {
                    snap.lastAttackerIndex = idx;
                    snap.lastAttackerSize = _lastAttacker.Size;
                    snap.lastAttackerDistance = _seen[idx].distance;
                }
            }

            float maxFeed = Species.feedHeight * Species.Stage(_actor.StageIndex).scale + 0.5f;
            _food = WorldRegistry.FindNearestFood(transform.position, searchRadius, Species.EdibleMask & ~FoodType.Meat, maxFeed);
            _carcass = (Species.EdibleMask & FoodType.Meat) != 0
                ? WorldRegistry.FindNearestFood(transform.position, searchRadius, FoodType.Meat, 100f) : null;
            _water = WorldRegistry.FindNearestWater(transform.position, searchRadius);
            snap.foodKnown = _food != null; snap.carcassKnown = _carcass != null; snap.waterKnown = _water != null;

            var previous = _decision.state;
            _decision = Time.time < _scaredUntil
                ? WildDecision.Of(WildState.Flee, -1, run: true)
                : WildDinoDecisions.Decide(snap, AttackRange());

            _target = _decision.targetIndex >= 0 && _decision.targetIndex < count ? _seenRefs[_decision.targetIndex] : null;
            if (_decision.state == WildState.Flee && _target != null) _fleeFrom = _target.transform.position;

            // Chase bookkeeping: give up after a few seconds so kids can always escape.
            if (_decision.state == WildState.Chase) _chaseTime += thinkInterval;
            else if (previous == WildState.Chase && _decision.state != WildState.Attack)
            {
                if (_chaseTime > 0f) _giveUpCooldown = WildDinoDecisions.ChaseGiveUpSeconds(WildDinoSettings.Hardcore) * 1.5f;
                _chaseTime = 0f;
            }
            if (_chaseTime >= WildDinoDecisions.ChaseGiveUpSeconds(WildDinoSettings.Hardcore)) { LoseTarget(12f); }

            if (_decision.state == WildState.Warn && previous != WildState.Warn) _warnStarted = Time.time;
            if (_decision.state == WildState.Wander && (transform.position - _wanderPoint).sqrMagnitude < 4f) PickWanderPoint();
        }

        int Perceive()
        {
            int n = 0;
            var all = DinoStealth.All;
            bool night = WildDinoSettings.IsNight;
            for (int i = 0; i < all.Count && n < _seen.Length; i++)
            {
                var o = all[i];
                if (o == _self || o == null || !o.isActiveAndEnabled) continue;
                if (o.Effects != null && o.Effects.IsHidden && o.isPlayer) continue;
                if (_self.Sees(o, night) != Awareness.Seen) continue;
                Fill(n++, o);
            }
            return n;
        }

        void Fill(int i, DinoStealth o)
        {
            _seenRefs[i] = o;
            _seen[i] = new SeenDino
            {
                index = i,
                distance = Vector3.Distance(transform.position, o.transform.position),
                size = o.Size, isPredator = o.IsPredator, isPlayer = o.isPlayer, isFainted = o.IsFainted,
                inSafeZone = o.InSafeZone, isSameSpecies = o.Species == Species,
            };
        }

        float AttackRange() => Species.reach * Species.Stage(_actor.StageIndex).scale + 1.5f;

        void Act(float dt)
        {
            var input = new DinoInputState { MoveIsWorldSpace = true };
            Vector3 pos = transform.position;

            switch (_decision.state)
            {
                case WildState.Wander:
                    Steer(ref input, _wanderPoint, 1.5f);
                    break;

                case WildState.Rest:
                    break; // stand still; stamina refills (Resting bool set below)

                case WildState.Flee:
                {
                    Vector3 away = pos - _fleeFrom; away.y = 0f;
                    if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                    Steer(ref input, pos + away.normalized * 15f, 0f);
                    input.Run = Stats.CanRun;
                    break;
                }

                case WildState.Chase:
                    if (_target == null || _target.IsFainted || _target.InSafeZone) { _decision.state = WildState.Wander; break; }
                    Steer(ref input, _target.transform.position, AttackRange() * 0.8f);
                    input.Run = _decision.run && Stats.CanRun;
                    input.Sneak = _decision.sneak;
                    break;

                case WildState.Attack:
                    if (_target == null || _target.IsFainted || _target.InSafeZone) { _decision.state = WildState.Wander; break; }
                    Face(ref input, _target.transform.position);
                    TryBite(_target);
                    break;

                case WildState.Warn:
                    if (_target != null) Face(ref input, _target.transform.position);
                    if (Time.time - _warnStarted < 0.1f) input.Call = true; // warning bellow
                    if (Time.time - _warnStarted > 2.5f && _target != null) { _decision.state = WildState.Chase; _decision.run = true; }
                    break;

                case WildState.Graze:
                    if (_food == null || !_food.HasFood) { _decision.state = WildState.Wander; break; }
                    if (Reach(ref input, _food.GetFeedPoint(pos))) EatFrom(_food);
                    break;

                case WildState.Scavenge:
                    if (_carcass == null || !_carcass.HasFood) { _decision.state = WildState.Wander; break; }
                    if (Reach(ref input, _carcass.GetFeedPoint(pos))) EatFrom(_carcass);
                    break;

                case WildState.Drink:
                    if (_water == null || !_water.CanDrink) { _decision.state = WildState.Wander; break; }
                    if (Reach(ref input, _water.GetDrinkPoint(pos)) && Time.time >= _nextBite)
                    {
                        _nextBite = Time.time + Species.biteInterval;
                        float got = _water.Drink(Species.biteSize, gameObject);
                        Stats.Drink(got);
                        _actor.Animator.SafeTrigger("Drink");
                        GameEvents.RaiseDrank(gameObject, got);
                        if (Stats.Water.IsFull) _nextThink = 0f;
                    }
                    break;
            }

            if (_fx != null && _fx.IsStunned) input = new DinoInputState { MoveIsWorldSpace = true };
            _actor.Motor.Input = input;
            _actor.Animator.SafeBool("Resting", _decision.state == WildState.Rest);
        }

        float _nextAttack;

        /// <summary>
        /// Wild dinos bite directly (cooldown from species data) so they work before/without a player
        /// attack component. Territorial dinos shove rather than bite hard.
        /// </summary>
        void TryBite(DinoStealth target)
        {
            if (Time.time < _nextAttack || target == null || target.InSafeZone) return;
            if (target.isPlayer && WildDinoSettings.FriendlyPredators) return;
            Vector3 d = target.transform.position - transform.position; d.y = 0f;
            if (d.magnitude > AttackRange() + 0.5f) return;
            _nextAttack = Time.time + Species.attackCooldown;
            var victim = target.GetComponent<IDamageable>();
            if (victim == null || victim.IsFainted) return;
            float dmg = Species.attackDamage * Stats.DamageMultiplier;
            if (Species.temperament == Temperament.Territorial && target.Size <= Species.size) dmg *= 0.5f;
            victim.TakeDamage(new DamageInfo(dmg, gameObject, target.transform.position));
            _self.BreakStealth();
            _actor.Animator.SafeTrigger("Attack");
            var fx = target.Effects;
            if (fx != null && Species.temperament == Temperament.Territorial) fx.Dash(d, 6f, 0.25f); // shove away
        }

        bool Reach(ref DinoInputState input, Vector3 point)
        {
            float reach = Species.reach * Species.Stage(_actor.StageIndex).scale + 0.6f;
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.magnitude <= reach) { Face(ref input, point); return true; }
            Steer(ref input, point, reach * 0.8f);
            return false;
        }

        void EatFrom(IFoodSource src)
        {
            if (Time.time < _nextBite) return;
            _nextBite = Time.time + Species.biteInterval;
            float taken = src.Bite(Species.biteSize, gameObject);
            Stats.Eat(taken * Species.FoodMultiplier(src.FoodType));
            _actor.Animator.SafeTrigger("Eat");
            GameEvents.RaiseAte(gameObject, src.FoodType, taken);
            if (Stats.Food.IsFull) _nextThink = 0f;
        }

        void Face(ref DinoInputState input, Vector3 point)
        {
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            // A tiny push toward the target turns the dino without walking it forward much.
            Vector3 n = d.normalized * 0.05f;
            input.Move = new Vector2(n.x, n.z);
        }

        void Steer(ref DinoInputState input, Vector3 destination, float stopDistance)
        {
            Vector3 pos = transform.position;
            Vector3 flat = destination - pos; flat.y = 0f;
            if (flat.magnitude <= stopDistance) return;

            Vector3 next = destination;
            if (Time.time >= _nextRepath || _cornerIndex >= _cornerCount)
            {
                _nextRepath = Time.time + 0.75f;
                if (NavMesh.CalculatePath(pos, destination, NavMesh.AllAreas, _path) && _path.status != NavMeshPathStatus.PathInvalid)
                {
                    _cornerCount = _path.GetCornersNonAlloc(_corners);
                    _cornerIndex = _cornerCount > 1 ? 1 : 0;
                }
                else _cornerCount = 0; // no NavMesh baked yet: walk straight
            }
            if (_cornerCount > 0 && _cornerIndex < _cornerCount)
            {
                next = _corners[_cornerIndex];
                Vector3 toCorner = next - pos; toCorner.y = 0f;
                if (toCorner.magnitude < 1f) _cornerIndex++;
            }

            Vector3 dir = next - pos; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            input.Move = new Vector2(dir.x, dir.z);
        }

        void PickWanderPoint()
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 r = Random.insideUnitCircle * homeRadius;
                Vector3 p = Home + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) { _wanderPoint = hit.position; return; }
            }
            _wanderPoint = Home;
        }

        void OnDamaged(DamageInfo info)
        {
            if (info.attacker == null) return;
            var s = info.attacker.GetComponentInParent<DinoStealth>();
            if (s == null || s == _self) return;
            _lastAttacker = s; _lastAttackedAt = Time.time; _nextThink = 0f;
        }

        void OnFainted(GameObject attacker)
        {
            if (_fainted) return;
            _fainted = true;
            _actor.Motor.Input = new DinoInputState { MoveIsWorldSpace = true };
            Invoke(nameof(BecomeMeat), faintToMeatSeconds);
        }

        void BecomeMeat()
        {
            float scale = Species.Stage(_actor.StageIndex).scale;
            float meat = meatPerSizeStep * (1 + (int)Species.size) * Mathf.Max(0.3f, scale);
            Carcass.Spawn(transform.position, meat);
            Despawned?.Invoke(this);
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_self == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _self.NoticeRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _wanderPoint);
        }
#endif
    }
}
