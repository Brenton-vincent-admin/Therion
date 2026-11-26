namespace Dino.Species
{
    /// <summary>Cooldowns, stamina costs and ranges for the signature abilities. Pure data, tune here.</summary>
    public struct AbilitySpec
    {
        public float cooldown, staminaCost, duration, range, power;
    }

    public static class AbilityTuning
    {
        /// <summary>Numbers for an ability at level 1..3. range is meters at adult size, power is a damage or speed multiplier.</summary>
        public static AbilitySpec Get(SignatureAbility a, int level)
        {
            int l = level < 1 ? 1 : level > 3 ? 3 : level;
            switch (a)
            {
                case SignatureAbility.Call:           return S(10f, 5f, 6f, l == 1 ? 20f : 35f, 1.2f);
                case SignatureAbility.HeadButt:       return S(6f, 15f, l == 1 ? 0.6f : 0.9f, 2f, 1.2f);
                case SignatureAbility.Hide:           return S(8f, 0f, l == 1 ? 10f : l == 2 ? 15f : 20f, 6f, 1f);
                case SignatureAbility.TailSwipe:      return S(4f, 10f, 0.4f, l == 1 ? 3.5f : 5f, 1.3f);
                case SignatureAbility.HornCharge:     return S(8f, 20f, l == 1 ? 1.0f : 1.4f, 2.5f, 1.5f);
                case SignatureAbility.HighReachStomp: return S(10f, 15f, 4f, l == 1 ? 10f : 16f, 1f);
                case SignatureAbility.Snatch:         return S(6f, 10f, 0.5f, 3f, l == 1 ? 1.4f : 1.7f);
                case SignatureAbility.SprintBurst:    return S(10f, 0f, l == 1 ? 2f : 3f, 10f, 1.5f);
                case SignatureAbility.NightVision:    return S(1f, 0f, 0f, l == 1 ? 0f : 25f, 1f);
                case SignatureAbility.Glide:          return S(1f, 0f, l == 1 ? 4f : 7f, 1.5f, 0.15f);
                case SignatureAbility.Pounce:         return S(5f, 15f, l == 1 ? 0.45f : 0.65f, 2f, 1.5f);
                case SignatureAbility.FrillFlash:     return S(8f, 10f, l == 1 ? 2f : 3.5f, 7f, 1f);
                case SignatureAbility.Roar:           return S(12f, 15f, 5f, l == 1 ? 15f : 25f, 1.2f);
                case SignatureAbility.BullRush:       return S(9f, 20f, l == 1 ? 1.4f : 2.0f, 2.5f, 1.4f);
                default:                              return S(1f, 0f, 0f, 0f, 1f);
            }
        }

        static AbilitySpec S(float cd, float st, float dur, float range, float power)
            => new AbilitySpec { cooldown = cd, staminaCost = st, duration = dur, range = range, power = power };
    }
}
