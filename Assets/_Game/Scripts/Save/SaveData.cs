using System;
using System.Collections.Generic;
using Dino.Core;
using UnityEngine;

namespace Dino.Save
{
    /// <summary>Everything saved on the phone. Plain serializable data (JsonUtility). No personal data.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = GameInfo.SaveVersion;
        public ProfileData profile = new ProfileData();
        public RunData run = new RunData();
        public SettingsData settings = new SettingsData();
    }

    /// <summary>Permanent progress: never lost on fainting or Hardcore restarts.</summary>
    [Serializable]
    public class ProfileData
    {
        public int lifetimeXp;
        /// <summary>Species ids the player has been told about (unlock celebration shown).</summary>
        public List<string> seenUnlocks = new List<string>();
        /// <summary>"speciesId:variantId" for earned variants.</summary>
        public List<string> unlockedVariants = new List<string>();
        public List<SpeciesColorChoice> colors = new List<SpeciesColorChoice>();
        public int runsStarted;
        public int timesGrownToAdult;
        public float bestHardcoreMinutes;
    }

    [Serializable]
    public class SpeciesColorChoice
    {
        public string speciesId;
        public string skinId;
        public Color baseColor = Color.white;
        public Color patternColor = Color.white;
    }

    /// <summary>The dino you are playing right now.</summary>
    [Serializable]
    public class RunData
    {
        public bool active;
        public GameMode mode;
        public string speciesId;
        public string variantId;
        public int stage;
        public float secondsInStage;
        public float health, stamina, food, water;
        public Vector3 position;
        public float yaw;
        public string nestId;
        public float minutesAlive;
    }

    [Serializable]
    public class SettingsData
    {
        public GameMode mode = GameMode.Survival;
        public bool showTouchControls = true;
        public float lookSensitivity = 0.12f;
        /// <summary>Parent setting: wild predators don't chase the player.</summary>
        public bool friendlyPredators;
        /// <summary>Parent/testing setting: every species can be picked. XP and real unlocks are untouched.</summary>
        public bool unlockAllSpecies;
    }
}
