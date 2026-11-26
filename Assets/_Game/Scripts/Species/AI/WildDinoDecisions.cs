// Pure C# decision rules for wild dinos, so behaviour can be unit tested without Unity.
// WildDinoBrain gathers a snapshot each think tick, calls Decide, and steers the motor.
namespace Dino.Species.AI
{
    public enum WildState { Wander, Graze, Drink, Rest, Flee, Chase, Attack, Warn, Scavenge }

    /// <summary>Another dino this one can currently see.</summary>
    public struct SeenDino
    {
        public int index;          // caller's id for the target (-1 = none)
        public float distance;
        public SizeClass size;
        public bool isPredator;    // wild predator or a player carnivore
        public bool isPlayer;
        public bool isFainted;
        public bool inSafeZone;    // standing in a nest: never attacked there
        public bool isSameSpecies;
    }

    public struct WildSnapshot
    {
        public Diet diet;
        public SizeClass size;
        public Temperament temperament;
        public float food, water, health, stamina;   // 0..1
        public bool isNight;
        public bool friendlyPredators;               // Explorer mode or the parent toggle
        public bool hardcore;
        public float chaseSeconds;                   // how long the current chase has lasted
        public float giveUpCooldown;                 // > 0: recently gave up a chase, don't start another
        public int lastAttackerIndex;                // -1 if not hit recently
        public SizeClass lastAttackerSize;
        public float lastAttackerDistance;
        public bool foodKnown, waterKnown, carcassKnown;
        public SeenDino[] seen;
        public int seenCount;
    }

    public struct WildDecision
    {
        public WildState state;
        public int targetIndex;   // into the caller's seen list ids, or -1
        public bool run;
        public bool sneak;
        public static WildDecision Of(WildState s, int target = -1, bool run = false, bool sneak = false)
            => new WildDecision { state = s, targetIndex = target, run = run, sneak = sneak };
    }

    public static class WildDinoDecisions
    {
        public const float SurvivalChaseGiveUp = 8f;   // design doc: predators give up quickly in Survival
        public const float HardcoreChaseGiveUp = 20f;
        public const float TerritoryRadius = 8f;
        public const float AttackRange = 2.5f;          // scaled by caller for big dinos
        public const float HungryBelow = 0.45f;
        public const float ThirstyBelow = 0.4f;
        public const float PredatorHuntsBelow = 0.6f;

        public static float ChaseGiveUpSeconds(bool hardcore) => hardcore ? HardcoreChaseGiveUp : SurvivalChaseGiveUp;

        /// <summary>Is 'other' something this dino should run from?</summary>
        public static bool IsThreat(in WildSnapshot me, in SeenDino other)
        {
            if (other.isFainted || other.isSameSpecies) return false;
            if (me.friendlyPredators && other.isPlayer) return false; // players are friendly too in Explorer
            if (other.isPredator && other.size >= me.size) return true;
            if (me.temperament == Temperament.Skittish && other.size > me.size) return true;
            // A Huge dino fears nothing but a Huge predator; a Large one only a bigger predator.
            return false;
        }

        /// <summary>Can this predator hunt 'other'?</summary>
        public static bool IsPrey(in WildSnapshot me, in SeenDino other)
        {
            if (me.diet == Diet.Herbivore || me.temperament != Temperament.Predator) return false;
            if (other.isFainted || other.inSafeZone || other.isSameSpecies) return false;
            if (me.friendlyPredators && other.isPlayer) return false;
            return other.size < me.size || (other.size == me.size && !other.isPredator);
        }

        public static WildDecision Decide(in WildSnapshot me, float attackRange = AttackRange)
        {
            // 1. Danger first: run from the closest threat.
            int threat = Closest(me, (in WildSnapshot m, in SeenDino d) => IsThreat(m, d));
            if (threat >= 0)
            {
                var t = me.seen[threat];
                bool standGround = me.temperament == Temperament.Territorial && me.health > 0.5f && t.size <= me.size;
                if (!standGround) return WildDecision.Of(WildState.Flee, t.index, run: true);
            }

            // 2. Hit recently: fight back if not outmatched, else run.
            if (me.lastAttackerIndex >= 0)
            {
                bool outmatched = me.lastAttackerSize > me.size || me.health < 0.3f;
                bool fighter = me.temperament != Temperament.Skittish;
                if (fighter && !outmatched && !me.friendlyPredators)
                    return WildDecision.Of(me.lastAttackerDistance <= attackRange ? WildState.Attack : WildState.Chase,
                        me.lastAttackerIndex, run: true);
                return WildDecision.Of(WildState.Flee, me.lastAttackerIndex, run: true);
            }

            // 3. Territorial dinos warn intruders that come close, then shove them away.
            if (me.temperament == Temperament.Territorial && !me.friendlyPredators)
            {
                int intruder = Closest(me, (in WildSnapshot m, in SeenDino d) =>
                    !d.isFainted && !d.isSameSpecies && !d.inSafeZone && d.distance <= TerritoryRadius && d.size <= m.size);
                if (intruder >= 0)
                {
                    var d = me.seen[intruder];
                    return d.distance <= attackRange
                        ? WildDecision.Of(WildState.Attack, d.index)
                        : WildDecision.Of(WildState.Warn, d.index);
                }
            }

            // 4. Thirst beats hunger: water is everywhere and drinking is quick.
            if (me.water < ThirstyBelow && me.waterKnown) return WildDecision.Of(WildState.Drink);

            // 5. Hungry meat eaters scavenge first, then hunt (unless they just gave up a chase).
            if (me.diet == Diet.Carnivore || me.temperament == Temperament.Predator)
            {
                if (me.food < HungryBelow && me.carcassKnown) return WildDecision.Of(WildState.Scavenge);
                if (me.food < PredatorHuntsBelow && me.giveUpCooldown <= 0f && me.stamina > 0.25f)
                {
                    int prey = Closest(me, (in WildSnapshot m, in SeenDino d) => IsPrey(m, d));
                    if (prey >= 0 && me.chaseSeconds < ChaseGiveUpSeconds(me.hardcore))
                    {
                        var p = me.seen[prey];
                        if (p.distance <= attackRange) return WildDecision.Of(WildState.Attack, p.index);
                        // Stalk quietly until close, then sprint.
                        bool close = p.distance < 12f;
                        return WildDecision.Of(WildState.Chase, p.index, run: close, sneak: !close);
                    }
                }
            }

            // 6. Plant and omnivore food. Omnivores also scavenge.
            if (me.food < HungryBelow)
            {
                if (me.diet != Diet.Carnivore && me.foodKnown) return WildDecision.Of(WildState.Graze);
                if (me.diet == Diet.Omnivore && me.carcassKnown) return WildDecision.Of(WildState.Scavenge);
                if (me.diet == Diet.Carnivore && me.foodKnown) return WildDecision.Of(WildState.Graze); // fish, eggs, bugs
            }

            // 7. Tired or night: rest. Otherwise wander, and snack when half full.
            if (me.stamina < 0.2f || (me.isNight && me.food > 0.5f && me.water > 0.5f)) return WildDecision.Of(WildState.Rest);
            if (me.food < 0.7f && me.foodKnown && me.diet != Diet.Carnivore) return WildDecision.Of(WildState.Graze);
            return WildDecision.Of(WildState.Wander);
        }

        delegate bool Filter(in WildSnapshot me, in SeenDino d);

        static int Closest(in WildSnapshot me, Filter f)
        {
            int best = -1; float bestD = float.MaxValue;
            if (me.seen == null) return -1;
            int n = me.seenCount < me.seen.Length ? me.seenCount : me.seen.Length;
            for (int i = 0; i < n; i++)
            {
                if (!f(me, me.seen[i])) continue;
                if (me.seen[i].distance < bestD) { bestD = me.seen[i].distance; best = i; }
            }
            return best;
        }
    }
}
