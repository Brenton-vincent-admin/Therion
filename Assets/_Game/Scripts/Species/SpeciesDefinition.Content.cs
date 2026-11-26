using UnityEngine;

namespace Dino.Species
{
    /// <summary>
    /// Species-thread fields on the core's SpeciesDefinition (the core owns SpeciesDefinition.cs;
    /// this partial adds content fields so neither side edits the other's file).
    /// </summary>
    public partial class SpeciesDefinition
    {
        [Header("Content (species thread)")]
        public string scientificName;
        [Tooltip("How a WILD dino of this species behaves.")]
        public Temperament temperament = Temperament.Defensive;
        [Tooltip("Stealth species: small notice radius, sharper eyes, can hide in cover.")]
        public bool stealthy;
        public SignatureAbility signatureAbility = SignatureAbility.None;
        [Tooltip("What the signature ability does at level 1, 2 and 3 (shown in the species picker).")]
        public string[] abilityLevelText = new string[3];
        [Tooltip("Account XP level that unlocks this species (0 = starter). unlockXp is derived from it.")]
        public int unlockXpLevel;
        [Tooltip("Adult nose-to-tail length in meters (Field Guide, model fitting).")]
        public float lengthMeters = 3f;

        [Header("Skin")]
        [Tooltip("Look a brand-new player gets for this species.")]
        public SkinLoadout defaultSkin = SkinLoadout.Of(SkinColor.Leaf, SkinColor.Cream, SkinPattern.Stripes, SkinColor.Moss, SkinColor.Sunset);
        [Tooltip("Where the model comes from and its license (see Docs/Species/ASSETS.md).")]
        [TextArea] public string modelSource;

        public bool IsStealthy => stealthy;

        /// <summary>Signature ability level for a growth stage: Lv 1 from hatching, Lv 2 adolescent, Lv 3 adult.</summary>
        public int AbilityLevel(int stageIndex) => Growth.AbilityLevel(stageIndex);

        /// <summary>1-5 stars for the species picker, derived from real units.</summary>
        public int HealthStars => SpeciesTuning.ToStars(SpeciesTuning.HealthHp, maxHealth);
        public int BiteStars => SpeciesTuning.ToStars(SpeciesTuning.AttackDamage, attackDamage);
        public int SpeedStars => SpeciesTuning.ToStars(SpeciesTuning.RunSpeed, runSpeed);
        public int StaminaStars => SpeciesTuning.ToStars(SpeciesTuning.StaminaPoints, maxStamina);
    }
}
