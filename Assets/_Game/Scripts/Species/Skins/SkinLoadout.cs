using System;

namespace Dino.Species
{
    /// <summary>
    /// A dino's look as six small numbers, so it is cheap to save and send over the network.
    /// Colors are indexes into <see cref="SkinPalette"/>; the pattern indexes the shared pattern masks.
    /// The look is saved per species and carries through every growth stage (design doc).
    /// Layers: base color, belly color, pattern + pattern color, accent (crest, frill, plates, claws), eyes.
    /// </summary>
    [Serializable]
    public struct SkinLoadout : IEquatable<SkinLoadout>
    {
        public byte baseColor;
        public byte bellyColor;
        public byte pattern;
        public byte patternColor;
        public byte accentColor;
        public byte eyeColor;

        public SkinLoadout(byte baseColor, byte bellyColor, byte pattern, byte patternColor, byte accentColor, byte eyeColor)
        {
            this.baseColor = baseColor; this.bellyColor = bellyColor; this.pattern = pattern;
            this.patternColor = patternColor; this.accentColor = accentColor; this.eyeColor = eyeColor;
        }

        public static SkinLoadout Of(SkinColor baseC, SkinColor belly, SkinPattern pattern, SkinColor patternC,
            SkinColor accent, SkinColor eyes = SkinColor.Amber)
            => new SkinLoadout((byte)baseC, (byte)belly, (byte)pattern, (byte)patternC, (byte)accent, (byte)eyes);

        public SkinColor Base => (SkinColor)baseColor;
        public SkinColor Belly => (SkinColor)bellyColor;
        public SkinPattern Pattern => (SkinPattern)pattern;
        public SkinColor PatternColor => (SkinColor)patternColor;
        public SkinColor Accent => (SkinColor)accentColor;
        public SkinColor Eyes => (SkinColor)eyeColor;

        /// <summary>Packs into 48 bits (one ulong) for saves and network sync.</summary>
        public ulong Pack()
            => (ulong)baseColor | (ulong)bellyColor << 8 | (ulong)pattern << 16 |
               (ulong)patternColor << 24 | (ulong)accentColor << 32 | (ulong)eyeColor << 40;

        public static SkinLoadout Unpack(ulong v)
            => new SkinLoadout((byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24), (byte)(v >> 32), (byte)(v >> 40));

        /// <summary>Clamps every index into range so an old or corrupted save can never break rendering.</summary>
        public SkinLoadout Sanitized()
        {
            byte C(byte x) => x < SkinPalette.Count ? x : (byte)0;
            byte p = pattern < SkinPalette.PatternCount ? pattern : (byte)0;
            return new SkinLoadout(C(baseColor), C(bellyColor), p, C(patternColor), C(accentColor), C(eyeColor));
        }

        public bool Equals(SkinLoadout o) => Pack() == o.Pack();
        public override bool Equals(object obj) => obj is SkinLoadout o && Equals(o);
        public override int GetHashCode() => Pack().GetHashCode();
        public override string ToString() => $"{Base}/{Belly}/{Pattern}:{PatternColor}/{Accent}/{Eyes}";
    }

    /// <summary>Saved by value: only ever add new entries at the end.</summary>
    public enum SkinColor : byte
    {
        Leaf, Moss, Sand, Clay, Stone, Cream, Sunset, Berry, Sky, Lagoon, Grape, Sunflower,
        Coral, Mint, Charcoal, Snow, Amber, Gold, Crystal, WaterfallBlue, NightGlow,
    }

    /// <summary>Saved by value: only ever add new entries at the end. Index = slice in the pattern mask array.</summary>
    public enum SkinPattern : byte { None, Stripes, Spots, Bands, Speckles, Tiger, Giraffe, Leopard, Starry }

    public static class SkinPalette
    {
        /// <summary>sRGB hex for each <see cref="SkinColor"/>: bright, warm, readable on a phone.</summary>
        public static readonly uint[] Hex =
        {
            0x5DBB4A, // Leaf
            0x3E7D3A, // Moss
            0xE3C27A, // Sand
            0xC0704A, // Clay
            0x8A8F96, // Stone
            0xF4E9C9, // Cream
            0xF08A3C, // Sunset
            0xC2386B, // Berry
            0x6FC3F0, // Sky
            0x1F9E9A, // Lagoon
            0x8A5BD6, // Grape
            0xF6D23A, // Sunflower
            0xF27C7C, // Coral
            0x9BE3B5, // Mint
            0x3A3A40, // Charcoal
            0xFAFAFA, // Snow
            0xF0A020, // Amber
            0xFFD447, // Gold          (Hardcore reward)
            0xB8F2FF, // Crystal       (Hardcore reward)
            0x3FA9F5, // Waterfall Blue (find the waterfall)
            0x7CFFB2, // Night Glow    (survive 3 nights; eyes)
        };

        public static int Count => Hex.Length;
        public static int PatternCount => Enum.GetValues(typeof(SkinPattern)).Length;

        /// <summary>Colors every new player owns. Everything else is earned in play, never sold.</summary>
        public static readonly SkinColor[] StarterColors =
        {
            SkinColor.Leaf, SkinColor.Moss, SkinColor.Sand, SkinColor.Clay, SkinColor.Stone, SkinColor.Cream,
            SkinColor.Sunset, SkinColor.Sky, SkinColor.Sunflower, SkinColor.Charcoal, SkinColor.Amber, SkinColor.Coral,
        };

        public static readonly SkinPattern[] StarterPatterns = { SkinPattern.None, SkinPattern.Stripes, SkinPattern.Spots };

        public static void ToRgb01(SkinColor c, out float r, out float g, out float b)
        {
            int i = (int)c;
            uint h = i >= 0 && i < Hex.Length ? Hex[i] : Hex[0];
            r = ((h >> 16) & 0xFF) / 255f; g = ((h >> 8) & 0xFF) / 255f; b = (h & 0xFF) / 255f;
        }

        /// <summary>
        /// The paint shop's random-look button. Picks only from what the player owns and keeps the
        /// pattern color different from the base so the pattern is always visible.
        /// </summary>
        public static SkinLoadout Random(System.Random rng, SkinColor[] owned, SkinPattern[] ownedPatterns)
        {
            if (owned == null || owned.Length == 0) owned = StarterColors;
            if (ownedPatterns == null || ownedPatterns.Length == 0) ownedPatterns = StarterPatterns;
            SkinColor Pick() => owned[rng.Next(owned.Length)];
            var baseC = Pick();
            var belly = Array.IndexOf(owned, SkinColor.Cream) >= 0 && rng.Next(2) == 0 ? SkinColor.Cream : Pick();
            var pat = ownedPatterns[rng.Next(ownedPatterns.Length)];
            var patC = Pick();
            if (patC == baseC && owned.Length > 1) patC = owned[(Array.IndexOf(owned, baseC) + 1) % owned.Length];
            return SkinLoadout.Of(baseC, belly, pat, patC, Pick(), SkinColor.Amber);
        }
    }

    /// <summary>Undo stack for the paint shop (random-look button with an undo).</summary>
    public class SkinEditHistory
    {
        readonly System.Collections.Generic.List<ulong> _stack = new System.Collections.Generic.List<ulong>();
        public const int Max = 20;

        public int Count => _stack.Count;

        public void Push(SkinLoadout s)
        {
            _stack.Add(s.Pack());
            if (_stack.Count > Max) _stack.RemoveAt(0);
        }

        public bool TryUndo(out SkinLoadout previous)
        {
            previous = default;
            if (_stack.Count == 0) return false;
            previous = SkinLoadout.Unpack(_stack[_stack.Count - 1]);
            _stack.RemoveAt(_stack.Count - 1);
            return true;
        }
    }
}
