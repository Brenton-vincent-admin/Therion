using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dino.Species
{
    /// <summary>
    /// Everything that makes a species different. One asset per species in Assets/_Game/Data/Species.
    /// The generic dino controller reads only this; no species gets its own controller class.
    /// Values are for an ADULT; growth stages scale them down.
    /// </summary>
    [CreateAssetMenu(menuName = "Dino/Species Definition", fileName = "Species_New")]
    public partial class SpeciesDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable save id, lower_snake_case. Never change after release.")]
        public string id = "new_species";
        public string displayName = "New Dino";
        [TextArea] public string kidDescription;
        public Diet diet = Diet.Herbivore;
        public MoveType moveType = MoveType.Land;
        public SizeClass size = SizeClass.Medium;
        public Sprite icon;

        [Header("Model")]
        [Tooltip("Visual prefab (mesh + Animator). Null = placeholder capsule.")]
        public GameObject modelPrefab;
        [Tooltip("Adult capsule size in meters, used for the CharacterController.")]
        public float adultHeight = 2f;
        public float adultRadius = 0.6f;
        [Tooltip("Uniform scale applied to the model prefab at adult size.")]
        public float adultModelScale = 1f;
        public Color placeholderColor = new Color(0.4f, 0.7f, 0.3f);

        [Header("Unlock")]
        public bool isStarter;
        [Tooltip("Lifetime player XP needed to unlock (ignored for starters).")]
        public int unlockXp = 500;
        [Tooltip("Order on the species select screen.")]
        public int sortOrder;

        [Header("Survival stats (adult)")]
        public float maxHealth = 100f;
        public float maxStamina = 100f;
        public float maxFood = 100f;
        public float maxWater = 100f;
        [Tooltip("Food lost per minute at rest.")]
        public float foodDrainPerMinute = 4f;
        [Tooltip("Water lost per minute at rest.")]
        public float waterDrainPerMinute = 6f;
        [Tooltip("Stamina regained per second when not running.")]
        public float staminaRegenPerSecond = 12f;
        [Tooltip("Stamina spent per second while running.")]
        public float runStaminaPerSecond = 15f;
        [Tooltip("Health regained per second while food and water are above half.")]
        public float healthRegenPerSecond = 0.5f;

        [Header("Movement (adult, m/s)")]
        public float walkSpeed = 3f;
        public float runSpeed = 7f;
        public float swimSpeed = 2f;
        public float sneakSpeed = 1.5f;
        [Tooltip("Degrees per second.")]
        public float turnSpeed = 360f;
        public float jumpHeight = 0.8f;

        [Header("Eating and combat")]
        [Tooltip("What this species can eat and how filling each food is (1 = normal).")]
        public List<FoodPreference> foods = new List<FoodPreference>();
        [Tooltip("Food units taken per bite.")]
        public float biteSize = 10f;
        [Tooltip("Seconds between bites while eating.")]
        public float biteInterval = 0.6f;
        [Tooltip("How far in front the dino can reach to eat, drink or bite (adult, meters).")]
        public float reach = 1.5f;
        [Tooltip("How high above the ground it can reach to eat leaves (adult, meters).")]
        public float feedHeight = 1.5f;
        public float attackDamage = 10f;
        public float attackCooldown = 1f;

        [Header("Stealth")]
        [Tooltip("Distance at which wild dinos notice this species (adult, meters). Small = sneaky.")]
        public float noticeRadius = 15f;
        [Tooltip("Multiplier on noticeRadius while sneaking.")]
        [Range(0.1f, 1f)] public float sneakNoticeMultiplier = 0.5f;

        [Header("Growth")]
        [Tooltip("Usually 4 stages: Hatchling, Juvenile, Adolescent, Adult.")]
        public List<GrowthStage> growthStages = GrowthStage.DefaultStages();

        [Header("Abilities and variants (owned by the species thread)")]
        [Tooltip("Ability ids, e.g. \"headbutt\", \"glide\", \"pack_call\", \"tail_swipe\".")]
        public List<string> abilityIds = new List<string>();
        [Tooltip("Stat variants that can be earned for this species.")]
        public List<StatVariant> variants = new List<StatVariant>();

        // ---------- helpers ----------

        public bool CanEat(FoodType type) => FoodMultiplier(type) > 0f;

        /// <summary>0 if the species can't eat it, otherwise how filling it is.</summary>
        public float FoodMultiplier(FoodType type)
        {
            for (int i = 0; i < foods.Count; i++)
                if ((foods[i].type & type) != 0) return foods[i].nutrition;
            return 0f;
        }

        public FoodType EdibleMask
        {
            get
            {
                FoodType mask = FoodType.None;
                foreach (var f in foods) if (f.nutrition > 0f) mask |= f.type;
                return mask;
            }
        }

        public GrowthStage Stage(int index)
        {
            if (growthStages == null || growthStages.Count == 0) return GrowthStage.Adult;
            return growthStages[Mathf.Clamp(index, 0, growthStages.Count - 1)];
        }

        public int StageCount => growthStages == null || growthStages.Count == 0 ? 1 : growthStages.Count;

        public StatVariant FindVariant(string variantId)
        {
            if (string.IsNullOrEmpty(variantId)) return null;
            return variants.Find(v => v != null && v.id == variantId);
        }

        void OnValidate()
        {
            if (growthStages == null || growthStages.Count == 0) growthStages = GrowthStage.DefaultStages();
        }
    }

    [Serializable]
    public struct FoodPreference
    {
        public FoodType type;
        [Tooltip("1 = normal. 0.5 = half as filling. 0 = can't eat.")]
        public float nutrition;
        public FoodPreference(FoodType type, float nutrition) { this.type = type; this.nutrition = nutrition; }
    }

    [Serializable]
    public class GrowthStage
    {
        public string name = "Adult";
        [Tooltip("Size relative to adult (1 = adult).")]
        [Range(0.1f, 1f)] public float scale = 1f;
        [Tooltip("Multiplier on health, damage, food/water capacity.")]
        [Range(0.1f, 1f)] public float statMultiplier = 1f;
        [Tooltip("Minutes of well-fed play to finish this stage. 0 = final stage.")]
        public float minutesToGrow = 0f;
        [Tooltip("XP awarded when reaching this stage.")]
        public int xpOnReach = 0;

        public GrowthStage() { }
        public GrowthStage(string name, float scale, float statMultiplier, float minutesToGrow, int xpOnReach)
        {
            this.name = name; this.scale = scale; this.statMultiplier = statMultiplier;
            this.minutesToGrow = minutesToGrow; this.xpOnReach = xpOnReach;
        }

        public static readonly GrowthStage Adult = new GrowthStage("Adult", 1f, 1f, 0f, 0);

        public static List<GrowthStage> DefaultStages() => new List<GrowthStage>
        {
            new GrowthStage("Hatchling",  0.30f, 0.30f, 8f,  0),
            new GrowthStage("Juvenile",   0.55f, 0.55f, 12f, 50),
            new GrowthStage("Adolescent", 0.80f, 0.80f, 15f, 100),
            new GrowthStage("Adult",      1.00f, 1.00f, 0f,  200),
        };
    }

    /// <summary>
    /// An earned stat variant (Swift, Aquatic, Fierce, Sturdy, Sneaky...). Multipliers, 1 = unchanged.
    /// The species thread owns the actual variant list and numbers.
    /// </summary>
    [Serializable]
    public class StatVariant
    {
        public string id = "swift";
        public string displayName = "Swift";
        public int unlockXp = 300;
        public float healthMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float swimMultiplier = 1f;
        public float staminaMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float noticeRadiusMultiplier = 1f;
        public float drainMultiplier = 1f;
    }
}
