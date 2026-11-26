using System;
using System.IO;
using UnityEngine;

namespace Dino.Save
{
    /// <summary>
    /// Saves to persistentDataPath/save.json. Writes to a temp file first and keeps one backup,
    /// so a phone dying mid-save never loses progress.
    /// </summary>
    public static class SaveSystem
    {
        public static string Folder { get; set; } = null;
        static string Dir => Folder ?? Application.persistentDataPath;
        public static string PathMain => Path.Combine(Dir, "save.json");
        static string PathTemp => Path.Combine(Dir, "save.json.tmp");
        static string PathBackup => Path.Combine(Dir, "save.json.bak");

        public static SaveData Load()
        {
            var data = TryRead(PathMain) ?? TryRead(PathBackup);
            return Migrate(data ?? new SaveData());
        }

        public static bool Save(SaveData data)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string json = JsonUtility.ToJson(data, prettyPrint: false);
                File.WriteAllText(PathTemp, json);
                if (File.Exists(PathMain))
                {
                    if (File.Exists(PathBackup)) File.Delete(PathBackup);
                    File.Move(PathMain, PathBackup);
                }
                File.Move(PathTemp, PathMain);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not save: {e.Message}");
                return false;
            }
        }

        /// <summary>Wipes everything (parent "reset progress" button, tests).</summary>
        public static void DeleteAll()
        {
            foreach (var p in new[] { PathMain, PathTemp, PathBackup })
                if (File.Exists(p)) File.Delete(p);
        }

        static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not read {Path.GetFileName(path)}: {e.Message}");
                return null;
            }
        }

        static SaveData Migrate(SaveData d)
        {
            if (d.profile == null) d.profile = new ProfileData();
            if (d.run == null) d.run = new RunData();
            if (d.settings == null) d.settings = new SettingsData();
            if (d.profile.seenUnlocks == null) d.profile.seenUnlocks = new System.Collections.Generic.List<string>();
            if (d.profile.unlockedVariants == null) d.profile.unlockedVariants = new System.Collections.Generic.List<string>();
            if (d.profile.colors == null) d.profile.colors = new System.Collections.Generic.List<SpeciesColorChoice>();
            // Future: if (d.version < 2) { ... }
            d.version = Core.GameInfo.SaveVersion;
            return d;
        }
    }
}
