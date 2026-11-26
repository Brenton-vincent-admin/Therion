using System;
using UnityEngine;

namespace Dino.Core
{
    /// <summary>
    /// Small static event hub for cross-system signals (UI, XP, audio, net).
    /// Gameplay components raise these; nothing here holds state.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>A dino ate. Args: eater, food type, food units eaten.</summary>
        public static event Action<GameObject, Species.FoodType, float> Ate;
        /// <summary>A dino drank. Args: drinker, water units drunk.</summary>
        public static event Action<GameObject, float> Drank;
        /// <summary>A dino reached a new growth stage. Args: dino, new stage index.</summary>
        public static event Action<GameObject, int> GrewUp;
        /// <summary>A dino fainted (health hit zero). Args: dino, attacker or null.</summary>
        public static event Action<GameObject, GameObject> Fainted;
        /// <summary>The local player gained XP. Args: amount, reason.</summary>
        public static event Action<int, string> XpGained;
        /// <summary>A species or variant became unlocked. Arg: id.</summary>
        public static event Action<string> Unlocked;

        public static void RaiseAte(GameObject eater, Species.FoodType type, float units) => Ate?.Invoke(eater, type, units);
        public static void RaiseDrank(GameObject drinker, float units) => Drank?.Invoke(drinker, units);
        public static void RaiseGrewUp(GameObject dino, int stage) => GrewUp?.Invoke(dino, stage);
        public static void RaiseFainted(GameObject dino, GameObject attacker) => Fainted?.Invoke(dino, attacker);
        public static void RaiseXpGained(int amount, string reason) => XpGained?.Invoke(amount, reason);
        public static void RaiseUnlocked(string id) => Unlocked?.Invoke(id);

        /// <summary>Clears all listeners (domain reload off / tests).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Ate = null; Drank = null; GrewUp = null; Fainted = null; XpGained = null; Unlocked = null;
        }
    }
}
