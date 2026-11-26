// Pure C# (no UnityEngine) so the species rules can be unit tested outside Unity.
// Diet, MoveType, SizeClass and FoodType live in the core's Species/Enums.cs.
namespace Dino.Species
{
    /// <summary>One signature ability per species (design doc, "Species and abilities").</summary>
    public enum SignatureAbility
    {
        None = 0,
        Call,           // Parasaur: highlight danger for friends
        HeadButt,       // Pachycephalosaurus: charge that stuns
        Hide,           // Psittacosaurus: vanish in tall grass
        TailSwipe,      // Stegosaurus: hit anything behind
        HornCharge,     // Triceratops: knock-back rush
        HighReachStomp, // Brachiosaurus: treetop food + scare stomp
        Snatch,         // Oviraptor: grab food and dash
        SprintBurst,    // Gallimimus: short top-speed dash
        NightVision,    // Troodon: see in the dark
        Glide,          // Microraptor: glide from height
        Pounce,         // Velociraptor: leap and pin small prey
        FrillFlash,     // Dilophosaurus: blind a target briefly
        Roar,           // T. rex: small dinos flee
    }

    /// <summary>How a WILD dino of this species reacts to others.</summary>
    public enum Temperament
    {
        Skittish = 0,    // runs from anything that is a threat
        Defensive = 1,   // grazes, fights back only when hit
        Territorial = 2, // warns, then charges anyone who stays too close
        Predator = 3,    // hunts smaller dinos when hungry
    }

    /// <summary>What a dino is doing right now, as far as being noticed goes.</summary>
    public enum Stance { Still, Walking, Sneaking, Running, Attacking, Calling }

    /// <summary>The eye icon over the player's head (design doc, "Stealth").</summary>
    public enum Awareness { Hidden = 0, Curious = 1, Seen = 2 }

    /// <summary>Ability ids written into SpeciesDefinition.abilityIds (core format is strings).</summary>
    public static class AbilityIds
    {
        public static string For(SignatureAbility a)
        {
            switch (a)
            {
                case SignatureAbility.Call: return "call";
                case SignatureAbility.HeadButt: return "headbutt";
                case SignatureAbility.Hide: return "hide";
                case SignatureAbility.TailSwipe: return "tail_swipe";
                case SignatureAbility.HornCharge: return "horn_charge";
                case SignatureAbility.HighReachStomp: return "high_reach_stomp";
                case SignatureAbility.Snatch: return "snatch";
                case SignatureAbility.SprintBurst: return "sprint_burst";
                case SignatureAbility.NightVision: return "night_vision";
                case SignatureAbility.Glide: return "glide";
                case SignatureAbility.Pounce: return "pounce";
                case SignatureAbility.FrillFlash: return "frill_flash";
                case SignatureAbility.Roar: return "roar";
                default: return "";
            }
        }

        public static SignatureAbility Parse(string id)
        {
            foreach (SignatureAbility a in System.Enum.GetValues(typeof(SignatureAbility)))
                if (a != SignatureAbility.None && For(a) == id) return a;
            return SignatureAbility.None;
        }
    }
}
