namespace Dino.Core
{
    public enum GameMode
    {
        /// <summary>Fainting respawns you at your nest (or a spawn point). Growth is kept.</summary>
        Survival = 0,
        /// <summary>Fainting ends the run: start again as a hatchling. Player XP and unlocks are kept.</summary>
        Hardcore = 1,
        /// <summary>Parent toggle: food, water and stamina never drain, no fainting.</summary>
        Explorer = 2,
    }
}
