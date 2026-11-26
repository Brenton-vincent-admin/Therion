using System.Collections.Generic;
using System.Linq;

namespace Dino.Species
{
    /// <summary>
    /// The 13 launch species, as set by the Roster & Art Direction doc (stats, roles, stealth)
    /// and the Game Design doc (ability levels, XP unlock levels). Single source of truth for the
    /// SpeciesDefinition assets; unit tests check it against the docs.
    /// </summary>
    public static class LaunchRoster
    {
        public static readonly IReadOnlyList<RosterEntry> All = Build();

        public static RosterEntry Get(string id) => All.FirstOrDefault(s => s.id == id);
        public static IEnumerable<RosterEntry> Starters => All.Where(s => s.IsStarter);

        static RosterEntry E(int order, string id, string name, string sci, Diet diet, MoveType move, SizeClass size,
            Temperament temper, string description, StarStats stars, bool stealthy, SignatureAbility ability,
            string lv1, string lv2, string lv3, float length, float height, int unlockLevel, SkinLoadout skin,
            string model, float growthMul = 1f)
            => new RosterEntry
            {
                sortOrder = order, id = id, displayName = name, scientificName = sci, diet = diet, moveType = move,
                size = size, temperament = temper, kidDescription = description, stars = stars, stealthy = stealthy,
                ability = ability, abilityLevelText = new[] { lv1, lv2, lv3 }, lengthMeters = length,
                heightMeters = height, unlockXpLevel = unlockLevel, defaultSkin = skin, modelSource = model,
                growthTimeMultiplier = growthMul,
            };

        const string Quaternius = "Quaternius Animated LowPoly Dinosaurs (CC0): ";

        static List<RosterEntry> Build() => new List<RosterEntry>
        {
            //                                               health bite speed stamina food water
            // ---------------- Herbivores ----------------
            E(0, "parasaur", "Parasaur", "Parasaurolophus", Diet.Herbivore, MoveType.Land, SizeClass.Large,
              Temperament.Skittish, "Fast runner and lookout, the friendliest starter",
              new StarStats(2, 1, 5, 5, 3, 3), false, SignatureAbility.Call,
              "Call highlights danger for nearby friends", "Wider call", "Friends get a speed boost",
              9f, 4.5f, 0, SkinLoadout.Of(SkinColor.Sunset, SkinColor.Cream, SkinPattern.Stripes, SkinColor.Clay, SkinColor.Berry),
              "New model needed (not in the free pack)"),

            E(1, "pachycephalosaurus", "Pachy", "Pachycephalosaurus", Diet.Herbivore, MoveType.Land, SizeClass.Medium,
              Temperament.Territorial, "Head-butter that stands up to bullies",
              new StarStats(3, 3, 4, 3, 3, 3), false, SignatureAbility.HeadButt,
              "Charge that stuns briefly", "Longer charge", "Knocks predators over",
              4.5f, 2f, 0, SkinLoadout.Of(SkinColor.Sand, SkinColor.Cream, SkinPattern.Speckles, SkinColor.Clay, SkinColor.Berry),
              "New model needed. Biped: can borrow the Velociraptor rig and clips with a dome head"),

            E(2, "psittacosaurus", "Psittaco", "Psittacosaurus", Diet.Herbivore, MoveType.Land, SizeClass.Small,
              Temperament.Skittish, "Small and quick, great at hiding",
              new StarStats(1, 1, 4, 4, 2, 2), true, SignatureAbility.Hide,
              "Vanish in tall grass", "Stay hidden while moving slowly", "Friends nearby hide too",
              2f, 0.8f, 2, SkinLoadout.Of(SkinColor.Leaf, SkinColor.Cream, SkinPattern.Spots, SkinColor.Moss, SkinColor.Sunflower),
              "New model needed. Small biped"),

            E(3, "stegosaurus", "Stego", "Stegosaurus", Diet.Herbivore, MoveType.Land, SizeClass.Large,
              Temperament.Defensive, "Spiky defender that punishes attackers",
              new StarStats(4, 3, 2, 3, 3, 4), false, SignatureAbility.TailSwipe,
              "Tail swipe hits anything behind it", "Wider swipe", "Swipe stuns briefly",
              9f, 4f, 6, SkinLoadout.Of(SkinColor.Moss, SkinColor.Sand, SkinPattern.Bands, SkinColor.Leaf, SkinColor.Sunset),
              Quaternius + "Stegosaurus"),

            E(4, "triceratops", "Trike", "Triceratops", Diet.Herbivore, MoveType.Land, SizeClass.Large,
              Temperament.Territorial, "Tank that stands its ground",
              new StarStats(5, 3, 2, 3, 4, 4), false, SignatureAbility.HornCharge,
              "Horn rush that knocks back", "Bigger charge", "Charge shoves boulders open",
              8.5f, 3f, 9, SkinLoadout.Of(SkinColor.Clay, SkinColor.Sand, SkinPattern.None, SkinColor.Stone, SkinColor.Sunset),
              Quaternius + "Triceratops", 1.15f),

            E(5, "brachiosaurus", "Brachio", "Brachiosaurus", Diet.Herbivore, MoveType.Land, SizeClass.Huge,
              Temperament.Defensive, "Giant long-neck that almost nothing can bring down",
              new StarStats(5, 2, 1, 2, 5, 5), false, SignatureAbility.HighReachStomp,
              "Eats treetop leaves; stomp scares dinos", "Wider stomp", "Stomp shakes fruit loose",
              22f, 12f, 12, SkinLoadout.Of(SkinColor.Sky, SkinColor.Cream, SkinPattern.Giraffe, SkinColor.Lagoon, SkinColor.Mint),
              Quaternius + "Apatosaurus as a stand-in until a Brachiosaurus model exists", 1.4f),

            // ---------------- Omnivores ----------------
            E(6, "oviraptor", "Oviraptor", "Oviraptor", Diet.Omnivore, MoveType.Land, SizeClass.Small,
              Temperament.Skittish, "Quick, clever forager that eats almost anything",
              new StarStats(2, 2, 4, 4, 2, 3), false, SignatureAbility.Snatch,
              "Grab food and dash", "Faster dash", "Grab food from bigger dinos",
              1.8f, 1f, 0, SkinLoadout.Of(SkinColor.Grape, SkinColor.Cream, SkinPattern.Speckles, SkinColor.Sky, SkinColor.Sunflower),
              "New model needed. Feathered biped on the Velociraptor rig"),

            E(7, "gallimimus", "Gallimimus", "Gallimimus", Diet.Omnivore, MoveType.Land, SizeClass.Medium,
              Temperament.Skittish, "The fastest runner in the game",
              new StarStats(2, 1, 5, 5, 2, 3), false, SignatureAbility.SprintBurst,
              "Short top-speed dash", "Longer dash", "Dust cloud confuses chasers",
              6f, 2.2f, 3, SkinLoadout.Of(SkinColor.Sand, SkinColor.Snow, SkinPattern.Stripes, SkinColor.Clay, SkinColor.Coral),
              "New model needed. Ostrich-like biped on the Velociraptor rig"),

            E(8, "troodon", "Troodon", "Troodon", Diet.Omnivore, MoveType.Land, SizeClass.Small,
              Temperament.Skittish, "Clever night forager",
              new StarStats(2, 2, 4, 3, 2, 3), true, SignatureAbility.NightVision,
              "See clearly in the dark", "Spot hidden food", "See dinos through bushes at night",
              2.4f, 1f, 5, SkinLoadout.Of(SkinColor.Charcoal, SkinColor.Stone, SkinPattern.Starry, SkinColor.Sky, SkinColor.Amber, SkinColor.Sunflower),
              "New model needed. Small feathered biped on the Velociraptor rig"),

            // ---------------- Carnivores ----------------
            E(9, "microraptor", "Microraptor", "Microraptor", Diet.Carnivore, MoveType.Glider, SizeClass.Tiny,
              Temperament.Skittish, "Tiny, sneaky glider that eats bugs and small prey",
              new StarStats(1, 1, 4, 4, 2, 2), true, SignatureAbility.Glide,
              "Glide from trees and cliffs", "Longer glide", "Climb trees",
              0.8f, 0.4f, 4, SkinLoadout.Of(SkinColor.Charcoal, SkinColor.Lagoon, SkinPattern.Bands, SkinColor.Sky, SkinColor.Lagoon),
              "New model needed. Four-winged glider, needs a glide pose"),

            E(10, "velociraptor", "Raptor", "Velociraptor", Diet.Carnivore, MoveType.Land, SizeClass.Small,
              Temperament.Predator, "Fast pack hunter, the easiest carnivore to learn",
              new StarStats(2, 2, 5, 4, 3, 3), true, SignatureAbility.Pounce,
              "Leap and pin small prey", "Farther pounce", "Pounce from stealth",
              2f, 0.9f, 0, SkinLoadout.Of(SkinColor.Clay, SkinColor.Sand, SkinPattern.Tiger, SkinColor.Charcoal, SkinColor.Sunset),
              Quaternius + "Velociraptor"),

            E(11, "dilophosaurus", "Dilo", "Dilophosaurus", Diet.Carnivore, MoveType.Land, SizeClass.Medium,
              Temperament.Predator, "Ambusher that waits in cover",
              new StarStats(3, 3, 3, 3, 3, 3), true, SignatureAbility.FrillFlash,
              "Frill flash blinds a target briefly", "Longer flash", "From stealth, a bigger surprise",
              7f, 2.5f, 8, SkinLoadout.Of(SkinColor.Moss, SkinColor.Sand, SkinPattern.Leopard, SkinColor.Leaf, SkinColor.Sunflower),
              "New model needed. Biped on the T. rex rig with head crests and a frill"),

            E(12, "trex", "T. rex", "Tyrannosaurus rex", Diet.Carnivore, MoveType.Land, SizeClass.Huge,
              Temperament.Predator, "Apex hunter, slow to grow and the end goal",
              new StarStats(5, 5, 2, 2, 5, 3), false, SignatureAbility.Roar,
              "Roar makes small dinos flee", "Wider roar", "Allies get a speed boost",
              12f, 4.5f, 15, SkinLoadout.Of(SkinColor.Stone, SkinColor.Cream, SkinPattern.Stripes, SkinColor.Charcoal, SkinColor.Sunset),
              Quaternius + "T. rex", 1.5f),
        };
    }
}
