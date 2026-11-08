using System.Collections.Generic;
using NullFrontier.AI;
using NullFrontier.Characters;
using NullFrontier.Combat;
using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Missions;
using NullFrontier.Player;
using NullFrontier.Progression;
using NullFrontier.UI;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// One-click scene generation. The project ships no hand-authored .unity files, so
    /// every gameplay object, light, volume and widget is assembled here from the
    /// licensed packs and the script-built UI kit. Re-running is safe: each scene is
    /// rebuilt from scratch and saved over its own path.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string VolumePath = ScenesDir + "/NullFrontier_Volume.asset";
        private const string DayVolumePath = ScenesDir + "/NullFrontier_Volume_Day.asset";
        private const string NavMeshPath = ScenesDir + "/VerticalCity_NavMesh.asset";
        // Production-safe default: nothing baked into the scenes. Set a deployed HTTPS
        // endpoint on BackendClient for online features; localhost must never ship.
        private const string ApiBaseUrl = "";

        private const string MainMenuScene = "MainMenu";
        private const string CityScene = "VerticalCity";

        private static readonly Color SkyInk = new Color(0.030f, 0.040f, 0.075f);

        /// <summary>Daytime distance haze: bright enough that the far skyline fades to sky
        /// rather than to soot, which is what made the old night fog swallow whole blocks.</summary>
        private static readonly Color DayHaze = new Color(0.62f, 0.72f, 0.86f);

        private static VolumeProfile _nightVolume;
        private static VolumeProfile _dayVolume;
        private static int _groundLayer, _coverLayer, _artLayer, _enemyLayer;

        /// <summary>The menu plinth's visual mount, handed to the roster so picking an
        /// operative swaps the body on screen instead of only changing the dossier.</summary>
        private static CharacterVisualMount _menuShowcase;

        // ------------------------------------------------------------------ entry points

        [MenuItem("Null Frontier/Build All Scenes")]
        public static void BuildAll()
        {
            // Scene creation is illegal in play mode, so the run would die on the first
            // step that needs one while the earlier asset work already looked finished.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[NullFrontier] Build cancelled: exit Play mode first, " +
                               "then run Build All Scenes.");
                return;
            }

            if (!ConfirmScenesAreSaved()) return;

            if (!Step("project settings", ProjectSetup.Apply)) return;
            if (!Step("character prefabs", CharacterPrefabs.RebuildAll)) return;
            if (!Step("character portraits", CharacterPortraits.RebuildAll)) return;
            if (!Step("weapon assets", WeaponAssets.RebuildAll)) return;
            if (!Step("layer cache", CacheLayers)) return;

            if (!Step("MainMenu scene", BuildMainMenuScene)) return;
            if (!Step("VerticalCity scene", BuildVerticalCityScene)) return;
            if (!Step("build settings", () => SetBuildScenes(new[] { MainMenuScene, CityScene }))) return;

            AssetDatabase.SaveAssets();
            // The run leaves VerticalCity open, so pressing Play straight after a build
            // started the city instead of the menu - or, if an old MainMenu tab was
            // still in memory, a scene that predates the rebuild. End on the shot the
            // user asked for.
            EditorSceneManager.OpenScene($"{ScenesDir}/{MainMenuScene}.unity", OpenSceneMode.Single);
            Debug.Log("[NullFrontier] MainMenu and VerticalCity rebuilt. " +
                      "MainMenu is open: press Play, then build the APK from File > Build Profiles.");
        }

        /// <summary>
        /// Only scenes with unsaved edits need the user's consent, because BuildAll throws
        /// them away. A clean untitled scene - what the editor is left holding after a
        /// Play-mode test - is not worth a prompt and is not a reason to stop: the portrait
        /// pass now copes with it, and every scene builder below opens a fresh scene anyway.
        /// </summary>
        private static bool ConfirmScenesAreSaved()
        {
            bool dirty = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) dirty = true;

            if (!dirty) return true;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;

            Debug.LogError("[NullFrontier] Build cancelled: the open scene has unsaved edits " +
                           "and they were kept. Save or discard them, then run " +
                           "Build All Scenes again.");
            return false;
        }

        /// <summary>Runs one step and names it when it throws, so a half-finished build
        /// can never be mistaken for a finished one.</summary>
        private static bool Step(string name, System.Action step)
        {
            try
            {
                step();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NullFrontier] Build failed during {name}: {e.Message}\n" +
                               "Nothing after this step was built, so the scenes are still the previous ones.");
                return false;
            }
        }

        [MenuItem("Null Frontier/Scenes/Build MainMenu")]
        public static void BuildMainMenuScene()
        {
            CacheLayers();
            EnsureScenesFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateBootstrap();
            CreateMenuCamera();
            var focus = CreateShowcase();
            MenuStage.Build(null, focus, _artLayer, _groundLayer);
            ApplyAtmosphere(false);
            // The Earth hangs 90 m down the avenue, and at the city's fog density the sky
            // colour took a quarter of it - the hero of the shot read as a grey disc. The
            // street keeps its depth from the light pools and the glow sprites instead.
            RenderSettings.fogDensity = 0.0028f;
            // The menu used to be the only scene without a volume, so every neon surface
            // in the stage rendered as a flat pale shape instead of glowing.
            AddVolume(false);
            BuildMenuUi();

            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/{MainMenuScene}.unity");
            Debug.Log("[NullFrontier] MainMenu scene built.");
        }

        [MenuItem("Null Frontier/Scenes/Build VerticalCity")]
        public static void BuildVerticalCityScene()
        {
            CacheLayers();
            EnsureScenesFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateBootstrap();
            CreateGameCamera();
            CreatePlayer();
            BuildCity();
            BakeNavMesh();
            CreateEnemySpawner();
            CreateObjectives();
            CreateMissionDirector();
            CreateHud();
            CreateTouchControls();
            AddVolume(true);
            ApplyAtmosphere(true);
            // Weather is decoration. It sits last and is fenced off because an exception
            // here used to abort the call before the scene could be saved.
            try
            {
                WeatherRig.Build(null, Vector3.zero, false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NullFrontier] Weather skipped: {e.Message}");
            }

            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/{CityScene}.unity");
            Debug.Log("[NullFrontier] VerticalCity scene built.");
        }

        // --------------------------------------------------------------------- bootstrap

        private static void CreateBootstrap()
        {
            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<GameManager>();
            bootstrap.AddComponent<SaveSystem>();
            bootstrap.AddComponent<SceneLoader>();
            bootstrap.AddComponent<AudioDirector>();
            // Loot, XP and reward hooks all resolve through these singletons. Without
            // them a completed contract pays out nothing and enemy drops vanish.
            bootstrap.AddComponent<InventorySystem>();
            bootstrap.AddComponent<ProgressionSystem>();
            bootstrap.AddComponent<SkillTree>();
            // Counts the badges the title screen's hex row opens. It has to live in the
            // bootstrap: mission events fire in the city, the wall is read in the menu.
            bootstrap.AddComponent<AchievementSystem>();
            var backend = bootstrap.AddComponent<BackendClient>();
            backend.SetApiBaseUrl(ApiBaseUrl);
        }

        private static void CreateMissionDirector()
        {
            var missions = new GameObject("Missions");
            missions.AddComponent<MissionManager>();
            var opening = new GameObject("OpeningMission");
            opening.transform.SetParent(missions.transform, false);
            opening.AddComponent<OpeningMission>();
        }

        private static void CacheLayers()
        {
            _groundLayer = Mathf.Max(0, ProjectSetup.GroundLayer);
            _coverLayer = Mathf.Max(0, ProjectSetup.CoverLayer);
            _artLayer = Mathf.Max(0, ProjectSetup.ArtLayer);
            _enemyLayer = Mathf.Max(0, ProjectSetup.EnemyLayer);
        }

        // ------------------------------------------------------------------------ cameras

        private static Camera ExistingCamera()
        {
            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyInk;
            return camera;
        }

        private static void CreateMenuCamera()
        {
            var camera = ExistingCamera();
            // 42 degrees cropped the avenue out of the frame: at that cone the street
            // walls only enter shot past 25 m and the gate at 76 m barely fits, so the
            // player saw a figure on a plinth in a black void instead of a city.
            // The dolly then pulled in to three metres, which put a 1.75 m operative
            // outside the top of the frame - the shot is authored at eight metres now.
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 2.0f, 7.6f), Quaternion.identity);
            camera.transform.LookAt(new Vector3(0f, 1.25f, 0f));
            camera.allowHDR = true;
            EnablePost(camera);
        }

        /// <summary>
        /// URP ships post-processing switched off per camera, so the volume, the bloom and
        /// the whole colour grade have been sitting in the project unused: every emissive
        /// strip rendered as a flat pale shape instead of glowing. This is the line that
        /// makes the neon read as neon. FXAA rather than SMAA because it has to run on a
        /// phone, and MSAA does not survive a post pass.
        /// </summary>
        private static void EnablePost(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        private static void CreateGameCamera()
        {
            var camera = ExistingCamera();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 260f;
            camera.allowHDR = true;
            EnablePost(camera);
            // SolidColor would paint the frame SkyInk over the sky material, and a flat
            // navy void is why the city read as night even where it was lit.
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.gameObject.AddComponent<CinemachineBrain>();
            // The brain owns the transform once a Cinemachine camera exists; park it
            // behind the player so the first frame is not a view from inside a capsule.
            camera.transform.position = new Vector3(0f, 2.2f, -6f);
            camera.transform.LookAt(new Vector3(0f, 1.2f, 0f));
        }

        private static Transform CreateShowcase()
        {
            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = "ShowcasePlinth";
            plinth.transform.position = new Vector3(0f, 0.25f, 0f);
            plinth.transform.localScale = new Vector3(2.1f, 0.25f, 2.1f);
            plinth.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetSolidMaterial("Plinth", new Color(0.10f, 0.11f, 0.15f), 0.55f);
            plinth.layer = _artLayer;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PlinthNeon";
            ring.transform.SetParent(plinth.transform, false);
            ring.transform.localPosition = new Vector3(0f, 1.06f, 0f);
            // The plinth is already scaled 2.1x, and a child scale multiplies through it:
            // 0.98 here is the 2.06 m ring that was intended. Left at 2.06 it became a
            // 4.3 m emissive disc that washed out the whole shot.
            ring.transform.localScale = new Vector3(0.98f, 0.025f, 0.98f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetNeonMaterial("PlinthRing", new Color(0.1f, 0.8f, 1f), 1.5f);
            ring.layer = _artLayer;

            // The hero faces the gate, not the player. The reference reads "about to step
            // through", and the turntable this used to spin put his face back to the camera
            // every half turn - two different shots flickering into each other. The camera
            // dolly still drifts, so the frame stays alive without turning the subject.
            var turntable = new GameObject("ShowcaseTurntable");
            turntable.transform.position = new Vector3(0f, 1.4f, 0f);
            turntable.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var mount = new GameObject("CharacterVisual");
            mount.transform.SetParent(turntable.transform, false);
            mount.AddComponent<CharacterVisualMount>();
            mount.AddComponent<NullFrontier.Characters.CharacterVisualRig>();

            var light = new GameObject("ShowcaseKey");
            light.transform.position = new Vector3(1.8f, 2.6f, 2.2f);
            light.transform.LookAt(new Vector3(0f, 1.2f, 0f));
            var key = light.AddComponent<Light>();
            key.type = LightType.Spot;
            key.color = new Color(0.85f, 0.92f, 1f);
            key.intensity = 5.2f;
            key.range = 14f;
            key.spotAngle = 60f;
            key.shadows = LightShadows.None;

            // One cool key plus a dark ambient left every operative as a black cut-out on
            // device. Fill from the camera side and a cyan rim behind give the body edges.
            var fillGo = new GameObject("ShowcaseFill");
            fillGo.transform.position = new Vector3(-1.4f, 1.5f, 3.0f);
            fillGo.transform.LookAt(new Vector3(0f, 1.1f, 0f));
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Spot;
            fill.color = new Color(0.72f, 0.78f, 0.9f);
            fill.intensity = 2.6f;
            fill.range = 12f;
            fill.spotAngle = 85f;
            fill.shadows = LightShadows.None;

            var rimGo = new GameObject("ShowcaseRim");
            rimGo.transform.position = new Vector3(-0.9f, 2.4f, -2.4f);
            rimGo.transform.LookAt(new Vector3(0f, 1.2f, 0f));
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.color = new Color(0.35f, 0.85f, 1f);
            rim.intensity = 3.4f;
            rim.range = 10f;
            rim.spotAngle = 70f;
            rim.shadows = LightShadows.None;

            // What the menu camera orbits and frames: face height, not the plinth.
            var focus = new GameObject("MenuFocus");
            focus.transform.position = new Vector3(0f, 1.12f, 0f);

            _menuShowcase = mount.GetComponent<CharacterVisualMount>();
            return focus.transform;
        }

        // -------------------------------------------------------------------- atmosphere

        /// <summary>
        /// Sky, fog, ambient and the sun for one scene. The city is a daytime run now:
        /// the night version left every street reading as black glass, and the player
        /// could not see the enemies, the pickups or the geometry they were standing in.
        /// The menu keeps the night - it is a portal, and its own starfield sells that.
        /// </summary>
        private static void ApplyAtmosphere(bool day)
        {
            Material sky = CreateSky(day);
            RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = day ? DayHaze : SkyInk;
            RenderSettings.fogDensity = day ? 0.0035f : 0.0055f;

            if (day)
            {
                // Ambient straight off the sky is what makes a sunny street look sunny:
                // the trilight rig below only ever lit the upward-facing surfaces.
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1.15f;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.42f, 0.50f, 0.70f);
                RenderSettings.ambientEquatorColor = new Color(0.20f, 0.24f, 0.36f);
                RenderSettings.ambientGroundColor = new Color(0.11f, 0.12f, 0.17f);
            }

            // Grabbing "the first light" used to reconfigure the showcase key into the sun,
            // which left the operative lit only by whatever was left over.
            Light sun = null;
            foreach (var candidate in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (candidate.type != LightType.Directional) continue;
                sun = candidate;
                break;
            }
            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                sun = go.AddComponent<Light>();
            }
            sun.name = "Directional Light";
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(day ? 46f : 52f, day ? -34f : -28f, 0f);
            sun.color = day ? new Color(1f, 0.94f, 0.84f) : new Color(0.62f, 0.72f, 1f);
            sun.intensity = day ? 2.15f : 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = day ? 0.82f : 0.55f;
            sun.cullingMask = ~0;
            RenderSettings.sun = sun;
        }

        /// <summary>
        /// The built-in procedural sky, authored as an asset rather than at runtime: a
        /// material that only exists in play cannot be packed into the player, and a
        /// shader reached for with Shader.Find at runtime is a shader that may have been
        /// stripped out of the build.
        /// </summary>
        private static Material CreateSky(bool day)
        {
            ArtAssets.EnsureFolder(ScenesDir);
            string path = $"{ScenesDir}/NullFrontier_Sky_{(day ? "Day" : "Night")}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                {
                    Debug.LogError("[NullFrontier] Skybox/Procedural is not available; the scene " +
                                   "keeps its flat background colour.");
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Exposure", day ? 1.25f : 0.22f);
            material.SetFloat("_SunSize", day ? 0.055f : 0.02f);
            material.SetFloat("_SunSizeConvergence", day ? 5f : 10f);
            material.SetFloat("_AtmosphereThickness", day ? 1.35f : 0.25f);
            material.SetColor("_SkyTint", day ? new Color(0.40f, 0.60f, 0.95f) : new Color(0.07f, 0.10f, 0.22f));
            material.SetColor("_GroundColor", day ? new Color(0.34f, 0.36f, 0.42f) : new Color(0.02f, 0.025f, 0.05f));
            material.SetColor("_Tint", Color.white);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void AddVolume(bool day)
        {
            var go = new GameObject("PostProcessing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 1f;
            // `Volume.profile` is a throwaway clone that exists only while the editor holds
            // the object; assigning it left the saved scene with sharedProfile = null and
            // the whole grade - bloom, ACES, grain, vignette - silently inactive.
            volume.sharedProfile = Grade(day);
        }

        /// <summary>
        /// Two grades, because one profile cannot serve a noon street and a night portal:
        /// the values that make neon glow in the dark are the same values that grey out a
        /// sunlit city. The profile has to be an asset before components are added or they
        /// are not persisted with it.
        /// </summary>
        private static VolumeProfile Grade(bool day)
        {
            if (day && _dayVolume != null) return _dayVolume;
            if (!day && _nightVolume != null) return _nightVolume;

            string path = day ? DayVolumePath : VolumePath;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            // Profiles written by the old builder carry null entries: the components were
            // added in memory and never attached to the asset, so they reloaded as holes.
            profile.components.RemoveAll(component => component == null);

            var tonemap = GetOrAdd<Tonemapping>(profile);
            tonemap.mode.Override(TonemappingMode.ACES);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(day ? 1.15f : 1.05f);
            bloom.intensity.Override(day ? 0.45f : 0.62f);
            bloom.scatter.Override(day ? 0.55f : 0.72f);
            bloom.clamp.Override(day ? 160f : 220f);

            // Daylight is the one place the grade adds rather than subtracts: exposure up,
            // contrast down, saturation positive. The night values below are the old ones,
            // which pulled colour out of the picture to keep the neon from blooming out.
            var grade = GetOrAdd<ColorAdjustments>(profile);
            // The night exposure was authored against a grade that never actually ran. Now
            // that ACES is live it darkens the midtones by design, so the night lift is
            // raised to keep the street where the lighting was tuned.
            grade.postExposure.Override(day ? 0.38f : 0.22f);
            grade.contrast.Override(day ? 10f : 28f);
            grade.saturation.Override(day ? 16f : -14f);

            // Cool shadows against a warm key is what separates a graded night city
            // from a flat render of dark boxes.
            var split = GetOrAdd<SplitToning>(profile);
            split.shadows.Override(day ? new Color(0.60f, 0.70f, 0.92f) : new Color(0.50f, 0.60f, 0.86f));
            split.highlights.Override(day ? new Color(1f, 0.96f, 0.88f) : new Color(0.94f, 0.88f, 0.80f));
            split.balance.Override(day ? -0.04f : -0.08f);

            // Sensor noise at this exposure reads as atmosphere; above 0.12 it reads as
            // a bug, and it is the cheapest full-screen effect in the stack.
            var grain = GetOrAdd<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(day ? 0.03f : 0.09f);
            grain.response.Override(0.55f);

            // Post-processing was never switched on, so this vignette used to be invisible.
            // With the grade live it stacks on top of the menu's own darkened frame, so it
            // is set to a corner shade rather than a cinematic tunnel.
            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(day ? 0.10f : 0.30f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            if (day) _dayVolume = profile; else _nightVolume = profile;
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing)) return existing;

            var component = profile.Add<T>(overrides: true);
            component.name = typeof(T).Name;
            // A component added to a profile lives in memory until it is attached to the
            // profile asset; without this line it reloads from disk as a null entry and
            // the override it carried is gone.
            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return component;
        }

        // -------------------------------------------------------------------------- player

        private static void CreatePlayer()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.layer = _artLayer;
            player.transform.position = new Vector3(0f, 1.15f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0f, 0f);
            controller.radius = 0.35f;
            // The kit's deck ramps step up 0.35 m at a time; the default 0.3 step
            // offset leaves the player stuck at the foot of every platform.
            controller.stepOffset = 0.42f;
            controller.slopeLimit = 58f;

            player.AddComponent<Health>();
            player.AddComponent<Stamina>();
            // Stats reads Health/Stamina on this object, and ConsumableUse is what the
            // supply panel spends items through.
            player.AddComponent<PlayerStats>();
            player.AddComponent<ConsumableUse>();
            player.AddComponent<PlayerCharacter>();
            player.AddComponent<DesktopInputProvider>();
            player.AddComponent<MobileInputProvider>();
            player.AddComponent<CharacterVisualMount>();

            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(player.transform, false);
            rig.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(rig.transform, false);

            var movement = player.AddComponent<PlayerController>();
            UiFactory.SetField(movement, "cameraRig", rig.transform);

            var cameraController = player.AddComponent<PlayerCameraController>();
            UiFactory.SetField(cameraController, "yawRig", rig.transform);
            UiFactory.SetField(cameraController, "pitchPivot", pivot.transform);

            var vcam = CreateCinemachineRig(pivot.transform);
            // Look input writes the lens through the virtual camera, never the Unity
            // camera: the brain overwrites the camera's own lens from the vcam every frame.
            UiFactory.SetField(cameraController, "lensSource", vcam);
            CreateWeapons(player);
            CreateAbilities(player.transform);

            var renderer = player.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = ArtAssets.GetSolidMaterial("PlayerGrey",
                    new Color(0.34f, 0.38f, 0.46f), 0.25f);
        }

        private static CinemachineCamera CreateCinemachineRig(Transform lookAt)
        {
            var camGo = new GameObject("CinemachineCamera");
            var cineCam = camGo.AddComponent<CinemachineCamera>();
            var follow = camGo.AddComponent<CinemachineThirdPersonFollow>();
            cineCam.Target.TrackingTarget = lookAt;
            cineCam.Priority = 100;
            // The brain copies the virtual lens onto the camera every frame, so this - not
            // PlayerCameraController's field - is the lens the game is actually shot on.
            // Left at Cinemachine's 40 deg default, the 3.2 m rig would have read as a
            // long-lens close-up and the aim punch-in could never have shown.
            cineCam.Lens.FieldOfView = 52f;
            // The pivot sits at the top of the player's chest, so these four numbers are
            // the whole framing. A 52 deg lens with the camera 3.8 m back puts a 1.75 m
            // operative across roughly half the frame height - the close over-the-shoulder
            // read of a third-person shooter. At the previous 6 m the body was a small
            // figure in the middle of a wide landscape and every character looked tiny.
            // The side offset is what keeps the held rifle out of the sight line: at 0.32 m
            // the lens sat directly behind the right shoulder and the gun filled the frame.
            follow.CameraDistance = 3.8f;
            follow.VerticalArmLength = 0.45f;
            follow.CameraSide = 0.45f;
            follow.ShoulderOffset = new Vector3(0.34f, 0.2f, 0f);
            follow.Damping = new Vector3(0.22f, 0.3f, 0.36f);

            var obstacles = follow.AvoidObstacles;
            obstacles.Enabled = true;
            obstacles.CollisionFilter = ProjectSetup.CameraBlockingLayerMask;
            obstacles.IgnoreTag = "Player";
            obstacles.CameraRadius = 0.2f;
            obstacles.DampingIntoCollision = 0f;
            obstacles.DampingFromCollision = 0.35f;
            follow.AvoidObstacles = obstacles;
            return cineCam;
        }

        private static void CreateWeapons(GameObject player)
        {
            var holder = new GameObject("Weapons");
            holder.transform.SetParent(player.transform, false);

            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(player.transform, false);
            // Chest height in the controller's frame: the pivot sits mid-body, so the
            // muzzle is a little above centre and forward of the shoulders. This is only
            // the fallback socket - the mount moves firing to the barrel once a gun is in
            // hand, and a round that starts in the sternum starts inside the wall the
            // player is leaning against.
            firePoint.transform.localPosition = new Vector3(0.28f, 0.32f, 0.55f);

            AddWeapon(holder.transform, "NF-9 Sidearm", "nf_pistol");
            AddWeapon(holder.transform, "NF-76 Assault Rifle", "nf_ar");
            AddWeapon(holder.transform, "Arc Light", "nf_energy");
            AddWeapon(holder.transform, "Breach Scattergun", "nf_shotgun");

            var loadout = player.AddComponent<WeaponLoadout>();
            var combat = player.AddComponent<PlayerCombat>();
            UiFactory.SetField(combat, "firePoint", firePoint.transform);
            UiFactory.SetField(loadout, "startIndex", 1);

            var visuals = player.AddComponent<WeaponVisualMount>();
            UiFactory.SetField(visuals, "defaultSocket", firePoint.transform);
        }

        private static void AddWeapon(Transform holder, string name, string weaponId)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            var weapon = go.AddComponent<HitscanWeapon>();
            UiFactory.SetField(weapon, "definition", WeaponAssets.Get(weaponId));
        }

        private static void CreateAbilities(Transform player)
        {
            var holder = new GameObject("Abilities");
            holder.transform.SetParent(player, false);
            holder.AddComponent<AbilityController>();

            AddAbility<TacticalDash>(holder.transform);
            AddAbility<HolographicDecoy>(holder.transform);
            AddAbility<EnergyShield>(holder.transform);
            AddAbility<EnhancedScanning>(holder.transform);
            AddAbility<EmpPulse>(holder.transform);
            AddAbility<TempOverdrive>(holder.transform);
        }

        private static void AddAbility<T>(Transform holder) where T : Ability
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(holder, false);
            var ability = go.AddComponent<T>();
            if (ability is EmpPulse)
            {
                // Boxed as a LayerMask: SetValue cannot convert a boxed int to the struct.
                LayerMask enemyMask = 1 << _enemyLayer;
                UiFactory.SetField(ability, "enemyMask", enemyMask);
            }
        }

        // -------------------------------------------------------------------------- city

        private static void BuildCity()
        {
            var city = new GameObject("VerticalCity");
            bool dressed = CityKit.Build(city.transform, new System.Random(20261106));
            if (!dressed) CreateGreyboxCity();
        }

        private static void CreateGreyboxCity()
        {
            Debug.LogWarning("[NullFrontier] City kits unavailable: building greybox blocks.");

            float half = 78f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = _groundLayer;
            ground.transform.localScale = new Vector3(half * 2f / 10f, 1f, half * 2f / 10f);
            ground.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetSolidMaterial("Asphalt", new Color(0.09f, 0.10f, 0.13f), 0.35f);

            var rng = new System.Random(1234);
            var concrete = ArtAssets.GetSolidMaterial("Concrete", new Color(0.24f, 0.25f, 0.29f), 0.2f);
            for (int i = 0; i < 70; i++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Greybox_Building";
                block.layer = _coverLayer;
                float w = 5f + (float)rng.NextDouble() * 7f;
                float h = 6f + (float)rng.NextDouble() * 32f;
                float x = (float)(rng.NextDouble() * half * 2f - half);
                float z = (float)(rng.NextDouble() * half * 2f - half);
                if (Mathf.Abs(x) < 12f && Mathf.Abs(z) < 12f)
                {
                    Object.DestroyImmediate(block);
                    continue;
                }
                block.transform.position = new Vector3(x, h * 0.5f, z);
                block.transform.localScale = new Vector3(w, h, w);
                block.GetComponent<Renderer>().sharedMaterial = concrete;
            }
        }

        /// <summary>
        /// Bakes the walkable surface and writes it out as an asset, because a build
        /// player does not bake at runtime: the NavMeshSurface has to ship pre-built data.
        /// </summary>
        private static void BakeNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ProjectSetup.NavigationLayerMask;
            surface.minRegionArea = 2f;
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[NullFrontier] NavMesh bake produced no data; " +
                               "enemies will not be able to path.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null)
                AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            Debug.Log("[NullFrontier] NavMesh baked to " + NavMeshPath);
        }

        // ------------------------------------------------------------------------ enemies

        private static void CreateEnemySpawner()
        {
            var go = new GameObject("EnemySpawner");
            var spawner = go.AddComponent<EnemySpawner>();

            float outer = Mathf.Max(30f, CityKit.Extent * 0.8f);
            // Total population stays under EnemySpawner.hardCap (14) so the commander
            // always has a slot instead of starving behind the rank-and-file.
            var kinds = new[]
            {
                EnemyKind("Enemies/DigitalSoldier", 6, 90f, 3.6f, 16f, outer, 0f),
                EnemyKind("Enemies/MazeStalker", 4, 70f, 4.4f, 20f, outer, 0f),
                EnemyKind("Enemies/SecurityDrone", 2, 55f, 3.0f, 24f, outer, 2.5f),
                EnemyKind("Enemies/SecurityCommander", 1, 240f, 3.2f, 40f, outer, 0f),
            };
            UiFactory.SetField(spawner, "kinds", kinds);
        }

        private static EnemySpawner.EnemyKind EnemyKind(string path, int population, float health,
            float speed, float inner, float outer, float hover)
        {
            return new EnemySpawner.EnemyKind
            {
                prefabResourcePath = path,
                population = population,
                health = health,
                moveSpeed = speed,
                innerRadius = inner,
                outerRadius = outer,
                hoverOffset = hover,
            };
        }

        // --------------------------------------------------------------------- objectives

        private static void CreateObjectives()
        {
            var root = new GameObject("Objectives");
            var rng = new System.Random(77);
            float half = Mathf.Max(26f, CityKit.Extent - CityKit.BlockPitch);

            ReachMarker(root.transform, "vertical_city_spawn", new Vector3(0f, 0f, 4f),
                "MissionMarker_Spawn");

            // Terminal 7 is the opening mission's interact target, so it goes on a
            // predictable corner rather than at random.
            Interactable(root.transform, "terminal_7", MissionManager.MissionEventType.Interact,
                "INVESTIGATE TERMINAL 7", null, 0, false,
                new Vector3(-half + CityKit.BlockPitch, 0f, half - CityKit.BlockPitch));

            ReachMarker(root.transform, "hidden_terminal",
                StreetPoint(rng, half, 0.82f), "MissionMarker_HiddenTerminal");

            // The relay is held by working it through three spin-up cycles while the district
            // shoots back, which is what the Defend objective counts.
            Interactable(root.transform, "relay_1", MissionManager.MissionEventType.Defend,
                "CYCLE RELAY", null, 0, false, 3, StreetPoint(rng, half, 0.55f));

            for (int i = 0; i < 6; i++)
                Interactable(root.transform, null, MissionManager.MissionEventType.Collect,
                    "TAKE DATA CACHE", "data_cache", 1, true, StreetPoint(rng, half, 0.35f + i * 0.1f));

            for (int i = 0; i < 7; i++)
                Interactable(root.transform, null, MissionManager.MissionEventType.Collect,
                    "COLLECT ALLOY SCRAP", "scrap_alloy", 2, true, StreetPoint(rng, half, 0.3f + i * 0.1f));

            Interactable(root.transform, null, MissionManager.MissionEventType.Collect,
                "TAKE CORE FRAGMENT", "core_fragment", 1, true, StreetPoint(rng, half, 0.95f));
        }

        /// <summary>A point on a road centre line, so pickups never spawn inside a block.</summary>
        private static Vector3 StreetPoint(System.Random rng, float half, float along)
        {
            float pitch = CityKit.BlockPitch;
            int lanes = Mathf.Max(1, Mathf.RoundToInt(half * 2f / pitch) + 1);
            float lane = -half + rng.Next(lanes) * pitch;
            float travel = Mathf.Lerp(-half, half, Mathf.Clamp01(along));
            return rng.Next(2) == 0
                ? new Vector3(lane, 0f, travel)
                : new Vector3(travel, 0f, lane);
        }

        private static void ReachMarker(Transform parent, string id, Vector3 position, string name)
        {
            // The beam is what the player aims at; the volume is what the mission reads.
            // They used to be the same cylinder, which meant a 2.4 m solid column of neon
            // standing exactly where the third-person camera wants to put the player's own
            // body - every objective looked like an oversized gun in the shot.
            var volume = new GameObject(name);
            volume.layer = _artLayer;
            volume.transform.SetParent(parent, false);
            volume.transform.position = position + Vector3.up * 1.4f;
            var trigger = volume.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4f, 3f, 4f);

            var marker = volume.AddComponent<MissionMarker>();
            UiFactory.SetField(marker, "id", id);
            UiFactory.SetField(marker, "eventType", MissionManager.MissionEventType.Reach);
            UiFactory.SetField(marker, "notifyOnce", true);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Beacon";
            pillar.layer = _artLayer;
            pillar.transform.SetParent(volume.transform, false);
            pillar.transform.localPosition = Vector3.zero;
            pillar.transform.localScale = new Vector3(0.42f, 1.4f, 0.42f);
            pillar.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetNeonMaterial("ObjectiveBeacon", new Color(0.2f, 0.95f, 0.6f), 1.6f);
            Object.DestroyImmediate(pillar.GetComponent<Collider>());

            // A beacon is a column of light, so it turns and breathes but stays planted.
            Ambient(pillar, 0f, 14f, 0.4f);
        }

        /// <summary>
        /// Gives an objective its idle motion. Every field is a constructor-style write
        /// on the component, so the builder owns the tuning and the runtime stays generic.
        /// </summary>
        private static void Ambient(GameObject go, float bob, float spin, float glow)
        {
            var motion = go.AddComponent<NullFrontier.Core.AmbientMotion>();
            UiFactory.SetField(motion, "bobHeight", bob);
            UiFactory.SetField(motion, "spinDegreesPerSecond", spin);
            UiFactory.SetField(motion, "glowAmount", glow);
        }

        private static void Interactable(Transform parent, string id,
            MissionManager.MissionEventType eventType, string prompt, string grantItemId,
            int grantQuantity, bool consumeOnUse, Vector3 position) =>
            Interactable(parent, id, eventType, prompt, grantItemId, grantQuantity, consumeOnUse,
                1, position);

        private static void Interactable(Transform parent, string id,
            MissionManager.MissionEventType eventType, string prompt, string grantItemId,
            int grantQuantity, bool consumeOnUse, int requiredUses, Vector3 position)
        {
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = string.IsNullOrEmpty(id) ? "Pickup_" + grantItemId : "Terminal_" + id;
            crate.layer = _artLayer;
            crate.transform.SetParent(parent, false);
            // A multi-use object is a relay pylon, not a crate: it has to read as machinery.
            bool pylon = Mathf.Max(1, requiredUses) > 1;
            crate.transform.position = position + Vector3.up * (pylon ? 1.2f : 0.55f);
            crate.transform.localScale = pylon
                ? new Vector3(2f, 2.4f, 2f)
                : new Vector3(0.95f, 0.7f, 0.95f);
            crate.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            crate.GetComponent<Renderer>().sharedMaterial = ArtAssets.GetNeonMaterial(
                pylon ? "Relay" : string.IsNullOrEmpty(grantItemId) ? "Terminal" : "Pickup",
                pylon ? new Color(0.55f, 0.3f, 1f)
                    : string.IsNullOrEmpty(grantItemId) ? new Color(0.1f, 0.8f, 1f)
                    : new Color(1f, 0.75f, 0.2f),
                1.4f);

            var object1 = crate.AddComponent<InteractableObject>();
            UiFactory.SetField(object1, "id", id ?? string.Empty);
            UiFactory.SetField(object1, "eventType", eventType);
            UiFactory.SetField(object1, "prompt", prompt);
            UiFactory.SetField(object1, "grantItemId", grantItemId ?? string.Empty);
            UiFactory.SetField(object1, "grantQuantity", grantQuantity);
            UiFactory.SetField(object1, "consumeOnUse", consumeOnUse);
            UiFactory.SetField(object1, "requiredUses", Mathf.Max(1, requiredUses));

            // Loose loot floats and turns; a terminal is bolted down and only breathes;
            // the relay is machinery, so it spins in place without leaving its plinth.
            if (pylon) Ambient(crate, 0f, 18f, 0.7f);
            else if (string.IsNullOrEmpty(grantItemId)) Ambient(crate, 0.03f, 0f, 0.6f);
            else Ambient(crate, 0.12f, 30f, 0.55f);
        }

        // ---------------------------------------------------------------------------- UI

        private static void CreateHud()
        {
            var canvas = CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<HUDController>();
            UiFactory.BuildHud(canvas, hud);
        }

        private static void BuildMenuUi()
        {
            var canvas = CreateCanvas("MenuCanvas", 0);
            EnsureEventSystem();

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            var screens = CreateScreens(canvas.transform);

            var title = VignettePage(screens, "Title");
            UiFactory.BuildTitleScreen(title.transform, controller);

            var settings = Page(screens, "Settings");
            var settingsController = settings.gameObject.AddComponent<SettingsMenuController>();
            UiFactory.BuildSettingsScreen(settings.transform, settingsController, "Title");

            // Both of these sit over the live 3D stage, so they get a vignette rather than
            // an opaque sheet: the edges darken for legibility, the centre stays the scene.
            var select = VignettePage(screens, "CharacterSelect");
            var selectController = select.gameObject.AddComponent<CharacterSelectUI>();
            UiFactory.SetField(selectController, "showcase", _menuShowcase);
            UiFactory.BuildCharacterSelect(select.transform, selectController, "Title");

            var account = Page(screens, "Account");
            var auth = account.gameObject.AddComponent<AuthUI>();
            UiFactory.BuildAuthScreen(account.transform, auth);

            var multiplayer = Page(screens, "Multiplayer");
            UiFactory.BuildMultiplayerScreen(multiplayer.transform, "Title");

            // The wall and the outbox are the same page in two filters: the wall reads the
            // lifetime counters, the outbox only lists drops that have not been taken.
            var badges = Page(screens, "Badges");
            var badgeBoard = badges.gameObject.AddComponent<AchievementBoardUI>();
            UiFactory.SetField(badgeBoard, "mode", AchievementBoardUI.Mode.Badges);
            UiFactory.BuildAchievementBoard(badges.transform, badgeBoard, "Title", false);

            var rewards = Page(screens, "Rewards");
            var rewardBoard = rewards.gameObject.AddComponent<AchievementBoardUI>();
            UiFactory.SetField(rewardBoard, "mode", AchievementBoardUI.Mode.Rewards);
            UiFactory.BuildAchievementBoard(rewards.transform, rewardBoard, "Title", true);

            var help = Page(screens, "Help");
            UiFactory.BuildHelpScreen(help.transform, "Title");

            var credits = Page(screens, "Credits");
            UiFactory.BuildCreditsScreen(credits.transform, "Help");
        }

        private static void CreateTouchControls()
        {
            // Above the HUD: the control layer must never be buried under a panel,
            // and the pause key shares the corner with one.
            var canvas = CreateCanvas("TouchCanvas", 20);
            EnsureEventSystem();

            var joystick = BuildMoveZone(canvas.transform);
            var provider = Object.FindFirstObjectByType<MobileInputProvider>();
            UiFactory.SetField(provider, "moveJoystick", joystick);

            // MOBA thumb deck: FIRE owns the corner, the three combat keys arc around it
            // and the utility keys sit one ring further out. Every centre is at least the
            // sum of the two radii from its neighbours, so a thumb can never straddle two.
            UiFactory.SetField(provider, "fireButton", UiFactory.SkillKey("Fire", canvas.transform,
                "FIRE", "HOLD", new Vector2(-190f, 170f), new Vector2(250f, 250f), UiFactory.Danger,
                KeyOverlay.Source.Weapon, UiFactory.Icon.Sword));
            UiFactory.SetField(provider, "abilityButton", UiFactory.SkillKey("Ability", canvas.transform,
                "PROTOCOL", null, new Vector2(-430f, 130f), new Vector2(165f, 165f), UiFactory.Accent2,
                KeyOverlay.Source.Ability, UiFactory.Icon.Bolt));
            UiFactory.SetField(provider, "dodgeButton", UiFactory.SkillKey("Dodge", canvas.transform,
                "DODGE", null, new Vector2(-300f, 350f), new Vector2(150f, 150f), UiFactory.Accent,
                null, UiFactory.Icon.Chevron));
            UiFactory.SetField(provider, "switchButton", UiFactory.SkillKey("Switch", canvas.transform,
                "SWAP", null, new Vector2(-190f, 380f), new Vector2(110f, 110f), UiFactory.Subtle,
                null, UiFactory.Icon.Chevron));
            UiFactory.SetField(provider, "jumpButton", UiFactory.SkillKey("Jump", canvas.transform,
                "JUMP", null, new Vector2(-30f, 430f), new Vector2(150f, 150f), UiFactory.Accent,
                null, UiFactory.Icon.Jump));
            UiFactory.SetField(provider, "aimButton", UiFactory.SkillKey("Aim", canvas.transform,
                "AIM", "HOLD", new Vector2(-560f, 300f), new Vector2(140f, 140f), UiFactory.Accent,
                null, UiFactory.Icon.Aim));
            UiFactory.SetField(provider, "sprintButton", UiFactory.SkillKey("Sprint", canvas.transform,
                "RUN", null, new Vector2(-140f, 560f), new Vector2(130f, 130f), UiFactory.Stamina,
                KeyOverlay.Source.Stamina, UiFactory.Icon.Run));
            UiFactory.SetField(provider, "reloadButton", UiFactory.SkillKey("Reload", canvas.transform,
                "RELOAD", null, new Vector2(-380f, 520f), new Vector2(130f, 130f), UiFactory.Subtle,
                null, UiFactory.Icon.Reload));
            UiFactory.SetField(provider, "crouchButton", UiFactory.SkillKey("Crouch", canvas.transform,
                "DROP", null, new Vector2(-580f, 470f), new Vector2(120f, 120f), UiFactory.Shield,
                null, UiFactory.Icon.Down));
            UiFactory.SetField(provider, "interactButton", UiFactory.SkillKey("Interact", canvas.transform,
                "USE", null, new Vector2(-700f, 180f), new Vector2(130f, 130f), UiFactory.Health,
                null, UiFactory.Icon.Interact));

            var pauseController = canvas.gameObject.AddComponent<PauseMenuController>();

            var pauseButton = UiFactory.Button("Pause", canvas.transform, "II", Vector2.zero,
                new Vector2(112f, 112f), UiFactory.Panel, 34);
            UiFactory.TopRight(pauseButton.transform as RectTransform, new Vector2(-40f, -40f));
            UiFactory.Bind(pauseButton, new UnityEngine.Events.UnityAction(pauseController.TogglePause));

            var screens = CreateScreens(canvas.transform);

            var pause = Page(screens, "Pause");
            UiFactory.BuildPauseScreen(pause.transform, pauseController, "Settings");

            // The board is the only way to pick up a second contract, so it hangs off the
            // pause screen rather than the HUD: browsing quests pauses the city.
            UiFactory.BackButton("Missions", pause.transform, "CONTRACTS", new Vector2(-245f, -270f),
                new Vector2(440f, 84f), "Missions");
            UiFactory.BackButton("Supply", pause.transform, "SUPPLY", new Vector2(245f, -270f),
                new Vector2(440f, 84f), "Supply");

            var missions = Page(screens, "Missions");
            var board = missions.gameObject.AddComponent<MissionBoardUI>();
            UiFactory.BuildMissionBoardScreen(missions.transform, board, "Pause");

            var supply = Page(screens, "Supply");
            var panel = supply.gameObject.AddComponent<SupplyPanel>();
            UiFactory.BuildSupplyScreen(supply.transform, panel, "Pause");

            var settings = Page(screens, "Settings");
            var settingsController = settings.gameObject.AddComponent<SettingsMenuController>();
            UiFactory.BuildSettingsScreen(settings.transform, settingsController, "Pause");
        }

        private static RectTransform CreateScreens(Transform canvas)
        {
            var screens = UiFactory.Root("Screens", canvas);
            UiFactory.Stretch(screens, 0f);
            screens.gameObject.AddComponent<ScreenManager>();
            return screens;
        }

        private static Image Page(Transform screens, string name)
        {
            var page = UiFactory.Sheet(name, screens, new Color(0.02f, 0.03f, 0.055f, 0.96f));
            // A page left active would cover the whole scene the moment it loads; the
            // ScreenManager only turns one on when a button asks for it.
            page.gameObject.SetActive(false);
            // Open screens must also swallow touches, or the control layer underneath
            // keeps answering them while the city is paused.
            page.raycastTarget = true;
            return page;
        }

        /// <summary>
        /// A page that keeps the 3D stage visible: the generated vignette is transparent
        /// in the middle and opaque at the edges, so panels read against darkness while
        /// the operative in the centre stays fully lit.
        /// </summary>
        private static Image VignettePage(Transform screens, string name)
        {
            var page = UiFactory.Solid(name, screens, new Color(0.012f, 0.018f, 0.035f, 0.98f),
                UiFactory.VignetteSprite);
            UiFactory.Stretch(page.rectTransform, 0f);
            page.gameObject.SetActive(false);
            return page;
        }

        private static VirtualJoystick BuildMoveZone(Transform canvas)
        {
            // The whole left 45% answers movement touches, but the pad itself stays drawn
            // where the thumb learns to find it - a MOBA stick, not a hidden one.
            var zone = UiFactory.Solid("MoveZone", canvas, new Color(1f, 1f, 1f, 0f));
            var zoneRt = zone.rectTransform;
            zoneRt.anchorMin = Vector2.zero;
            zoneRt.anchorMax = new Vector2(0.45f, 1f);
            zoneRt.offsetMin = Vector2.zero;
            zoneRt.offsetMax = Vector2.zero;
            zone.raycastTarget = true;

            var ring = UiFactory.Solid("Ring", canvas, new Color(0.10f, 0.78f, 0.92f, 0.55f),
                UiFactory.RingSprite);
            ring.rectTransform.sizeDelta = new Vector2(340f, 340f);
            UiFactory.BottomLeft(ring.rectTransform, new Vector2(280f, 270f));
            ring.raycastTarget = false;

            var bed = UiFactory.Solid("Bed", ring.transform, new Color(0.02f, 0.03f, 0.06f, 0.55f),
                UiFactory.CircleSprite);
            UiFactory.Stretch(bed.rectTransform, 14f);
            bed.raycastTarget = false;

            var handle = UiFactory.Solid("Handle", ring.transform, new Color(0.85f, 0.95f, 1f, 0.88f),
                UiFactory.CircleSprite);
            handle.rectTransform.sizeDelta = new Vector2(132f, 132f);
            UiFactory.Center(handle.rectTransform, Vector2.zero);
            handle.raycastTarget = false;

            var knob = UiFactory.Solid("Knob", handle.transform, new Color(0.10f, 0.78f, 0.92f, 0.9f),
                UiFactory.CircleSprite);
            UiFactory.Stretch(knob.rectTransform, 34f);
            knob.raycastTarget = false;

            var joystick = zone.gameObject.AddComponent<VirtualJoystick>();
            UiFactory.SetField(joystick, "ring", ring.rectTransform);
            UiFactory.SetField(joystick, "handle", handle.rectTransform);
            UiFactory.SetField(joystick, "floating", false);
            return joystick;
        }

        // -------------------------------------------------------------------- plumbing

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Half-way match. Nothing in the layout may come closer than ~50 px to the top
            // or bottom edge, because a 21:9 phone only shows 978 px of the 1080 box.
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ScenesDir)) AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        private static void SetBuildScenes(string[] sceneNames)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string name in sceneNames)
            {
                string path = $"{ScenesDir}/{name}.unity";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
