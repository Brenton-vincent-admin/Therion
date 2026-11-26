using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Dino.World.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Dino.World.EditorTools
{
    /// <summary>
    /// One-click island: generates terrain, biomes, water, food, nests and lighting from the IslandSpec
    /// and saves it as Assets/_Game/World/Island/Island.unity. Safe to run again at any time; everything
    /// it makes lives in Generated/ and is replaced on each build. Menu: Dino > World > Build Island.
    /// </summary>
    public static class IslandBuilder
    {
        public const string WorldRoot = "Assets/_Game/World";
        public const string IslandRoot = WorldRoot + "/Island";
        public const string GeneratedRoot = IslandRoot + "/Generated";
        public const string ScenePath = IslandRoot + "/Island.unity";
        public const string SpecPath = IslandRoot + "/IslandSpec.asset";
        public const string ArtSetPath = WorldRoot + "/Art/WorldArtSet.asset";

        const int AlphamapResolution = 512;
        const int DetailResolution = 1024;
        const int BiomeMapResolution = 256;
        const float WaterMeshTile = 256f;

        [MenuItem("Dino/World/Build Island", priority = 0)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(LoadOrCreateSpec(), LoadOrCreateArtSet());
        }

        [MenuItem("Dino/World/Select Island Spec", priority = 20)]
        public static void SelectSpec() => Selection.activeObject = LoadOrCreateSpec();

        [MenuItem("Dino/World/Select World Art Set", priority = 21)]
        public static void SelectArt() => Selection.activeObject = LoadOrCreateArtSet();

        public static IslandSpec LoadOrCreateSpec()
        {
            var spec = AssetDatabase.LoadAssetAtPath<IslandSpec>(SpecPath);
            if (spec) return spec;
            PlaceholderArt.EnsureFolder(IslandRoot);
            spec = ScriptableObject.CreateInstance<IslandSpec>();
            spec.ResetToDefaults();
            AssetDatabase.CreateAsset(spec, SpecPath);
            AssetDatabase.SaveAssets();
            return spec;
        }

        public static WorldArtSet LoadOrCreateArtSet()
        {
            var art = AssetDatabase.LoadAssetAtPath<WorldArtSet>(ArtSetPath);
            if (art) return art;
            PlaceholderArt.EnsureFolder(Path.GetDirectoryName(ArtSetPath).Replace('\\', '/'));
            art = ScriptableObject.CreateInstance<WorldArtSet>();
            foreach (WorldPropKind k in System.Enum.GetValues(typeof(WorldPropKind)))
                if (k != WorldPropKind.Puddle) art.props.Add(new PropArt { kind = k });
            AssetDatabase.CreateAsset(art, ArtSetPath);
            AssetDatabase.SaveAssets();
            return art;
        }

        public static void Build(IslandSpec spec, WorldArtSet art)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                Progress("Graphics settings", 0.02f);
                GraphicsTiers.SetUp();
                Progress("Generating terrain shape", 0.05f);
                var data = IslandGenerator.Generate(spec);
                Progress("Placing nests, water and food", 0.25f);
                IslandPlacer.Place(spec, data);

                Progress("Making art", 0.35f);
                PlaceholderArt.EnsureFolder(GeneratedRoot);
                var ph = PlaceholderArt.Build();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Island");
                var world = root.AddComponent<IslandWorld>();

                Progress("Building terrain", 0.45f);
                var terrain = BuildTerrain(spec, data, art, ph, root.transform);
                world.terrain = terrain;

                Progress("Baking biome map", 0.6f);
                world.biomeMap = BuildBiomeMap(data);

                Progress("Building water", 0.7f);
                BuildWater(spec, data, art, ph, root.transform);

                Progress("Placing food, nests and landmarks", 0.8f);
                var foodCuller = BuildProps(spec, data, art, ph, root.transform);
                BuildLandmarks(spec, data, root.transform);

                Progress("Lighting and atmosphere", 0.9f);
                BuildAtmosphere(art, ph, root.transform);
                BuildPreviewCamera(spec, data);

                var tier = root.AddComponent<WorldQualityTier>();
                tier.terrain = terrain;
                tier.cullers = new[] { foodCuller };
                tier.Apply();

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuild(ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Island] Built {ScenePath} in {clock.Elapsed.TotalSeconds:0.0}s: " +
                          $"{Count(data, WorldPropKind.Nest)} nests, {Count(data, WorldPropKind.Puddle)} puddles, " +
                          $"{CountFood(data)} food sources, {CountDecor(data)} trees/rocks. " +
                          $"Farthest walk to water {data.WorstWaterDistance:0} m, to a nest {data.WorstNestDistance:0} m.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void Progress(string what, float t) => EditorUtility.DisplayProgressBar("Building island", what, t);

        // ================= terrain =================

        static Terrain BuildTerrain(IslandSpec spec, IslandData data, WorldArtSet art, PlaceholderArt.Result ph, Transform parent)
        {
            int res = data.Resolution;
            string tdPath = GeneratedRoot + "/IslandTerrain.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
            if (!td) { td = new TerrainData(); AssetDatabase.CreateAsset(td, tdPath); }

            td.heightmapResolution = res;
            td.size = new Vector3(spec.size, spec.terrainHeight, spec.size);
            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Clamp01((data.Heights[data.Index(x, z)] - spec.terrainBottom) / spec.terrainHeight);
            td.SetHeights(0, 0, heights);

            // Ground layers: 4 layers render in a single pass on phones; forest floor is a 5th only if provided.
            var layers = new List<TerrainLayer>
            {
                art.grass ? art.grass : ph.grass,
                art.sand ? art.sand : ph.sand,
                art.mud ? art.mud : ph.mud,
                art.rock ? art.rock : ph.rock,
            };
            bool forestLayer = art.forestFloor != null;
            if (forestLayer) layers.Add(art.forestFloor);
            td.terrainLayers = layers.ToArray();
            td.alphamapResolution = AlphamapResolution;
            td.baseMapResolution = 512;
            td.SetAlphamaps(0, 0, Splat(spec, data, layers.Count, forestLayer));

            BuildTrees(spec, data, art, ph, td);
            BuildGrass(spec, data, art, ph, td);
            EditorUtility.SetDirty(td);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, spec.terrainBottom, 0f);
            go.isStatic = true;
            var t = go.GetComponent<Terrain>();
            bool heightBlend = layers.Count == 4 && layers.TrueForAll(l => l && l.maskMapTexture);
            t.materialTemplate = TerrainMaterial(heightBlend);
            t.drawInstanced = true;
            t.heightmapPixelError = 6f;          // higher = fewer triangles
            t.basemapDistance = 180f;
            t.treeDistance = 320f;
            t.treeBillboardDistance = 320f;      // placeholder trees use LODs, not billboards
            t.treeMaximumFullLODCount = 60;
            t.detailObjectDistance = 60f;   // WorldQualityTier adjusts these per graphics level
            t.detailObjectDensity = 0.6f;
            t.shadowCastingMode = ShadowCastingMode.TwoSided;
            t.allowAutoConnect = false;
            go.GetComponent<TerrainCollider>().terrainData = td;
            return t;
        }

        /// <summary>Height blend: where two ground layers meet, the higher bumps (stones, tufts) win, so edges look natural.</summary>
        static Material TerrainMaterial(bool heightBlend)
        {
            string path = GeneratedRoot + "/M_Terrain.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (!shader) return null;
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_EnableHeightBlend")) m.SetFloat("_EnableHeightBlend", heightBlend ? 1f : 0f);
            if (m.HasProperty("_HeightTransition")) m.SetFloat("_HeightTransition", 0.08f);
            if (heightBlend) m.EnableKeyword("_TERRAIN_BLEND_HEIGHT"); else m.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Layer weights: 0 grass, 1 sand, 2 mud, 3 rock, 4 forest floor (optional).</summary>
        static float[,,] Splat(IslandSpec spec, IslandData data, int layerCount, bool forestLayer)
        {
            int n = AlphamapResolution;
            var map = new float[n, n, layerCount];

            // distance to drinkable water on a coarse grid, for wet banks and beaches
            int step = Mathf.Max(1, spec.placementStep);
            int w = (data.Resolution - 1) / step + 1;
            var src = new bool[w * w];
            for (int z = 0; z < w; z++)
            for (int x = 0; x < w; x++)
                src[z * w + x] = data.IsWater(data.Index(x * step, z * step));
            var nearWater = DistanceField.Compute(src, w, w, data.Cell * step);

            var weights = new float[layerCount];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (z + 0.5f) / n;
                int ix = Mathf.Clamp(Mathf.RoundToInt(u * (data.Resolution - 1)), 0, data.Resolution - 1);
                int iz = Mathf.Clamp(Mathf.RoundToInt(v * (data.Resolution - 1)), 0, data.Resolution - 1);
                int i = data.Index(ix, iz);
                float h = data.Heights[i];
                float slope = data.SlopeAt(ix, iz);
                byte biome = data.Biomes[i];
                float wd = nearWater[Mathf.Min(iz / step, w - 1) * w + Mathf.Min(ix / step, w - 1)];
                float patch = IslandNoise.Fbm(u * 60f, v * 60f, spec.seed + 501, 3);

                float rock = IslandNoise.SmoothStep(28f, 40f, slope);
                if (biome == BiomeIds.HighlandsCode) rock += 0.55f + 0.4f * patch;
                if (h > 95f) rock = 1f;
                float sand = 0f;
                if (biome != BiomeIds.SwampCode) sand += 1f - IslandNoise.SmoothStep(1.2f, 3.2f, h);
                if (biome == BiomeIds.RiverbanksCode || biome == BiomeIds.LakeCode) sand += 0.8f * (1f - IslandNoise.SmoothStep(3f, 12f, wd));
                float mud = biome == BiomeIds.SwampCode ? 0.75f + 0.3f * patch : 0f;
                mud += 0.45f * (1f - IslandNoise.SmoothStep(0f, 4f, wd));
                float forest = 0f;
                if (biome == BiomeIds.ForestCode)
                {
                    if (forestLayer) forest = 0.75f + 0.3f * patch;
                    else mud += 0.3f + 0.2f * patch;
                }
                // Big soft patches of leaf litter break up the repeating grass photo seen from far away.
                if (rock < 0.5f && biome != BiomeIds.SwampCode)
                    mud += 0.7f * IslandNoise.SmoothStep(0.50f, 0.62f, IslandNoise.Fbm(u * 11f, v * 11f, spec.seed + 903, 3));
                float grass = Mathf.Max(0.05f, 1.1f - rock - sand - mud - forest + (patch - 0.5f) * 0.3f);

                weights[0] = grass; weights[1] = sand; weights[2] = mud; weights[3] = rock;
                if (layerCount > 4) weights[4] = forest;
                float sum = 0f;
                for (int k = 0; k < layerCount; k++) { weights[k] = Mathf.Max(0f, weights[k]); sum += weights[k]; }
                for (int k = 0; k < layerCount; k++) map[z, x, k] = weights[k] / sum;
            }
            return map;
        }

        static readonly WorldPropKind[] TreeKinds = { WorldPropKind.Tree, WorldPropKind.Pine, WorldPropKind.SwampTree, WorldPropKind.Rock, WorldPropKind.Log };

        /// <summary>Decor goes in as terrain trees: GPU-instanced, LOD'd and with capsule colliders, far cheaper than GameObjects.</summary>
        static void BuildTrees(IslandSpec spec, IslandData data, WorldArtSet art, PlaceholderArt.Result ph, TerrainData td)
        {
            var protos = new List<TreePrototype>();
            var protoIndex = new Dictionary<(WorldPropKind, int), int>();
            foreach (var kind in TreeKinds)
            {
                int variants = 1;
                var entry = art.props.Find(p => p.kind == kind);
                if (entry != null && art.HasPrefabs(kind)) variants = entry.prefabs.FindAll(p => p != null).Count;
                for (int v = 0; v < variants; v++)
                {
                    var prefab = art.PickPrefab(kind, v);
                    if (!prefab) prefab = ph.prefabs[kind];
                    protoIndex[(kind, v)] = protos.Count;
                    protos.Add(new TreePrototype { prefab = prefab, bendFactor = 0f });
                }
            }
            td.treePrototypes = protos.ToArray();

            var instances = new List<TreeInstance>();
            int counter = 0;
            foreach (var p in data.Props)
            {
                if (System.Array.IndexOf(TreeKinds, p.kind) < 0) continue;
                int variants = 0;
                while (protoIndex.ContainsKey((p.kind, variants))) variants++;
                int proto = protoIndex[(p.kind, counter++ % Mathf.Max(1, variants))];
                float s = p.kind == WorldPropKind.Rock ? p.scale * 1.3f : p.scale;
                instances.Add(new TreeInstance
                {
                    prototypeIndex = proto,
                    position = new Vector3(p.position.x / spec.size, (p.position.y - spec.terrainBottom) / spec.terrainHeight, p.position.z / spec.size),
                    widthScale = s,
                    heightScale = s * (0.9f + 0.2f * IslandNoise.Hash01(counter, 3, 1)),
                    rotation = p.yaw * Mathf.Deg2Rad,
                    color = Color.Lerp(Color.white, new Color(0.85f, 0.9f, 0.8f), IslandNoise.Hash01(counter, 5, 1)),
                    lightmapColor = Color.white,
                });
            }
            td.SetTreeInstances(instances.ToArray(), true);
        }

        static void BuildGrass(IslandSpec spec, IslandData data, WorldArtSet art, PlaceholderArt.Result ph, TerrainData td)
        {
            // Real blades (short lawn + tall tufts) when our grass shader is there; otherwise the old card tuft.
            var real = art.grassDetail ? null : RealisticArt.BuildGrass(art);
            var protos = new List<DetailPrototype>();
            protos.Add(GrassProto(real != null ? real.shortTuft : (art.grassDetail ? art.grassDetail : ph.grassDetail)));
            if (real != null) protos.Add(GrassProto(real.tallTuft));
            td.detailPrototypes = protos.ToArray();
            td.SetDetailResolution(DetailResolution, 32);
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            int n = DetailResolution;
            var shortLayer = new int[n, n];
            var tallLayer = new int[n, n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (z + 0.5f) / n;
                int ix = Mathf.Clamp(Mathf.RoundToInt(u * (data.Resolution - 1)), 0, data.Resolution - 1);
                int iz = Mathf.Clamp(Mathf.RoundToInt(v * (data.Resolution - 1)), 0, data.Resolution - 1);
                int i = data.Index(ix, iz);
                if (data.IsWater(i) || data.Heights[i] < 1.2f || data.Heights[i] > 80f || data.SlopeAt(ix, iz) > 32f) continue;
                float lawn, tall;
                switch (data.Biomes[i])
                {
                    case BiomeIds.MeadowCode: lawn = 16f; tall = 1.6f; break;
                    case BiomeIds.LakeCode: lawn = 14f; tall = 1.6f; break;
                    case BiomeIds.RiverbanksCode: lawn = 12f; tall = 2.6f; break;
                    case BiomeIds.SwampCode: lawn = 8f; tall = 3.5f; break;
                    case BiomeIds.ForestCode: lawn = 8f; tall = 1f; break;
                    case BiomeIds.HighlandsCode: lawn = 2f; tall = 0.4f; break;
                    default: lawn = 0f; tall = 0f; break;
                }
                float patch = IslandNoise.Fbm(u * 40f, v * 40f, spec.seed + 777, 3);
                float clump = IslandNoise.Fbm(u * 90f, v * 90f, spec.seed + 778, 2);
                float thin = 1f - IslandNoise.SmoothStep(18f, 32f, data.SlopeAt(ix, iz));
                shortLayer[z, x] = Mathf.Min(16, Mathf.RoundToInt(lawn * thin * Mathf.Clamp(patch * 1.4f + 0.3f, 0.55f, 1f)));
                tallLayer[z, x] = Mathf.Min(8, Mathf.RoundToInt(tall * thin * Mathf.Clamp01(clump * 3.2f - 1.2f) * 2f));
            }
            td.SetDetailLayer(0, 0, 0, shortLayer);
            if (real != null) td.SetDetailLayer(0, 0, 1, tallLayer);
        }

        static DetailPrototype GrassProto(GameObject prefab)
        {
            return new DetailPrototype
            {
                positionJitter = 1f, // scattered, not in rows
                alignToGround = 0.6f,
                prototype = prefab,
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = 0.8f, maxWidth = 1.35f,
                minHeight = 0.7f, maxHeight = 1.35f,
                noiseSpread = 0.3f,
                healthyColor = Color.white,
                dryColor = new Color(0.85f, 0.8f, 0.6f),
            };
        }

        // ================= biome map =================

        static BiomeMap BuildBiomeMap(IslandData data)
        {
            string path = GeneratedRoot + "/BiomeMap.asset";
            var map = AssetDatabase.LoadAssetAtPath<BiomeMap>(path);
            if (!map) { map = ScriptableObject.CreateInstance<BiomeMap>(); AssetDatabase.CreateAsset(map, path); }
            int n = BiomeMapResolution;
            map.origin = Vector2.zero;
            map.worldSize = data.Size;
            map.resolution = n;
            map.biomes = BiomeMap.DefaultBiomes();
            map.codes = new byte[n * n];
            var px = new Color32[n * n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float wx = (x + 0.5f) / n * data.Size, wz = (z + 0.5f) / n * data.Size;
                byte code = data.BiomeAt(wx, wz);
                map.codes[z * n + x] = code;
                Color c = map.Get(BiomeIds.FromCode(code)).mapColor;
                int ix = Mathf.Clamp(Mathf.RoundToInt(wx / data.Cell), 0, data.Resolution - 1);
                int iz = Mathf.Clamp(Mathf.RoundToInt(wz / data.Cell), 0, data.Resolution - 1);
                if (data.IsWater(data.Index(ix, iz))) c = Color.Lerp(c, new Color(0.24f, 0.55f, 0.85f), 0.7f);
                px[z * n + x] = c;
            }
            // Pause-screen map picture
            string pngPath = GeneratedRoot + "/IslandMap.png";
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            map.mapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            EditorUtility.SetDirty(map);
            return map;
        }

        // ================= water =================

        static void BuildWater(IslandSpec spec, IslandData data, WorldArtSet art, PlaceholderArt.Result ph, Transform parent)
        {
            var waterRoot = new GameObject("Water").transform;
            waterRoot.SetParent(parent, false);
            var realWater = art.water ? null : RealisticArt.Water(false);
            var realOcean = art.ocean ? null : RealisticArt.Water(true);
            var waterMat = art.water ? art.water : realWater ? realWater : ph.water;
            var oceanMat = art.ocean ? art.ocean : realOcean ? realOcean : ph.ocean;

            // Ocean: one big plane at sea level, plus a swim-only volume (no drinking seawater).
            var ocean = new GameObject("Ocean");
            ocean.transform.SetParent(waterRoot, false);
            float oceanSize = spec.size * 4f;
            ocean.transform.position = new Vector3(spec.size * 0.5f, 0f, spec.size * 0.5f);
            var om = SaveMesh(Plane(oceanSize, 8), "OceanPlane");
            ocean.AddComponent<MeshFilter>().sharedMesh = om;
            var omr = ocean.AddComponent<MeshRenderer>();
            omr.sharedMaterial = oceanMat;
            omr.shadowCastingMode = ShadowCastingMode.Off;
            // WaterVolume's surface is transform.y + size.y/2, so the box sits 20 m below sea level.
            var volGo = new GameObject("OceanVolume");
            volGo.transform.SetParent(ocean.transform, false);
            volGo.transform.localPosition = new Vector3(0f, -20f, 0f);
            var ov = volGo.AddComponent<WaterVolume>();
            ov.size = new Vector3(oceanSize, 40f, oceanSize);
            ov.drinkable = false;

            // Inland water surfaces, chunked into tiles so off-screen water is culled.
            BuildWaterMeshes(data, waterMat, waterRoot);

            // Drink/swim volumes
            var vols = new GameObject("Volumes").transform;
            vols.SetParent(waterRoot, false);
            BuildRiverVolumes(spec, data, vols);
            BuildFlatVolumes(data, vols);

            // Our water shader animates itself; the scroller is only for other (placeholder or custom) materials.
            var scrolled = new List<Material>();
            if (waterMat != realWater) scrolled.Add(waterMat);
            if (oceanMat != realOcean) scrolled.Add(oceanMat);
            if (scrolled.Count > 0) waterRoot.gameObject.AddComponent<WaterSurfaceAnimator>().materials = scrolled.ToArray();
        }

        static void BuildWaterMeshes(IslandData data, Material mat, Transform parent)
        {
            const int step = 2; // 3.5 m quads
            int res = data.Resolution;
            float cell = data.Cell;
            int tiles = Mathf.CeilToInt(data.Size / WaterMeshTile);
            var builders = new Dictionary<int, (List<Vector3> v, List<Vector2> uv, List<int> t, List<Vector2> flow)>();
            int[] cornerIndex = new int[4];
            var corner = new float[4];

            for (int z = 0; z + step < res; z += step)
            for (int x = 0; x + step < res; x += step)
            {
                int i00 = data.Index(x, z), i10 = data.Index(x + step, z), i01 = data.Index(x, z + step), i11 = data.Index(x + step, z + step);
                bool any = Wet(data, i00) || Wet(data, i10) || Wet(data, i01) || Wet(data, i11);
                if (!any) continue;
                corner[0] = Level(data, i00); corner[1] = Level(data, i10); corner[2] = Level(data, i01); corner[3] = Level(data, i11);
                float sum = 0f; int cnt = 0;
                foreach (var c in corner) if (!float.IsNaN(c)) { sum += c; cnt++; }
                if (cnt == 0) continue;
                float avg = sum / cnt;
                for (int k = 0; k < 4; k++) if (float.IsNaN(corner[k])) corner[k] = avg;

                int tile = Mathf.Min(tiles - 1, (int)(x * cell / WaterMeshTile)) + tiles * Mathf.Min(tiles - 1, (int)(z * cell / WaterMeshTile));
                if (!builders.TryGetValue(tile, out var b)) builders[tile] = b = (new List<Vector3>(), new List<Vector2>(), new List<int>(), new List<Vector2>());
                cornerIndex[0] = i00; cornerIndex[1] = i10; cornerIndex[2] = i01; cornerIndex[3] = i11;
                for (int k = 0; k < 4; k++) b.flow.Add(RiverFlow(data, cornerIndex[k]));
                int s = b.v.Count;
                float x0 = x * cell, x1 = (x + step) * cell, z0 = z * cell, z1 = (z + step) * cell;
                b.v.Add(new Vector3(x0, corner[0] + 0.02f, z0)); b.uv.Add(new Vector2(x0, z0) * 0.1f);
                b.v.Add(new Vector3(x1, corner[1] + 0.02f, z0)); b.uv.Add(new Vector2(x1, z0) * 0.1f);
                b.v.Add(new Vector3(x0, corner[2] + 0.02f, z1)); b.uv.Add(new Vector2(x0, z1) * 0.1f);
                b.v.Add(new Vector3(x1, corner[3] + 0.02f, z1)); b.uv.Add(new Vector2(x1, z1) * 0.1f);
                b.t.Add(s); b.t.Add(s + 2); b.t.Add(s + 1);
                b.t.Add(s + 1); b.t.Add(s + 2); b.t.Add(s + 3);
            }

            foreach (var kv in builders)
            {
                var b = kv.Value;
                var mesh = new Mesh { name = $"Water_{kv.Key}" };
                if (b.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(b.v);
                mesh.SetUVs(0, b.uv);
                mesh.SetUVs(1, b.flow);
                mesh.SetTriangles(b.t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, mesh.name);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                go.isStatic = true;
            }
        }

        /// <summary>Downstream direction times speed (m/s) for river cells, zero for still water. Read by the water shader.</summary>
        static Vector2 RiverFlow(IslandData d, int i)
        {
            if (d.Water[i] != WaterKind.River) return Vector2.zero;
            float wx = (i % d.Resolution) * d.Cell, wz = (i / d.Resolution) * d.Cell;
            float best = float.MaxValue; Vector2 flow = Vector2.zero;
            foreach (var river in d.Rivers)
            {
                var P = river.points;
                if (P == null || P.Length < 2) continue;
                float sign = river.level[0] >= river.level[P.Length - 1] ? 1f : -1f; // points run downstream when the first is higher
                for (int k = 0; k < P.Length; k++)
                {
                    float dx = P[k].x * d.Size - wx, dz = P[k].y * d.Size - wz;
                    float dist = dx * dx + dz * dz;
                    if (dist >= best) continue;
                    best = dist;
                    int a = Mathf.Max(0, k - 1), c = Mathf.Min(P.Length - 1, k + 1);
                    Vector2 along = (P[c] - P[a]) * d.Size;
                    float len = along.magnitude;
                    if (len < 0.001f) continue;
                    float drop = Mathf.Abs(river.level[a] - river.level[c]) / len;
                    flow = along / len * sign * Mathf.Min(7f, 1.1f + drop * 14f);
                }
            }
            return flow;
        }

        static bool Wet(IslandData d, int i) => d.Water[i] != WaterKind.None && d.Water[i] != WaterKind.Sea && d.Heights[i] < d.WaterLevel[i] + 0.2f;

        static float Level(IslandData d, int i) => d.Water[i] != WaterKind.None && d.Water[i] != WaterKind.Sea ? d.WaterLevel[i] : float.NaN;

        /// <summary>Rotated boxes along each river, about 20 m long, skipping the waterfall drop.</summary>
        static void BuildRiverVolumes(IslandSpec spec, IslandData data, Transform parent)
        {
            foreach (var river in data.Rivers)
            {
                var P = river.points;
                int chunk = 3;
                for (int k = 0; k + chunk < P.Length; k += chunk)
                {
                    Vector3 a = new Vector3(P[k].x * data.Size, 0, P[k].y * data.Size);
                    Vector3 b = new Vector3(P[k + chunk].x * data.Size, 0, P[k + chunk].y * data.Size);
                    float la = river.level[k], lb = river.level[k + chunk];
                    if (Mathf.Abs(la - lb) > 1.5f) continue; // waterfall or rapids: no swim box
                    float level = Mathf.Max(la, lb);
                    float hw = (river.halfWidth[k] + river.halfWidth[k + chunk]) * 0.5f + 0.5f;
                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 dir = b - a;
                    if (dir.sqrMagnitude < 0.01f) continue;
                    var go = new GameObject($"{river.name}_{k / chunk}");
                    go.transform.SetParent(parent, false);
                    go.transform.position = new Vector3(mid.x, level - 2f, mid.z);
                    go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    var vol = go.AddComponent<WaterVolume>();
                    vol.size = new Vector3(hw * 2f, 4f, dir.magnitude + 2f);
                }
            }
        }

        /// <summary>Axis-aligned boxes covering lakes, ponds, puddles and swamp pools (greedy rectangles on a 7 m grid).</summary>
        static void BuildFlatVolumes(IslandData data, Transform parent)
        {
            const int step = 4;
            int w = (data.Resolution - 1) / step + 1;
            float cell = data.Cell * step;
            var used = new bool[w * w];
            int count = 0;
            for (int z = 0; z < w; z++)
            for (int x = 0; x < w; x++)
            {
                int c = z * w + x;
                if (used[c] || !FlatWater(data, x * step, z * step, out var kind, out float level)) continue;
                // grow right
                int x1 = x;
                while (x1 + 1 < w && !used[z * w + x1 + 1] && Same(data, (x1 + 1) * step, z * step, kind, level)) x1++;
                // grow up while the whole row matches
                int z1 = z;
                while (z1 + 1 < w)
                {
                    bool ok = true;
                    for (int xx = x; xx <= x1 && ok; xx++)
                        ok = !used[(z1 + 1) * w + xx] && Same(data, xx * step, (z1 + 1) * step, kind, level);
                    if (!ok) break;
                    z1++;
                }
                float minBed = level;
                for (int zz = z; zz <= z1; zz++)
                for (int xx = x; xx <= x1; xx++)
                {
                    used[zz * w + xx] = true;
                    minBed = Mathf.Min(minBed, data.Heights[data.Index(xx * step, zz * step)]);
                }
                float depth = Mathf.Max(1f, level - minBed + 0.5f);
                var go = new GameObject($"{kind}_{count++}");
                go.transform.SetParent(parent, false);
                float cx = (x + x1) * 0.5f * cell, cz = (z + z1) * 0.5f * cell;
                go.transform.position = new Vector3(cx, level - depth * 0.5f, cz);
                var vol = go.AddComponent<WaterVolume>();
                vol.size = new Vector3((x1 - x + 1) * cell, depth, (z1 - z + 1) * cell);
            }
        }

        static bool FlatWater(IslandData d, int x, int z, out WaterKind kind, out float level)
        {
            int i = d.Index(x, z);
            kind = d.Water[i]; level = d.WaterLevel[i];
            return kind != WaterKind.None && kind != WaterKind.Sea && kind != WaterKind.River && d.IsWater(i);
        }

        static bool Same(IslandData d, int x, int z, WaterKind kind, float level)
            => FlatWater(d, x, z, out var k, out var l) && k == kind && Mathf.Abs(l - level) < 0.05f;

        static Mesh Plane(float size, int segments)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int z = 0; z <= segments; z++)
            for (int x = 0; x <= segments; x++)
            {
                float px = (x / (float)segments - 0.5f) * size, pz = (z / (float)segments - 0.5f) * size;
                v.Add(new Vector3(px, 0, pz)); uv.Add(new Vector2(px, pz) * 0.02f);
            }
            for (int z = 0; z < segments; z++)
            for (int x = 0; x < segments; x++)
            {
                int a = z * (segments + 1) + x;
                t.Add(a); t.Add(a + segments + 1); t.Add(a + 1);
                t.Add(a + 1); t.Add(a + segments + 1); t.Add(a + segments + 2);
            }
            var m = new Mesh { name = "Plane" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        // ================= props =================

        static DistanceCuller BuildProps(IslandSpec spec, IslandData data, WorldArtSet art, PlaceholderArt.Result ph, Transform parent)
        {
            var food = new GameObject("Food").transform;
            food.SetParent(parent, false);
            var culler = food.gameObject.AddComponent<DistanceCuller>();
            culler.visibleDistance = 110f;
            var nests = new GameObject("Nests").transform;
            nests.SetParent(parent, false);

            var counters = new Dictionary<WorldPropKind, int>();
            foreach (var p in data.Props)
            {
                if (System.Array.IndexOf(TreeKinds, p.kind) >= 0 || p.kind == WorldPropKind.Puddle) continue;
                counters.TryGetValue(p.kind, out int n);
                counters[p.kind] = n + 1;
                var prefab = art.PickPrefab(p.kind, n);
                if (!prefab) ph.prefabs.TryGetValue(p.kind, out prefab);
                if (!prefab) continue;

                Transform holder = p.kind == WorldPropKind.Nest ? nests : p.kind == WorldPropKind.GreatTree ? parent : food;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                go.transform.position = p.position;
                go.transform.rotation = Quaternion.Euler(0f, p.yaw, 0f);
                if (p.kind != WorldPropKind.Nest && p.kind != WorldPropKind.GreatTree) go.transform.localScale = Vector3.one * p.scale;
                string biomeId = BiomeIds.FromCode(p.biome);

                if (p.kind == WorldPropKind.Nest)
                {
                    go.name = p.isMain ? "Nest_Main_meadow" : $"Nest_{n:00}_{biomeId}";
                    var nest = go.GetComponent<Nest>();
                    if (!nest) nest = go.AddComponent<Nest>();
                    nest.nestId = p.isMain ? "nest_main" : $"nest_{biomeId}_{n:00}";
                    var spawn = go.GetComponent<PlayerSpawnPoint>();
                    if (!spawn) spawn = go.AddComponent<PlayerSpawnPoint>();
                    spawn.biomeId = biomeId;
                }
                else if (p.kind == WorldPropKind.GreatTree)
                {
                    go.name = "GreatTree";
                }
                else
                {
                    go.name = $"{p.kind}_{n:000}";
                }
            }
            return culler;
        }

        static void BuildLandmarks(IslandSpec spec, IslandData data, Transform parent)
        {
            var root = new GameObject("Landmarks").transform;
            root.SetParent(parent, false);

            Vector3 W(Vector2 norm) { float x = norm.x * data.Size, z = norm.y * data.Size; return new Vector3(x, data.SampleHeight(x, z), z); }

            // Volcano summit (crater rim) = Red Highlands peak lookout secret
            var rim = spec.volcanoCenter + new Vector2(spec.craterRadius * 1.3f, 0f);
            AddLandmark(root, "volcano", "Smoky Volcano", LandmarkKind.Landmark, BiomeIds.Highlands, W(spec.volcanoCenter), 60f);
            AddLandmark(root, "secret_peak_lookout", "Peak Lookout", LandmarkKind.Secret, BiomeIds.Highlands, W(rim), 10f);
            AddLandmark(root, "great_tree", "Great Old Tree", LandmarkKind.Landmark, BiomeIds.Meadow, W(spec.greatTree), 20f);

            // Waterfall: middle of the big river's drop, with a mist effect
            var big = data.Rivers.Find(r => r.hasWaterfall);
            if (big != null)
            {
                float sMid = (big.waterfallRange.x + big.waterfallRange.y) * 0.5f;
                int k = 0;
                while (k < big.along.Length - 1 && big.along[k] < sMid) k++;
                var p = W(big.points[k]);
                p.y = big.level[k];
                var wf = AddLandmark(root, "waterfall", "Giant Waterfall", LandmarkKind.Landmark, BiomeIds.Riverbanks, p, 25f);
                AddMist(wf.transform, big.level[Mathf.Min(k + 6, big.level.Length - 1)] - p.y);

                // Stepping-stone path (riverbanks secret): a calm stretch downstream of the meadow
                int s = big.points.Length * 2 / 3;
                var st = W(big.points[s]); st.y = big.level[s];
                AddLandmark(root, "secret_stepping_stones", "Stepping Stones", LandmarkKind.Secret, BiomeIds.Riverbanks, st, 12f);
            }

            // Meadow secret: shell under the big log, next to the main nest
            var main = data.Props.Find(pp => pp.kind == WorldPropKind.Nest && pp.isMain);
            AddLandmark(root, "secret_meadow_log_shell", "Shell Under the Log", LandmarkKind.Secret, BiomeIds.Meadow, main.position + new Vector3(14f, 0f, 9f), 6f);

            // Hidden nests: forest tree hollow, swamp
            AddSecretNest(root, data, BiomeIds.Forest, "secret_tree_hollow_nest", "Tree Hollow Nest");
            AddSecretNest(root, data, BiomeIds.Swamp, "secret_swamp_nest", "Hidden Swamp Nest");

            // Lake Basin fossil cave: north shore of the lake
            var shore = new Vector2(spec.lakeCenter.x, spec.lakeCenter.y + spec.lakeRadii.y * 1.08f);
            AddLandmark(root, "secret_fossil_cave", "Fossil Cave", LandmarkKind.Secret, BiomeIds.Lake, W(shore), 10f);
        }

        static Landmark AddLandmark(Transform parent, string id, string display, LandmarkKind kind, string biome, Vector3 pos, float radius)
        {
            var go = new GameObject($"Landmark_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Landmark>();
            l.landmarkId = id; l.displayName = display; l.kind = kind; l.biomeId = biome; l.radius = radius;
            return l;
        }

        /// <summary>A hidden nest: the biome nest farthest from the main nest gets a secret twin 25 m away.</summary>
        static void AddSecretNest(Transform parent, IslandData data, string biome, string id, string display)
        {
            byte code = BiomeIds.ToCode(biome);
            var main = data.Props.Find(p => p.kind == WorldPropKind.Nest && p.isMain);
            WorldProp best = default; float bestD = -1f;
            foreach (var p in data.Props)
            {
                if (p.kind != WorldPropKind.Nest || p.biome != code) continue;
                float d = Vector3.Distance(p.position, main.position);
                if (d > bestD) { bestD = d; best = p; }
            }
            if (bestD < 0f) return;
            var pos = best.position + Quaternion.Euler(0, best.yaw, 0) * new Vector3(25f, 0f, 0f);
            pos.y = data.SampleHeight(pos.x, pos.z);
            var l = AddLandmark(parent, id, display, LandmarkKind.Secret, biome, pos, 6f);
            var nest = l.gameObject.AddComponent<Nest>();
            nest.nestId = id;
            nest.radius = 2.5f;
        }

        static void AddMist(Transform at, float drop)
        {
            var go = new GameObject("Mist");
            go.transform.SetParent(at, false);
            go.transform.localPosition = new Vector3(0f, Mathf.Min(0f, drop), 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 3f;
            main.startSpeed = 1.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startColor = new Color(1f, 1f, 1f, 0.25f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 15f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 2f, 6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderArt.Root + "/M_WaterfallMist.mat");
        }

        // ================= atmosphere =================

        static void BuildAtmosphere(WorldArtSet art, PlaceholderArt.Result ph, Transform parent)
        {
            bool photoSky = art.skybox && art.hasSkyLighting;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            // With a sky photo the light comes from where the sun is in the picture.
            sunGo.transform.rotation = photoSky && art.sunDirection.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(-art.sunDirection.normalized)
                : Quaternion.Euler(48f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.955f, 0.87f);
            sun.intensity = photoSky ? 1.35f : 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = photoSky ? 0.92f : 0.85f;
            sun.shadowBias = 0.6f;
            sun.shadowNormalBias = 0.6f;

            RenderSettings.sun = sun;
            RenderSettings.skybox = art.skybox ? art.skybox : ph.skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = photoSky ? art.ambientSky * 1.3f : new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = photoSky ? art.ambientEquator * 1.3f : new Color(0.52f, 0.56f, 0.52f);
            RenderSettings.ambientGroundColor = photoSky ? art.ambientGround * 1.5f : new Color(0.30f, 0.27f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0012f;
            RenderSettings.fogColor = photoSky ? art.fogColor : new Color(0.70f, 0.77f, 0.84f);

            // Reflections: a cubemap of this sky, saved with the scene.
            var reflection = RealisticArt.BakeSkyReflection();
            if (reflection)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflection;
                RenderSettings.reflectionIntensity = 1f;
            }
            else
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.reflectionIntensity = 0.8f;
            }

            // Post-processing: filmic tonemapping, grading, soft bloom and a light vignette. Needs
            // "Post Processing" ticked on the game camera to show (the game session does that).
            var volGo = new GameObject("PostProcessing");
            volGo.transform.SetParent(parent, false);
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
        }

        static VolumeProfile PostProfile()
        {
            string path = GeneratedRoot + "/IslandPostProcessing.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.72f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.saturation.Override(4f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(5f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void BuildPreviewCamera(IslandSpec spec, IslandData data)
        {
            var main = data.Props.Find(p => p.kind == WorldPropKind.Nest && p.isMain);
            var go = new GameObject("PreviewCamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.farClipPlane = 1600f;
            cam.nearClipPlane = 0.3f;
            var extra = go.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = true;
            go.AddComponent<IslandPreviewCamera>();
            go.transform.position = main.position + new Vector3(-30f, 14f, -40f);
            go.transform.LookAt(main.position + Vector3.up * 2f);
        }

        // ================= misc =================

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            PlaceholderArt.EnsureFolder(GeneratedRoot + "/Meshes");
            string path = $"{GeneratedRoot}/Meshes/{name}.asset";
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

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static int Count(IslandData d, WorldPropKind k) { int n = 0; foreach (var p in d.Props) if (p.kind == k) n++; return n; }

        static int CountFood(IslandData d)
        {
            int n = 0;
            foreach (var p in d.Props)
                if (p.kind <= WorldPropKind.FishSpot) n++;
            return n;
        }

        static int CountDecor(IslandData d)
        {
            int n = 0;
            foreach (var p in d.Props) if (System.Array.IndexOf(TreeKinds, p.kind) >= 0) n++;
            return n;
        }
    }
}
