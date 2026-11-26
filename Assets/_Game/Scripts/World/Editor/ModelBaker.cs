using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Dino.World.Generation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Dino.World.EditorTools
{
    /// <summary>
    /// Turns a downloaded photo-scanned model (FBX) into game-ready prefabs: splits "sets" into single
    /// objects, puts the pivot at the base, scales to a sensible size, merges everything into one mesh,
    /// makes lighter LODs for distance, builds materials (with wind for plants) and adds a collider.
    /// The result has the shape terrain trees need: a LODGroup on the root and one child per LOD.
    /// </summary>
    public static class ModelBaker
    {
        public class Options
        {
            public string name;
            public string outFolder;
            public float minSize = 1f, maxSize = 2f;
            /// <summary>Measure by the longest side (rocks, logs) instead of the height (plants).</summary>
            public bool sizeByLongest;
            /// <summary>Tip standing trunks over so they lie on the ground.</summary>
            public bool layFlat;
            /// <summary>Fraction of the height buried in the ground (rocks, logs).</summary>
            public float sink;
            public bool wind;
            public int maxVariants = 1;
            public int lod0Triangles = 40000;
            public bool collider = true;
            /// <summary>Called on the prefab root just before it is saved (to add gameplay components).</summary>
            public Action<GameObject, Baked> decorate;
        }

        public class Baked
        {
            public GameObject prefab;
            public Mesh mesh;
            public Vector3 size;
            public float trunkRadius;
            public int triangles, sourceTriangles;
        }

        class Part
        {
            public Mesh mesh;
            public Material[] materials;
            public Matrix4x4 matrix;
            public Bounds bounds;
        }

        static readonly Regex SkipName = new Regex(@"(lod[ _]?[1-9]|collision|collider)", RegexOptions.IgnoreCase);
        static readonly Regex LeafName = new Regex(@"(leaf|leaves|foliage|needle|frond|grass|flower|petal|fern|twig|blade|plant)", RegexOptions.IgnoreCase);

        public static List<Baked> Bake(string modelPath, Options o)
        {
            var result = new List<Baked>();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!root) { Debug.LogWarning($"[Island] Couldn't load model {modelPath}"); return result; }

            var parts = new List<Part>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                var mr = mf.GetComponent<MeshRenderer>();
                if (!mesh || !mr || !mesh.isReadable || mesh.vertexCount < 3) continue;
                if (SkipName.IsMatch(mf.name)) continue;
                var m = mf.transform.localToWorldMatrix;
                parts.Add(new Part { mesh = mesh, materials = mr.sharedMaterials, matrix = m, bounds = TransformBounds(mesh.bounds, m) });
            }
            if (parts.Count == 0) { Debug.LogWarning($"[Island] {modelPath} has no usable meshes."); return result; }

            // Group parts that stand on the same spot (trunk + leaves); separate spots are separate objects.
            var clusters = Cluster(parts);
            clusters.Sort((a, b) => Longest(Union(b).size).CompareTo(Longest(Union(a).size)));
            float biggest = Longest(Union(clusters[0]).size);

            PlaceholderArt.EnsureFolder(o.outFolder);
            string textureFolder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            var materialCache = new Dictionary<Material, (Material mat, bool foliage)>();
            int made = 0;
            foreach (var cluster in clusters)
            {
                if (made >= o.maxVariants) break;
                if (Longest(Union(cluster).size) < biggest * 0.3f) continue; // debris
                try
                {
                    var baked = BakeCluster(cluster, o, made, textureFolder, materialCache);
                    if (baked != null) { result.Add(baked); made++; }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Island] Couldn't bake {o.name} variant {made} from {modelPath}: {e.Message}");
                }
            }
            return result;
        }

        static Baked BakeCluster(List<Part> cluster, Options o, int variant, string textureFolder,
            Dictionary<Material, (Material mat, bool foliage)> materialCache)
        {
            var bounds = Union(cluster);
            var turn = Matrix4x4.identity;
            if (o.layFlat && bounds.size.y > 1.4f * Mathf.Max(bounds.size.x, bounds.size.z))
                turn = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));

            // One mesh per source material, then one mesh with a submesh per material.
            var sourceMaterials = new List<Material>();
            var perMaterial = new List<List<CombineInstance>>();
            foreach (var p in cluster)
            {
                for (int s = 0; s < p.mesh.subMeshCount; s++)
                {
                    var mat = p.materials.Length == 0 ? null : p.materials[Mathf.Min(s, p.materials.Length - 1)];
                    int idx = sourceMaterials.IndexOf(mat);
                    if (idx < 0) { idx = sourceMaterials.Count; sourceMaterials.Add(mat); perMaterial.Add(new List<CombineInstance>()); }
                    perMaterial[idx].Add(new CombineInstance { mesh = p.mesh, subMeshIndex = s, transform = turn * p.matrix });
                }
            }
            var temp = new List<Mesh>();
            var finalParts = new CombineInstance[perMaterial.Count];
            for (int i = 0; i < perMaterial.Count; i++)
            {
                var m = new Mesh { indexFormat = IndexFormat.UInt32 };
                m.CombineMeshes(perMaterial[i].ToArray(), true, true);
                temp.Add(m);
                finalParts[i] = new CombineInstance { mesh = m, subMeshIndex = 0, transform = Matrix4x4.identity };
            }
            string baseName = variant == 0 ? o.name : $"{o.name}_{(char)('A' + variant)}";
            var mesh0 = new Mesh { name = baseName + "_LOD0", indexFormat = IndexFormat.UInt32 };
            mesh0.CombineMeshes(finalParts, false, false);
            foreach (var m in temp) Object.DestroyImmediate(m);

            // Pivot at the base, sensible size.
            var verts = mesh0.vertices;
            mesh0.RecalculateBounds();
            var b = mesh0.bounds;
            float metric = o.sizeByLongest ? Longest(b.size) : b.size.y;
            if (metric < 1e-5f) { Object.DestroyImmediate(mesh0); return null; }
            float scale = metric < o.minSize ? o.minSize / metric : metric > o.maxSize ? o.maxSize / metric : 1f;

            var pivot = new Vector3(b.center.x, b.min.y + o.sink * b.size.y, b.center.z);
            float trunk = 0.3f * Mathf.Min(b.size.x, b.size.z);
            if (!o.sizeByLongest)
            {
                // Stand plants on the middle of their lowest slice (the trunk), not the middle of the crown.
                float top = b.min.y + b.size.y * 0.05f;
                Vector3 sum = Vector3.zero; int count = 0;
                for (int i = 0; i < verts.Length; i++) if (verts[i].y <= top) { sum += verts[i]; count++; }
                if (count > 0)
                {
                    var c = sum / count;
                    float r2 = 0f;
                    for (int i = 0; i < verts.Length; i++)
                        if (verts[i].y <= top) { float dx = verts[i].x - c.x, dz = verts[i].z - c.z; r2 += dx * dx + dz * dz; }
                    trunk = Mathf.Sqrt(r2 / count) * 1.25f;
                    pivot.x = c.x; pivot.z = c.z;
                }
            }
            for (int i = 0; i < verts.Length; i++) verts[i] = (verts[i] - pivot) * scale;
            mesh0.vertices = verts;
            mesh0.RecalculateBounds();
            var size = mesh0.bounds.size;
            trunk *= scale;

            // Materials
            var mats = new Material[sourceMaterials.Count];
            var foliage = new bool[sourceMaterials.Count];
            for (int i = 0; i < mats.Length; i++)
            {
                var src = sourceMaterials[i];
                var key = src ? src : null;
                if (key == null || !materialCache.TryGetValue(key, out var entry))
                {
                    entry = ConvertMaterial(src, textureFolder, o, size.y);
                    if (key != null) materialCache[key] = entry;
                }
                mats[i] = entry.mat;
                foliage[i] = entry.foliage;
            }

            // LODs
            int tris = TriangleCount(mesh0);
            int sourceTris = tris;
            var lods = new List<Mesh>();
            if (tris > o.lod0Triangles)
            {
                var reduced = Reduce(mesh0, foliage, (float)o.lod0Triangles / tris, baseName + "_LOD0", 2f);
                Object.DestroyImmediate(mesh0);
                mesh0 = reduced;
                tris = TriangleCount(mesh0);
            }
            lods.Add(mesh0);
            if (tris > 1500)
            {
                // Each step is far lighter; leaves grow to keep the crown's shape. The last one is for the horizon.
                lods.Add(Reduce(mesh0, foliage, 0.28f, baseName + "_LOD1", 2.2f));
                if (tris > 6000) lods.Add(Reduce(mesh0, foliage, 0.06f, baseName + "_LOD2", 4.5f));
                if (tris > 20000) lods.Add(Reduce(mesh0, foliage, 0.012f, baseName + "_LOD3", 9f));
            }

            var saved = new List<Mesh>();
            foreach (var m in lods) saved.Add(SaveMesh(m, $"{o.outFolder}/{m.name}.asset"));

            var go = new GameObject(baseName);
            var group = go.AddComponent<LODGroup>();
            float[] heights = saved.Count == 4 ? new[] { 0.30f, 0.12f, 0.04f, 0.008f } : saved.Count == 3 ? new[] { 0.30f, 0.10f, 0.012f } : saved.Count == 2 ? new[] { 0.22f, 0.012f } : new[] { 0.012f };
            var entries = new LOD[saved.Count];
            for (int i = 0; i < saved.Count; i++)
            {
                var child = new GameObject($"{baseName}_LOD{i}");
                child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = saved[i];
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = i >= 3 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                entries[i] = new LOD(heights[i], new Renderer[] { mr });
            }
            group.SetLODs(entries);
            group.RecalculateBounds();

            if (o.collider)
            {
                var c = go.AddComponent<CapsuleCollider>();
                if (o.layFlat)
                {
                    bool alongX = size.x >= size.z;
                    c.direction = alongX ? 0 : 2;
                    c.radius = Mathf.Max(0.1f, 0.45f * Mathf.Min(size.y, alongX ? size.z : size.x));
                    c.height = alongX ? size.x : size.z;
                    c.center = new Vector3(saved[0].bounds.center.x, c.radius * 0.8f, saved[0].bounds.center.z);
                }
                else if (o.sizeByLongest)
                {
                    c.direction = 1;
                    c.radius = Mathf.Max(0.15f, Mathf.Min(0.4f * Mathf.Min(size.x, size.z), 0.6f * size.y));
                    c.height = Mathf.Max(size.y * 0.9f, c.radius * 2f);
                    c.center = new Vector3(0f, saved[0].bounds.center.y, 0f);
                }
                else
                {
                    c.direction = 1;
                    c.radius = Mathf.Clamp(trunk, 0.12f, Mathf.Max(0.15f, size.y * 0.08f));
                    c.height = size.y * 0.9f;
                    c.center = new Vector3(0f, size.y * 0.45f, 0f);
                }
            }

            var baked = new Baked { mesh = saved[0], size = size, trunkRadius = trunk, triangles = tris, sourceTriangles = sourceTris };
            o.decorate?.Invoke(go, baked);
            baked.prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{o.outFolder}/P_{baseName}.prefab");
            Object.DestroyImmediate(go);
            return baked;
        }

        // ================= materials =================

        static (Material mat, bool foliage) ConvertMaterial(Material src, string textureFolder, Options o, float height)
        {
            string srcName = src ? src.name : "Default";
            Texture baseTex = null;
            if (src && src.HasProperty("_BaseMap")) baseTex = src.GetTexture("_BaseMap");
            if (!baseTex && src && src.HasProperty("_MainTex")) baseTex = src.GetTexture("_MainTex");
            Texture normalTex = src && src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;

            var files = ImageFiles(textureFolder);
            if (!baseTex) baseTex = LoadByPattern(files, srcName, @"_(diff|diffuse|col|color|albedo|basecolor)(_|\.)");
            if (!normalTex) normalTex = LoadByPattern(files, srcName, @"_nor_gl(_|\.)") ?? LoadByPattern(files, srcName, @"_(nor|normal)(_|\.)");

            bool hasAlpha = false;
            string basePath = baseTex ? AssetDatabase.GetAssetPath(baseTex) : null;
            if (!string.IsNullOrEmpty(basePath))
            {
                // A separate opacity map (same name, "_alpha"/"_opacity"/"_mask") is folded into the colour texture.
                string alphaPath = FindSibling(files, basePath, @"_(alpha|opacity|mask)(_|\.)");
                if (alphaPath != null)
                {
                    var merged = MergeAlpha(basePath, alphaPath);
                    if (merged) { baseTex = merged; hasAlpha = true; }
                }
                else if (AssetImporter.GetAtPath(basePath) is TextureImporter ti && ti.DoesSourceTextureHaveAlpha())
                {
                    hasAlpha = true;
                    if (!ti.alphaIsTransparency || !ti.mipMapsPreserveCoverage)
                {
                    ti.alphaIsTransparency = true;
                    ti.mipMapsPreserveCoverage = true; // leaves keep their size in the distance instead of fading away
                    ti.alphaTestReferenceValue = 0.35f;
                    ti.SaveAndReimport();
                }
                }
            }
            string normalPath = normalTex ? AssetDatabase.GetAssetPath(normalTex) : null;
            if (!string.IsNullOrEmpty(normalPath) && AssetImporter.GetAtPath(normalPath) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            {
                ni.textureType = TextureImporterType.NormalMap;
                ni.SaveAndReimport();
                normalTex = AssetDatabase.LoadAssetAtPath<Texture>(normalPath);
            }

            bool foliage = hasAlpha;
            var foliageShader = Shader.Find("Therion/Foliage");
            bool useFoliage = o.wind && foliageShader;
            var shader = useFoliage ? foliageShader : PlaceholderArt.LitShader();

            string path = $"{o.outFolder}/M_{o.name}_{Sanitize(srcName)}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", Color.white);
            if (baseTex) m.SetTexture("_BaseMap", baseTex);
            if (normalTex)
            {
                m.SetTexture("_BumpMap", normalTex);
                m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty("_UseNormalMap")) m.SetFloat("_UseNormalMap", 1f);
            }
            m.SetFloat("_Smoothness", hasAlpha ? 0.22f : 0.1f);
            if (hasAlpha)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.35f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
                if (!useFoliage) m.doubleSidedGI = true;
            }
            else
            {
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_Cull", 2f);
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "");
                m.renderQueue = (int)RenderQueue.Geometry;
            }
            if (useFoliage)
            {
                m.SetFloat("_Translucency", foliage ? 0.7f : 0f);
                m.SetFloat("_BendHeight", Mathf.Max(0.2f, height));
                m.SetFloat("_WindStrength", Mathf.Clamp(height * 0.018f, 0.02f, 0.35f));
                m.SetFloat("_Flutter", foliage ? Mathf.Clamp(height * 0.004f, 0.006f, 0.03f) : 0f);
                m.SetFloat("_Gradient", 0f);
                m.SetFloat("_NormalUp", 0f);
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return (m, foliage);
        }

        static List<string> ImageFiles(string folder)
        {
            var list = new List<string>();
            if (!Directory.Exists(folder)) return list;
            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".tga") list.Add(f.Replace('\\', '/'));
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Best texture whose file name matches the pattern, preferring names that mention the material.</summary>
        static Texture LoadByPattern(List<string> files, string materialName, string pattern)
        {
            var rx = new Regex(pattern, RegexOptions.IgnoreCase);
            string best = null; int bestScore = -1;
            var tokens = materialName.ToLowerInvariant().Split(new[] { '_', ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var f in files)
            {
                string n = Path.GetFileName(f).ToLowerInvariant();
                if (!rx.IsMatch(n) || n.Contains("_diffa")) continue;
                int score = 0;
                foreach (var t in tokens) if (t.Length > 2 && n.Contains(t)) score++;
                if (score > bestScore) { bestScore = score; best = f; }
            }
            return best == null ? null : AssetDatabase.LoadAssetAtPath<Texture>(best);
        }

        static string FindSibling(List<string> files, string basePath, string pattern)
        {
            string n = Path.GetFileName(basePath);
            var cut = Regex.Match(n, @"_(diff|diffuse|col|color|albedo|basecolor)(_|\.)", RegexOptions.IgnoreCase);
            if (!cut.Success) return null;
            string prefix = n.Substring(0, cut.Index).ToLowerInvariant();
            var rx = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (var f in files)
            {
                // The opacity map must belong to this exact texture set ("x_leaves_alpha" is not the alpha of "x").
                string fn = Path.GetFileName(f).ToLowerInvariant();
                if (!fn.StartsWith(prefix)) continue;
                var m = rx.Match(fn, prefix.Length);
                if (m.Success && m.Index == prefix.Length) return f;
            }
            return null;
        }

        static Texture MergeAlpha(string colorPath, string alphaPath)
        {
            string outPath = Regex.Replace(colorPath, @"\.[A-Za-z]+$", "") + "_diffa.png";
            if (!File.Exists(outPath))
            {
                var c = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var a = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!c.LoadImage(File.ReadAllBytes(colorPath)) || !a.LoadImage(File.ReadAllBytes(alphaPath))) return null;
                    var cp = c.GetPixels32();
                    var ap = a.GetPixels32();
                    int w = c.width, h = c.height;
                    for (int y = 0; y < h; y++)
                    {
                        int ay = Mathf.Min(a.height - 1, y * a.height / h);
                        for (int x = 0; x < w; x++)
                        {
                            int ax = Mathf.Min(a.width - 1, x * a.width / w);
                            var px = cp[y * w + x];
                            px.a = ap[ay * a.width + ax].r;
                            cp[y * w + x] = px;
                        }
                    }
                    c.SetPixels32(cp);
                    c.Apply();
                    File.WriteAllBytes(outPath, c.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(c); Object.DestroyImmediate(a); }
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            }
            if (AssetImporter.GetAtPath(outPath) is TextureImporter imp)
            {
                imp.alphaIsTransparency = true;
                imp.mipMapsPreserveCoverage = true;
                imp.alphaTestReferenceValue = 0.35f;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture>(outPath);
        }

        static string Sanitize(string s) => Regex.Replace(s, @"[^A-Za-z0-9_]+", "_");

        // ================= mesh reduction =================

        static int TriangleCount(Mesh m)
        {
            long n = 0;
            for (int s = 0; s < m.subMeshCount; s++) n += m.GetIndexCount(s) / 3;
            return (int)n;
        }

        /// <summary>
        /// A lighter copy of the mesh. Leaf submeshes lose whole leaves (so the rest keep their shape);
        /// solid submeshes (trunks, rocks) get their vertices snapped to a grid and merged.
        /// </summary>
        static Mesh Reduce(Mesh src, bool[] foliage, float fraction, string name, float maxGrow)
        {
            var v = src.vertices;
            var n = src.normals;
            var uv = src.uv;
            bool hasN = n != null && n.Length == v.Length, hasUV = uv != null && uv.Length == v.Length;
            float extent = Mathf.Max(0.001f, Longest(src.bounds.size));
            var min = src.bounds.min;

            var outV = new List<Vector3>();
            var outN = new List<Vector3>();
            var outUV = new List<Vector2>();
            var outTris = new List<int>[src.subMeshCount];
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s);
                int target = Mathf.Max(12, Mathf.RoundToInt(tris.Length / 3 * fraction));
                if (s < foliage.Length && foliage[s])
                {
                    var thinned = ThinFoliage(v, n, uv, hasN, hasUV, tris, fraction, maxGrow, outV, outN, outUV);
                    if (thinned != null) { outTris[s] = thinned; continue; }
                }
                int[] reduced = SimplifyWithMeshLod(v, tris, target);
                if (reduced != null && reduced.Length / 3 > target * 1.6f + 200)
                {
                    // The simplifier couldn't get there (loose needles or twigs without an opacity map): thin it like leaves.
                    var thinned = ThinFoliage(v, n, uv, hasN, hasUV, tris, fraction, maxGrow, outV, outN, outUV);
                    if (thinned != null) { outTris[s] = thinned; continue; }
                }
                if (reduced == null) reduced = ClusterToTarget(v, tris, extent, min, target);

                var remap = new Dictionary<int, int>();
                var list = new List<int>(reduced.Length);
                for (int i = 0; i < reduced.Length; i++)
                {
                    int old = reduced[i];
                    if (!remap.TryGetValue(old, out int idx))
                    {
                        idx = outV.Count;
                        remap[old] = idx;
                        outV.Add(v[old]);
                        if (hasN) outN.Add(n[old]);
                        if (hasUV) outUV.Add(uv[old]);
                    }
                    list.Add(idx);
                }
                outTris[s] = list;
            }

            var mesh = new Mesh { name = name, indexFormat = outV.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(outV);
            if (hasN) mesh.SetNormals(outN);
            if (hasUV) mesh.SetUVs(0, outUV);
            mesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < outTris.Length; s++) mesh.SetTriangles(outTris[s], s, false);
            if (!hasN) mesh.RecalculateNormals();
            if (hasUV) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Unity's own simplifier (Mesh LOD, Unity 6.2+): keeps the silhouette and the texture mapping.
        /// Returns triangles that index the original vertices, or null when it isn't available.
        /// </summary>
        static int[] SimplifyWithMeshLod(Vector3[] v, int[] tris, int target)
        {
            if (tris.Length / 3 <= target) return tris;
            Mesh temp = null;
            try
            {
                var getLods = typeof(Mesh).GetMethod("GetLods", new[] { typeof(int) });
                if (getLods == null) return null;
                temp = new Mesh { indexFormat = IndexFormat.UInt32 };
                temp.vertices = v;
                temp.SetTriangles(tris, 0, false);
                temp.RecalculateBounds();
                MeshLodUtility.GenerateMeshLods(temp, -1);
                if (!(getLods.Invoke(temp, new object[] { 0 }) is System.Array ranges) || ranges.Length < 2) return null;

                // Pick the most detailed level that fits the budget (levels get lighter as they go).
                long start = -1, count = 0;
                foreach (var range in ranges)
                {
                    var t = range.GetType();
                    long s = Convert.ToInt64(Member(t, range, "indexStart"));
                    long c = Convert.ToInt64(Member(t, range, "indexCount"));
                    if (c < 9) continue;
                    start = s; count = c;
                    if (c / 3 <= target) break;
                }
                if (start < 0) return null;

                long subStart = temp.GetSubMesh(0).indexStart;
                var result = new int[count];
                using (var data = Mesh.AcquireReadOnlyMeshData(temp))
                {
                    var d = data[0];
                    if (d.indexFormat == IndexFormat.UInt32)
                    {
                        var idx = d.GetIndexData<uint>();
                        for (long i = 0; i < count; i++) result[i] = (int)idx[(int)(subStart + start + i)];
                    }
                    else
                    {
                        var idx = d.GetIndexData<ushort>();
                        for (long i = 0; i < count; i++) result[i] = idx[(int)(subStart + start + i)];
                    }
                }
                foreach (int i in result) if (i < 0 || i >= v.Length) return null;
                return result;
            }
            catch (Exception) { return null; }
            finally { if (temp) Object.DestroyImmediate(temp); }
        }

        static object Member(Type t, object o, string name)
        {
            var p = t.GetProperty(name);
            if (p != null) return p.GetValue(o);
            var f = t.GetField(name);
            return f != null ? f.GetValue(o) : 0;
        }

        static long CellKey(Vector3 p, Vector3 min, float cell)
        {
            long x = (long)((p.x - min.x) / cell) & 0x1FFFFF;
            long y = (long)((p.y - min.y) / cell) & 0x1FFFFF;
            long z = (long)((p.z - min.z) / cell) & 0x1FFFFF;
            return (x << 42) | (y << 21) | z;
        }

        static int[] ClusterToTarget(Vector3[] v, int[] tris, float extent, Vector3 min, int target)
        {
            int[] divisions = { 110, 76, 52, 36, 25, 17, 11, 7 };
            List<int> result = null;
            var rep = new Dictionary<long, int>();
            foreach (int div in divisions)
            {
                float cell = extent / div;
                rep.Clear();
                result = new List<int>(tris.Length / 2);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int a = Rep(rep, v, tris[i], min, cell), b = Rep(rep, v, tris[i + 1], min, cell), c = Rep(rep, v, tris[i + 2], min, cell);
                    if (a == b || b == c || a == c) continue;
                    result.Add(a); result.Add(b); result.Add(c);
                }
                if (result.Count / 3 <= target) break;
            }
            return result.ToArray();
        }

        static int Rep(Dictionary<long, int> rep, Vector3[] v, int index, Vector3 min, float cell)
        {
            long key = CellKey(v[index], min, cell);
            if (rep.TryGetValue(key, out int r)) return r;
            rep[key] = index;
            return index;
        }

        /// <summary>
        /// Keeps a fraction of the leaves and enlarges the ones that stay, so the crown looks just as full
        /// with far fewer triangles. Works on whole connected pieces (a leaf, a twig) when the mesh has
        /// them, otherwise on single triangles. Appends to the output lists and returns the new triangles.
        /// </summary>
        static List<int> ThinFoliage(Vector3[] v, Vector3[] n, Vector2[] uv, bool hasN, bool hasUV, int[] tris, float fraction, float maxGrow,
            List<Vector3> outV, List<Vector3> outN, List<Vector2> outUV)
        {
            int triCount = tris.Length / 3;
            if (triCount < 4) return null;
            fraction = Mathf.Clamp(fraction, 0.01f, 1f);
            float grow = Mathf.Clamp(1f / Mathf.Sqrt(fraction), 1f, maxGrow);
            var result = new List<int>((int)(tris.Length * fraction) + 3);

            // Connected pieces by shared vertices.
            var parent = new int[v.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = Find(tris[i]), b = Find(tris[i + 1]), c = Find(tris[i + 2]);
                if (b != a) parent[b] = a;
                c = Find(c);
                if (c != a) parent[c] = a;
            }
            var pieces = new Dictionary<int, (Vector3 sum, int count)>();
            for (int i = 0; i < tris.Length; i++)
            {
                int r = Find(tris[i]);
                pieces.TryGetValue(r, out var e);
                pieces[r] = (e.sum + v[tris[i]], e.count + 1);
            }

            if (pieces.Count >= 40)
            {
                // Scanned leaves are often dozens of triangles each. A leaf that is nearly flat is replaced
                // by one textured rectangle (same picture, 2 triangles), which keeps every leaf on the tree.
                var members = new Dictionary<int, List<int>>(pieces.Count);
                var triCountOf = new Dictionary<int, int>(pieces.Count);
                var seen = new bool[v.Length];
                for (int i = 0; i < tris.Length; i++)
                {
                    int vi = tris[i];
                    int r = Find(vi);
                    if (i % 3 == 0) { triCountOf.TryGetValue(r, out int tc); triCountOf[r] = tc + 1; }
                    if (seen[vi]) continue;
                    seen[vi] = true;
                    if (!members.TryGetValue(r, out var list)) members[r] = list = new List<int>(8);
                    list.Add(vi);
                }
                var quads = new Dictionary<int, Vector3[]>();
                var quadUV = new Dictionary<int, Vector2[]>();
                var quadNormal = new Dictionary<int, Vector3>();
                if (hasUV)
                {
                    foreach (var kv in members)
                    {
                        if (triCountOf[kv.Key] <= 2) continue;
                        if (FitQuad(v, n, uv, hasN, kv.Value, out var corners, out var cornerUV, out var normal))
                        {
                            quads[kv.Key] = corners; quadUV[kv.Key] = cornerUV; quadNormal[kv.Key] = normal;
                        }
                    }
                }
                long afterQuads = 0;
                foreach (var kv in triCountOf) afterQuads += quads.ContainsKey(kv.Key) ? 2 : kv.Value;
                float keep = Mathf.Clamp01(triCount * fraction / Mathf.Max(1f, afterQuads));
                grow = keep >= 1f ? 1f : Mathf.Clamp(1f / Mathf.Sqrt(keep), 1f, maxGrow);

                foreach (var kv in quads)
                {
                    int r = kv.Key;
                    if (keep < 1f && IslandNoise.Hash01(r, r >> 9, 17) > keep) continue;
                    var e = pieces[r];
                    Vector3 center = e.sum / e.count;
                    int start = outV.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        outV.Add(center + (kv.Value[k] - center) * grow);
                        if (hasN) outN.Add(quadNormal[r]);
                        outUV.Add(quadUV[r][k]);
                    }
                    result.Add(start); result.Add(start + 1); result.Add(start + 2);
                    result.Add(start); result.Add(start + 2); result.Add(start + 3);
                }
                var remap = new Dictionary<int, int>();
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int r = Find(tris[i]);
                    if (quads.ContainsKey(r)) continue;
                    if (keep < 1f && IslandNoise.Hash01(r, r >> 9, 17) > keep) continue;
                    var e = pieces[r];
                    Vector3 center = e.sum / e.count;
                    for (int k = 0; k < 3; k++)
                    {
                        int old = tris[i + k];
                        if (!remap.TryGetValue(old, out int idx))
                        {
                            idx = outV.Count;
                            remap[old] = idx;
                            outV.Add(center + (v[old] - center) * grow);
                            if (hasN) outN.Add(n[old]);
                            if (hasUV) outUV.Add(uv[old]);
                        }
                        result.Add(idx);
                    }
                }
            }
            else
            {
                // One welded mass (typical for needles): drop single triangles and enlarge the rest.
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int t = i / 3;
                    if (IslandNoise.Hash01(t, t >> 9, 19) > fraction) continue;
                    Vector3 center = (v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f;
                    for (int k = 0; k < 3; k++)
                    {
                        int old = tris[i + k];
                        result.Add(outV.Count);
                        outV.Add(center + (v[old] - center) * grow);
                        if (hasN) outN.Add(n[old]);
                        if (hasUV) outUV.Add(uv[old]);
                    }
                }
            }
            return result.Count >= 9 ? result : null;
        }

        /// <summary>
        /// Fits a flat rectangle to one leaf: finds the straight-line mapping from its texture coordinates to
        /// its positions, then places the corners of its texture rectangle. False when the piece isn't flat
        /// enough (a twig with several leaves, a curled frond) and should keep its own triangles.
        /// </summary>
        static bool FitQuad(Vector3[] v, Vector3[] n, Vector2[] uv, bool hasN, List<int> verts,
            out Vector3[] corners, out Vector2[] cornerUV, out Vector3 normal)
        {
            corners = null; cornerUV = null; normal = Vector3.up;
            int count = verts.Count;
            if (count < 4) return false;
            double mu = 0, mv = 0, mx = 0, my = 0, mz = 0;
            float uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue;
            Vector3 nSum = Vector3.zero;
            foreach (int i in verts)
            {
                mu += uv[i].x; mv += uv[i].y; mx += v[i].x; my += v[i].y; mz += v[i].z;
                uMin = Mathf.Min(uMin, uv[i].x); uMax = Mathf.Max(uMax, uv[i].x);
                vMin = Mathf.Min(vMin, uv[i].y); vMax = Mathf.Max(vMax, uv[i].y);
                if (hasN) nSum += n[i];
            }
            mu /= count; mv /= count; mx /= count; my /= count; mz /= count;
            double suu = 0, suv = 0, svv = 0, sux = 0, suy = 0, suz = 0, svx = 0, svy = 0, svz = 0, spread = 0;
            foreach (int i in verts)
            {
                double du = uv[i].x - mu, dv = uv[i].y - mv, dx = v[i].x - mx, dy = v[i].y - my, dz = v[i].z - mz;
                suu += du * du; suv += du * dv; svv += dv * dv;
                sux += du * dx; suy += du * dy; suz += du * dz;
                svx += dv * dx; svy += dv * dy; svz += dv * dz;
                spread += dx * dx + dy * dy + dz * dz;
            }
            double det = suu * svv - suv * suv;
            if (det < 1e-14 || det < 1e-6 * suu * svv || spread < 1e-12) return false;
            // position = mean + A * du + B * dv
            var A = new Vector3((float)((sux * svv - svx * suv) / det), (float)((suy * svv - svy * suv) / det), (float)((suz * svv - svz * suv) / det));
            var B = new Vector3((float)((svx * suu - sux * suv) / det), (float)((svy * suu - suy * suv) / det), (float)((svz * suu - suz * suv) / det));
            var mean = new Vector3((float)mx, (float)my, (float)mz);
            double error = 0;
            foreach (int i in verts)
            {
                var fit = mean + A * (float)(uv[i].x - mu) + B * (float)(uv[i].y - mv);
                error += (fit - v[i]).sqrMagnitude;
            }
            if (error > 0.08 * spread) return false;

            cornerUV = new[] { new Vector2(uMin, vMin), new Vector2(uMax, vMin), new Vector2(uMax, vMax), new Vector2(uMin, vMax) };
            corners = new Vector3[4];
            for (int k = 0; k < 4; k++)
                corners[k] = mean + A * (float)(cornerUV[k].x - mu) + B * (float)(cornerUV[k].y - mv);
            normal = hasN && nSum.sqrMagnitude > 1e-8f ? nSum.normalized : Vector3.Cross(A, B).normalized;
            return true;
        }

        // ================= helpers =================

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static float Longest(Vector3 s) => Mathf.Max(s.x, Mathf.Max(s.y, s.z));

        static Bounds Union(List<Part> parts)
        {
            var b = parts[0].bounds;
            for (int i = 1; i < parts.Count; i++) b.Encapsulate(parts[i].bounds);
            return b;
        }

        static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return result;
        }

        /// <summary>Groups parts whose footprints overlap.</summary>
        static List<List<Part>> Cluster(List<Part> parts)
        {
            int n = parts.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                var a = parts[i].bounds; var b = parts[j].bounds;
                float ax = a.extents.x * 0.8f, az = a.extents.z * 0.8f, bx = b.extents.x * 0.8f, bz = b.extents.z * 0.8f;
                bool overlap = Mathf.Abs(a.center.x - b.center.x) < ax + bx && Mathf.Abs(a.center.z - b.center.z) < az + bz;
                if (overlap) parent[Find(i)] = Find(j);
            }
            var map = new Dictionary<int, List<Part>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!map.TryGetValue(r, out var list)) map[r] = list = new List<Part>();
                list.Add(parts[i]);
            }
            return new List<List<Part>>(map.Values);
        }
    }
}
