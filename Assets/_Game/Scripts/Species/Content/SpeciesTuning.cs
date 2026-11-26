using System;
using System.Collections.Generic;
using Dino.Progression; // XpCurve lives in core (same formula we agreed)

namespace Dino.Species
{
    /// <summary>
    /// All species tuning in one place: turns roster stars into the real units SpeciesDefinition
    /// uses (m/s, units/min, meters). Pure C#, so the numbers are unit tested without Unity.
    /// The game design track tunes the tables here, then runs "Dino/Species/Rebuild Species Assets".
    /// </summary>
    public static class SpeciesTuning
    {
        // index 0 = 1 star ... index 4 = 5 stars. Adult values.
        public static readonly float[] HealthHp      = { 60f, 100f, 160f, 250f, 400f };
        public static readonly float[] AttackDamage  = { 5f, 10f, 18f, 30f, 50f };
        public static readonly float[] RunSpeed      = { 6f, 7.5f, 9f, 10.5f, 12f };
        public static readonly float[] StaminaPoints = { 60f, 80f, 100f, 130f, 160f };

        /// <summary>
        /// Minutes for a full food or water bar to empty in Survival while walking. The design doc
        /// asks for 6 to 8 minutes, so 1 star (least hungry) = 8 min and 5 stars (hungriest) = 6 min.
        /// Hardcore speeds this up in GameSession, Explorer turns it off.
        /// </summary>
        public static readonly float[] MinutesToEmpty = { 8f, 7.5f, 7f, 6.5f, 6f };

        public const float BarSize = 100f;           // maxFood / maxWater
        public const float WalkFraction = 0.4f;      // walk = 40% of run
        public const float SwimFraction = 0.45f;     // land dinos paddle slowly
        public const float SemiAquaticSwimFraction = 0.9f;
        public const float RunStaminaPerSecond = 10f;
        public const float StaminaRefillSeconds = 8f;   // empty -> full while walking
        public const float HealthRefillSeconds = 200f;  // regen while fed and watered

        // Stealth: design doc "a normal dino is noticed at about 25 m, a stealthy one at about 10,
        // and a sneaking stealthy one only at about 4".
        public const float NormalNoticeRadius = 25f;
        public const float StealthyNoticeRadius = 10f;
        public const float StealthySneakMultiplier = 0.4f; // 10 m -> 4 m
        public const float NormalSneakMultiplier = 0.7f;

        public static float Stars(float[] table, int stars) => table[ClampStars(stars) - 1];
        public static int ClampStars(int s) => s < 1 ? 1 : s > 5 ? 5 : s;

        /// <summary>Inverse of <see cref="Stars"/>, for UI: the nearest star rating for a real value.</summary>
        public static int ToStars(float[] table, float value)
        {
            int best = 1; float bestD = float.MaxValue;
            for (int i = 0; i < table.Length; i++)
            {
                float d = Math.Abs(table[i] - value);
                if (d < bestD) { bestD = d; best = i + 1; }
            }
            return best;
        }

        static float BySize(SizeClass s, float tiny, float small, float medium, float large, float huge)
        {
            switch (s)
            {
                case SizeClass.Tiny: return tiny;
                case SizeClass.Small: return small;
                case SizeClass.Medium: return medium;
                case SizeClass.Large: return large;
                default: return huge;
            }
        }

        /// <summary>Real-unit numbers for one species, field-for-field with SpeciesDefinition.</summary>
        public static SpeciesNumbers Compute(RosterEntry e)
        {
            var n = new SpeciesNumbers();
            var st = e.stars;

            n.maxHealth = Stars(HealthHp, st.health);
            n.maxStamina = Stars(StaminaPoints, st.stamina);
            n.maxFood = BarSize;
            n.maxWater = BarSize;
            n.foodDrainPerMinute = BarSize / Stars(MinutesToEmpty, st.foodDrain);
            n.waterDrainPerMinute = BarSize / Stars(MinutesToEmpty, st.waterDrain);
            n.runStaminaPerSecond = RunStaminaPerSecond;
            n.staminaRegenPerSecond = n.maxStamina / StaminaRefillSeconds;
            n.healthRegenPerSecond = n.maxHealth / HealthRefillSeconds;

            n.runSpeed = Stars(RunSpeed, st.speed);
            n.walkSpeed = n.runSpeed * WalkFraction;
            n.sneakSpeed = n.runSpeed * (e.stealthy ? 0.28f : 0.2f);
            n.swimSpeed = n.runSpeed * (e.moveType == MoveType.SemiAquatic ? SemiAquaticSwimFraction : SwimFraction);
            n.turnSpeed = BySize(e.size, 540f, 450f, 360f, 240f, 150f);
            n.jumpHeight = BySize(e.size, 0.8f, 1.0f, 0.8f, 0.5f, 0.3f);

            n.foods = FoodsFor(e);
            n.biteSize = BySize(e.size, 4f, 6f, 10f, 16f, 25f);
            n.biteInterval = 0.6f;
            n.reach = Clamp(e.lengthMeters * 0.25f, 0.4f, 4f);
            // Brachiosaurus' High reach: treetop leaves no one else can reach.
            n.feedHeight = e.ability == SignatureAbility.HighReachStomp ? e.heightMeters : e.heightMeters * 0.9f;
            n.attackDamage = Stars(AttackDamage, st.bite);
            n.attackCooldown = BySize(e.size, 0.6f, 0.8f, 1.0f, 1.3f, 1.6f);

            n.noticeRadius = e.stealthy ? StealthyNoticeRadius : NormalNoticeRadius;
            n.sneakNoticeMultiplier = e.stealthy ? StealthySneakMultiplier : NormalSneakMultiplier;

            // CharacterController is an upright capsule; long animals get a wide one.
            n.adultRadius = Clamp(e.lengthMeters * 0.17f, 0.2f, 3f);
            n.adultHeight = Math.Max(e.heightMeters, n.adultRadius * 2f + 0.01f);

            n.growthStages = Growth.StagesFor(e.growthTimeMultiplier);
            n.isStarter = e.IsStarter;
            n.unlockXp = e.IsStarter ? 0 : XpCurve.XpForLevel(e.unlockXpLevel);
            n.variants = Variants.All();
            n.abilityIds = e.ability == SignatureAbility.None ? new List<string>() : new List<string> { AbilityIds.For(e.ability) };
            return n;
        }

        /// <summary>
        /// Food per diet (design doc "Diets and food"): plants eat berries and leaves; omnivores eat
        /// almost anything; meat eaters eat meat piles, fish and eggs. Tiny and small dinos can always
        /// eat bugs so nobody has to fight to grow. Nutrition 1 = normal.
        /// </summary>
        public static List<FoodNumbers> FoodsFor(RosterEntry e)
        {
            var list = new List<FoodNumbers>();
            bool small = e.size <= SizeClass.Small;
            switch (e.diet)
            {
                case Diet.Herbivore:
                    list.Add(new FoodNumbers(FoodType.Berries, 1f));
                    list.Add(new FoodNumbers(FoodType.Leaves, 1f));
                    if (small) list.Add(new FoodNumbers(FoodType.Bugs, 0.6f));
                    break;
                case Diet.Omnivore:
                    list.Add(new FoodNumbers(FoodType.Berries, 0.9f));
                    list.Add(new FoodNumbers(FoodType.Leaves, 0.5f));
                    list.Add(new FoodNumbers(FoodType.Bugs, 1f));
                    list.Add(new FoodNumbers(FoodType.Eggs, e.ability == SignatureAbility.Snatch ? 1.3f : 1f));
                    list.Add(new FoodNumbers(FoodType.Fish, 0.8f));
                    list.Add(new FoodNumbers(FoodType.Meat, 0.7f));
                    break;
                default:
                    list.Add(new FoodNumbers(FoodType.Meat, 1f));
                    list.Add(new FoodNumbers(FoodType.Fish, 1f));
                    list.Add(new FoodNumbers(FoodType.Eggs, 0.8f));
                    if (small) list.Add(new FoodNumbers(FoodType.Bugs, 1f));
                    break;
            }
            return list;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }

    public struct FoodNumbers
    {
        public FoodType type; public float nutrition;
        public FoodNumbers(FoodType t, float n) { type = t; nutrition = n; }
    }

    public struct GrowthNumbers
    {
        public string name; public float scale, statMultiplier, minutesToGrow; public int xpOnReach;
        public GrowthNumbers(string name, float scale, float stat, float minutes, int xp)
        { this.name = name; this.scale = scale; statMultiplier = stat; minutesToGrow = minutes; xpOnReach = xp; }
    }

    public struct VariantNumbers
    {
        public string id, displayName, badge;
        public int unlockXp;
        public float health, speed, swim, stamina, damage, noticeRadius, drain;
    }

    public class SpeciesNumbers
    {
        public float maxHealth, maxStamina, maxFood, maxWater, foodDrainPerMinute, waterDrainPerMinute;
        public float staminaRegenPerSecond, runStaminaPerSecond, healthRegenPerSecond;
        public float walkSpeed, runSpeed, swimSpeed, sneakSpeed, turnSpeed, jumpHeight;
        public List<FoodNumbers> foods;
        public float biteSize, biteInterval, reach, feedHeight, attackDamage, attackCooldown;
        public float noticeRadius, sneakNoticeMultiplier;
        public float adultHeight, adultRadius;
        public List<GrowthNumbers> growthStages;
        public bool isStarter; public int unlockXp;
        public List<VariantNumbers> variants;
        public List<string> abilityIds;
    }

    /// <summary>Four growth stages shared by every species (roster and design docs).</summary>
    public static class Growth
    {
        // Design doc: Juvenile at 15-20 min, Sub-adult at 45-60 min total, Adult at 2-3 h total.
        // minutesToGrow is the length of each stage, so totals are 18, 50 and 150 minutes.
        public static readonly GrowthNumbers[] Base =
        {
            new GrowthNumbers("Hatchling",  0.20f, 0.25f, 18f, 0),
            new GrowthNumbers("Juvenile",   0.45f, 0.50f, 32f, 50),
            new GrowthNumbers("Adolescent", 0.75f, 0.80f, 100f, 100),
            new GrowthNumbers("Adult",      1.00f, 1.00f, 0f, 250),
        };

        /// <summary>Signature ability level per stage: Lv 1 from hatching, Lv 2 adolescent, Lv 3 adult.</summary>
        public static int AbilityLevel(int stageIndex) => stageIndex >= 3 ? 3 : stageIndex >= 2 ? 2 : 1;

        public static List<GrowthNumbers> StagesFor(float timeMultiplier)
        {
            if (timeMultiplier <= 0f) timeMultiplier = 1f;
            var list = new List<GrowthNumbers>();
            foreach (var g in Base)
                list.Add(new GrowthNumbers(g.name, g.scale, g.statMultiplier, (float)Math.Round(g.minutesToGrow * timeMultiplier, 1), g.xpOnReach));
            return list;
        }

        public static float TotalMinutesToAdult(List<GrowthNumbers> stages)
        {
            float t = 0f; foreach (var g in stages) t += g.minutesToGrow; return t;
        }
    }

    /// <summary>
    /// Stat variants: design doc names and badges, roster doc trade-offs (each one raises something
    /// by 10-25% and lowers something slightly, so none is simply better). Picked once at hatch.
    /// Unlocked by lifetime XP (design doc: "Variants unlock with XP"). No variant = plain dino.
    /// </summary>
    public static class Variants
    {
        public const string Swift = "swift", Aquatic = "aquatic", Fierce = "fierce", Sturdy = "sturdy", Sneaky = "sneaky";

        public static List<VariantNumbers> All() => new List<VariantNumbers>
        {
            V(Swift,   "Swift",   "feather", XpCurve.XpForLevel(2), speed: 1.12f, stamina: 1.10f, health: 0.95f),
            V(Aquatic, "Aquatic", "wave",    XpCurve.XpForLevel(3), swim: 1.25f, speed: 0.95f),
            V(Fierce,  "Fierce",  "tooth",   XpCurve.XpForLevel(4), damage: 1.15f, drain: 1.10f),
            V(Sturdy,  "Sturdy",  "shield",  XpCurve.XpForLevel(5), health: 1.15f, drain: 0.95f, speed: 0.95f),
            V(Sneaky,  "Sneaky",  "leaf",    XpCurve.XpForLevel(6), notice: 0.6f, health: 0.90f),
        };

        static VariantNumbers V(string id, string name, string badge, int xp, float health = 1, float speed = 1,
            float swim = 1, float stamina = 1, float damage = 1, float notice = 1, float drain = 1)
            => new VariantNumbers
            {
                id = id, displayName = name, badge = badge, unlockXp = xp, health = health, speed = speed,
                swim = swim, stamina = stamina, damage = damage, noticeRadius = notice, drain = drain,
            };
    }
}
