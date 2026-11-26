using Dino.Core;

namespace Dino.Species.AI
{
    /// <summary>World-wide switches for wild dinos. GameSession sets these when a world starts.</summary>
    public static class WildDinoSettings
    {
        public static GameMode Mode = GameMode.Survival;

        /// <summary>Parent setting: predators wander and roar but never chase or bite players.</summary>
        public static bool FriendlyPredatorsSetting;

        /// <summary>Set by the day/night cycle.</summary>
        public static bool IsNight;

        public static bool FriendlyPredators => FriendlyPredatorsSetting || Mode == GameMode.Explorer;
        public static bool Hardcore => Mode == GameMode.Hardcore;
    }
}
