using System;
using Dino.Core;
using Dino.Species;
using Dino.World;
using UnityEngine;

namespace Dino.Player
{
    public enum InteractKind { None, Food, Water, Nest }

    /// <summary>
    /// Finds what the dino can eat, drink or rest at right now, and does it while Action is held.
    /// Reach and how high it can feed scale with growth stage (hatchlings can't reach tree leaves).
    /// </summary>
    [RequireComponent(typeof(DinoActor))]
    public class DinoInteractor : MonoBehaviour
    {
        public InteractKind Kind { get; private set; }
        public IFoodSource TargetFood { get; private set; }
        public IWaterSource TargetWater { get; private set; }
        public Nest TargetNest { get; private set; }
        public bool IsEating { get; private set; }

        /// <summary>Kid-readable label for the action button.</summary>
        public string ActionLabel => Kind switch
        {
            InteractKind.Food => TargetFood.FoodType switch
            {
                FoodType.Meat => "Eat meat",
                FoodType.Bugs => "Eat bugs",
                FoodType.Leaves => "Eat leaves",
                FoodType.Berries => "Eat berries",
                FoodType.Fish => "Catch fish",
                _ => "Eat",
            },
            InteractKind.Water => "Drink",
            InteractKind.Nest => "Rest",
            _ => "Eat",
        };

        /// <summary>Raised when the dino rests at a nest (Survival: becomes the respawn point).</summary>
        public event Action<Nest> RestedAtNest;

        const float ScanInterval = 0.15f;
        const float WaterPerSip = 12f;

        DinoActor _actor;
        float _scanTimer, _biteTimer;

        void Awake() => _actor = GetComponent<DinoActor>();

        float Reach => Mathf.Max(0.8f, _actor.Species.reach * _actor.Scale) + _actor.Controller.radius;
        float MaxFeedHeight => _actor.Species.feedHeight * _actor.Scale + 0.4f;

        void Update()
        {
            if (_actor.Species == null || _actor.Stats.IsFainted) { Kind = InteractKind.None; IsEating = false; return; }

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f) { _scanTimer = ScanInterval; Scan(); }

            bool held = _actor.Motor.Input.Action;
            IsEating = held && Kind != InteractKind.None;
            _actor.Animator.SafeBool("Eating", IsEating);
            if (!IsEating) { _biteTimer = 0f; return; }

            FaceTarget();
            _biteTimer -= Time.deltaTime;
            if (_biteTimer > 0f) return;
            _biteTimer = _actor.Species.biteInterval;
            DoBite();
        }

        void Scan()
        {
            var sp = _actor.Species;
            Vector3 pos = transform.position;
            float reach = Reach;

            TargetFood = _actor.Stats.Food.IsFull ? null
                : WorldRegistry.FindNearestFood(pos, reach, sp.EdibleMask, MaxFeedHeight);
            TargetWater = _actor.Stats.Water.IsFull ? null : WorldRegistry.FindNearestWater(pos, reach + 0.5f);
            TargetNest = Nest.FindWithin(pos);

            // Prefer whichever meter is emptier; nests only when nothing else to do.
            if (TargetFood != null && TargetWater != null)
                Kind = _actor.Stats.Food.Normalized <= _actor.Stats.Water.Normalized ? InteractKind.Food : InteractKind.Water;
            else if (TargetFood != null) Kind = InteractKind.Food;
            else if (TargetWater != null) Kind = InteractKind.Water;
            else if (TargetNest != null) Kind = InteractKind.Nest;
            else Kind = InteractKind.None;
        }

        void DoBite()
        {
            var sp = _actor.Species;
            float stageMul = Mathf.Lerp(0.4f, 1f, _actor.Scale);
            switch (Kind)
            {
                case InteractKind.Food:
                    if (TargetFood == null || !TargetFood.HasFood) { Scan(); return; }
                    float want = sp.biteSize * stageMul;
                    float taken = TargetFood.Bite(want, gameObject);
                    float nutrition = sp.FoodMultiplier(TargetFood.FoodType);
                    _actor.Stats.Eat(taken * nutrition);
                    if (taken > 0f) GameEvents.RaiseAte(gameObject, TargetFood.FoodType, taken * nutrition);
                    break;
                case InteractKind.Water:
                    if (TargetWater == null) { Scan(); return; }
                    float sip = TargetWater.Drink(WaterPerSip * stageMul, gameObject);
                    _actor.Stats.Drink(sip);
                    if (sip > 0f) GameEvents.RaiseDrank(gameObject, sip);
                    break;
                case InteractKind.Nest:
                    if (TargetNest == null) return;
                    _actor.Stats.Heal(_actor.Stats.Health.Max * 0.05f);
                    RestedAtNest?.Invoke(TargetNest);
                    break;
            }
        }

        void FaceTarget()
        {
            Vector3 p;
            if (Kind == InteractKind.Food && TargetFood != null) p = TargetFood.GetFeedPoint(transform.position);
            else if (Kind == InteractKind.Water && TargetWater != null) p = TargetWater.GetDrinkPoint(transform.position);
            else return;
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), _actor.Species.turnSpeed * Time.deltaTime);
        }
    }
}
