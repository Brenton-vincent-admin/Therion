using Dino.Core;
using Dino.Session;

namespace Dino.Species.AI
{
    /// <summary>
    /// World-wide switches for wild dinos. Mode and the parent "friendly predators" setting come from
    /// GameSession when one is running; the static fields are fallbacks for tests and test scenes.
    /// </summary>
    public static class WildDinoSettings
    {
        public static GameMode FallbackMode = GameMode.Survival;
        public static bool FriendlyPredatorsSetting;

        /// <summary>Set by the day/night cycle.</summary>
        public static bool IsNight;

        public static GameMode Mode
        {
            get
            {
                var s = GameSession.Instance;
                return s != null && s.Save != null ? s.Mode : FallbackMode;
            }
        }

        public static bool FriendlyPredators
        {
            get
            {
                var s = GameSession.Instance;
                bool saved = s != null && s.Save != null && s.Save.settings != null && s.Save.settings.friendlyPredators;
                return FriendlyPredatorsSetting || saved || Mode == GameMode.Explorer;
            }
        }

        public static bool Hardcore => Mode == GameMode.Hardcore;
    }
}
