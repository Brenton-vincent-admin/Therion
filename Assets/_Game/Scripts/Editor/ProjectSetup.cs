using Dino.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Dino.EditorTools
{
    /// <summary>
    /// Applies the game's name, package id and Android settings from Dino.Core.GameInfo.
    /// Runs automatically whenever the editor loads (including CI batch mode), and from the menu
    /// "Dino/Project Setup". Only writes settings that differ, so it is cheap and idempotent.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        static ProjectSetup() => EditorApplication.delayCall += () => Apply(false);

        [MenuItem("Dino/Project Setup")]
        static void ApplyFromMenu() => Apply(true);

        public static void Apply() => Apply(true);

        public static void Apply(bool log)
        {
            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> setter)
            {
                if (Equals(current, wanted)) return;
                setter(wanted);
                changed = true;
            }

            Set(PlayerSettings.productName, GameInfo.ProductName, v => PlayerSettings.productName = v);
            Set(PlayerSettings.companyName, GameInfo.CompanyName, v => PlayerSettings.companyName = v);
            var android = NamedBuildTarget.Android;
            Set(PlayerSettings.GetApplicationIdentifier(android), GameInfo.AndroidPackageId,
                v => PlayerSettings.SetApplicationIdentifier(android, v));
            Set(PlayerSettings.GetScriptingBackend(android), ScriptingImplementation.IL2CPP,
                v => PlayerSettings.SetScriptingBackend(android, v));
            Set(PlayerSettings.Android.targetArchitectures, AndroidArchitecture.ARM64,
                v => PlayerSettings.Android.targetArchitectures = v);
            Set(PlayerSettings.Android.minSdkVersion, AndroidSdkVersions.AndroidApiLevel26,
                v => PlayerSettings.Android.minSdkVersion = v);
            Set(PlayerSettings.defaultInterfaceOrientation, UIOrientation.AutoRotation,
                v => PlayerSettings.defaultInterfaceOrientation = v);
            Set(PlayerSettings.allowedAutorotateToPortrait, false, v => PlayerSettings.allowedAutorotateToPortrait = v);
            Set(PlayerSettings.allowedAutorotateToPortraitUpsideDown, false, v => PlayerSettings.allowedAutorotateToPortraitUpsideDown = v);
            Set(PlayerSettings.allowedAutorotateToLandscapeLeft, true, v => PlayerSettings.allowedAutorotateToLandscapeLeft = v);
            Set(PlayerSettings.allowedAutorotateToLandscapeRight, true, v => PlayerSettings.allowedAutorotateToLandscapeRight = v);
            changed |= EnsureInputHandlerBoth();
            // Kid safety: no internet permission is requested unless multiplayer code needs it (the Net thread decides).

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[Dino] Project settings applied: {GameInfo.ProductName} ({GameInfo.AndroidPackageId}). Commit ProjectSettings/ProjectSettings.asset.");
            }
            else if (log) Debug.Log("[Dino] Project settings already up to date.");
        }

        /// <summary>
        /// Sets Player Settings > Active Input Handling to "Both" (2): the Input System package for
        /// touch/gamepad plus the old manager for anything legacy. Takes effect after an editor restart.
        /// </summary>
        static bool EnsureInputHandlerBoth()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == 2) return false;
            prop.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!Application.isBatchMode &&
                EditorUtility.DisplayDialog("Therion setup",
                    "Input handling was switched to \"Both\" so touch and controller input work. Unity needs to restart once.",
                    "Restart now", "Later"))
                EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
            return true;
        }
    }
}
