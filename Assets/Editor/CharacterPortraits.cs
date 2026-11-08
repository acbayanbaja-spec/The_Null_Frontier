using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Renders one portrait per character prefab into Assets/Resources/Portraits so the
    /// roster can show the actual model instead of a coloured rectangle with a name in
    /// it. Rendering normally happens in a throwaway additive scene - the operative's own
    /// prefab, the scene the user has open and the asset database are all left alone - and
    /// on a 2000 m platform in the open scene when Unity will not allow one.
    /// </summary>
    public static class CharacterPortraits
    {
        public const string PortraitDir = "Assets/Resources/Portraits";

        private const int Width = 384;
        private const int Height = 480;
        private const float FieldOfView = 26f;
        private const string IdleState = "Idle";

        /// <summary>
        /// Height of the shot platform. The fallback path renders inside whatever scene is
        /// already open, and nothing in any scene of this game comes within 2000 m of this,
        /// so the camera still only sees the operative it is shooting.
        /// </summary>
        private const float Isolation = 2000f;

        public static void RebuildAll()
        {
            ArtAssets.EnsureFolder(PortraitDir);

            bool throwaway = TryOpenScratchScene(out Scene scene);
            var stage = BuildStage(scene);
            try
            {
                int written = 0;

                foreach (string prefabPath in AssetDatabase.FindAssets("t:Prefab", new[] { ArtAssets.CharacterPrefabDir }))
                {
                    string asset = AssetDatabase.GUIDToAssetPath(prefabPath);
                    string name = Path.GetFileNameWithoutExtension(asset);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
                    if (prefab == null) continue;

                    if (RenderPortrait(stage, prefab, name)) written++;
                }

                Debug.Log($"[NullFrontier] {written} character portraits written to {PortraitDir}.");
            }
            finally
            {
                if (throwaway) EditorSceneManager.CloseScene(scene, true);
                else if (stage != null) Object.DestroyImmediate(stage.gameObject);
            }
        }

        /// <summary>
        /// Unity refuses to open an additive scene while an unsaved "Untitled" one is in
        /// focus, which is exactly the state the editor is left in after a Play-mode test -
        /// and that refusal used to abort the whole rebuild three steps in, after the heavy
        /// asset work and before a single scene was written.
        /// </summary>
        private static bool TryOpenScratchScene(out Scene scene)
        {
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (scene.IsValid()) return true;
            }
            catch (System.Exception e)
            {
                Debug.Log("[NullFrontier] Portrait scratch scene unavailable (" + e.Message +
                          "); shooting in the open scene instead.");
            }

            scene = SceneManager.GetActiveScene();
            return false;
        }

        /// <summary>Camera plus the three-point rig the portraits are lit with.</summary>
        private static Transform BuildStage(Scene scene)
        {
            var root = new GameObject("PortraitStage");
            root.transform.position = new Vector3(0f, Isolation, 0f);
            SceneManager.MoveGameObjectToScene(root, scene);

            var cameraGo = new GameObject("PortraitCamera");
            cameraGo.transform.SetParent(root.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            Light key = AddLight(root.transform, "Key", new Vector3(2.2f, 3.4f, 3.0f),
                new Vector3(0f, -18f, -22f), new Color(0.85f, 0.92f, 1f), 2.6f);
            key.shadows = LightShadows.None;

            Light rim = AddLight(root.transform, "Rim", new Vector3(-2.6f, 2.4f, -2.4f),
                new Vector3(0f, 26f, 24f), new Color(0.35f, 0.75f, 1f), 1.8f);
            rim.shadows = LightShadows.None;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.2f, 0.32f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.1f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.05f);

            return root.transform;
        }

        private static Light AddLight(Transform parent, string name, Vector3 position,
            Vector3 euler, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = 20f;
            light.spotAngle = 70f;
            return light;
        }

        private static bool RenderPortrait(Transform stage, GameObject prefab, string name)
        {
            var instance = Object.Instantiate(prefab, stage);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            try
            {
                PoseForPortrait(instance);

                Bounds bounds = ArtAssets.CombinedBounds(instance);
                if (bounds.size.y <= 0.01f)
                {
                    Debug.LogError($"[NullFrontier] Portrait {name}: model has no renderers.");
                    return false;
                }

                var camera = stage.GetComponentInChildren<Camera>();
                Vector3 focus = bounds.center + Vector3.up * bounds.size.y * 0.04f;
                float distance = (bounds.size.y * 0.56f) /
                    Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

                // A three-quarter view reads as a person; dead-on reads as a mugshot.
                camera.transform.position = focus +
                    Quaternion.Euler(5f, 17f, 0f) * new Vector3(0f, 0f, distance);
                camera.transform.LookAt(focus);

                var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);

                var previous = RenderTexture.active;
                camera.targetTexture = target;
                camera.Render();

                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                    pixels.Apply();
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    target.Release();
                    Object.DestroyImmediate(target);
                }

                string path = $"{PortraitDir}/{name}.png";
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Object.DestroyImmediate(pixels);

                ImportAsSprite(path);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Skinned-mesh bounds are meaningless until the animator has written a frame,
        /// so the idle pose is sampled once before measuring.
        /// </summary>
        private static void PoseForPortrait(GameObject instance)
        {
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) return;

            animator.applyRootMotion = false;
            animator.Play(IdleState, 0, 0.22f);
            animator.Update(0f);
        }

        private static void ImportAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128f;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                Debug.LogError($"[NullFrontier] {path} is not a sprite after import.");
        }
    }
}
