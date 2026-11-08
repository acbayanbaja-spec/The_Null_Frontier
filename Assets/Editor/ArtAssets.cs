using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Typed access to the licensed packs: model lookup, URP material baking,
    /// mesh-bounds measurement and animation-clip resolution. Everything here is
    /// art-only; gameplay assembly lives in SceneBuilder.
    /// </summary>
    public static class ArtAssets
    {
        public const string Root = "Assets/Art";
        public const string KitRoadDir = Root + "/Kenney/CityKitRoads";
        public const string KitIndustrialDir = Root + "/Kenney/CityKitIndustrial";
        public const string CharacterDir = Root + "/Quaternius/Characters";
        public const string SpaceDir = Root + "/Space";
        public const string MaterialDir = Root + "/Materials";
        public const string RigDir = Root + "/Rigs";
        public const string CharacterPrefabDir = "Assets/Resources/Characters";
        public const string EnemyPrefabDir = "Assets/Resources/Enemies";

        private static Shader _litShader;
        private static Shader _unlitShader;
        private static Shader _vertexColourShader;
        private static readonly Dictionary<string, Material> MaterialCache = new();
        private static readonly Dictionary<string, GameObject> ModelCache = new();

        public static Shader LitShader =>
            _litShader ??= Shader.Find("Universal Render Pipeline/Lit");

        /// <summary>
        /// Self-lit surfaces. Emission on URP/Lit is gated behind a shader_feature that
        /// this project's material assets did not reliably keep, so every surface that
        /// only needs to glow uses Unlit instead: it always draws its own colour, and
        /// its blend state is driven by properties rather than keywords.
        /// </summary>
        public static Shader UnlitShader =>
            _unlitShader ??= Shader.Find("Universal Render Pipeline/Unlit") ?? LitShader;

        /// <summary>Lit-quality shader that multiplies in the mesh vertex-colour channel.</summary>
        public static Shader VertexColourShader =>
            _vertexColourShader ??= Shader.Find("Universal Render Pipeline/Particles/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit");

        public static void ResetCaches()
        {
            MaterialCache.Clear();
            ModelCache.Clear();
        }

        // ---------------------------------------------------------------- folders

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static string SafeName(string raw)
        {
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else sb.Append('_');
            }
            return sb.Length == 0 ? "material" : sb.ToString();
        }

        // ------------------------------------------------------------------ models

        public static GameObject LoadModel(string dir, string fileNameWithoutExtension)
        {
            string path = $"{dir}/{fileNameWithoutExtension}.fbx";
            if (ModelCache.TryGetValue(path, out var cached)) return cached;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                Debug.LogError($"[NullFrontier] Model not imported yet: {path}");
            ModelCache[path] = model;
            return model;
        }

        /// <summary>Quiet existence probe, for pack lookups where a miss is normal.</summary>
        public static bool HasModel(string dir, string fileNameWithoutExtension)
            => File.Exists($"{dir}/{fileNameWithoutExtension}.fbx");

        public static List<string> ModelNames(string dir)
        {
            var names = new List<string>();
            if (!Directory.Exists(dir)) return names;
            foreach (string file in Directory.GetFiles(dir, "*.fbx"))
                names.Add(Path.GetFileNameWithoutExtension(file));
            names.Sort(System.StringComparer.Ordinal);
            return names;
        }

        /// <summary>World-space union of every renderer on a live instance.</summary>
        public static Bounds CombinedBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Scales the instance so its visual height matches targetHeight metres,
        /// which is what makes a pack authored in centimetres or in decimetres drop
        /// straight into a 1.8 m CharacterController. Returns the applied factor.
        /// When maxFootprint is positive the scale is also capped so neither horizontal
        /// side exceeds it: kit blocks have wildly different aspect ratios, and a short
        /// wide block fitted to a tall height otherwise becomes a slab wider than the
        /// street it is supposed to line.
        /// </summary>
        public static float FitToHeight(GameObject instance, float targetHeight,
            float maxFootprint = 0f)
        {
            Bounds bounds = CombinedBounds(instance);
            float measured = bounds.size.y;
            if (measured <= 0.0001f) return 1f;

            float factor = targetHeight / measured;
            if (maxFootprint > 0.0001f)
            {
                float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                if (footprint > 0.0001f)
                    factor = Mathf.Min(factor, maxFootprint / footprint);
            }

            instance.transform.localScale = Vector3.one * factor;
            return factor;
        }

        // -------------------------------------------------------------- materials

        /// <summary>
        /// Kenney's colormap atlas is a bright, fully saturated toy palette. Multiplying
        /// it toward cool grey is what makes the same blocks read as concrete, glass and
        /// weathered steel under the city lights.
        /// </summary>
        // Daylit renders exposed how little this multiplied: peach walls and mint poles
        // still read as a toy set. Pulling the tint down to a cool mid grey is what turns
        // the same atlas into weathered concrete and painted steel.
        private static readonly Color KitTint = new Color(0.60f, 0.64f, 0.72f, 1f);

        /// <summary>
        /// One shared URP material per city kit, driven by the pack's single
        /// colormap atlas. Every kit piece then shares two materials in the build.
        /// </summary>
        public static Material GetKitMaterial(string packDir, string label)
        {
            string texturePath = packDir + "/Textures/colormap.png";
            string assetPath = $"{MaterialDir}/Kit_{SafeName(label)}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var hit)) return hit;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool fresh = material == null;
            if (fresh)
            {
                material = NewUrpMaterial();
                EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null)
            {
                material.mainTexture = texture;
                material.SetTexture("_BaseMap", texture);
            }
            else
            {
                Debug.LogWarning($"[NullFrontier] Kit texture missing at {texturePath}; {label} uses flat colour.");
            }

            // Restyled on every run, including when the .mat already exists: an out-of-date
            // cached material would otherwise keep the original saturated look forever.
            material.SetFloat("_Smoothness", 0.22f);
            material.SetFloat("_Metallic", 0.08f);
            material.color = KitTint;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", KitTint);
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>
        /// Replaces a character's legacy FBX materials with saved URP copies that
        /// keep the source colour, plus a faint accent emission so the roster reads
        /// against the neon city instead of being swallowed by bloom.
        /// </summary>
        public static void BakeCharacterMaterials(GameObject instance, string modelName, Color accent)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                bool vertexColoured = HasVertexColors(renderer);

                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (source == null) continue;

                    string assetPath = $"{MaterialDir}/Characters/{SafeName(modelName)}_{SafeName(source.name)}.mat";
                    if (!MaterialCache.TryGetValue(assetPath, out var baked))
                    {
                        baked = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                        bool fresh = baked == null;
                        if (fresh)
                        {
                            baked = new Material(VertexColourShader);
                            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
                            AssetDatabase.CreateAsset(baked, assetPath);
                        }

                        // Restyle every time, not only on creation: these assets were
                        // generated when the vertex-colour channel was still being dropped,
                        // and a cached near-black material would survive the import fix.
                        Restyle(baked, source, vertexColoured, accent);
                        MaterialCache[assetPath] = baked;
                    }
                    slots[i] = baked;
                }
                renderer.sharedMaterials = slots;
            }
        }

        /// <summary>
        /// Character surfaces are flat vertex-coloured geometry, so they need a shader that
        /// multiplies the base colour by the vertex colour. URP/Lit ignores that channel
        /// entirely; URP/Particles/Lit is the shipped Lit-quality shader that reads it.
        /// </summary>
        private static void Restyle(Material baked, Material source, bool vertexColoured, Color accent)
        {
            baked.shader = VertexColourShader;
            var albedo = source.mainTexture;
            baked.SetTexture("_BaseMap", albedo);

            // A vertex-coloured mesh must not be tinted: any multiplier darkens the palette.
            Color baseColor = albedo != null ? Color.white
                : vertexColoured ? Color.white
                : Readable(source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color);

            // The Quaternius packs ship neither textures nor a vertex-colour channel, so a
            // part's only colour is its flat FBX material value - which the night grade then
            // flattens to grey. Pulling the operative's accent through every non-flesh part
            // is what gives the roster six distinguishable silhouettes.
            bool flesh = IsFlesh(source.name);
            if (!flesh) baseColor = Color.Lerp(baseColor, accent, 0.34f);
            baked.SetColor("_BaseColor", baseColor);

            baked.SetFloat("_Smoothness", flesh ? 0.18f : 0.34f);
            baked.SetFloat("_Metallic", 0.02f);
            baked.SetFloat("_Cull", 2f);
            baked.SetFloat("_Surface", 0f);
            baked.SetFloat("_SrcBlend", 2f);
            baked.SetFloat("_DstBlend", 7f);
            baked.SetFloat("_ZWrite", 1f);
            baked.SetColor("_EmissionColor", accent * (flesh ? 0.03f : 0.09f));
            baked.EnableKeyword("_EMISSION");
            baked.DisableKeyword("_COLORADDSUBDIFF_ON");
            baked.DisableKeyword("_ALPHABLEND_ON");
            EditorUtility.SetDirty(baked);
        }

        private static readonly string[] FleshParts =
        {
            "skin", "face", "teeth", "eye", "brain", "guts", "bone",
        };

        /// <summary>Flesh keeps its own colour; only kit, cloth and metal take the accent.</summary>
        private static bool IsFlesh(string partName)
        {
            if (string.IsNullOrEmpty(partName)) return false;
            var lower = partName.ToLowerInvariant();
            foreach (var marker in FleshParts)
            {
                if (lower.Contains(marker)) return true;
            }
            return false;
        }

        /// <summary>Keeps a textureless part from reading as a hole in the silhouette.</summary>
        private static Color Readable(Color c)
        {
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            // Lifted toward a dark neutral, not a light one: a pale substitute is what made
            // every cloth part read as the same grey figure under the night grade.
            return peak >= 0.3f ? c : Color.Lerp(c, new Color(0.24f, 0.27f, 0.33f), 0.75f);
        }

        private static bool HasVertexColors(Renderer renderer)
        {
            Mesh mesh = renderer switch
            {
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                MeshRenderer => renderer.GetComponent<MeshFilter>() != null
                    ? renderer.GetComponent<MeshFilter>().sharedMesh : null,
                _ => null,
            };
            return mesh != null && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color);
        }

        /// <summary>Pure emissive surface for neon strips, holo props and scan rings.</summary>
        public static Material GetNeonMaterial(string label, Color color, float gain)
        {
            // The colour is part of the file name: two callers that reuse a label with a
            // different colour must not silently share one material asset.
            string key = $"{SafeName(label)}_{Mathf.RoundToInt(color.r * 255)}" +
                         $"-{Mathf.RoundToInt(color.g * 255)}-{Mathf.RoundToInt(color.b * 255)}";
            string assetPath = $"{MaterialDir}/Neon_{key}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            // Restyled on every run: the gain is the HDR brightness that puts the
            // surface over the bloom threshold, so a cached copy with an old gain would
            // read as a flat coloured strip for the life of the project.
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = UnlitShader;
            // Over 1 on purpose: Unlit writes this straight to the HDR target, so the
            // gain is what makes the strip bloom instead of merely being bright paint.
            material.SetColor("_BaseColor", color * gain);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>Flat-colour surface for concrete, asphalt and generated platforms.</summary>
        public static Material GetSolidMaterial(string label, Color color, float smoothness)
        {
            string assetPath = $"{MaterialDir}/Solid_{SafeName(label)}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            // Restyled every run for the same reason as the kit materials: an existing
            // asset would otherwise keep whatever asphalt colour the first build wrote.
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        private static readonly Vector4 EarthSun =
            new Vector4(-0.45f, 0.35f, 0.82f, 0f);

        private static Shader CustomShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader != null) return shader;
            Debug.LogWarning($"[NullFrontier] Shader {name} not found; falling back to Unlit.");
            return UnlitShader;
        }

        /// <summary>
        /// The planet's surface. A fixed sun built into the shader supplies the terminator,
        /// ocean specular, warm sunset band and night-side city lights; the scene's own
        /// directional light points away from the visible hemisphere and would leave a
        /// lit-material Earth as a dark disc. The shader also samples the cloud sheet to
        /// shade the ground under the weather.
        /// </summary>
        public static Material GetEarthMaterial()
        {
            string assetPath = $"{MaterialDir}/Space_Earth.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            // Restyled on every run: a cached copy would keep an old exposure for the
            // life of the project.
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpaceDir}/Earth_Day.jpg");
            if (albedo == null)
                Debug.LogWarning($"[NullFrontier] Earth map missing at {SpaceDir}/Earth_Day.jpg");
            var clouds = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpaceDir}/Earth_Clouds.jpg");

            material.shader = CustomShader("NullFrontier/EarthSurface");
            material.SetTexture("_MainTex", albedo);
            if (clouds != null) material.SetTexture("_CloudTex", clouds);
            material.SetVector("_SunDirection", EarthSun);
            material.SetColor("_SunColor", new Color(1.0f, 0.95f, 0.88f, 1f));
            material.SetColor("_AmbientColor", new Color(0.11f, 0.15f, 0.26f, 1f));
            material.SetColor("_NightColor", new Color(0.015f, 0.022f, 0.045f, 1f));
            material.SetColor("_AtmosphereColor", new Color(0.30f, 0.55f, 1f, 1f));
            material.SetColor("_SunsetColor", new Color(1f, 0.55f, 0.22f, 1f));
            material.SetColor("_SpecColor", new Color(0.75f, 0.88f, 1f, 1f));
            material.SetColor("_CityLightColor", new Color(1f, 0.70f, 0.32f, 1f));
            material.SetFloat("_AtmosphereStrength", 0.55f);
            material.SetFloat("_SunsetStrength", 0.65f);
            material.SetFloat("_SpecIntensity", 0.8f);
            material.SetFloat("_Smoothness", 0.86f);
            material.SetFloat("_CityLightStrength", 0.8f);
            material.SetFloat("_CloudShadow", 0.35f);
            material.SetVector("_CloudOffset", new Vector4(0.013f, 0f, 0f, 0f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>
        /// A cloud shell. Coverage is the sheet's luminance; colour is shaded by the same
        /// fixed sun as the surface, so the weather dims through the terminator. Two shells
        /// are built from this method: the low deck and a thinner, faster cirrus layer.
        /// </summary>
        public static Material GetCloudMaterial()
        {
            // At 0.92 the low deck covered the continents completely and the planet read
            // as a white ball; the surface map carries most of the detail.
            return GetCloudMaterial("Space_Clouds", 0.6f);
        }

        public static Material GetCloudMaterial(string label, float opacity)
        {
            string assetPath = $"{MaterialDir}/{label}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            var coverage = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpaceDir}/Earth_Clouds.jpg");
            if (coverage == null)
                Debug.LogWarning($"[NullFrontier] Cloud map missing at {SpaceDir}/Earth_Clouds.jpg");

            material.shader = CustomShader("NullFrontier/EarthClouds");
            material.SetTexture("_MainTex", coverage);
            material.SetVector("_SunDirection", EarthSun);
            material.SetColor("_SunColor", new Color(1.0f, 0.96f, 0.92f, 1f));
            material.SetColor("_NightColor", new Color(0.04f, 0.06f, 0.11f, 1f));
            material.SetColor("_Tint", new Color(0.96f, 0.98f, 1f, 1f));
            material.SetColor("_SunsetColor", new Color(1f, 0.55f, 0.22f, 1f));
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_Softness", 0.16f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>Additive fresnel shell that hugs the planet silhouette.</summary>
        public static Material GetAtmosphereMaterial()
        {
            string assetPath = $"{MaterialDir}/Space_AtmosphereShell.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = CustomShader("NullFrontier/AtmosphereShell");
            material.SetColor("_Color", new Color(0.26f, 0.52f, 1f, 1f));
            material.SetFloat("_Strength", 0.85f);
            material.SetFloat("_Power", 3.4f);
            material.SetVector("_SunDirection", EarthSun);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>Animated energy membrane seen through the gate ring.</summary>
        public static Material GetPortalMaterial()
        {
            string assetPath = $"{MaterialDir}/Portal_Membrane.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = CustomShader("NullFrontier/PortalVortex");
            material.SetColor("_Color", new Color(0.32f, 0.76f, 1f, 1f));
            material.SetFloat("_Intensity", 0.85f);
            material.SetFloat("_Speed", 0.45f);
            material.SetFloat("_Ribbons", 5f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>Lit-window grid painted onto one facade quad.</summary>
        public static Material GetWindowMaterial(string label, Color cool, float intensity)
        {
            string assetPath = $"{MaterialDir}/Windows_{SafeName(label)}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = CustomShader("NullFrontier/WindowGrid");
            material.SetColor("_CoolColor", cool);
            material.SetColor("_WarmColor", new Color(1f, 0.68f, 0.34f, 1f));
            material.SetFloat("_Columns", 4f);
            material.SetFloat("_Rows", 11f);
            material.SetFloat("_LitRatio", 0.42f);
            material.SetFloat("_Intensity", intensity);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>Abstract holographic shopfront panel.</summary>
        public static Material GetHoloMaterial(string label, Color color, float intensity)
        {
            string assetPath = $"{MaterialDir}/Holo_{SafeName(label)}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = CustomShader("NullFrontier/HoloSign");
            material.SetColor("_Color", color);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Scroll", 1.2f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>Additive particle material built on URP's shipped particle shader.</summary>
        public static Material GetParticleMaterial(string label, Color color)
        {
            string assetPath = $"{MaterialDir}/Particle_{SafeName(label)}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material = shader != null ? new Material(shader) : NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            var glow = UiFactory.GlowSprite != null ? UiFactory.GlowSprite.texture : null;
            if (glow != null) material.SetTexture("_BaseMap", glow);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);   // additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        /// <summary>Polished wet surface for puddles and the cable bundles.</summary>
        public static Material GetWetMaterial(string label, Color color, float smoothness,
            float metallic)
        {
            string assetPath = $"{MaterialDir}/Wet_{SafeName(label)}.mat";
            if (MaterialCache.TryGetValue(assetPath, out var cached)) return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = NewUrpMaterial();
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);

            MaterialCache[assetPath] = material;
            return material;
        }

        private static Material NewUrpMaterial()
        {
            var shader = LitShader;
            if (shader == null)
            {
                Debug.LogError("[NullFrontier] URP Lit shader not found - materials will render pink.");
                shader = Shader.Find("Standard");
            }
            return new Material(shader);
        }

        // --------------------------------------------------------------- animation

        /// <summary>
        /// FBX take names arrive as "Armature|Walk" (Blender action naming), so
        /// matches are made on the bare last segment, case-insensitively.
        /// </summary>
        public static AnimationClip FindClip(GameObject model, params string[] wantedNames)
        {
            if (model == null) return null;

            var clips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model));
            foreach (var wanted in wantedNames)
            {
                foreach (var obj in clips)
                {
                    if (obj is not AnimationClip clip || clip.legacy) continue;
                    if (NormalisedName(clip.name) == NormalisedName(wanted)) return clip;
                }
            }

            foreach (var obj in clips)
            {
                if (obj is not AnimationClip clip || clip.legacy) continue;
                foreach (var wanted in wantedNames)
                {
                    if (NormalisedName(clip.name).Contains(NormalisedName(wanted))) return clip;
                }
            }
            return null;
        }

        public static IEnumerable<AnimationClip> AllClips(GameObject model)
        {
            if (model == null) yield break;
            string path = AssetDatabase.GetAssetPath(model);
            if (string.IsNullOrEmpty(path)) yield break;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is AnimationClip clip && !clip.legacy) yield return clip;
        }

        private static string NormalisedName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            int bar = raw.LastIndexOf('|');
            string tail = bar >= 0 ? raw.Substring(bar + 1) : raw;
            return tail.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        }
    }
}
