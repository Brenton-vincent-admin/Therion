using UnityEngine;
using UnityEngine.Rendering;

namespace Dino.World
{
    /// <summary>
    /// Fits the island to the current graphics level (Edit > Project Settings > Quality, or
    /// QualitySettings.SetQualityLevel at runtime): how far grass and trees draw, how detailed the
    /// ground mesh is, and whether water may use depth and refraction. Low is for small phones,
    /// Medium for mid-range phones, High for a PC. Added to the island root by the Island Builder.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-400)]
    public class WorldQualityTier : MonoBehaviour
    {
        public enum Tier { Low, Medium, High }

        public Terrain terrain;
        public DistanceCuller[] cullers;

        [System.Serializable]
        public class Settings
        {
            public float grassDistance = 50f;
            [Range(0f, 1f)] public float grassDensity = 0.5f;
            public float treeDistance = 350f;
            public float terrainPixelError = 6f;
            public float groundDetailDistance = 180f;
            public float foodDistance = 120f;
        }

        public Settings low = new Settings { grassDistance = 28f, grassDensity = 0.25f, treeDistance = 220f, terrainPixelError = 10f, groundDetailDistance = 90f, foodDistance = 90f };
        public Settings medium = new Settings { grassDistance = 48f, grassDensity = 0.5f, treeDistance = 380f, terrainPixelError = 6f, groundDetailDistance = 200f, foodDistance = 130f };
        public Settings high = new Settings { grassDistance = 95f, grassDensity = 1f, treeDistance = 1200f, terrainPixelError = 3f, groundDetailDistance = 900f, foodDistance = 320f };

        public static Tier Current { get; private set; } = Tier.Medium;

        static readonly int WaterDepth = Shader.PropertyToID("_TherionWaterDepth");
        static readonly int WaterRefraction = Shader.PropertyToID("_TherionWaterRefraction");

        int _appliedLevel = -1;
        RenderPipelineAsset _appliedPipeline;
        float _nextCheck;

        void OnEnable() => Apply();

        void Update()
        {
            if (Time.unscaledTime < _nextCheck && Application.isPlaying) return;
            _nextCheck = Time.unscaledTime + 1f;
            if (QualitySettings.GetQualityLevel() != _appliedLevel || GraphicsSettings.currentRenderPipeline != _appliedPipeline) Apply();
        }

        public void Apply()
        {
            _appliedLevel = QualitySettings.GetQualityLevel();
            _appliedPipeline = GraphicsSettings.currentRenderPipeline;
            Current = DetectTier(_appliedPipeline, _appliedLevel);
            var s = Current == Tier.High ? high : Current == Tier.Low ? low : medium;

            Shader.SetGlobalFloat(WaterDepth, ReadBool(_appliedPipeline, "supportsCameraDepthTexture") ? 1f : 0f);
            Shader.SetGlobalFloat(WaterRefraction, ReadBool(_appliedPipeline, "supportsCameraDepthTexture") && ReadBool(_appliedPipeline, "supportsCameraOpaqueTexture") ? 1f : 0f);

            if (terrain)
            {
                terrain.detailObjectDistance = s.grassDistance;
                terrain.detailObjectDensity = s.grassDensity;
                terrain.treeDistance = s.treeDistance;
                terrain.treeBillboardDistance = s.treeDistance;
                terrain.heightmapPixelError = s.terrainPixelError;
                terrain.basemapDistance = s.groundDetailDistance;
            }
            if (cullers != null)
            {
                foreach (var c in cullers)
                {
                    if (!c || Mathf.Approximately(c.visibleDistance, s.foodDistance)) continue;
                    c.visibleDistance = s.foodDistance;
                    if (Application.isPlaying && c.isActiveAndEnabled) { c.enabled = false; c.enabled = true; }
                }
            }
        }

        /// <summary>The graphics menu names its pipeline assets ..._Low, ..._Medium, ..._High; otherwise go by level.</summary>
        static Tier DetectTier(RenderPipelineAsset pipeline, int level)
        {
            if (pipeline)
            {
                string n = pipeline.name;
                if (n.EndsWith("_High")) return Tier.High;
                if (n.EndsWith("_Medium")) return Tier.Medium;
                if (n.EndsWith("_Low")) return Tier.Low;
            }
            int count = Mathf.Max(1, QualitySettings.names.Length);
            int third = level * 3 / count;
            return third <= 0 ? Tier.Low : third == 1 ? Tier.Medium : Tier.High;
        }

        // The runtime assembly doesn't reference URP, so read the two flags by name.
        static bool ReadBool(RenderPipelineAsset pipeline, string property)
        {
            if (!pipeline) return false;
            var p = pipeline.GetType().GetProperty(property);
            if (p == null || p.PropertyType != typeof(bool)) return false;
            try { return (bool)p.GetValue(pipeline); }
            catch { return false; }
        }
    }
}
