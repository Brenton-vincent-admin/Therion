using System;

namespace Dino.Species
{
    public enum Diet { Herbivore = 0, Omnivore = 1, Carnivore = 2 }

    /// <summary>What a food source offers. Flags so a species can list everything it eats.</summary>
    [Flags]
    public enum FoodType
    {
        None    = 0,
        Berries = 1 << 0,
        Leaves  = 1 << 1,
        Bugs    = 1 << 2,
        Meat    = 1 << 3, // carcasses / meat piles
        Fish    = 1 << 4, // semi-aquatic species
        Eggs    = 1 << 5,
    }

    /// <summary>How the species moves. Land is the default; others add abilities on top of walking.</summary>
    public enum MoveType { Land = 0, SemiAquatic = 1, Glider = 2, Flyer = 3 }

    public enum SizeClass { Tiny = 0, Small = 1, Medium = 2, Large = 3, Huge = 4 }
}
