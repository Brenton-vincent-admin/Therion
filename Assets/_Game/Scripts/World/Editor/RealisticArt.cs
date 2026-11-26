using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dino.World.EditorTools
{
    /// <summary>
    /// Generated art that goes with the realistic look and needs no download: blade-by-blade grass tufts
    /// that sway in the wind, the water materials, and a reflection cubemap baked from the sky.
    /// Everything lands in Generated/Realistic and is rebuilt on every island build.
    /// </summary>
    public static class RealisticArt
    {
        public const string Root = IslandBuilder.GeneratedRoot + "/Realistic";

        public class Grass
        {
            public GameObject shortTuft, tallTuft;
        }

        /// <summary>Null when the Therion/Foliage shader isn't available (the builder then uses the placeholder tuft).</summary>
        public static Grass BuildGrass(WorldArtSet art)
        {
            var shader = Shader.Find("Therion/Foliage");
            if (!shader) return null;
            PlaceholderArt.EnsureFolder(Root);
            var g = new Grass();
            Color dry = Color.Lerp(art.grassTip, new Color(0.62f, 0.55f, 0.24f), 0.6f);
            var shortMat = GrassMaterial(shader, "M_GrassShort", art.grassRoot, art.grassTip, dry, 0.35f, 0.45f, 0.12f);
            var tallMat = GrassMaterial(shader, "M_GrassTall", art.grassRoot * 0.9f, Color.Lerp(art.grassTip, dry, 0.35f), dry, 0.5f, 1.0f, 0.3f);
            g.shortTuft = TuftPrefab("GrassShort", Tuft("GrassShort", 34, 0.42f, 0.18f, 0.42f, 0.028f, 11), shortMat);
            g.tallTuft = TuftPrefab("GrassTall", Tuft("GrassTall", 16, 0.34f, 0.55f, 0.95f, 0.032f, 23), tallMat);
            return g;
        }

        static Material GrassMaterial(Shader shader, string name, Color root, Color tip, Color dry, float dryAmount, float height, float bend)
        {
            string path = $"{Root}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Gradient", 1f);
            m.SetFloat("_NormalUp", 1f);
            m.SetColor("_RootColor", Opaque(root));
            m.SetColor("_TipColor", Opaque(tip));
            m.SetColor("_DryColor", Opaque(dry));
            m.SetFloat("_DryAmount", dryAmount);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Translucency", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_BendHeight", height);
            m.SetFloat("_WindStrength", bend);
            m.SetFloat("_Flutter", 0.006f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        /// <summary>A clump of single grass blades: each a tapered, curved strip. uv.y runs root (0) to tip (1).</summary>
        static Mesh Tuft(string name, int blades, float radius, float minHeight, float maxHeight, float width, int seed)
        {
            const int Segments = 3;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int b = 0; b < blades; b++)
            {
                float a = Generation.IslandNoise.Hash01(b, 1, seed) * Mathf.PI * 2f;
                float d = Mathf.Sqrt(Generation.IslandNoise.Hash01(b, 2, seed)) * radius;
                var root = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Mathf.Lerp(minHeight, maxHeight, Generation.IslandNoise.Hash01(b, 3, seed));
                float leanAngle = a + (Generation.IslandNoise.Hash01(b, 4, seed) - 0.5f) * 2.4f; // mostly outward
                var lean = new Vector3(Mathf.Cos(leanAngle), 0f, Mathf.Sin(leanAngle));
                float leanAmount = h * Mathf.Lerp(0.15f, 0.55f, Generation.IslandNoise.Hash01(b, 5, seed));
                float faceAngle = leanAngle + Mathf.PI * 0.5f + (Generation.IslandNoise.Hash01(b, 6, seed) - 0.5f) * 1.2f;
                var side = new Vector3(Mathf.Cos(faceAngle), 0f, Mathf.Sin(faceAngle));
                var normal = (Vector3.up + lean * 0.25f).normalized;

                int start = v.Count;
                for (int s = 0; s <= Segments; s++)
                {
                    float k = (float)s / Segments;
                    var center = root + Vector3.up * (h * k * (1f - 0.12f * k)) + lean * (leanAmount * k * k);
                    if (s == Segments)
                    {
                        v.Add(center); n.Add(normal); uv.Add(new Vector2(0.5f, 1f));
                    }
                    else
                    {
                        float half = width * 0.5f * (1f - 0.55f * k);
                        v.Add(center - side * half); n.Add(normal); uv.Add(new Vector2(0f, k));
                        v.Add(center + side * half); n.Add(normal); uv.Add(new Vector2(1f, k));
                    }
                }
                for (int s = 0; s < Segments - 1; s++)
                {
                    int i = start + s * 2;
                    t.Add(i); t.Add(i + 2); t.Add(i + 1);
                    t.Add(i + 1); t.Add(i + 2); t.Add(i + 3);
                }
                int last = start + (Segments - 1) * 2;
                t.Add(last); t.Add(last + 2); t.Add(last + 1);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static GameObject TuftPrefab(string name, Mesh mesh, Material mat)
        {
            string meshPath = $"{Root}/Mesh_{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/P_{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ================= water =================

        /// <summary>Null when the Therion/Water shader isn't available.</summary>
        public static Material Water(bool ocean)
        {
            var shader = Shader.Find("Therion/Water");
            if (!shader) return null;
            PlaceholderArt.EnsureFolder(Root);
            string path = $"{Root}/{(ocean ? "M_Ocean" : "M_Water")}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PlaceholderArt.Root + "/T_WaterNormal.png");
            if (normal) m.SetTexture("_BumpMap", normal);
            if (ocean)
            {
                m.SetColor("_ShallowColor", new Color(0.10f, 0.42f, 0.42f, 1f));
                m.SetColor("_DeepColor", new Color(0.01f, 0.09f, 0.17f, 0.97f));
                m.SetFloat("_DepthFade", 7f);
                m.SetFloat("_ShoreFade", 0.4f);
                m.SetFloat("_NormalTiling", 0.06f);
                m.SetFloat("_NormalStrength", 0.6f);
                m.SetVector("_FlowSpeed", new Vector4(0.035f, 0.02f, 0f, 0f));
                m.SetFloat("_FoamWidth", 0.9f);
            }
            else
            {
                m.SetColor("_ShallowColor", new Color(0.20f, 0.40f, 0.33f, 1f));
                m.SetColor("_DeepColor", new Color(0.02f, 0.10f, 0.11f, 0.92f));
                m.SetFloat("_DepthFade", 3.2f);
                m.SetFloat("_ShoreFade", 0.25f);
                m.SetFloat("_NormalTiling", 0.12f);
                m.SetFloat("_NormalStrength", 0.4f);
                m.SetVector("_FlowSpeed", new Vector4(0.02f, 0.013f, 0f, 0f));
                m.SetFloat("_FoamWidth", 0.3f);
            }
            m.renderQueue = (int)RenderQueue.Transparent - 10;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================= sky reflection =================

        /// <summary>
        /// Renders the scene's skybox into a cubemap asset used for reflections everywhere (water, wet mud,
        /// leaves). Saved with the scene, so builds don't depend on a lighting bake. Null if it fails.
        /// </summary>
        public static Cubemap BakeSkyReflection()
        {
            GameObject go = null;
            try
            {
                PlaceholderArt.EnsureFolder(Root);
                go = new GameObject("SkyCapture") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.cullingMask = 0;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.allowHDR = true;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                go.transform.position = new Vector3(0f, 50f, 0f);

                var cube = new Cubemap(256, TextureFormat.RGBAHalf, true);
                if (!cam.RenderToCubemap(cube)) { Object.DestroyImmediate(cube); return null; }
                try { cube.SmoothEdges(); } catch (System.Exception) { }
                cube.Apply(true);

                string path = $"{Root}/SkyReflection.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                if (existing) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(cube, path);
                return cube;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Island] Couldn't bake the sky reflection (using Unity's default instead): " + e.Message);
                return null;
            }
            finally
            {
                if (go) Object.DestroyImmediate(go);
            }
        }
    }
}
