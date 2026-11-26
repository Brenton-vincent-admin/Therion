using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Dino.World.EditorTools
{
    /// <summary>
    /// Three graphics levels for the whole game, as URP pipeline assets in Assets/_Game/Settings:
    /// Low (small phones), Medium (mid-range phones, the Android default) and High (PC: long soft shadows,
    /// ambient occlusion, anti-aliasing, water depth and refraction). Menu: Dino > Graphics > Set Up
    /// Quality Tiers. Safe to run again; it only rewrites its own three assets and the Quality list.
    /// Settings are written by serialized name so a URP update can't break compilation; a renamed
    /// setting is skipped with a warning.
    /// </summary>
    public static class GraphicsTiers
    {
        public const string Folder = "Assets/_Game/Settings";
        const string UrpPackage = "Packages/com.unity.render-pipelines.universal";
        static readonly string[] TierNames = { "Low", "Medium", "High" };

        public static string PipelinePath(int tier) => $"{Folder}/Therion_URP_{TierNames[tier]}.asset";
        static string RendererPath(bool ssao) => $"{Folder}/Therion_Renderer_{(ssao ? "High" : "Mobile")}.asset";

        /// <summary>Runs the setup once (when the High asset doesn't exist yet).</summary>
        public static void EnsureSetUp()
        {
            if (!AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath(2))) SetUp();
        }

        [MenuItem("Dino/Graphics/Set Up Quality Tiers", priority = 0)]
        public static void SetUp()
        {
            try
            {
                PlaceholderArt.EnsureFolder(Folder);
                var mobileRenderer = Renderer(false);
                var highRenderer = Renderer(true);
                var assets = new RenderPipelineAsset[3];
                for (int t = 0; t < 3; t++) assets[t] = Pipeline(t, t == 2 ? highRenderer : mobileRenderer);
                AssetDatabase.SaveAssets();
                ApplyToQualityLevels(assets);
                if (assets[1]) GraphicsSettings.defaultRenderPipeline = assets[1];
                AssetDatabase.SaveAssets();
                Debug.Log("[Graphics] Quality tiers ready: Low and Medium for phones, High for PC. " +
                          "Switch in Edit > Project Settings > Quality (the editor uses the highlighted level).");
            }
            catch (Exception e)
            {
                Debug.LogError("[Graphics] Setting up quality tiers failed, the project keeps its current graphics settings: " + e);
            }
        }

        [MenuItem("Dino/Graphics/Use High In Editor", priority = 20)]
        public static void UseHigh() => QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);

        [MenuItem("Dino/Graphics/Use Medium In Editor (phone look)", priority = 21)]
        public static void UseMedium() => QualitySettings.SetQualityLevel(Mathf.Min(2, QualitySettings.names.Length - 1), true);

        // ---------- renderer ----------

        static ScriptableRendererData Renderer(bool ssao)
        {
            string path = RendererPath(ssao);
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (!data)
            {
                string source = Folder + "/URP_Renderer.asset";
                if (File.Exists(source) && AssetDatabase.CopyAsset(source, path))
                    data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (!data)
                {
                    data = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(data, path);
                    ReloadResources(data);
                }
            }

            var so = new SerializedObject(data);
            var post = so.FindProperty("postProcessData");
            if (post != null && post.objectReferenceValue == null)
            {
                var ppd = AssetDatabase.LoadAssetAtPath<Object>(UrpPackage + "/Runtime/Data/PostProcessData.asset");
                if (ppd) post.objectReferenceValue = ppd;
                else Debug.LogWarning("[Graphics] URP's PostProcessData asset wasn't found; post-processing may be off.");
            }
            // Copy depth right after the solid objects, so water (drawn later) can read how deep it is.
            SetInt(so, "m_CopyDepthMode", 0, quiet: true);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (ssao) AddAmbientOcclusion(data);
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>Adds URP's Screen Space Ambient Occlusion feature (soft contact shadows under plants and rocks).</summary>
        static void AddAmbientOcclusion(ScriptableRendererData data)
        {
            try
            {
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type == null) { Debug.LogWarning("[Graphics] This URP version has no SSAO feature; skipping ambient occlusion."); return; }

                ScriptableObject feature = null;
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(data)))
                    if (sub && type.IsInstanceOfType(sub)) feature = (ScriptableObject)sub;

                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance(type);
                    feature.name = "ScreenSpaceAmbientOcclusion";
                    AssetDatabase.AddObjectToAsset(feature, data);
                    ReloadResources(feature);
                    AssetDatabase.SaveAssets();

                    var so = new SerializedObject(data);
                    var list = so.FindProperty("m_RendererFeatures");
                    var map = so.FindProperty("m_RendererFeatureMap");
                    if (list == null) { Debug.LogWarning("[Graphics] Couldn't register SSAO on the renderer."); return; }
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                    if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
                    {
                        map.arraySize++;
                        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var fso = new SerializedObject(feature);
                SetFloat(fso, "m_Settings.Intensity", 1.1f, quiet: true);
                SetFloat(fso, "m_Settings.Radius", 0.7f, quiet: true);
                SetFloat(fso, "m_Settings.DirectLightingStrength", 0.35f, quiet: true);
                SetInt(fso, "m_Settings.Source", 0, quiet: true); // depth: works with every shader
                fso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] Couldn't add ambient occlusion (the rest still works): " + e.Message);
            }
        }

        // ---------- pipeline assets ----------

        static RenderPipelineAsset Pipeline(int tier, ScriptableRendererData renderer)
        {
            string path = PipelinePath(tier);
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (!asset)
            {
                string source = Folder + "/URP_Pipeline.asset";
                if (File.Exists(source) && AssetDatabase.CopyAsset(source, path))
                    asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (!asset)
                {
                    asset = CreatePipelineInstance(renderer);
                    AssetDatabase.CreateAsset(asset, path);
                    ReloadResources(asset);
                }
            }

            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            if (list != null)
            {
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            }
            SetInt(so, "m_DefaultRendererIndex", 0);
            SetInt(so, "m_SupportsHDR", 1);
            SetInt(so, "m_UseSRPBatcher", 1);
            SetInt(so, "m_MainLightRenderingMode", 1);
            SetInt(so, "m_MainLightShadowsSupported", 1);
            SetInt(so, "m_AdditionalLightsRenderingMode", 1);
            SetInt(so, "m_AdditionalLightShadowsSupported", 0);
            SetFloat(so, "m_CascadeBorder", 0.15f);
            SetInt(so, "m_ColorGradingLutSize", 32);

            switch (tier)
            {
                case 0: // Low: small phones
                    SetInt(so, "m_RequireDepthTexture", 0);
                    SetInt(so, "m_RequireOpaqueTexture", 0);
                    SetInt(so, "m_MSAA", 1);
                    SetFloat(so, "m_RenderScale", 0.8f);
                    SetInt(so, "m_MainLightShadowmapResolution", 1024);
                    SetFloat(so, "m_ShadowDistance", 50f);
                    SetInt(so, "m_ShadowCascadeCount", 1);
                    SetInt(so, "m_SoftShadowsSupported", 0);
                    SetInt(so, "m_ColorGradingMode", 0);
                    break;
                case 1: // Medium: mid-range phones
                    SetInt(so, "m_RequireDepthTexture", 1);
                    SetInt(so, "m_RequireOpaqueTexture", 0);
                    SetInt(so, "m_MSAA", 2);
                    SetFloat(so, "m_RenderScale", 1f);
                    SetInt(so, "m_MainLightShadowmapResolution", 2048);
                    SetFloat(so, "m_ShadowDistance", 110f);
                    SetInt(so, "m_ShadowCascadeCount", 2);
                    SetFloat(so, "m_Cascade2Split", 0.3f);
                    SetInt(so, "m_SoftShadowsSupported", 1);
                    SetInt(so, "m_SoftShadowQuality", 1);
                    SetInt(so, "m_ColorGradingMode", 0);
                    break;
                default: // High: PC
                    SetInt(so, "m_RequireDepthTexture", 1);
                    SetInt(so, "m_RequireOpaqueTexture", 1);
                    SetInt(so, "m_OpaqueDownsampling", 0);
                    SetInt(so, "m_MSAA", 4);
                    SetFloat(so, "m_RenderScale", 1f);
                    SetInt(so, "m_MainLightShadowmapResolution", 4096);
                    SetFloat(so, "m_ShadowDistance", 240f);
                    SetInt(so, "m_ShadowCascadeCount", 4);
                    var split = so.FindProperty("m_Cascade4Split");
                    if (split != null) split.vector3Value = new Vector3(0.06f, 0.18f, 0.45f);
                    SetInt(so, "m_SoftShadowsSupported", 1);
                    SetInt(so, "m_SoftShadowQuality", 3);
                    SetInt(so, "m_ColorGradingMode", 1);
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static RenderPipelineAsset CreatePipelineInstance(ScriptableRendererData renderer)
        {
            // URP's own factory fills in defaults; fall back to a bare instance if it has moved.
            try
            {
                var create = typeof(UniversalRenderPipelineAsset).GetMethod("Create", new[] { typeof(ScriptableRendererData) });
                if (create != null && create.Invoke(null, new object[] { renderer }) is RenderPipelineAsset made) return made;
            }
            catch (Exception) { }
            return ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        }

        /// <summary>Fills in built-in shader/texture references the way URP's "Create" menu does.</summary>
        static void ReloadResources(Object target)
        {
            try
            {
                var type = typeof(RenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.ResourceReloader")
                           ?? typeof(VolumeProfile).Assembly.GetType("UnityEngine.Rendering.ResourceReloader");
                var method = type?.GetMethod("ReloadAllNullIn", new[] { typeof(object), typeof(string) });
                method?.Invoke(null, new object[] { target, UrpPackage });
            }
            catch (Exception) { }
        }

        // ---------- quality levels ----------

        /// <summary>Spreads Low/Medium/High over the project's quality levels (lowest third, middle, top).</summary>
        static void ApplyToQualityLevels(RenderPipelineAsset[] assets)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (settings == null || settings.Length == 0) { Debug.LogWarning("[Graphics] Couldn't open the Quality settings."); return; }
            var so = new SerializedObject(settings[0]);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null || levels.arraySize == 0) { Debug.LogWarning("[Graphics] No quality levels found."); return; }
            int n = levels.arraySize;
            for (int i = 0; i < n; i++)
            {
                int tier = n == 1 ? 1 : i == n - 1 ? 2 : Mathf.Min(2, i * 3 / n);
                var level = levels.GetArrayElementAtIndex(i);
                var rp = level.FindPropertyRelative("customRenderPipeline");
                if (rp != null) rp.objectReferenceValue = assets[tier];
                SetRelFloat(level, "lodBias", tier == 2 ? 2f : tier == 1 ? 1f : 0.7f);
                SetRelInt(level, "maximumLODLevel", tier == 2 ? 0 : 1); // phones skip the most detailed tree meshes
                SetRelInt(level, "anisotropicTextures", tier == 2 ? 2 : tier == 1 ? 1 : 0);
                SetRelInt(level, "realtimeReflectionProbes", tier == 2 ? 1 : 0);
                SetRelInt(level, "vSyncCount", tier == 2 ? 1 : 0);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- helpers ----------

        static void SetInt(SerializedObject so, string name, int value, bool quiet = false)
        {
            var p = so.FindProperty(name);
            if (p == null) { if (!quiet) Debug.LogWarning($"[Graphics] Setting '{name}' not found in this URP version; skipped."); return; }
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
            else if (p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
            else p.intValue = value;
        }

        static void SetFloat(SerializedObject so, string name, float value, bool quiet = false)
        {
            var p = so.FindProperty(name);
            if (p == null) { if (!quiet) Debug.LogWarning($"[Graphics] Setting '{name}' not found in this URP version; skipped."); return; }
            if (p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
            else p.intValue = Mathf.RoundToInt(value);
        }

        static void SetRelInt(SerializedProperty parent, string name, int value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
            else p.intValue = value;
        }

        static void SetRelFloat(SerializedProperty parent, string name, float value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
        }
    }
}
