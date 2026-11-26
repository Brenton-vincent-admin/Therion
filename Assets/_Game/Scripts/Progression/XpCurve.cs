using UnityEngine;

namespace Dino.Progression
{
    /// <summary>Player level from lifetime XP. XpForLevel(L) = 250(L-1) + 50(L-1)^2  (L2 = 300, L15 = 13300).</summary>
    public static class XpCurve
    {
        public static int XpForLevel(int level)
        {
            int n = Mathf.Max(0, level - 1);
            return 250 * n + 50 * n * n;
        }

        public static int LevelForXp(int xp)
        {
            int level = 1;
            while (XpForLevel(level + 1) <= xp && level < 999) level++;
            return level;
        }

        /// <summary>0..1 progress from the current level to the next.</summary>
        public static float ProgressToNext(int xp)
        {
            int l = LevelForXp(xp);
            int a = XpForLevel(l), b = XpForLevel(l + 1);
            return b <= a ? 1f : Mathf.Clamp01((xp - a) / (float)(b - a));
        }
    }
}
