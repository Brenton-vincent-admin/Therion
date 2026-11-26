using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dino.Species;
using Dino.World.Generation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Dino.World.EditorTools
{
    /// <summary>
    /// Downloads free photo-scanned art from Poly Haven (polyhaven.com) and fits it into the WorldArtSet:
    /// a real sky photo (HDRI) that also lights the scene, 2K ground textures, and scanned trees, rocks,
    /// logs and plants. Every Poly Haven asset is CC0: free for any use, no credit required. Files land in
    /// Assets/_Game/World/Art/PolyHaven (not committed to git; run this once per computer).
    /// Menu: Dino > World > Make Island Realistic.
    /// </summary>
    public static class PolyHavenLibrary
    {
        public const string Root = "Assets/_Game/World/Art/PolyHaven";
        const string Api = "https://api.polyhaven.com";
        const string UserAgent = "TherionIslandBuilder/1.0";

        // ---------- what to fetch (ids are tried in order; unknown ids are skipped) ----------

        static readonly string[] Skies =
        {
            "kloofendal_48d_partly_cloudy_puresky", "kloofendal_43d_clear_puresky", "sunflowers_puresky",
            "kloppenheim_06_puresky", "autumn_field_puresky",
        };

        static readonly (string layer, float tile, string[] ids)[] Ground =
        {
            ("Grass", 5f, new[] { "leafy_grass", "aerial_grass_rock", "forrest_ground_01", "grass_path_2" }),
            ("Sand", 4f, new[] { "coast_sand_01", "aerial_beach_01", "sandy_gravel", "coast_sand_rocks_02" }),
            ("Mud", 4f, new[] { "brown_mud_leaves_01", "forest_leaves_02", "brown_mud_02", "forrest_ground_01" }),
            ("Rock", 9f, new[] { "rocky_terrain_02", "aerial_rocks_02", "red_laterite_soil_stones", "rock_face" }),
        };

        class Slot
        {
            public WorldPropKind kind;
            public string[] ids;
            public string[] searchAny;      // fallback: words to look for in the Poly Haven catalogue
            public string[] searchNone;
            public int assets = 1;          // how many different scans to use
            public int variantsPerAsset = 1;
            public float min, max;
            public bool byLongest, layFlat, wind, collider = true;
            public float sink;
            public int lod0 = 250000;
        }

        static readonly string[] NotOutdoor = { "pot", "potted", "planter", "vase", "indoor", "christmas", "house", "wooden", "furniture", "stump" };

        static readonly Slot[] Slots =
        {
            new Slot { kind = WorldPropKind.Tree, ids = new[] { "island_tree_01", "island_tree_02", "jacaranda_tree", "tree_small_02" },
                searchAny = new[] { "tree" }, searchNone = NotOutdoor, assets = 2, min = 8f, max = 14f, wind = true, lod0 = 110000 },
            new Slot { kind = WorldPropKind.Pine, ids = new[] { "fir_tree_01", "pine_tree_01", "fir_sapling_medium", "fir_sapling" },
                searchAny = new[] { "fir", "pine", "conifer" }, searchNone = NotOutdoor, assets = 1, min = 9f, max = 15f, wind = true, lod0 = 250000 },
            new Slot { kind = WorldPropKind.SwampTree, ids = new[] { "island_tree_03", "island_tree_02", "jacaranda_tree" },
                searchAny = new[] { "tree" }, searchNone = NotOutdoor, assets = 1, min = 8f, max = 13f, wind = true, lod0 = 110000 },
            new Slot { kind = WorldPropKind.Rock, ids = new[] { "boulder_01", "rock_moss_set_01", "rock_moss_set_02", "namaqualand_boulder_02", "rock_07", "rock_09", "coast_rocks_01" },
                searchAny = new[] { "boulder", "rock" }, searchNone = new[] { "wall", "brick", "cliff", "face" }, assets = 3, variantsPerAsset = 3,
                min = 1.3f, max = 3.2f, byLongest = true, sink = 0.14f, lod0 = 12000 },
            new Slot { kind = WorldPropKind.Log, ids = new[] { "dead_tree_trunk", "dead_tree_trunk_02", "tree_stump_01", "tree_stump_02" },
                searchAny = new[] { "trunk", "log", "stump" }, searchNone = new[] { "pot", "bench", "furniture", "cabin" }, assets = 2,
                min = 3f, max = 6.5f, byLongest = true, layFlat = true, sink = 0.1f, lod0 = 15000 },
            new Slot { kind = WorldPropKind.BerryBush, ids = new[] { "shrub_01", "shrub_02", "shrub_03", "searsia_lucida" },
                searchAny = new[] { "shrub", "bush" }, searchNone = NotOutdoor, assets = 2, variantsPerAsset = 2,
                min = 1.0f, max = 1.6f, wind = true, collider = false, lod0 = 25000 },
            new Slot { kind = WorldPropKind.LeafShrub, ids = new[] { "searsia_burchellii", "shrub_04", "shrub_sorrel_01", "shrub_02" },
                searchAny = new[] { "shrub", "bush" }, searchNone = NotOutdoor, assets = 2, variantsPerAsset = 2,
                min = 1.3f, max = 2.1f, wind = true, collider = false, lod0 = 30000 },
            new Slot { kind = WorldPropKind.Fern, ids = new[] { "fern_02", "fern_01", "nettle_plant", "weed_plant_02", "periwinkle_plant" },
                searchAny = new[] { "fern", "weed", "nettle" }, searchNone = NotOutdoor, assets = 2, variantsPerAsset = 2,
                min = 0.55f, max = 0.95f, wind = true, collider = false, lod0 = 12000 },
            new Slot { kind = WorldPropKind.TallLeafTree, ids = new[] { "jacaranda_tree", "island_tree_01", "island_tree_02" },
                searchAny = new[] { "tree" }, searchNone = NotOutdoor, assets = 1, min = 11f, max = 14f, wind = true, lod0 = 110000 },
            new Slot { kind = WorldPropKind.GreatTree, ids = new[] { "island_tree_01", "jacaranda_tree", "island_tree_02" },
                searchAny = new[] { "tree" }, searchNone = NotOutdoor, assets = 1, min = 30f, max = 34f, wind = true, lod0 = 300000 },
        };

        static readonly List<string> Credits = new List<string>();

        // ---------- menus ----------

        [MenuItem("Dino/World/Make Island Realistic (download free art, then build)", priority = 1)]
        public static void MakeRealistic()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GraphicsTiers.EnsureSetUp();
            GraphicsTiers.UseHigh();
            if (!DownloadAll()) return;
            IslandBuilder.Build(IslandBuilder.LoadOrCreateSpec(), IslandBuilder.LoadOrCreateArtSet());
        }

        [MenuItem("Dino/World/Download Realistic Art Only (Poly Haven, CC0)", priority = 2)]
        public static void DownloadMenu() => DownloadAll();

        /// <summary>Returns false if cancelled. Anything that fails to download keeps its placeholder.</summary>
        public static bool DownloadAll()
        {
            var art = IslandBuilder.LoadOrCreateArtSet();
            PlaceholderArt.EnsureFolder(Root);
            PlaceholderArt.EnsureFolder(IslandBuilder.GeneratedRoot);
            Credits.Clear();
            FilesCache.Clear();
            _catalogue = null;
            var report = new StringBuilder();
            bool cancelled = false;
            try
            {
                try { Sky(art, report); } catch (OperationCanceledException) { throw; } catch (Exception e) { report.AppendLine("Sky failed: " + e.Message); }
                try { GroundLayers(art, report); } catch (OperationCanceledException) { throw; } catch (Exception e) { report.AppendLine("Ground failed: " + e.Message); }
                try { Models(art, report); } catch (OperationCanceledException) { throw; } catch (Exception e) { report.AppendLine("Models failed: " + e.Message); }
            }
            catch (OperationCanceledException) { cancelled = true; }
            finally { EditorUtility.ClearProgressBar(); }

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            WriteCredits();
            Debug.Log("[Island] Poly Haven art " + (cancelled ? "(cancelled part way)" : "ready") + ":\n" + report);
            return !cancelled;
        }

        static void Step(string what, float t)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Downloading free art from Poly Haven", what, t))
                throw new OperationCanceledException();
        }

        // ================= sky =================

        static void Sky(WorldArtSet art, StringBuilder report)
        {
            string folder = Root + "/Sky";
            PlaceholderArt.EnsureFolder(folder);
            foreach (var id in Skies)
            {
                Step($"Sky: {id}", 0.02f);
                string path = $"{folder}/{id}.hdr";
                if (!File.Exists(path))
                {
                    var files = Files(id);
                    string url = FindUrl(files, u => Regex.IsMatch(u, @"_4k\.hdr$")) ?? FindUrl(files, u => Regex.IsMatch(u, @"_2k\.hdr$"));
                    if (url == null && files == null) url = $"https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/4k/{id}_4k.hdr";
                    if (url == null || !Download(url, path)) continue;
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(path) is TextureImporter imp)
                {
                    imp.textureShape = TextureImporterShape.Texture2D;
                    imp.textureType = TextureImporterType.Default;
                    imp.wrapModeU = TextureWrapMode.Repeat;
                    imp.wrapModeV = TextureWrapMode.Clamp;
                    imp.mipmapEnabled = false;
                    imp.maxTextureSize = 4096;
                    imp.npotScale = TextureImporterNPOTScale.None;
                    var android = imp.GetPlatformTextureSettings("Android");
                    android.overridden = true;
                    android.maxTextureSize = 2048;
                    android.format = TextureImporterFormat.ASTC_HDR_6x6;
                    imp.SetPlatformTextureSettings(android);
                    imp.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var shader = Shader.Find("Skybox/Panoramic");
                if (!tex || !shader) { report.AppendLine($"Sky: {id} downloaded but couldn't be used."); continue; }

                string matPath = $"{folder}/M_Sky.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, matPath); }
                m.shader = shader;
                m.SetTexture("_MainTex", tex);
                m.SetFloat("_Mapping", 1f);
                m.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
                m.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
                m.SetFloat("_ImageType", 0f);
                m.SetFloat("_MirrorOnBack", 0f);
                m.SetFloat("_Layout", 0f);
                m.SetFloat("_Rotation", 0f);

                float exposure = 1f;
                try
                {
                    var a = HdrAnalysis.Analyze(path);
                    // Bring every sky to the same brightness so the lighting numbers below always fit.
                    exposure = Mathf.Clamp(0.42f / Mathf.Max(0.02f, a.skyLuminance), 0.3f, 3f);
                    art.sunDirection = a.sunDirection;
                    art.ambientSky = Tone(a.sky * exposure, 1.4f);
                    art.ambientEquator = Tone(a.horizon * exposure, 1.2f);
                    art.ambientGround = Tone(Color.Lerp(a.horizon * exposure, new Color(0.22f, 0.24f, 0.12f), 0.6f) * 0.55f, 0.8f);
                    art.fogColor = Tone(a.horizon * exposure * 1.05f, 1.0f);
                    art.hasSkyLighting = true;
                }
                catch (Exception e)
                {
                    art.hasSkyLighting = false;
                    report.AppendLine("Sky: couldn't measure the sun position (" + e.Message + "); using default lighting.");
                }
                m.SetFloat("_Exposure", exposure);
                EditorUtility.SetDirty(m);
                art.skybox = m;
                Credits.Add($"Sky: {id} https://polyhaven.com/a/{id}");
                report.AppendLine($"Sky: {id}, sun {Mathf.Asin(Mathf.Clamp(art.sunDirection.y, -1f, 1f)) * Mathf.Rad2Deg:0} degrees up.");
                return;
            }
            report.AppendLine("Sky: nothing downloaded, keeping the placeholder sky.");
        }

        static Color Tone(Color c, float max)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m > max) c *= max / m;
            return new Color(c.r, c.g, c.b, 1f);
        }

        // ================= ground =================

        static void GroundLayers(WorldArtSet art, StringBuilder report)
        {
            string folder = Root + "/Ground";
            PlaceholderArt.EnsureFolder(folder);
            for (int i = 0; i < Ground.Length; i++)
            {
                var (layer, tile, ids) = Ground[i];
                var order = new List<string>(ids);
                if (layer == "Grass")
                {
                    // Look at every candidate photo and start with the greenest one.
                    var score = new Dictionary<string, float>();
                    foreach (var id in ids)
                    {
                        Step($"Ground: comparing grass photos ({id})", 0.05f);
                        string diff = $"{folder}/{layer}_{id}_diff.jpg";
                        if (!File.Exists(diff) && !FetchGroundMap(id, diff, @"_(diff|col|color|albedo)_2k\.jpg$", "_diff_2k.jpg")) { score[id] = -1f; continue; }
                        try
                        {
                            var c = AverageColor(diff);
                            score[id] = c.g - Mathf.Max(c.r, c.b);
                            report.AppendLine($"  grass candidate {id}: average colour ({c.r:0.00}, {c.g:0.00}, {c.b:0.00})");
                        }
                        catch (Exception) { score[id] = -1f; }
                    }
                    order.Sort((x, y) => score[y].CompareTo(score[x]));
                }

                TerrainLayer tl = null;
                foreach (var id in order)
                {
                    Step($"Ground: {layer} ({id})", 0.06f + 0.14f * i / Ground.Length);
                    tl = GroundLayer(folder, layer, tile, id, art);
                    if (tl) { Credits.Add($"Ground {layer}: {id} https://polyhaven.com/a/{id}"); report.AppendLine($"Ground {layer}: {id}"); break; }
                }
                if (!tl) { report.AppendLine($"Ground {layer}: nothing downloaded, keeping the placeholder."); continue; }
                switch (layer)
                {
                    case "Grass": art.grass = tl; break;
                    case "Sand": art.sand = tl; break;
                    case "Mud": art.mud = tl; break;
                    case "Rock": art.rock = tl; break;
                }
            }
            // Four layers draw in one pass and can blend by height; a fifth would turn both off.
            art.forestFloor = null;
        }

        static bool FetchGroundMap(string id, string path, string pattern, string cdnSuffix)
        {
            var files = Files(id);
            string u = FindUrl(files, s => Regex.IsMatch(s, pattern));
            if (u == null && files == null && cdnSuffix != null) u = $"https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/{id}/{id}{cdnSuffix}";
            return u != null && Download(u, path);
        }

        static TerrainLayer GroundLayer(string folder, string layer, float tile, string id, WorldArtSet art)
        {
            string diff = $"{folder}/{layer}_{id}_diff.jpg", nor = $"{folder}/{layer}_{id}_normal.jpg";
            string arm = $"{folder}/{layer}_{id}_arm.jpg", disp = $"{folder}/{layer}_{id}_disp.png", mask = $"{folder}/{layer}_{id}_mask.png";
            if (!File.Exists(diff) && !FetchGroundMap(id, diff, @"_(diff|col|color|albedo)_2k\.jpg$", "_diff_2k.jpg")) return null;
            if (!File.Exists(nor)) FetchGroundMap(id, nor, @"_nor_gl_2k\.jpg$", "_nor_gl_2k.jpg");
            if (!File.Exists(mask))
            {
                FetchGroundMap(id, arm, @"_arm_2k\.jpg$", null);
                if (!FetchGroundMap(id, disp, @"_(disp|height)_2k\.png$", null)) FetchGroundMap(id, disp, @"_(disp|height)_2k\.jpg$", null);
            }

            Color average = new Color(0.3f, 0.36f, 0.16f);
            bool haveMask = File.Exists(mask) || BuildMask(arm, disp, mask);
            try { average = AverageColor(diff); } catch (Exception) { }

            AssetDatabase.ImportAsset(diff, ImportAssetOptions.ForceUpdate);
            ConfigureTexture(diff, TextureImporterType.Default, true, 2048);
            Texture2D normalTex = null, maskTex = null;
            if (File.Exists(nor))
            {
                AssetDatabase.ImportAsset(nor, ImportAssetOptions.ForceUpdate);
                ConfigureTexture(nor, TextureImporterType.NormalMap, false, 2048);
                normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(nor);
            }
            if (haveMask)
            {
                AssetDatabase.ImportAsset(mask, ImportAssetOptions.ForceUpdate);
                ConfigureTexture(mask, TextureImporterType.Default, false, 1024);
                maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(mask);
            }
            // The raw arm/disp files are only ingredients for the mask.
            if (File.Exists(arm)) { File.Delete(arm); File.Delete(arm + ".meta"); }
            if (File.Exists(disp)) { File.Delete(disp); File.Delete(disp + ".meta"); }

            string tlPath = $"{folder}/TL_{layer}.terrainlayer";
            var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(tlPath);
            if (!tl) { tl = new TerrainLayer(); AssetDatabase.CreateAsset(tl, tlPath); }
            tl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diff);
            tl.normalMapTexture = normalTex;
            tl.maskMapTexture = maskTex;
            tl.normalScale = 1f;
            tl.tileSize = new Vector2(tile, tile);
            tl.metallic = 0f;
            tl.smoothness = layer == "Mud" ? 0.3f : 0.08f;
            tl.maskMapRemapMin = new Vector4(0f, 0f, 0f, 0f);
            tl.maskMapRemapMax = new Vector4(0f, 1f, 1f, 1f);
            EditorUtility.SetDirty(tl);
            if (!tl.diffuseTexture) return null;

            if (layer == "Grass")
            {
                // Grass blades take their colour from the ground photo so the two blend together.
                // Always a living green, however pale the photo is; only the exact hue follows it.
                Color.RGBToHSV(average, out float h, out float s, out float v);
                h = Mathf.Clamp(h, 0.23f, 0.32f);
                art.grassRoot = Color.HSVToRGB(h + 0.01f, Mathf.Clamp(s * 1.3f, 0.60f, 0.85f), Mathf.Clamp(v * 0.6f, 0.10f, 0.17f));
                art.grassTip = Color.HSVToRGB(h - 0.02f, Mathf.Clamp(s * 1.2f, 0.58f, 0.78f), Mathf.Clamp(v * 1.15f, 0.30f, 0.42f));
            }
            return tl;
        }

        /// <summary>Terrain mask map: R metallic (0), G ambient occlusion, B height, A smoothness.</summary>
        static bool BuildMask(string armPath, string dispPath, string outPath)
        {
            if (!File.Exists(armPath) && !File.Exists(dispPath)) return false;
            Texture2D arm = null, disp = null, result = null;
            try
            {
                const int N = 1024;
                Color32[] ap = null, dp = null;
                int aw = 0, ah = 0, dw = 0, dh = 0;
                if (File.Exists(armPath)) { arm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); if (arm.LoadImage(File.ReadAllBytes(armPath))) { ap = arm.GetPixels32(); aw = arm.width; ah = arm.height; } }
                if (File.Exists(dispPath)) { disp = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); if (disp.LoadImage(File.ReadAllBytes(dispPath))) { dp = disp.GetPixels32(); dw = disp.width; dh = disp.height; } }
                if (ap == null && dp == null) return false;

                // Stretch the height to the full range so layers blend crisply.
                int lo = 255, hi = 0;
                if (dp != null) for (int i = 0; i < dp.Length; i += 7) { int r = dp[i].r; if (r < lo) lo = r; if (r > hi) hi = r; }
                float span = Mathf.Max(1, hi - lo);

                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    byte ao = 255, smooth = 20, height = 128;
                    if (ap != null)
                    {
                        var a = ap[(y * ah / N) * aw + x * aw / N];
                        ao = a.r;
                        smooth = (byte)Mathf.Clamp((255 - a.g) * 0.8f, 0f, 255f);
                    }
                    if (dp != null) height = (byte)Mathf.Clamp((dp[(y * dh / N) * dw + x * dw / N].r - lo) * 255f / span, 0f, 255f);
                    px[y * N + x] = new Color32(0, ao, height, smooth);
                }
                result = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
                result.SetPixels32(px);
                result.Apply();
                File.WriteAllBytes(outPath, result.EncodeToPNG());
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Island] Couldn't build a terrain mask map: " + e.Message);
                return false;
            }
            finally
            {
                if (arm) Object.DestroyImmediate(arm);
                if (disp) Object.DestroyImmediate(disp);
                if (result) Object.DestroyImmediate(result);
            }
        }

        static Color AverageColor(string imagePath)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(File.ReadAllBytes(imagePath))) return new Color(0.3f, 0.36f, 0.16f);
                var px = t.GetPixels32();
                long r = 0, g = 0, b = 0; int n = 0;
                for (int i = 0; i < px.Length; i += 37) { r += px[i].r; g += px[i].g; b += px[i].b; n++; }
                return new Color(r / (255f * n), g / (255f * n), b / (255f * n));
            }
            finally { Object.DestroyImmediate(t); }
        }

        static void ConfigureTexture(string path, TextureImporterType type, bool srgb, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return;
            imp.textureType = type;
            if (type == TextureImporterType.Default) imp.sRGBTexture = srgb;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.maxTextureSize = maxSize;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 8;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = Mathf.Min(1024, maxSize);
            android.format = TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
        }

        // ================= models =================

        static void Models(WorldArtSet art, StringBuilder report)
        {
            PlaceholderArt.Build(); // berry material and gameplay numbers come from the placeholders
            string modelsFolder = Root + "/Models";
            string prefabFolder = Root + "/Prefabs";
            PlaceholderArt.EnsureFolder(modelsFolder);
            PlaceholderArt.EnsureFolder(prefabFolder);

            for (int si = 0; si < Slots.Length; si++)
            {
                var slot = Slots[si];
                float progress = 0.2f + 0.8f * si / Slots.Length;
                var prefabs = new List<GameObject>();
                var used = new List<string>();
                var candidates = new List<string>(slot.ids);
                bool searched = false;
                for (int c = 0; c < candidates.Count && used.Count < slot.assets; c++)
                {
                    string id = candidates[c];
                    Step($"{slot.kind}: {id}", progress);
                    try
                    {
                        string fbx = FetchModel(id, modelsFolder);
                        if (fbx != null)
                        {
                            var o = new ModelBaker.Options
                            {
                                name = $"{slot.kind}_{id}", outFolder = $"{prefabFolder}/{slot.kind}",
                                minSize = slot.min, maxSize = slot.max, sizeByLongest = slot.byLongest, layFlat = slot.layFlat,
                                sink = slot.sink, wind = slot.wind, maxVariants = slot.variantsPerAsset, lod0Triangles = slot.lod0,
                                collider = slot.collider, decorate = Decorator(slot.kind),
                            };
                            var baked = ModelBaker.Bake(fbx, o);
                            foreach (var b in baked)
                            {
                                if (!b.prefab) continue;
                                prefabs.Add(b.prefab);
                                report.AppendLine($"  {b.prefab.name}: {b.sourceTriangles} triangles in the scan, {b.triangles} used, {b.size.y:0.0} m tall");
                            }
                            if (baked.Count > 0) { used.Add(id); Credits.Add($"{slot.kind}: {id} https://polyhaven.com/a/{id}"); }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e) { Debug.LogWarning($"[Island] {slot.kind}: '{id}' failed ({e.Message})"); }

                    // Ran out of known names: look through the catalogue for more.
                    if (c == candidates.Count - 1 && used.Count < slot.assets && !searched)
                    {
                        searched = true;
                        foreach (var extra in Search(slot.searchAny, slot.searchNone))
                            if (!candidates.Contains(extra) && candidates.Count < slot.ids.Length + 6) candidates.Add(extra);
                    }
                }

                if (prefabs.Count == 0) { report.AppendLine($"{slot.kind}: nothing usable, keeping the placeholder."); continue; }
                var entry = art.props.Find(p => p.kind == slot.kind);
                if (entry == null) { entry = new PropArt { kind = slot.kind }; art.props.Add(entry); }
                entry.prefabs = prefabs;
                report.AppendLine($"{slot.kind}: {prefabs.Count} prefab(s) from {string.Join(", ", used)}");
                EditorUtility.SetDirty(art);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Downloads the FBX and its textures (cached), imports them, returns the FBX asset path.</summary>
        static string FetchModel(string id, string modelsFolder)
        {
            string folder = $"{modelsFolder}/{id}";
            string fbxPath = $"{folder}/{id}.fbx";
            if (!File.Exists(fbxPath))
            {
                var files = Files(id);
                if (files == null) return null;
                var node = FindNode(files, u => Regex.IsMatch(u, @"_2k\.fbx$")) ?? FindNode(files, u => Regex.IsMatch(u, @"_1k\.fbx$"))
                           ?? FindNode(files, u => u.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
                string url = MiniJson.Str(node, "url");
                if (url == null) return null;
                Directory.CreateDirectory(folder);
                var include = MiniJson.Obj(node, "include");
                if (include != null)
                {
                    foreach (var kv in include)
                    {
                        string rel = kv.Key.Replace('\\', '/');
                        string lower = rel.ToLowerInvariant();
                        if (rel.Contains("..") || rel.StartsWith("/") || rel.Contains(":")) continue;
                        if (lower.EndsWith(".exr") || lower.Contains("_nor_dx_") || lower.Contains("_disp_") || lower.Contains("_rough_") || lower.Contains("_metal_") || lower.Contains("_ao_")) continue;
                        string u = MiniJson.Str(kv.Value, "url");
                        if (u == null) continue;
                        string target = $"{folder}/{rel}";
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        if (!File.Exists(target)) Download(u, target);
                    }
                }
                // The model goes last, so a half-finished download is retried next time.
                if (!Download(url, fbxPath)) return null;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
                string n = Path.GetFileName(p).ToLowerInvariant();
                bool normal = n.Contains("_nor_");
                bool data = normal || n.Contains("_arm_") || n.Contains("_alpha") || n.Contains("_opacity") || n.Contains("_mask");
                var android = ti.GetPlatformTextureSettings("Android");
                bool changed = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                if (!normal && data && ti.sRGBTexture) { ti.sRGBTexture = false; changed = true; }
                if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; changed = true; }
                if (!android.overridden) { android.overridden = true; android.maxTextureSize = 1024; android.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(android); changed = true; }
                if (changed) ti.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(fbxPath) is ModelImporter mi)
            {
                if (!mi.isReadable || mi.importCameras || mi.importLights || mi.animationType != ModelImporterAnimationType.None)
                {
                    mi.isReadable = true;
                    mi.importCameras = false;
                    mi.importLights = false;
                    mi.importAnimation = false;
                    mi.animationType = ModelImporterAnimationType.None;
                    mi.SaveAndReimport();
                }
                return fbxPath;
            }
            return null;
        }

        /// <summary>Adds the gameplay parts food plants need (same numbers as the placeholder plants).</summary>
        static Action<GameObject, ModelBaker.Baked> Decorator(WorldPropKind kind)
        {
            switch (kind)
            {
                case WorldPropKind.BerryBush:
                    return (go, b) =>
                    {
                        var food = AddFood(go, FoodType.Berries, 60f, 15f, Mathf.Min(0.7f, b.size.y * 0.6f), Mathf.Clamp(0.4f * Mathf.Max(b.size.x, b.size.z), 0.5f, 1.2f));
                        food.foodVisual = Berries(go, b);
                    };
                case WorldPropKind.LeafShrub:
                    return (go, b) => AddFood(go, FoodType.Leaves, 80f, 15f, Mathf.Min(1.0f, b.size.y * 0.6f), Mathf.Clamp(0.4f * Mathf.Max(b.size.x, b.size.z), 0.6f, 1.5f));
                case WorldPropKind.Fern:
                    return (go, b) => AddFood(go, FoodType.Leaves, 40f, 12f, Mathf.Min(0.4f, b.size.y * 0.6f), Mathf.Clamp(0.4f * Mathf.Max(b.size.x, b.size.z), 0.4f, 0.9f));
                case WorldPropKind.TallLeafTree:
                    return (go, b) => AddFood(go, FoodType.Leaves, 200f, 20f, Mathf.Min(8f, b.size.y * 0.72f), Mathf.Clamp(b.trunkRadius + 1.5f, 1.5f, 3f));
                default:
                    return null;
            }
        }

        static FoodSource AddFood(GameObject go, FoodType type, float capacity, float regrow, float feedHeight, float radius)
        {
            var food = go.AddComponent<FoodSource>();
            food.foodType = type; food.capacity = capacity; food.regrowPerMinute = regrow; food.feedHeight = feedHeight; food.radius = radius;
            return food;
        }

        /// <summary>Red berries dotted over the bush so players can tell it from a plain shrub. They shrink as it is eaten.</summary>
        static Transform Berries(GameObject go, ModelBaker.Baked b)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderArt.Root + "/M_Berries.mat");
            if (!mat || !b.mesh) return null;
            var verts = b.mesh.vertices;
            var normals = b.mesh.normals;
            var k = new MeshKit(1);
            int placed = 0;
            for (int i = 0; i < 4000 && placed < 34; i++)
            {
                int vi = (int)(IslandNoise.Hash01(i, 11, 5) * (verts.Length - 1));
                var p = verts[vi];
                if (p.y < b.size.y * 0.3f) continue;
                if (normals.Length == verts.Length) p += normals[vi] * 0.02f;
                k.Blob(0, p, Vector3.one * (0.028f + 0.012f * IslandNoise.Hash01(i, 12, 5)), 1, 0f, i);
                placed++;
            }
            if (placed == 0) return null;
            var mesh = k.ToMesh(go.name + "_Berries");
            string path = $"{Root}/Prefabs/BerryBush/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, path);
            var child = new GameObject("Berries");
            child.transform.SetParent(go.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return child.transform;
        }

        // ================= Poly Haven API =================

        static readonly Dictionary<string, object> FilesCache = new Dictionary<string, object>();
        static Dictionary<string, object> _catalogue;

        /// <summary>The file list for one asset, or null when the id doesn't exist or we're offline.</summary>
        static object Files(string id)
        {
            if (FilesCache.TryGetValue(id, out var cached)) return cached;
            object parsed = null;
            var bytes = Get($"{Api}/files/{id}", quiet: true);
            if (bytes != null)
            {
                parsed = MiniJson.Parse(Encoding.UTF8.GetString(bytes));
                if (parsed is Dictionary<string, object> d && d.Count == 0) parsed = null;
            }
            FilesCache[id] = parsed;
            return parsed;
        }

        /// <summary>Model ids from the catalogue whose name, tags or categories contain one of the words, most downloaded first.</summary>
        static List<string> Search(string[] any, string[] none)
        {
            var result = new List<string>();
            if (any == null || any.Length == 0) return result;
            if (_catalogue == null)
            {
                var bytes = Get($"{Api}/assets?t=models", quiet: true);
                _catalogue = (bytes != null ? MiniJson.Parse(Encoding.UTF8.GetString(bytes)) : null) as Dictionary<string, object> ?? new Dictionary<string, object>();
            }
            var scored = new List<(string id, double downloads)>();
            foreach (var kv in _catalogue)
            {
                if (!(kv.Value is Dictionary<string, object> info)) continue;
                var words = new HashSet<string>(kv.Key.ToLowerInvariant().Split('_'));
                AddWords(words, info, "tags");
                AddWords(words, info, "categories");
                if (info.TryGetValue("name", out var nm) && nm is string name)
                    foreach (var w in name.ToLowerInvariant().Split(' ', '_', '-')) words.Add(w);
                bool hit = false, blocked = false;
                foreach (var w in any) if (words.Contains(w)) hit = true;
                if (none != null) foreach (var w in none) if (words.Contains(w)) blocked = true;
                if (!hit || blocked) continue;
                double dl = info.TryGetValue("download_count", out var dc) && dc is double dd ? dd : 0;
                scored.Add((kv.Key, dl));
            }
            scored.Sort((a, b) => b.downloads.CompareTo(a.downloads));
            foreach (var s in scored) result.Add(s.id);
            return result;
        }

        static void AddWords(HashSet<string> words, Dictionary<string, object> info, string key)
        {
            if (!info.TryGetValue(key, out var v) || !(v is List<object> list)) return;
            foreach (var item in list)
                if (item is string s)
                    foreach (var w in s.ToLowerInvariant().Split(' ', '_', '-')) words.Add(w);
        }

        /// <summary>First "url" anywhere in the JSON tree that passes the test.</summary>
        static string FindUrl(object node, Func<string, bool> test) => MiniJson.Str(FindNode(node, test), "url");

        static Dictionary<string, object> FindNode(object node, Func<string, bool> test)
        {
            if (node is Dictionary<string, object> d)
            {
                if (d.TryGetValue("url", out var u) && u is string s && test(s)) return d;
                foreach (var kv in d)
                {
                    if (kv.Key == "include") continue;
                    var found = FindNode(kv.Value, test);
                    if (found != null) return found;
                }
            }
            else if (node is List<object> l)
            {
                foreach (var item in l)
                {
                    var found = FindNode(item, test);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // ================= downloading =================

        static HttpClient _http;

        static byte[] Get(string url, bool quiet = false)
        {
            string firstError = null;
            try
            {
                if (_http == null)
                {
                    _http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                    _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                }
                // Run off the main thread so waiting here can't deadlock the editor.
                return Task.Run(async () =>
                {
                    using (var response = await _http.GetAsync(url).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) throw new IOException("HTTP " + (int)response.StatusCode);
                        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                }).GetAwaiter().GetResult();
            }
            catch (Exception e) { firstError = (e.InnerException ?? e).Message; }

            if (firstError != null && firstError.StartsWith("HTTP 4")) { if (!quiet) Debug.LogWarning($"[Island] {url}: {firstError}"); return null; }
            try
            {
                using (var req = UnityWebRequest.Get(url))
                {
                    req.SetRequestHeader("User-Agent", UserAgent);
                    req.timeout = 900;
                    var op = req.SendWebRequest();
                    while (!op.isDone) System.Threading.Thread.Sleep(20);
                    if (req.result == UnityWebRequest.Result.Success) return req.downloadHandler.data;
                    if (!quiet) Debug.LogWarning($"[Island] Download failed: {url} ({firstError}; {req.error})");
                }
            }
            catch (Exception e) { if (!quiet) Debug.LogWarning($"[Island] Download failed: {url} ({firstError}; {e.Message})"); }
            return null;
        }

        static bool Download(string url, string path)
        {
            var bytes = Get(url);
            if (bytes == null || bytes.Length == 0) return false;
            File.WriteAllBytes(path, bytes);
            return true;
        }

        static void WriteCredits()
        {
            if (Credits.Count == 0) return;
            var sb = new StringBuilder();
            sb.AppendLine("# Art from Poly Haven");
            sb.AppendLine();
            sb.AppendLine("Everything in this folder was downloaded from https://polyhaven.com and is CC0 (public domain):");
            sb.AppendLine("free for any use, no credit required. Listed here as a thank-you and so each asset can be found again.");
            sb.AppendLine();
            foreach (var c in Credits) sb.AppendLine("- " + c);
            File.WriteAllText(Root + "/CREDITS.md", sb.ToString());
        }
    }

    /// <summary>
    /// Reads a Radiance .hdr sky photo and measures where the sun is and what colour the sky and the
    /// horizon are, so the sun light, ambient light and fog can match the picture.
    /// </summary>
    public static class HdrAnalysis
    {
        public struct Result
        {
            public Vector3 sunDirection;
            public Color sky, horizon;
            public float skyLuminance;
        }

        public static Result Analyze(string path)
        {
            var data = File.ReadAllBytes(path);
            int pos = 0;
            int width = 0, height = 0;
            // Header: text lines, a blank line, then the size line "-Y height +X width".
            bool sawBlank = false;
            while (pos < data.Length)
            {
                int start = pos;
                while (pos < data.Length && data[pos] != (byte)'\n') pos++;
                string line = Encoding.ASCII.GetString(data, start, pos - start).Trim();
                pos++;
                if (!sawBlank) { if (line.Length == 0) sawBlank = true; continue; }
                var m = Regex.Match(line, @"-Y\s+(\d+)\s+\+X\s+(\d+)");
                if (!m.Success) throw new IOException("unsupported .hdr orientation");
                height = int.Parse(m.Groups[1].Value);
                width = int.Parse(m.Groups[2].Value);
                break;
            }
            if (width < 8 || height < 4) throw new IOException("not a Radiance .hdr file");

            var scale = new float[256];
            for (int e = 1; e < 256; e++) scale[e] = Mathf.Pow(2f, e - 136);
            var scan = new byte[width * 4];

            double sx = 0, sy = 0, sz = 0, sw = 0;
            double skyR = 0, skyG = 0, skyB = 0, skyW = 0, horR = 0, horG = 0, horB = 0, horW = 0;
            float peak = 0f;
            var rowPeak = new List<(int row, int x, float lum)>();
            int stepX = Mathf.Max(1, width / 1024);

            for (int row = 0; row < height; row++)
            {
                ReadScanline(data, ref pos, scan, width);
                float polar = Mathf.PI * (row + 0.5f) / height;
                float y = Mathf.Cos(polar), ring = Mathf.Sin(polar);
                if (y < -0.05f) continue; // below the horizon
                for (int x = 0; x < width; x += stepX)
                {
                    float f = scale[scan[width * 3 + x]];
                    float r = scan[x] * f, g = scan[width + x] * f, b = scan[width * 2 + x] * f;
                    float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                    if (lum > peak) peak = lum;
                    if (lum > 20f) rowPeak.Add((row, x, lum));
                    float capped = Mathf.Min(1f, 6f / Mathf.Max(lum, 1e-4f)); // keep the sun out of the averages
                    if (y > 0.25f) { skyR += r * capped * ring; skyG += g * capped * ring; skyB += b * capped * ring; skyW += ring; }
                    else if (y >= 0f && y < 0.12f) { horR += r * capped; horG += g * capped; horB += b * capped; horW += 1; }
                }
            }

            // Sun = brightness-weighted middle of the brightest pixels.
            float threshold = peak * 0.5f;
            foreach (var p in rowPeak)
            {
                if (p.lum < threshold) continue;
                var d = Direction(p.x, p.row, width, height);
                sx += d.x * p.lum; sy += d.y * p.lum; sz += d.z * p.lum; sw += p.lum;
            }
            var result = new Result();
            if (sw > 0 && peak > 50f) result.sunDirection = new Vector3((float)(sx / sw), (float)(sy / sw), (float)(sz / sw)).normalized;
            else result.sunDirection = new Vector3(0.4f, 0.72f, -0.56f).normalized; // overcast: no visible sun
            if (result.sunDirection.y < 0.2f) result.sunDirection = new Vector3(result.sunDirection.x, 0.2f, result.sunDirection.z).normalized;

            if (skyW <= 0) throw new IOException("empty sky");
            result.sky = new Color((float)(skyR / skyW), (float)(skyG / skyW), (float)(skyB / skyW));
            result.horizon = horW > 0 ? new Color((float)(horR / horW), (float)(horG / horW), (float)(horB / horW)) : result.sky;
            result.skyLuminance = 0.2126f * result.sky.r + 0.7152f * result.sky.g + 0.0722f * result.sky.b;
            return result;
        }

        /// <summary>World direction of a pixel, matching Unity's Skybox/Panoramic shader at rotation 0.</summary>
        static Vector3 Direction(int x, int row, int width, int height)
        {
            float u = (x + 0.5f) / width;
            float polar = Mathf.PI * (row + 0.5f) / height;
            float lon = (0.5f - u) * 2f * Mathf.PI;
            float ring = Mathf.Sin(polar);
            return new Vector3(Mathf.Cos(lon) * ring, Mathf.Cos(polar), Mathf.Sin(lon) * ring);
        }

        /// <summary>One row into planar R, G, B, E (each 'width' bytes).</summary>
        static void ReadScanline(byte[] data, ref int pos, byte[] scan, int width)
        {
            if (pos + 4 > data.Length) throw new IOException("truncated .hdr file");
            bool rle = width >= 8 && width < 32768 && data[pos] == 2 && data[pos + 1] == 2 && (data[pos + 2] & 0x80) == 0;
            if (!rle)
            {
                for (int x = 0; x < width; x++)
                {
                    scan[x] = data[pos++]; scan[width + x] = data[pos++]; scan[width * 2 + x] = data[pos++]; scan[width * 3 + x] = data[pos++];
                }
                return;
            }
            pos += 4;
            for (int c = 0; c < 4; c++)
            {
                int x = 0, offset = c * width;
                while (x < width)
                {
                    int count = data[pos++];
                    if (count > 128)
                    {
                        count -= 128;
                        byte value = data[pos++];
                        if (x + count > width) throw new IOException("bad .hdr run");
                        for (int i = 0; i < count; i++) scan[offset + x++] = value;
                    }
                    else
                    {
                        if (count == 0 || x + count > width) throw new IOException("bad .hdr run");
                        for (int i = 0; i < count; i++) scan[offset + x++] = data[pos++];
                    }
                }
            }
        }
    }
}
