using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dino.Species.Editor
{
    /// <summary>
    /// Builds the 13 launch SpeciesDefinition assets and the SpeciesCatalog from LaunchRoster.
    /// Safe to re-run after tuning: numbers are rewritten, but art references (model prefab, icon,
    /// model scale) are kept.
    /// </summary>
    public static class SpeciesAssetBuilder
    {
        public const string SpeciesFolder = "Assets/_Game/Data/Species";
        public const string CatalogPath = "Assets/_Game/Resources/SpeciesCatalog.asset";

        [MenuItem("Dino/Species/Rebuild Species Assets")]
        public static void Rebuild()
        {
            EnsureFolder(SpeciesFolder);
            EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));

            var defs = new List<SpeciesDefinition>();
            foreach (var entry in LaunchRoster.All)
            {
                string path = $"{SpeciesFolder}/Species_{entry.id}.asset";
                var def = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>(path);
                bool isNew = def == null;
                if (isNew) def = ScriptableObject.CreateInstance<SpeciesDefinition>();

                SpeciesContentApplier.Apply(entry, def);
                def.name = $"Species_{entry.id}";

                if (isNew) AssetDatabase.CreateAsset(def, path);
                else EditorUtility.SetDirty(def);
                defs.Add(def);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<SpeciesCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SpeciesCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            // Keep any extra species someone added by hand; replace launch ones.
            var keep = catalog.species.FindAll(s => s != null && LaunchRoster.Get(s.id) == null);
            catalog.species = new List<SpeciesDefinition>(defs);
            catalog.species.AddRange(keep);
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Species] Built {defs.Count} species assets and {CatalogPath}.");
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
