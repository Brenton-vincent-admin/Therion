namespace Dino.World
{
    /// <summary>
    /// The six biomes from the design doc, plus Ocean. String ids are what PlayerSpawnPoint.biomeId and
    /// saves use; byte codes are what the baked BiomeMap grid stores.
    /// </summary>
    public static class BiomeIds
    {
        public const string Ocean = "ocean";
        public const string Meadow = "meadow";         // Nest Meadow: starting area, hatchling
        public const string Riverbanks = "riverbanks"; // hatchling
        public const string Forest = "forest";         // Fern Forest: juvenile
        public const string Swamp = "swamp";           // Misty Swamp: sub-adult
        public const string Highlands = "highlands";   // Red Highlands incl. the volcano: sub-adult
        public const string Lake = "lake";             // Lake Basin: adult

        public const byte OceanCode = 0, MeadowCode = 1, RiverbanksCode = 2, ForestCode = 3, SwampCode = 4, HighlandsCode = 5, LakeCode = 6;
        public const int Count = 7;

        static readonly string[] _ids = { Ocean, Meadow, Riverbanks, Forest, Swamp, Highlands, Lake };

        public static string FromCode(byte code) => code < _ids.Length ? _ids[code] : Ocean;

        public static byte ToCode(string id)
        {
            for (int i = 0; i < _ids.Length; i++) if (_ids[i] == id) return (byte)i;
            return OceanCode;
        }
    }
}
