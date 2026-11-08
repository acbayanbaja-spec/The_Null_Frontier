using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Weather for the open city: rain that falls through the streets and slow dust that
    /// drifts in the neon. Both are single world-space particle systems - one draw call
    /// each - so the ambience costs the phone almost nothing, and the emitter is re-centred
    /// on the operative at runtime by WeatherFollow.
    /// </summary>
    public static class WeatherRig
    {
        private const string MaterialDir = ArtAssets.Root + "/Materials/Weather";

        private static Material _rain, _motes;

        [MenuItem("Null Frontier/Art/Rebuild Weather Materials")]
        public static void RebuildWeatherMaterials()
        {
            _rain = null;
            _motes = null;
            Build(null, Vector3.zero);
            Debug.Log("[NullFrontier] Weather materials rebuilt.");
        }

        /// <summary>
        /// Builds the rig. A missing particle shader aborts the whole thing rather than
        /// filling the streets with pink quads.
        ///
        /// Rain is optional because the city is a daytime run now, and falling water
        /// against a high sun reads as a leak rather than as weather. The motes stay: in
        /// daylight they are dust in the sunlight, which is the cheaper half of the effect.
        /// </summary>
        public static void Build(Transform parent, Vector3 centre, bool rain = true)
        {
            if (!PrepareMaterials())
            {
                Debug.LogError("[NullFrontier] No usable particle shader found - weather skipped.");
                return;
            }

            var root = new GameObject("Weather");
            root.transform.SetParent(parent, false);
            root.transform.position = centre + new Vector3(0f, 24f, 0f);

            if (rain) Rain(root);
            Motes(root);
            root.AddComponent<NullFrontier.Core.WeatherFollow>();
        }

        private static void Rain(GameObject root)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1400;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 23f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
            main.startColor = new Color(0.66f, 0.82f, 1f, 0.42f);
            main.gravityModifier = 0.9f;

            var emission = ps.emission;
            emission.rateOverTime = 460f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(58f, 1f, 58f);
            shape.randomDirectionAmount = 0f;

            // Adding a ParticleSystem already creates its renderer; asking for a second
            // one throws and takes the rest of the scene build down with it.
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Configure(renderer, _rain, ParticleSystemRenderMode.Stretch);
            // A stretched drop is velocity x lengthScale metres long: at 20 m/s a 0.09
            // factor draws the 1.8 m streak, where the default of 1 would draw 20 m.
            renderer.lengthScale = 0.09f;
            renderer.velocityScale = 0f;
            renderer.cameraVelocityScale = 0.03f;
        }

        private static void Motes(GameObject root)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, -14f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.duration = 8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 320;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = new Color(0.58f, 0.78f, 1f, 0.16f);
            main.gravityModifier = -0.01f;

            var emission = ps.emission;
            emission.rateOverTime = 42f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 9f, 34f);

            // The drift is what sells it: motes that only fall read as dirt, motes that
            // wander read as the city's own steam.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.32f);
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.18f;
            noise.damping = true;

            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.1f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Configure(renderer, _motes, ParticleSystemRenderMode.Billboard);
        }

        private static void Configure(ParticleSystemRenderer renderer, Material material,
            ParticleSystemRenderMode mode)
        {
            renderer.material = material;
            renderer.renderMode = mode;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 4;
            renderer.minParticleSize = 0.004f;
            renderer.maxParticleSize = 0.6f;
            renderer.shadowBias = 1f;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Rain and motes need opposite blends: a drop wants to disappear against the
        /// skybox, steam wants to add light to the frame.
        /// </summary>
        private static bool PrepareMaterials()
        {
            if (_rain != null && _motes != null) return true;

            var shader = ParticleShader();
            if (shader == null) return false;

            ArtAssets.EnsureFolder(MaterialDir);
            _rain = LoadOrCreate($"{MaterialDir}/Weather_Rain.mat", shader,
                $"{UiFactory.SpriteDir}/Streak.png", BlendState.Alpha);
            _motes = LoadOrCreate($"{MaterialDir}/Weather_Dust.mat", shader,
                $"{UiFactory.SpriteDir}/Glow.png", BlendState.Additive);
            return _rain != null && _motes != null;
        }

        private enum BlendState { Alpha, Additive }

        private static Shader ParticleShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null) return shader;

            shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                Debug.LogWarning("[NullFrontier] URP particle shader missing - using Sprites/Default.");
                return shader;
            }

            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            if (shader != null)
            {
                Debug.LogWarning("[NullFrontier] URP particle shader missing - using a legacy particle shader.");
                return shader;
            }
            return null;
        }

        private static Material LoadOrCreate(string path, Shader shader, string texturePath,
            BlendState blend)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = material == null;
            if (fresh)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.mainTexture = texture;
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.hideFlags = HideFlags.None;

            if (shader.name.StartsWith("Universal Render Pipeline"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", blend == BlendState.Additive ? 2f : 0f);
                material.SetFloat("_Cull", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_ZTest", 4f); // LEqual: drops must hide behind geometry.
                material.SetFloat("_AlphaModulate", 1f);
                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_EmissionColor", Color.white);
                material.SetFloat("_EmissiveExposureWeight", 0f);
                material.SetFloat("_SrcBlend", blend == BlendState.Additive
                    ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", blend == BlendState.Additive
                    ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TRANSPARENT");
                material.DisableKeyword("_SURFACE_OPAQUE");
                material.EnableKeyword(blend == BlendState.Additive
                    ? "_ADDBLEND_ON" : "_ALPHABLEND_ON");
                material.DisableKeyword(blend == BlendState.Additive
                    ? "_ALPHABLEND_ON" : "_ADDBLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.DisableKeyword("_EMISSION");
                material.renderQueue = 3000;
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
