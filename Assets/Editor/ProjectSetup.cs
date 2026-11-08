using UnityEditor;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Project-level settings the scenes depend on: the CoverPoint tag
    /// (DigitalSoldier's FindGameObjectsWithTag throws when it is undefined) and the
    /// layers the NavMesh bake filters on. Idempotent - safe to run every build.
    /// </summary>
    public static class ProjectSetup
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        public static readonly string[] RequiredTags = { "CoverPoint" };

        /// <summary>
        /// Ground = walkable, Cover = blocking art, Art = visual only (excluded from
        /// the NavMesh bake), Enemy = spawned AI bodies.
        /// </summary>
        public static readonly string[] RequiredLayers = { "Ground", "Cover", "Art", "Enemy" };

        [MenuItem("Null Frontier/Apply Project Settings")]
        public static void Apply()
        {
            EnsureTags();
            EnsureLayers();
            ConfigurePlayer();
            Debug.Log("[NullFrontier] Project tags, layers and player defaults applied.");
        }

        public static int GroundLayer => LayerMask.NameToLayer("Ground");
        public static int CoverLayer => LayerMask.NameToLayer("Cover");
        public static int ArtLayer => LayerMask.NameToLayer("Art");
        public static int EnemyLayer => LayerMask.NameToLayer("Enemy");

        /// <summary>Layer mask used by the NavMesh bake: walkable plus blocking.</summary>
        public static int NavigationLayerMask =>
            (1 << Mathf.Max(0, GroundLayer)) | (1 << Mathf.Max(0, CoverLayer));

        /// <summary>
        /// What the third-person camera bounces off: buildings and decks only. Kit
        /// clutter (containers, pipes, lamps - all on Art) is dense enough to collapse the
        /// rig to first person the moment the player stands near a crate, which is worse
        /// than occasionally clipping a thin prop.
        /// </summary>
        public static int CameraBlockingLayerMask => NavigationLayerMask;

        private static void EnsureTags()
        {
            var manager = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (manager == null || manager.Length == 0)
            {
                Debug.LogError($"[NullFrontier] {TagManagerPath} not readable - tags were not added.");
                return;
            }

            var serialized = new SerializedObject(manager[0]);
            var tags = serialized.FindProperty("tags");
            if (tags == null) return;

            bool changed = false;
            foreach (string wanted in RequiredTags)
            {
                if (Contains(tags, wanted)) continue;
                int index = tags.arraySize;
                tags.InsertArrayElementAtIndex(index);
                tags.GetArrayElementAtIndex(index).stringValue = wanted;
                changed = true;
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureLayers()
        {
            var manager = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (manager == null || manager.Length == 0) return;

            var serialized = new SerializedObject(manager[0]);
            var layers = serialized.FindProperty("layers");
            if (layers == null) return;

            bool changed = false;
            foreach (string wanted in RequiredLayers)
            {
                if (LayerMask.NameToLayer(wanted) >= 0) continue;
                // 0-2 are built in and 5 is UI; the first blank slot is free.
                for (int i = 3; i < layers.arraySize; i++)
                {
                    var element = layers.GetArrayElementAtIndex(i);
                    if (i == 5 || !string.IsNullOrEmpty(element.stringValue)) continue;
                    element.stringValue = wanted;
                    changed = true;
                    break;
                }
            }
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // NameToLayer reads the in-memory tag manager, so re-validate after writing.
                AssetDatabase.SaveAssets();
            }
        }

        private static void ConfigurePlayer()
        {
            // The whole HUD layout assumes a landscape touch screen.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            Application.targetFrameRate = 60;

            // The title screen prints Application.version, and an empty version name
            // comes back as "1.0" - the build number the player sees has to be real.
            PlayerSettings.bundleVersion = "1.0.0";
        }

        private static bool Contains(SerializedProperty array, string value)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).stringValue == value) return true;
            return false;
        }
    }
}
