using System;

namespace Dino.Species
{
    /// <summary>
    /// Adult stats on the roster doc's 1 to 5 scale. Food and water are "higher means hungrier".
    /// <see cref="SpeciesTuning"/> turns these into the real units SpeciesDefinition uses.
    /// </summary>
    [Serializable]
    public struct StarStats
    {
        public int health, bite, speed, stamina, foodDrain, waterDrain;

        public StarStats(int health, int bite, int speed, int stamina, int foodDrain, int waterDrain)
        {
            this.health = health; this.bite = bite; this.speed = speed;
            this.stamina = stamina; this.foodDrain = foodDrain; this.waterDrain = waterDrain;
        }
    }

    /// <summary>
    /// One species as the design and roster docs describe it: stars, diet, ability, unlock level,
    /// default look. Pure data; the editor menu "Dino/Species/Rebuild Species Assets" turns each
    /// entry into a SpeciesDefinition asset through <see cref="SpeciesTuning"/>.
    /// </summary>
    [Serializable]
    public class RosterEntry
    {
        public string id;              // stable save id, lower_snake_case. Never rename.
        public string displayName;     // short, kid-friendly
        public string scientificName;  // Field Guide
        public string kidDescription;  // one line from the roster doc
        public Diet diet;
        public MoveType moveType;
        public SizeClass size;
        public Temperament temperament;
        public StarStats stars;
        public bool stealthy;
        public SignatureAbility ability;
        public string[] abilityLevelText = new string[3];
        public float lengthMeters;     // adult nose-to-tail
        public float heightMeters;     // adult head height (sets feed height and capsule)
        public int unlockXpLevel;      // 0 = starter, else the account XP level from the design doc
        public float growthTimeMultiplier = 1f; // T. rex grows slower: the reward species
        public SkinLoadout defaultSkin;
        public string modelSource;     // where the model comes from, see Docs/Species/ASSETS.md
        public int sortOrder;

        public bool IsStarter => unlockXpLevel <= 0;
    }
}
