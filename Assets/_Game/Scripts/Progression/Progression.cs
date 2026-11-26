using System.Collections.Generic;
using Dino.Core;
using Dino.Save;
using Dino.Species;

namespace Dino.Progression
{
    /// <summary>
    /// Permanent XP and unlocks. XP is never spent and never lost; species and variants unlock
    /// when lifetime XP reaches their threshold.
    /// </summary>
    public class ProgressionService
    {
        readonly ProfileData _profile;
        readonly SpeciesCatalog _catalog;

        public ProgressionService(ProfileData profile, SpeciesCatalog catalog)
        {
            _profile = profile;
            _catalog = catalog;
        }

        public int Xp => _profile.lifetimeXp;
        public int Level => XpCurve.LevelForXp(_profile.lifetimeXp);

        public bool IsUnlocked(SpeciesDefinition s) => s != null && (s.isStarter || _profile.lifetimeXp >= s.unlockXp);

        public bool IsVariantUnlocked(SpeciesDefinition s, StatVariant v)
        {
            if (s == null || v == null) return false;
            return _profile.unlockedVariants.Contains($"{s.id}:{v.id}") || (IsUnlocked(s) && _profile.lifetimeXp >= v.unlockXp);
        }

        /// <summary>Adds XP and returns species that became unlocked because of it.</summary>
        public List<SpeciesDefinition> AddXp(int amount, string reason)
        {
            var newly = new List<SpeciesDefinition>();
            if (amount <= 0) return newly;
            _profile.lifetimeXp += amount;
            GameEvents.RaiseXpGained(amount, reason);

            if (_catalog == null) return newly;
            foreach (var s in _catalog.species)
            {
                if (s == null || s.isStarter || !IsUnlocked(s) || _profile.seenUnlocks.Contains(s.id)) continue;
                _profile.seenUnlocks.Add(s.id);
                newly.Add(s);
                GameEvents.RaiseUnlocked(s.id);
            }
            return newly;
        }
    }

    /// <summary>XP amounts for things you do. Kept small and frequent so young players see progress.</summary>
    public static class XpRewards
    {
        public const float FoodUnitsPerXp = 10f;
        public const float WaterUnitsPerXp = 15f;
        public const int PerMinuteAlive = 3;
        public const int FaintedAWildDino = 15;
        public const int RestedAtNewNest = 10;
    }
}
