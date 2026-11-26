using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Dino.World.EditorTools
{
    /// <summary>
    /// Downloads photo-scanned ground textures from Poly Haven (polyhaven.com, all CC0: free for any use,
    /// no credit required) and turns them into terrain layers on the WorldArtSet. 1K resolution keeps the
    /// game small and fast on phones. Run from Dino > World > Download Realistic Ground Textures.
    /// </summary>
    public static class PolyHavenTextures
    {
        public const string Folder = "Assets/_Game/World/Art/Ground";

        // Candidate Poly Haven asset ids per layer, tried in order until one downloads.
        static readonly (string layer, float tile, string[] ids)[] Layers =
        {
            ("Grass",       6f, new[] { "aerial_grass_rock", "grass_path_2", "forrest_ground_01" }),
            ("ForestFloor", 5f, new[] { "forest_leaves_02", "forrest_ground_01", "brown_mud_leaves_01" }),
            ("Sand",        5f, new[] { "coast_sand_01", "aerial_beach_01", "sandy_gravel" }),
            ("Mud",         5f, new[] { "brown_mud_leaves_01", "brown_mud_02", "dirt" }),
            ("Rock",       10f, new[] { "red_laterite_soil_stones", "rocky_terrain_02", "aerial_rocks_02" }),
        };

        [MenuItem("Dino/World/Download Realistic Ground Textures (Poly Haven, CC0)")]
        public static void DownloadAll()
        {
            var art = IslandBuilder.LoadOrCreateArtSet();
            PlaceholderArt.EnsureFolder(Folder);
            int ok = 0;
            try
            {
                for (int i = 0; i < Layers.Length; i++)
                {
                    var (layer, tile, ids) = Layers[i];
                    EditorUtility.DisplayProgressBar("Poly Haven textures", $"Downloading {layer}...", (float)i / Layers.Length);
                    var tl = DownloadLayer(layer, tile, ids);
                    if (!tl) continue;
                    ok++;
                    switch (layer)
                    {
                        case "Grass": art.grass = tl; break;
                        case "ForestFloor": art.forestFloor = tl; break;
                        case "Sand": art.sand = tl; break;
                        case "Mud": art.mud = tl; break;
                        case "Rock": art.rock = tl; break;
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Island] Poly Haven: {ok}/{Layers.Length} ground layers ready in {Folder}. Rebuild the island to use them.");
            if (ok < Layers.Length)
                Debug.LogWarning("[Island] Some layers failed to download (offline, or the asset was renamed). " +
                                 "Those keep the generated placeholder; you can drag any TerrainLayer into the WorldArtSet instead.");
        }

        static TerrainLayer DownloadLayer(string layer, float tile, string[] ids)
        {
            foreach (var id in ids)
            {
                string diffPath = $"{Folder}/{layer}_{id}_diff.jpg";
                string norPath = $"{Folder}/{layer}_{id}_normal.jpg";
                if (!File.Exists(diffPath))
                {
                    var urls = FindUrls(id);
                    if (urls.diff == null) continue;
                    if (!Download(urls.diff, diffPath)) continue;
                    if (urls.normal != null) Download(urls.normal, norPath);
                }
                AssetDatabase.ImportAsset(diffPath, ImportAssetOptions.ForceUpdate);
                Configure(diffPath, false);
                Texture2D normal = null;
                if (File.Exists(norPath))
                {
                    AssetDatabase.ImportAsset(norPath, ImportAssetOptions.ForceUpdate);
                    Configure(norPath, true);
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>(norPath);
                }

                string tlPath = $"{Folder}/TL_{layer}.terrainlayer";
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(tlPath);
                if (!tl) { tl = new TerrainLayer(); AssetDatabase.CreateAsset(tl, tlPath); }
                tl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
                tl.normalMapTexture = normal;
                tl.normalScale = 1f;
                tl.tileSize = new Vector2(tile, tile);
                tl.smoothness = layer == "Mud" ? 0.25f : 0.05f;
                EditorUtility.SetDirty(tl);
                Debug.Log($"[Island] {layer}: Poly Haven '{id}' (CC0) https://polyhaven.com/a/{id}");
                return tl;
            }
            return null;
        }

        /// <summary>Asks the Poly Haven API for the 1K JPG diffuse and OpenGL normal map of an asset.</summary>
        static (string diff, string normal) FindUrls(string id)
        {
            string json = GetText($"https://api.polyhaven.com/files/{id}");
            if (string.IsNullOrEmpty(json))
            {
                // Fall back to the documented CDN layout.
                string b = $"https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/{id}/{id}";
                return ($"{b}_diff_1k.jpg", $"{b}_nor_gl_1k.jpg");
            }
            string diff = Match(json, id, @"_diff_1k\.jpg");
            string nor = Match(json, id, @"_nor_gl_1k\.jpg");
            return (diff, nor);
        }

        static string Match(string json, string id, string suffix)
        {
            var m = Regex.Match(json, "\"url\"\\s*:\\s*\"(https://[^\"]*" + Regex.Escape(id) + suffix + ")\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        static string GetText(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 30;
                var op = req.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(20);
                return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null;
            }
        }

        static bool Download(string url, string path)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 120;
                var op = req.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(20);
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"[Island] Download failed: {url} ({req.error})"); return false; }
                File.WriteAllBytes(path, req.downloadHandler.data);
                return true;
            }
        }

        static void Configure(string path, bool normal)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.maxTextureSize = 1024;
            imp.mipmapEnabled = true;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
        }
    }
}
