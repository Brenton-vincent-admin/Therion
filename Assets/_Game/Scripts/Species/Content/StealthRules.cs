using System;

namespace Dino.Species
{
    /// <summary>Everything that affects how noticeable a dino is right now.</summary>
    public struct StealthContext
    {
        public Stance stance;
        public bool inCover;        // tall grass or deep shade
        public bool stealthBroken;  // ran, attacked or called in the last few seconds
        public bool hideActive;     // Psittacosaurus Hide ability
        public int hideLevel;
        public float scale;         // growth scale, 0.2 hatchling .. 1 adult
    }

    /// <summary>
    /// Stealth (roster and design docs). A stealthy species is noticed from about a third of the
    /// normal range; standing still in tall grass or shade it can only be spotted face to face.
    /// It also sees further, so it spots others first. Running, attacking or calling breaks
    /// stealth for a few seconds. Small hatchlings of any species can hide in tall grass.
    /// </summary>
    public static class StealthRules
    {
        public const float FaceToFaceRange = 2f;
        public const float StealthBreakSeconds = 3f;
        public const float LoudMultiplier = 1.4f;

        /// <summary>
        /// Distance (m) at which others notice this dino right now.
        /// baseRadius = species noticeRadius x variant multiplier; sneakMultiplier = species sneakNoticeMultiplier.
        /// </summary>
        public static float NoticeRange(float baseRadius, float sneakMultiplier, bool stealthy, StealthContext c)
        {
            if (c.stealthBroken || c.stance == Stance.Running || c.stance == Stance.Attacking || c.stance == Stance.Calling)
                return Math.Max(baseRadius, SpeciesTuning.NormalNoticeRadius) * LoudMultiplier * SizeFactor(c.scale);

            float range = baseRadius;
            if (c.stance == Stance.Sneaking || c.stance == Stance.Still) range *= sneakMultiplier;

            bool tiny = c.scale <= 0.25f;
            bool quiet = c.stance == Stance.Still || c.stance == Stance.Sneaking;
            if (c.inCover && quiet)
                range = (stealthy || tiny) ? Math.Min(range, FaceToFaceRange) : range * 0.6f;

            if (c.hideActive && c.inCover && (quiet || c.hideLevel >= 2))
                range = Math.Min(range, 1.5f);

            return Math.Max(1f, range * SizeFactor(c.scale));
        }

        /// <summary>Bigger bodies are easier to see. A hatchling is about 70% as visible as an adult.</summary>
        public static float SizeFactor(float scale) => 0.6f + 0.4f * (scale < 0 ? 0 : scale > 1 ? 1 : scale);

        /// <summary>Stealthy species have sharper eyes; night shrinks sight unless the observer has night vision.</summary>
        public static float EyesMultiplier(bool observerStealthy, bool isNight, bool nightVision)
        {
            float eyes = observerStealthy ? 1.3f : 1f;
            if (isNight && !nightVision) eyes *= 0.7f;
            return eyes;
        }

        /// <summary>The eye icon: Seen inside notice range, Curious just outside it, otherwise Hidden.</summary>
        public static Awareness Check(float distance, float targetNoticeRange, float observerEyes)
        {
            float seeAt = targetNoticeRange * observerEyes;
            if (distance <= seeAt) return Awareness.Seen;
            if (distance <= seeAt * 1.5f) return Awareness.Curious;
            return Awareness.Hidden;
        }
    }
}
