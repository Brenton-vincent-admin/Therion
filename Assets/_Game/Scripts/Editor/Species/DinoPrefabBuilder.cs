using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Dino.Species.Editor
{
    /// <summary>
    /// Turns the model files in Assets/_Game/Art/Dinos/&lt;id&gt;/ into ready-to-use prefabs and hooks them to
    /// their species. For every folder that has &lt;id&gt;.fbx it:
    ///  1. sets the import settings (Generic rig, clip names and looping, normal map, linear mask),
    ///  2. makes a Dino/DinoSkin material from &lt;id&gt;_Albedo / _Normal / _Mask,
    ///  3. makes an Animator Controller (Idle/Walk/Run blend on "Speed", plus the triggers and bools the game sets),
    ///  4. saves Assets/_Game/Prefabs/Dinos/&lt;id&gt;.prefab with a LOD group and DinoSkin,
    ///  5. assigns it to Species_&lt;id&gt;.modelPrefab.
    /// Safe to re-run whenever a model is regenerated.
    /// </summary>
    public static class DinoPrefabBuilder
    {
        public const string ArtRoot = "Assets/_Game/Art/Dinos";
        public const string PrefabRoot = "Assets/_Game/Prefabs/Dinos";

        static readonly string[] LoopClips = { "Idle", "Walk", "Run", "Rest" };
        static readonly string[] TriggerClips = { "Attack", "Ability", "Eat", "Drink", "Hurt" };

        /// <summary>Speeds the walk and run clips were animated at (m/s), from Art/Dinos/README.md. Used to limit foot sliding.</summary>
        static readonly Dictionary<string, Vector2> AuthoredWalkRun = new Dictionary<string, Vector2>
        {
            { "trex", new Vector2(2.4f, 7.0f) },
            { "carnotaurus", new Vector2(2.0f, 9.0f) },
        };

        [MenuItem("Dino/Species/Build Dino Prefabs From Models")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot))
            {
                Debug.LogWarning($"[Species] No model folder at {ArtRoot} yet.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SpeciesCatalog>(SpeciesAssetBuilder.CatalogPath) == null)
                SpeciesAssetBuilder.Rebuild();
            if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(PatternMaskGenerator.OutputPath) == null)
                PatternMaskGenerator.Generate();

            int built = 0;
            foreach (var folder in AssetDatabase.GetSubFolders(ArtRoot))
            {
                string id = Path.GetFileName(folder);
                if (!File.Exists($"{folder}/{id}.fbx")) continue;
                try { if (Build(id)) built++; }
                catch (System.Exception e) { Debug.LogError($"[Species] Could not build the '{id}' prefab: {e}"); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Species] Built {built} dino prefab(s) in {PrefabRoot}.");
        }

        public static bool Build(string id)
        {
            string folder = $"{ArtRoot}/{id}";
            string fbxPath = $"{folder}/{id}.fbx";

            ConfigureModel(fbxPath);
            ConfigureTextures(folder, id);

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    clips[CleanClipName(clip.name)] = clip;

            var species = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>($"{SpeciesAssetBuilder.SpeciesFolder}/Species_{id}.asset");
            var material = BuildMaterial(folder, id);
            var controller = BuildController(folder, id, clips, species);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) { Debug.LogError($"[Species] {fbxPath} did not import as a model."); return false; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            instance.name = id;
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    r.sharedMaterial = material;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
                }
                BuildLods(instance, renderers);

                if (instance.GetComponent<DinoSkin>() == null) instance.AddComponent<DinoSkin>();

                EnsureFolder(PrefabRoot);
                string prefabPath = $"{PrefabRoot}/{id}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

                if (species != null)
                {
                    species.modelPrefab = prefab;
                    species.adultModelScale = 1f; // models are authored at real adult size
                    EditorUtility.SetDirty(species);
                }
                else Debug.LogWarning($"[Species] No Species_{id}.asset: prefab saved but not assigned to a species.");
            }
            finally { Object.DestroyImmediate(instance); }
            return true;
        }

        static string CleanClipName(string name)
        {
            int bar = name.LastIndexOf('|');   // Blender exports takes as "Rig|Walk"
            return bar >= 0 ? name.Substring(bar + 1) : name;
        }

        static void ConfigureModel(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.importAnimation = true;

            var source = importer.defaultClipAnimations;
            var result = new List<ModelImporterClipAnimation>();
            foreach (var c in source)
            {
                string clean = CleanClipName(c.name);
                c.name = clean;
                c.loopTime = System.Array.IndexOf(LoopClips, clean) >= 0;
                c.loopPose = c.loopTime;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
                result.Add(c);
            }
            if (result.Count > 0) importer.clipAnimations = result.ToArray();
            importer.SaveAndReimport();
        }

        static void ConfigureTextures(string folder, string id)
        {
            SetTexture($"{folder}/{id}_Albedo.png", TextureImporterType.Default, true);
            SetTexture($"{folder}/{id}_Normal.png", TextureImporterType.NormalMap, false);
            SetTexture($"{folder}/{id}_Mask.png", TextureImporterType.Default, false);
        }

        static void SetTexture(string path, TextureImporterType type, bool srgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;

            // Phones: 1024 with ASTC keeps a realistic dino around 1 MB per texture.
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        static Material BuildMaterial(string folder, string id)
        {
            string path = $"{folder}/{id}_Skin.mat";
            var shader = Shader.Find("Dino/DinoSkin");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{id}_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{id}_Normal.png");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{id}_Mask.png");
            var patterns = AssetDatabase.LoadAssetAtPath<Texture2DArray>(PatternMaskGenerator.OutputPath);

            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null && mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
            if (mask != null && mat.HasProperty("_ZoneMask")) mat.SetTexture("_ZoneMask", mask);
            if (patterns != null && mat.HasProperty("_PatternMasks")) mat.SetTexture("_PatternMasks", patterns);
            if (mat.HasProperty("_DetailAverage"))
                mat.SetFloat("_DetailAverage", Mathf.Clamp(AverageLuminance($"{folder}/{id}_Albedo.png"), 0.05f, 1f));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Average brightness of the albedo, so the recolor shader keeps the texture's detail centered.</summary>
        static float AverageLuminance(string path)
        {
            if (!File.Exists(path)) return 0.35f;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) return 0.35f;
                var px = tex.GetPixels32();
                if (px.Length == 0) return 0.35f;
                double sum = 0; int step = Mathf.Max(1, px.Length / 65536);
                int n = 0;
                for (int i = 0; i < px.Length; i += step)
                {
                    sum += (0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b) / 255.0;
                    n++;
                }
                return (float)(sum / n);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        static AnimatorController BuildController(string folder, string id, Dictionary<string, AnimationClip> clips, SpeciesDefinition species)
        {
            string path = $"{folder}/{id}.controller";
            AssetDatabase.DeleteAsset(path); // rebuild from scratch; the prefab is re-saved right after with the new reference
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // Parameters the game sets (DinoMotor, WildDinoBrain, SignatureAbilityRunner). Speed is 0..1 of run speed.
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var b in new[] { "Grounded", "Swimming", "Sneaking", "Fainted", "Resting" })
                controller.AddParameter(b, AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Jump", "Attack", "Ability", "Eat", "Drink", "Hurt" })
                controller.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            clips.TryGetValue("Idle", out var idle);
            clips.TryGetValue("Walk", out var walk);
            clips.TryGetValue("Run", out var run);

            float walkAt = species != null && species.runSpeed > 0f ? Mathf.Clamp(species.walkSpeed / species.runSpeed, 0.1f, 0.9f) : 0.4f;
            float walkScale = 1f, runScale = 1f;
            if (species != null && AuthoredWalkRun.TryGetValue(id, out var authored))
            {
                // Play the clips faster or slower so the feet match the game's speeds (clamped so it never looks silly).
                walkScale = Mathf.Clamp(species.walkSpeed / Mathf.Max(0.1f, authored.x), 0.6f, 1.75f);
                runScale = Mathf.Clamp(species.runSpeed / Mathf.Max(0.1f, authored.y), 0.6f, 1.75f);
            }

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, walkAt);
            if (run != null) tree.AddChild(run, 1f);
            var children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion == walk) children[i].timeScale = walkScale;
                else if (children[i].motion == run && run != null) children[i].timeScale = runScale;
            }
            tree.children = children;
            sm.defaultState = locomotion;

            foreach (var name in TriggerClips)
            {
                if (!clips.TryGetValue(name, out var clip)) continue;
                var state = sm.AddState(name);
                state.motion = clip;
                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, name);
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Fainted");
                enter.hasExitTime = false; enter.duration = 0.1f; enter.canTransitionToSelf = false;
                var exit = state.AddTransition(locomotion);
                exit.hasExitTime = true; exit.exitTime = 0.9f; exit.duration = 0.15f;
            }

            if (clips.TryGetValue("Rest", out var rest))
            {
                var state = sm.AddState("Rest");
                state.motion = rest;
                var enter = locomotion.AddTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Resting");
                enter.hasExitTime = false; enter.duration = 0.4f;
                var exit = state.AddTransition(locomotion);
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Resting");
                exit.hasExitTime = false; exit.duration = 0.4f;
            }

            if (clips.TryGetValue("Faint", out var faint))
            {
                // Flop over and stay down until revived. Never a death pose.
                var state = sm.AddState("Faint");
                state.motion = faint;
                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Fainted");
                enter.hasExitTime = false; enter.duration = 0.15f; enter.canTransitionToSelf = false;
                var exit = state.AddTransition(locomotion);
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Fainted");
                exit.hasExitTime = false; exit.duration = 0.3f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BuildLods(GameObject root, Renderer[] renderers)
        {
            var levels = new List<Renderer>[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            foreach (var r in renderers)
            {
                string n = r.name.ToUpperInvariant();
                int level = n.EndsWith("_LOD1") ? 1 : n.EndsWith("_LOD2") ? 2 : 0;
                levels[level].Add(r);
            }
            if (levels[1].Count == 0 && levels[2].Count == 0) return; // single mesh: no LOD group needed

            var group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();
            var lods = new List<LOD> { new LOD(0.30f, levels[0].ToArray()) };
            if (levels[1].Count > 0) lods.Add(new LOD(0.10f, levels[1].ToArray()));
            if (levels[2].Count > 0) lods.Add(new LOD(0.015f, levels[2].ToArray()));
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();
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
