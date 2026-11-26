using System.Collections.Generic;
using Dino.Player;
using Dino.Survival;
using UnityEngine;

namespace Dino.Species
{
    /// <summary>
    /// Put on every dino (player and wild). Works out how noticeable the dino is right now and
    /// keeps a static list so wild AI and the eye icon can find dinos without physics queries.
    /// Reads stance from the same DinoInputState that drives the motor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DinoActor))]
    public class DinoStealth : MonoBehaviour
    {
        static readonly List<DinoStealth> _all = new List<DinoStealth>();
        public static IReadOnlyList<DinoStealth> All => _all;

        [Tooltip("True for player dinos (local or remote). Wild dinos leave this off.")]
        public bool isPlayer;

        public DinoActor Actor { get; private set; }
        public SpeciesDefinition Species => Actor != null ? Actor.Species : null;
        public SurvivalStats Stats { get; private set; }
        public DinoStatusEffects Effects { get; private set; }

        /// <summary>Distance at which others notice this dino this frame (meters).</summary>
        public float NoticeRange { get; private set; } = SpeciesTuning.NormalNoticeRadius;
        public Stance CurrentStance { get; private set; }
        public bool InCover => _coverCount > 0;
        public bool InSafeZone => _safeCount > 0;
        public bool IsFainted => Stats != null && Stats.IsFainted;
        public float Scale => Species != null ? Species.Stage(Actor.StageIndex).scale : 1f;
        public SizeClass Size => Species != null ? Species.size : SizeClass.Medium;
        public bool IsPredator => Species != null && (Species.temperament == Temperament.Predator || Species.diet == Diet.Carnivore);

        /// <summary>Hide ability (Psittacosaurus) sets these.</summary>
        public bool HideActive { get; set; }
        public int HideLevel { get; set; }

        /// <summary>Parasaur's call marks dangers for friends until this time.</summary>
        public float HighlightedUntil { get; set; }
        public bool IsHighlighted => Time.time < HighlightedUntil;

        float _brokenUntil;
        int _coverCount, _safeCount;
        float _variantNotice = 1f;

        void Awake()
        {
            Actor = GetComponent<DinoActor>();
            Stats = GetComponent<SurvivalStats>();
            Effects = GetComponent<DinoStatusEffects>();
        }

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() { _all.Remove(this); _coverCount = 0; _safeCount = 0; }

        /// <summary>Call after Setup or when the variant changes.</summary>
        public void RefreshVariant()
        {
            var v = Species != null ? Species.FindVariant(Actor.VariantId) : null;
            _variantNotice = v != null ? v.noticeRadiusMultiplier : 1f;
        }

        /// <summary>Running, attacking, calling or roaring makes the dino loud for a few seconds.</summary>
        public void BreakStealth(float seconds = StealthRules.StealthBreakSeconds)
            => _brokenUntil = Mathf.Max(_brokenUntil, Time.time + seconds);

        void Update()
        {
            if (Species == null) return;
            var input = Actor.Motor != null ? Actor.Motor.Input : default;

            Stance stance;
            if (input.Attack) stance = Stance.Attacking;
            else if (input.Call) stance = Stance.Calling;
            else if (input.Move.sqrMagnitude < 0.01f && (Effects == null || !Effects.IsDashing)) stance = Stance.Still;
            else if (input.Run || (Effects != null && Effects.IsDashing)) stance = Stance.Running;
            else if (input.Sneak) stance = Stance.Sneaking;
            else stance = Stance.Walking;
            CurrentStance = stance;
            if (stance == Stance.Running || stance == Stance.Attacking || stance == Stance.Calling) BreakStealth();

            var ctx = new StealthContext
            {
                stance = stance, inCover = InCover, stealthBroken = Time.time < _brokenUntil,
                hideActive = HideActive, hideLevel = HideLevel, scale = Scale,
            };
            NoticeRange = StealthRules.NoticeRange(Species.noticeRadius * _variantNotice, Species.sneakNoticeMultiplier, Species.stealthy, ctx);
            if (Effects != null) Effects.IsHidden = NoticeRange <= StealthRules.FaceToFaceRange + 0.01f;
        }

        /// <summary>How well this dino (as observer) sees 'target' right now.</summary>
        public Awareness Sees(DinoStealth target, bool isNight)
        {
            if (target == null || target == this) return Awareness.Hidden;
            bool nightVision = Species != null && Species.signatureAbility == SignatureAbility.NightVision;
            float eyes = StealthRules.EyesMultiplier(Species != null && Species.stealthy, isNight, nightVision);
            float d = Vector3.Distance(transform.position, target.transform.position);
            return StealthRules.Check(d, target.NoticeRange, eyes);
        }

        /// <summary>The player's eye icon: the most aware any wild dino is of this dino.</summary>
        public Awareness MostAwareWildObserver(bool isNight)
        {
            var best = Awareness.Hidden;
            for (int i = 0; i < _all.Count; i++)
            {
                var o = _all[i];
                if (o == this || o.isPlayer || o.IsFainted) continue;
                var a = o.Sees(this, isNight);
                if (a > best) best = a;
                if (best == Awareness.Seen) break;
            }
            return best;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<CoverVolume>() != null) _coverCount++;
            if (other.GetComponentInParent<Dino.World.Nest>() != null) _safeCount++;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<CoverVolume>() != null) _coverCount = Mathf.Max(0, _coverCount - 1);
            if (other.GetComponentInParent<Dino.World.Nest>() != null) _safeCount = Mathf.Max(0, _safeCount - 1);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all.Clear();
    }
}
