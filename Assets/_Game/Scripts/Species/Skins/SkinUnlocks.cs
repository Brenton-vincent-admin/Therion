using System.Collections.Generic;

namespace Dino.Species
{
    /// <summary>
    /// What unlocks which colors and patterns (roster doc, "What unlocks what"). Everything is earned
    /// in play, never sold. Progression raises the milestone ids; this table maps them to rewards.
    /// Pure C# so the UI and tests can use it anywhere.
    /// </summary>
    public static class SkinUnlocks
    {
        public struct Reward
        {
            public string milestoneId; public string hint;
            public SkinColor[] colors; public SkinPattern[] patterns;
        }

        public static readonly IReadOnlyList<Reward> All = new List<Reward>
        {
            R("stage_juvenile", "Grow to Juvenile", null, new[] { SkinPattern.Bands, SkinPattern.Speckles }),
            R("stage_adult", "Grow to Adult", new[] { SkinColor.Berry, SkinColor.Grape }, new[] { SkinPattern.Tiger }),
            R("found_waterfall", "Find the waterfall", new[] { SkinColor.WaterfallBlue, SkinColor.Lagoon }, null),
            R("survive_3_nights", "Survive 3 nights in a row", new[] { SkinColor.NightGlow }, new[] { SkinPattern.Starry }),
            R("visit_all_biomes", "Visit every place on the map", new[] { SkinColor.Mint, SkinColor.Snow }, new[] { SkinPattern.Giraffe }),
            R("species_challenge", "Use your special move 20 times", null, new[] { SkinPattern.Leopard }),
            R("hardcore_adult", "Grow to Adult in Hardcore", new[] { SkinColor.Gold, SkinColor.Crystal }, null),
            R("play_together", "Play with a friend", new[] { SkinColor.Coral }, null),
        };

        static Reward R(string id, string hint, SkinColor[] c, SkinPattern[] p)
            => new Reward { milestoneId = id, hint = hint, colors = c ?? new SkinColor[0], patterns = p ?? new SkinPattern[0] };

        /// <summary>Everything owned given the milestones reached (starter set included).</summary>
        public static void Owned(ICollection<string> milestones, List<SkinColor> colors, List<SkinPattern> patterns)
        {
            colors.Clear(); patterns.Clear();
            colors.AddRange(SkinPalette.StarterColors);
            patterns.AddRange(SkinPalette.StarterPatterns);
            foreach (var r in All)
            {
                if (milestones == null || !milestones.Contains(r.milestoneId)) continue;
                foreach (var c in r.colors) if (!colors.Contains(c)) colors.Add(c);
                foreach (var p in r.patterns) if (!patterns.Contains(p)) patterns.Add(p);
            }
        }

        /// <summary>The hint shown on a locked swatch (a picture hint in the UI, never a price).</summary>
        public static string HintFor(SkinColor c)
        {
            foreach (var r in All) if (System.Array.IndexOf(r.colors, c) >= 0) return r.hint;
            return null;
        }
    }
}
