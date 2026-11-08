using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Applies Android player settings and exposes one-click APK/AAB build menu items.
    /// Run "Null Frontier/Build/Android APK" after installing the Android Build Support
    /// module. Debug keystore is used automatically; replace before any store release.
    /// </summary>
    public static class AndroidBuildSettings
    {
        private const string ApkOutput = "Builds/NullFrontier.apk";
        private const string AabOutput = "Builds/NullFrontier.aab";

        [MenuItem("Null Frontier/Build/Android APK")]
        public static void BuildApk()
        {
            if (!ValidateForBuild() || !EnsureAndroidTarget()) return;
            Configure();
            var report = BuildPipeline.BuildPlayer(GetScenes(), ApkOutput, BuildTarget.Android, BuildOptions.None);
            LogReport(report, ApkOutput);
        }

        [MenuItem("Null Frontier/Build/Android AAB")]
        public static void BuildAab()
        {
            if (!ValidateForBuild() || !EnsureAndroidTarget()) return;
            Configure();
            Debug.LogWarning("[NullFrontier] This AAB is debug-signed. Configure a release " +
                             "keystore before uploading it to a store.");
            EditorUserBuildSettings.buildAppBundle = true;
            var report = BuildPipeline.BuildPlayer(GetScenes(), AabOutput, BuildTarget.Android, BuildOptions.None);
            EditorUserBuildSettings.buildAppBundle = false;
            LogReport(report, AabOutput);
        }

        public static void Configure()
        {
            PlayerSettings.companyName = "NullFrontier";
            PlayerSettings.productName = "NULL FRONTIER";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.nullfrontier.game");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.useCustomKeystore = false; // debug keystore; set before store release

            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
        }

        /// <summary>
        /// Blocks a device build that would ship a loopback endpoint or omit the CC BY
        /// attribution. These are configuration errors, not warnings.
        /// </summary>
        private static bool ValidateForBuild()
        {
            bool ok = true;
            if (!File.Exists("Assets/StreamingAssets/CREDITS.txt"))
            {
                Debug.LogError("[NullFrontier] Build blocked: StreamingAssets/CREDITS.txt is missing; " +
                               "the Earth texture attribution must ship with the app.");
                ok = false;
            }

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled || !File.Exists(scene.path)) continue;
                string text = File.ReadAllText(scene.path);
                if (text.Contains("localhost:3000"))
                {
                    Debug.LogError($"[NullFrontier] Build blocked: {scene.path} contains a " +
                                   "localhost backend endpoint. Rebuild the scene from SceneBuilder.");
                    ok = false;
                }
                if (text.Contains("apiBaseUrl: http://"))
                {
                    Debug.LogError($"[NullFrontier] Build blocked: {scene.path} uses a cleartext " +
                                   "backend URL. Devices require HTTPS.");
                    ok = false;
                }
            }

            return ok;
        }

        private static string[] GetScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var paths = new System.Collections.Generic.List<string>();
            foreach (var scene in scenes)
            {
                if (scene.enabled) paths.Add(scene.path);
            }
            Debug.Log($"[NullFrontier] Build scenes (index 0 is the boot scene): {string.Join(" | ", paths)}");
            return paths.ToArray();
        }

        private static bool EnsureAndroidTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return true;
            Debug.Log("[NullFrontier] Switching active build target to Android. The Editor will recompile scripts...");
            if (EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android)) return true;
            Debug.LogError("[NullFrontier] Could not switch to Android. Android Build Support is not loaded in this Editor session - restart Unity after the module install finishes.");
            return false;
        }

        private static void LogReport(BuildReport report, string output)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[NullFrontier] Build succeeded: {output} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime})");
            }
            else
            {
                Debug.LogError($"[NullFrontier] Build failed: {report.summary.result}. See Console for errors.");
            }
        }
    }
}
