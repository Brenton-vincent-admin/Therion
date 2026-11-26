using System.Collections.Generic;
using System.IO;
using Dino.Species;
using Dino.World.Generation;
using UnityEditor;
using UnityEngine;

namespace Dino.World.EditorTools
{
    /// <summary>
    /// Generates the fallback art the Island Builder uses for any empty WorldArtSet slot: procedural ground
    /// textures, water normals, materials, and low-poly vegetation prefabs with LODs. Everything lands in
    /// Assets/_Game/World/Island/Generated/Placeholder and is rebuilt on every build.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string Root = IslandBuilder.GeneratedRoot + "/Placeholder";

        public class Result
        {
            public TerrainLayer grass, forestFloor, sand, mud, rock;
            public Material water, ocean, skybox, mist;
            public readonly Dictionary<WorldPropKind, GameObject> prefabs = new Dictionary<WorldPropKind, GameObject>();
            public GameObject grassDetail;
        }

        public static Result Build()
        {
            EnsureFolder(Root);
            var r = new Result();

            // ---- ground ----
            r.grass = Layer("Grass", Ground("Grass", new Color(0.33f, 0.47f, 0.19f), new Color(0.47f, 0.55f, 0.24f), 0.55f, 3), 9f);
            r.forestFloor = Layer("ForestFloor", Ground("ForestFloor", new Color(0.24f, 0.20f, 0.12f), new Color(0.30f, 0.33f, 0.16f), 0.65f, 5), 8f);
            r.sand = Layer("Sand", Ground("Sand", new Color(0.72f, 0.64f, 0.48f), new Color(0.80f, 0.73f, 0.58f), 0.35f, 7), 7f);
            r.mud = Layer("Mud", Ground("Mud", new Color(0.22f, 0.18f, 0.13f), new Color(0.32f, 0.28f, 0.20f), 0.5f, 9), 8f);
            r.rock = Layer("Rock", Ground("Rock", new Color(0.42f, 0.27f, 0.20f), new Color(0.58f, 0.44f, 0.36f), 0.8f, 11), 14f);

            // ---- water and sky ----
            var waterNormal = WaterNormal();
            r.water = Water("Water", new Color(0.16f, 0.33f, 0.35f, 0.72f), waterNormal, 18f);
            r.ocean = Water("Ocean", new Color(0.08f, 0.26f, 0.40f, 0.85f), waterNormal, 40f);
            r.skybox = Skybox();
            r.mist = Mat("WaterfallMist", new Color(1f, 1f, 1f, 0.35f), 0f, transparent: true, particles: true);

            // ---- vegetation and props ----
            var bark = Mat("Bark", new Color(0.30f, 0.22f, 0.15f), 0.1f);
            var leaves = Mat("Leaves", new Color(0.22f, 0.40f, 0.14f), 0.25f);
            var darkLeaves = Mat("DarkLeaves", new Color(0.12f, 0.28f, 0.12f), 0.25f);
            var needles = Mat("Needles", new Color(0.12f, 0.24f, 0.13f), 0.2f);
            var swampLeaves = Mat("SwampLeaves", new Color(0.30f, 0.36f, 0.18f), 0.3f);
            var berries = Mat("Berries", new Color(0.62f, 0.05f, 0.12f), 0.6f);
            var stone = Mat("Stone", new Color(0.45f, 0.40f, 0.36f), 0.15f);
            var redStone = Mat("RedStone", new Color(0.50f, 0.31f, 0.24f), 0.12f);
            var dirt = Mat("Dirt", new Color(0.33f, 0.24f, 0.16f), 0.05f);
            var bugs = Mat("Bugs", new Color(0.06f, 0.05f, 0.04f), 0.7f);
            var fish = Mat("Fish", new Color(0.55f, 0.60f, 0.62f), 0.8f);
            var straw = Mat("NestStraw", new Color(0.62f, 0.50f, 0.30f), 0.05f);
            var fern = Mat("Fern", new Color(0.20f, 0.42f, 0.12f), 0.3f, doubleSided: true);
            var grassCard = GrassMaterial();

            r.prefabs[WorldPropKind.Tree] = TreePrefab("Tree", bark, leaves, 9f, 0.35f, 3.2f, 1);
            r.prefabs[WorldPropKind.Pine] = PinePrefab("Pine", bark, needles);
            r.prefabs[WorldPropKind.SwampTree] = TreePrefab("SwampTree", bark, swampLeaves, 11f, 0.3f, 3.8f, 2, flat: true);
            r.prefabs[WorldPropKind.Rock] = RockPrefab("Rock", stone, redStone);
            r.prefabs[WorldPropKind.Log] = LogPrefab("Log", bark);
            r.prefabs[WorldPropKind.GreatTree] = GreatTreePrefab(bark, darkLeaves);

            r.prefabs[WorldPropKind.BerryBush] = BushPrefab("BerryBush", leaves, berries, FoodType.Berries, 60f, 15f, 0.7f, 0.9f);
            r.prefabs[WorldPropKind.LeafShrub] = BushPrefab("LeafShrub", darkLeaves, leaves, FoodType.Leaves, 80f, 15f, 1.0f, 1.2f);
            r.prefabs[WorldPropKind.Fern] = FernPrefab(fern);
            r.prefabs[WorldPropKind.TallLeafTree] = TallLeafTreePrefab(bark, leaves);
            r.prefabs[WorldPropKind.BugMound] = BugMoundPrefab(dirt, bugs);
            r.prefabs[WorldPropKind.FishSpot] = FishSpotPrefab(fish);
            r.prefabs[WorldPropKind.Nest] = NestPrefab(straw);
            r.grassDetail = GrassPrefab(grassCard);

            AssetDatabase.SaveAssets();
            return r;
        }

        // ================= textures =================

        static Texture2D Ground(string name, Color a, Color b, float contrast, int seed)
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // tileable noise: sample on a torus-like wrap using periodic coordinates
                float u = (float)x / N, v = (float)y / N;
                float n = Periodic(u, v, 8, seed) * 0.55f + Periodic(u, v, 32, seed + 5) * 0.3f + Periodic(u, v, 64, seed + 9) * 0.15f;
                float speck = IslandNoise.Hash01(x, y, seed) > 0.985f ? 0.25f : 0f;
                Color c = Color.Lerp(a, b, Mathf.Clamp01(0.5f + (n - 0.5f) * 2f * contrast));
                c *= 0.9f + speck + (IslandNoise.Hash01(x, y, seed + 1) - 0.5f) * 0.08f;
                c.a = 1f;
                px[y * N + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return SavePng(tex, $"{Root}/T_{name}.png", false);
        }

        /// <summary>Value noise that repeats every 'period' cells so ground textures tile without seams.</summary>
        static float Periodic(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float H(int i, int j) => IslandNoise.Hash01(((i % period) + period) % period, ((j % period) + period) % period, seed);
            float ab = Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), cd = Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(ab, cd, fy);
        }

        static Texture2D WaterNormal()
        {
            const int N = 256;
            var h = new float[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                h[y * N + x] = Periodic(u, v, 8, 71) * 0.6f + Periodic(u, v, 16, 73) * 0.3f + Periodic(u, v, 32, 79) * 0.1f;
            }
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true);
            var px = new Color[N * N];
            const float k = 3.5f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                var n = new Vector3(-dx * k, -dy * k, 1f).normalized;
                px[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            tex.SetPixels(px);
            tex.Apply();
            return SavePng(tex, $"{Root}/T_WaterNormal.png", true);
        }

        static Texture2D GrassBlades()
        {
            const int W = 128, Hh = 128;
            var tex = new Texture2D(W, Hh, TextureFormat.RGBA32, true);
            var px = new Color[W * Hh];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.3f, 0.45f, 0.18f, 0f);
            for (int b = 0; b < 14; b++)
            {
                float baseX = 6 + IslandNoise.Hash01(b, 1, 5) * (W - 12);
                float height = Hh * (0.55f + IslandNoise.Hash01(b, 2, 5) * 0.45f);
                float lean = (IslandNoise.Hash01(b, 3, 5) - 0.5f) * 30f;
                float shade = 0.75f + IslandNoise.Hash01(b, 4, 5) * 0.35f;
                for (int y = 0; y < height; y++)
                {
                    float t = y / height;
                    float half = Mathf.Lerp(2.6f, 0.4f, t);
                    float cx = baseX + lean * t * t;
                    for (int x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
                    {
                        if (x < 0 || x >= W) continue;
                        var c = Color.Lerp(new Color(0.22f, 0.34f, 0.12f), new Color(0.52f, 0.62f, 0.26f), t) * shade;
                        c.a = 1f;
                        px[y * W + x] = c;
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return SavePng(tex, $"{Root}/T_GrassBlades.png", false, alpha: true);
        }

        static Texture2D SavePng(Texture2D tex, string path, bool normalMap, bool alpha = false)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.alphaIsTransparency = alpha;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = 256;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ================= materials =================

        static TerrainLayer Layer(string name, Texture2D diffuse, float tile)
        {
            string path = $"{Root}/TL_{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = diffuse;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.05f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        public static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        static Material Mat(string name, Color color, float smoothness, bool transparent = false, bool doubleSided = false, bool particles = false)
        {
            string path = $"{Root}/M_{name}.mat";
            var shader = particles ? (Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? LitShader()) : LitShader();
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;
            if (transparent) MakeTransparent(m);
            if (doubleSided && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void MakeTransparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        static Material Water(string name, Color color, Texture2D normal, float tile)
        {
            var m = Mat(name, color, 0.92f, transparent: true);
            if (m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.SetTextureScale("_BumpMap", new Vector2(1f / tile, 1f / tile) * 10f);
                m.SetFloat("_BumpScale", 0.6f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GrassMaterial()
        {
            var tex = GrassBlades();
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? LitShader();
            string path = $"{Root}/M_GrassDetail.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            SetColor(m, Color.white);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.45f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Skybox()
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (!shader) return null;
            string path = $"{Root}/M_Sky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_SunDisk", 2f);
            m.SetFloat("_SunSize", 0.035f);
            m.SetFloat("_SunSizeConvergence", 6f);
            m.SetFloat("_AtmosphereThickness", 0.9f);
            m.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.75f));
            m.SetColor("_GroundColor", new Color(0.38f, 0.40f, 0.42f));
            m.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================= prefabs =================

        static GameObject TreePrefab(string name, Material bark, Material leaves, float height, float trunkR, float canopyR, int seed, bool flat = false)
        {
            Mesh Lod(int detail, int sides, int blobs)
            {
                var k = new MeshKit(2);
                k.Cylinder(0, Vector3.zero, Vector3.up * height * 0.75f, trunkR, trunkR * 0.55f, sides);
                for (int b = 0; b < blobs; b++)
                {
                    float a = b * 2.4f + seed;
                    Vector3 off = b == 0 ? Vector3.zero : new Vector3(Mathf.Cos(a), -0.25f + 0.2f * (b % 2), Mathf.Sin(a)) * canopyR * 0.55f;
                    Vector3 rad = new Vector3(canopyR, canopyR * (flat ? 0.45f : 0.8f), canopyR) * (b == 0 ? 1f : 0.7f);
                    k.Blob(1, Vector3.up * (height * 0.78f) + off, rad, detail, 0.35f, seed * 13 + b);
                }
                return k.ToMesh($"{name}_LOD{(detail >= 2 ? 0 : 1)}");
            }
            return LodPrefab(name, new[] { Lod(2, 7, 4), Lod(1, 5, 1) }, new[] { bark, leaves },
                new Capsule { radius = trunkR * 1.2f, height = height * 0.75f }, 0.012f);
        }

        static GameObject PinePrefab(string name, Material bark, Material needles)
        {
            Mesh Lod(int sides, int tiers)
            {
                var k = new MeshKit(2);
                k.Cylinder(0, Vector3.zero, Vector3.up * 11f, 0.3f, 0.12f, sides);
                for (int t = 0; t < tiers; t++)
                {
                    float y = 2.5f + t * (8f / tiers);
                    float r = 2.6f * (1f - (float)t / (tiers + 0.6f));
                    k.Cone(1, Vector3.up * y, Vector3.up * (y + 4.2f - t * 0.4f), r, sides + 2, 0.25f, t);
                }
                return k.ToMesh($"{name}_LOD{(tiers > 2 ? 0 : 1)}");
            }
            return LodPrefab(name, new[] { Lod(7, 4), Lod(5, 2) }, new[] { bark, needles },
                new Capsule { radius = 0.4f, height = 8f }, 0.012f);
        }

        static GameObject RockPrefab(string name, Material stone, Material redStone)
        {
            Mesh Lod(int detail)
            {
                var k = new MeshKit(1);
                k.Blob(0, Vector3.up * 0.4f, new Vector3(1.6f, 1.0f, 1.3f), detail, 0.45f, 77);
                return k.ToMesh($"{name}_LOD{(detail >= 2 ? 0 : 1)}");
            }
            return LodPrefab(name, new[] { Lod(2), Lod(1) }, new[] { stone },
                new Capsule { radius = 1.1f, height = 2.2f, horizontal = true }, 0.01f);
        }

        static GameObject LogPrefab(string name, Material bark)
        {
            Mesh Lod(int sides)
            {
                var k = new MeshKit(1);
                k.Cylinder(0, new Vector3(-2.5f, 0.35f, 0), new Vector3(2.5f, 0.32f, 0.2f), 0.42f, 0.36f, sides, caps: true);
                return k.ToMesh($"{name}_LOD{(sides > 5 ? 0 : 1)}");
            }
            return LodPrefab(name, new[] { Lod(8), Lod(5) }, new[] { bark },
                new Capsule { radius = 0.4f, height = 5f, horizontal = true, center = new Vector3(0, 0.35f, 0) }, 0.01f);
        }

        static GameObject GreatTreePrefab(Material bark, Material leaves)
        {
            var k = new MeshKit(2);
            k.Cylinder(0, Vector3.zero, Vector3.up * 20f, 2.2f, 1.1f, 10);
            for (int b = 0; b < 4; b++) // roots
            {
                float a = b * Mathf.PI * 0.5f + 0.4f;
                k.Cylinder(0, Vector3.up * 2.5f, new Vector3(Mathf.Cos(a) * 4.5f, -0.3f, Mathf.Sin(a) * 4.5f), 1.0f, 0.3f, 6);
            }
            for (int b = 0; b < 6; b++) // boughs
            {
                float a = b * 1.05f;
                Vector3 end = new Vector3(Mathf.Cos(a) * 9f, 24f + (b % 2) * 3f, Mathf.Sin(a) * 9f);
                k.Cylinder(0, Vector3.up * 16f, end, 0.8f, 0.35f, 6);
                k.Blob(1, end + Vector3.up * 1.5f, new Vector3(7f, 4.5f, 7f), 2, 0.35f, 300 + b);
            }
            k.Blob(1, Vector3.up * 30f, new Vector3(10f, 6f, 10f), 2, 0.3f, 399);
            var mesh = SaveMesh(k.ToMesh("GreatTree"), "GreatTree");

            var go = new GameObject("GreatTree");
            AddRenderer(go, mesh, new[] { bark, leaves });
            go.AddComponent<CapsuleCollider>().height = 20f;
            go.GetComponent<CapsuleCollider>().radius = 2.4f;
            go.GetComponent<CapsuleCollider>().center = Vector3.up * 10f;
            var food = go.AddComponent<FoodSource>();
            food.foodType = FoodType.Leaves; food.capacity = 400f; food.regrowPerMinute = 30f; food.feedHeight = 9f; food.radius = 6f;
            return SavePrefab(go, "GreatTree");
        }

        static GameObject BushPrefab(string name, Material body, Material foodMat, FoodType type, float capacity, float regrow, float feedHeight, float radius)
        {
            var k = new MeshKit(1);
            for (int b = 0; b < 3; b++)
            {
                float a = b * 2.1f;
                k.Blob(0, new Vector3(Mathf.Cos(a) * 0.35f, 0.55f, Mathf.Sin(a) * 0.35f) * radius, new Vector3(0.75f, 0.6f, 0.75f) * radius, 2, 0.3f, b + name.Length);
            }
            var bodyMesh = SaveMesh(k.ToMesh(name), name);

            var f = new MeshKit(1);
            if (type == FoodType.Berries)
            {
                for (int b = 0; b < 22; b++)
                {
                    float a = b * 2.39996f, y = 0.35f + IslandNoise.Hash01(b, 7, 1) * 0.65f;
                    float rr = Mathf.Lerp(0.95f, 0.6f, Mathf.Abs(y - 0.6f) / 0.6f) * radius;
                    f.Blob(0, new Vector3(Mathf.Cos(a) * rr, y * radius * 1.1f, Mathf.Sin(a) * rr), Vector3.one * 0.07f, 0, 0f, b);
                }
            }
            else
            {
                for (int b = 0; b < 5; b++)
                {
                    float a = b * 1.26f;
                    f.Blob(0, new Vector3(Mathf.Cos(a) * 0.45f, 0.95f, Mathf.Sin(a) * 0.45f) * radius, new Vector3(0.4f, 0.3f, 0.4f) * radius, 1, 0.35f, 50 + b);
                }
            }
            var foodMesh = SaveMesh(f.ToMesh(name + "_Food"), name + "_Food");

            var go = new GameObject(name);
            AddRenderer(go, bodyMesh, new[] { body });
            var vis = new GameObject("Food");
            vis.transform.SetParent(go.transform, false);
            AddRenderer(vis, foodMesh, new[] { foodMat });
            var food = go.AddComponent<FoodSource>();
            food.foodType = type; food.capacity = capacity; food.regrowPerMinute = regrow; food.feedHeight = feedHeight; food.radius = radius;
            food.foodVisual = vis.transform;
            return SavePrefab(go, name);
        }

        static GameObject FernPrefab(Material fern)
        {
            var k = new MeshKit(1);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.698f + IslandNoise.Hash01(i, 3, 9) * 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0.9f, Mathf.Sin(a)).normalized;
                k.Frond(0, Vector3.up * 0.05f, dir, 1.1f, 0.32f, 0.55f, 5);
            }
            var mesh = SaveMesh(k.ToMesh("Fern"), "Fern");
            var go = new GameObject("Fern");
            var vis = new GameObject("Food");
            vis.transform.SetParent(go.transform, false);
            AddRenderer(vis, mesh, new[] { fern });
            var food = go.AddComponent<FoodSource>();
            food.foodType = FoodType.Leaves; food.capacity = 40f; food.regrowPerMinute = 12f; food.feedHeight = 0.4f; food.radius = 0.7f;
            food.foodVisual = vis.transform;
            return SavePrefab(go, "Fern");
        }

        static GameObject TallLeafTreePrefab(Material bark, Material leaves)
        {
            var trunk = new MeshKit(1);
            trunk.Cylinder(0, Vector3.zero, Vector3.up * 9f, 0.45f, 0.3f, 7);
            var trunkMesh = SaveMesh(trunk.ToMesh("TallLeafTree"), "TallLeafTree");
            var crown = new MeshKit(1);
            for (int b = 0; b < 4; b++)
            {
                float a = b * 1.57f;
                crown.Blob(0, new Vector3(Mathf.Cos(a) * 1.4f, 9.5f + (b % 2) * 0.8f, Mathf.Sin(a) * 1.4f), new Vector3(2.4f, 1.6f, 2.4f), 2, 0.35f, 90 + b);
            }
            var crownMesh = SaveMesh(crown.ToMesh("TallLeafTree_Crown"), "TallLeafTree_Crown");
            var go = new GameObject("TallLeafTree");
            AddRenderer(go, trunkMesh, new[] { bark });
            var vis = new GameObject("Food");
            vis.transform.SetParent(go.transform, false);
            AddRenderer(vis, crownMesh, new[] { leaves });
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.5f; col.height = 9f; col.center = Vector3.up * 4.5f;
            var food = go.AddComponent<FoodSource>();
            food.foodType = FoodType.Leaves; food.capacity = 200f; food.regrowPerMinute = 20f; food.feedHeight = 8f; food.radius = 2.5f;
            food.foodVisual = vis.transform;
            return SavePrefab(go, "TallLeafTree");
        }

        static GameObject BugMoundPrefab(Material dirt, Material bugMat)
        {
            var k = new MeshKit(1);
            k.Blob(0, Vector3.zero, new Vector3(0.9f, 0.45f, 0.9f), 2, 0.3f, 61);
            var mound = SaveMesh(k.ToMesh("BugMound"), "BugMound");
            var b = new MeshKit(1);
            for (int i = 0; i < 26; i++)
            {
                float a = i * 2.39996f, rr = 0.25f + IslandNoise.Hash01(i, 9, 2) * 0.55f;
                float y = Mathf.Sqrt(Mathf.Max(0f, 1f - rr * rr / 0.81f)) * 0.45f + 0.02f;
                b.Blob(0, new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr), new Vector3(0.05f, 0.03f, 0.08f), 0, 0f, i);
            }
            var bugMesh = SaveMesh(b.ToMesh("BugMound_Bugs"), "BugMound_Bugs");
            var go = new GameObject("BugMound");
            AddRenderer(go, mound, new[] { dirt });
            var vis = new GameObject("Food");
            vis.transform.SetParent(go.transform, false);
            AddRenderer(vis, bugMesh, new[] { bugMat });
            var food = go.AddComponent<FoodSource>();
            food.foodType = FoodType.Bugs; food.capacity = 40f; food.regrowPerMinute = 10f; food.feedHeight = 0.2f; food.radius = 0.8f;
            food.foodVisual = vis.transform;
            return SavePrefab(go, "BugMound");
        }

        static GameObject FishSpotPrefab(Material fishMat)
        {
            var k = new MeshKit(1);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f;
                var c = new Vector3(Mathf.Cos(a) * 0.9f, -0.35f - 0.1f * (i % 2), Mathf.Sin(a) * 0.9f);
                k.Blob(0, c, new Vector3(0.12f, 0.08f, 0.38f), 1, 0f, i);
            }
            var mesh = SaveMesh(k.ToMesh("FishSpot"), "FishSpot");
            var go = new GameObject("FishSpot");
            var vis = new GameObject("Food");
            vis.transform.SetParent(go.transform, false);
            AddRenderer(vis, mesh, new[] { fishMat });
            var food = go.AddComponent<FoodSource>();
            food.foodType = FoodType.Fish; food.capacity = 60f; food.regrowPerMinute = 12f; food.feedHeight = 0f; food.radius = 1.5f;
            food.foodVisual = vis.transform;
            return SavePrefab(go, "FishSpot");
        }

        static GameObject NestPrefab(Material straw)
        {
            var k = new MeshKit(1);
            for (int i = 0; i < 28; i++)
            {
                float a0 = i * 0.2244f * 2f, a1 = a0 + 0.9f;
                float r0 = 2.2f + IslandNoise.Hash01(i, 1, 4) * 0.4f, r1 = 2.2f + IslandNoise.Hash01(i, 2, 4) * 0.4f;
                float y = 0.15f + (i % 3) * 0.13f;
                k.Cylinder(0, new Vector3(Mathf.Cos(a0) * r0, y, Mathf.Sin(a0) * r0), new Vector3(Mathf.Cos(a1) * r1, y + 0.05f, Mathf.Sin(a1) * r1), 0.09f, 0.07f, 5);
            }
            k.Blob(0, Vector3.up * 0.02f, new Vector3(2.1f, 0.12f, 2.1f), 2, 0.2f, 5);
            var mesh = SaveMesh(k.ToMesh("Nest"), "Nest");
            var go = new GameObject("Nest");
            AddRenderer(go, mesh, new[] { straw });
            go.AddComponent<Nest>().radius = 2.5f;
            go.AddComponent<PlayerSpawnPoint>().radius = 2f;
            return SavePrefab(go, "Nest");
        }

        static GameObject GrassPrefab(Material mat)
        {
            var k = new MeshKit(1);
            for (int i = 0; i < 3; i++) k.Card(0, Vector3.zero, i * 60f, 0.9f, 0.55f);
            var mesh = SaveMesh(k.ToMesh("GrassTuft"), "GrassTuft");
            var go = new GameObject("GrassTuft");
            AddRenderer(go, mesh, new[] { mat });
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return SavePrefab(go, "GrassTuft");
        }

        // ================= helpers =================

        /// <summary>Prefab with a LODGroup at the root and one child per LOD (what terrain trees need).</summary>
        struct Capsule
        {
            public float radius, height;
            public bool horizontal; // lying along X instead of standing up
            public Vector3 center;
        }

        static GameObject LodPrefab(string name, Mesh[] lods, Material[] mats, Capsule? colliderTemplate, float cullHeight)
        {
            var go = new GameObject(name);
            var group = go.AddComponent<LODGroup>();
            var entries = new LOD[lods.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                var mesh = SaveMesh(lods[i], lods[i].name);
                var child = new GameObject($"{name}_LOD{i}");
                child.transform.SetParent(go.transform, false);
                var mr = AddRenderer(child, mesh, mats);
                if (i > 0) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float h = i == lods.Length - 1 ? cullHeight : Mathf.Lerp(0.6f, cullHeight, (float)(i + 1) / lods.Length) * 0.25f;
                entries[i] = new LOD(i == 0 ? 0.08f : h, new Renderer[] { mr });
            }
            group.SetLODs(entries);
            group.RecalculateBounds();
            if (colliderTemplate.HasValue)
            {
                var t = colliderTemplate.Value;
                var c = go.AddComponent<CapsuleCollider>();
                c.radius = t.radius;
                c.height = t.height;
                c.direction = t.horizontal ? 0 : 1;
                c.center = t.center == Vector3.zero && !t.horizontal ? Vector3.up * t.height * 0.5f : t.center;
            }
            return SavePrefab(go, name);
        }

        static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Material[] mats)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var list = new Material[mesh.subMeshCount];
            for (int i = 0; i < list.Length; i++) list[i] = mats[Mathf.Min(i, mats.Length - 1)];
            mr.sharedMaterials = list;
            return mr;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{Root}/Mesh_{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{Root}/P_{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
