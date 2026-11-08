using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Dresses VerticalCity with the Kenney CC0 road and industrial kits.
    ///
    /// Nothing here hard-codes a dimension: the module pitch, every building footprint
    /// and every collider is measured from the imported meshes at build time, so the
    /// city comes out at the right scale whatever units the authoring tool wrote and
    /// whatever the model importer's file-scale correction produced.
    /// </summary>
    public static class CityKit
    {
        /// <summary>Metres between road centre lines. The kit is scaled to match.</summary>
        public const float BlockPitch = 13f;
        private const int IntersectionsPerAxis = 7;
        private const int MaxLampLights = 8;
        private const float MaxBuildingHeight = 26f;

        private const string Roads = ArtAssets.KitRoadDir;
        private const string Industrial = ArtAssets.KitIndustrialDir;

        private static readonly string[] BuildingModels =
        {
            "building-a", "building-b", "building-c", "building-d", "building-e", "building-f",
            "building-g", "building-h", "building-i", "building-j", "building-k", "building-l",
            "building-m", "building-n", "building-o", "building-p", "building-q", "building-r",
            "building-s", "building-t"
        };

        private static readonly string[] CoverModels =
        {
            "dumpster", "shipping-container-a", "shipping-container-b", "construction-barrier"
        };

        private static readonly string[] DetailModels =
        {
            "construction-cone", "construction-fence", "road-sign-street", "road-sign-warning",
            "electricity-pole", "water-tower", "detail-tank", "solar-panel-flat"
        };

        private static readonly Color[] NeonColors =
        {
            new Color(0.15f, 0.85f, 1f),
            new Color(1f, 0.25f, 0.6f),
            new Color(0.6f, 0.35f, 1f),
            new Color(0.2f, 1f, 0.65f)
        };

        private static int _groundLayer, _coverLayer, _artLayer;
        private static int _lampLights;
        private static float _roadScale = 1f;
        private static bool _roadScaleLongAxisX;
        private static Transform _cityRoot;
        private static Material _roadMat, _kitMat, _asphaltMat, _concreteMat;
        private static readonly Dictionary<string, Vector3> _extents = new();

        /// <summary>Half the width of the built city, in metres. Used by the spawner.</summary>
        public static float Extent { get; private set; }

        /// <summary>Returns false when the art is not imported, so the caller can greybox.</summary>
        public static bool Build(Transform parent, System.Random rng)
        {
            _cityRoot = parent;
            _lampLights = 0;
            Extent = 0f;
            _extents.Clear();

            _groundLayer = Mathf.Max(0, ProjectSetup.GroundLayer);
            _coverLayer = Mathf.Max(0, ProjectSetup.CoverLayer);
            _artLayer = Mathf.Max(0, ProjectSetup.ArtLayer);

            _roadMat = ArtAssets.GetKitMaterial(Roads, "CityKitRoads");
            _kitMat = ArtAssets.GetKitMaterial(Industrial, "CityKitIndustrial");
            // The shared tint is authored for the night menu. Under daylight the road
            // atlas rendered as pale pavement and the blocks as pastel card; these two
            // city-only copies go a stop darker so the lane paint, the kerbs and the
            // shadows have a value to read against.
            Tint(_roadMat, new Color(0.33f, 0.36f, 0.42f));
            Tint(_kitMat, new Color(0.44f, 0.47f, 0.54f));
            // Both were tuned for a night street, where a sunlit road is the brightest
            // thing in frame anyway. Under the day sun these values kept the ground at
            // near-black and swallowed anything dropped on it.
            // Under the 1.6 sun a 0.26 asphalt rendered as white pavement; real asphalt
            // reads dark even at noon, and it is the value that grounds every other prop.
            _asphaltMat = ArtAssets.GetSolidMaterial("Asphalt", new Color(0.14f, 0.15f, 0.18f), 0.42f);
            _concreteMat = ArtAssets.GetSolidMaterial("Concrete", new Color(0.40f, 0.42f, 0.47f), 0.25f);

            if (!CalibrateRoadModule()) return false;

            float span = (IntersectionsPerAxis - 1) * BlockPitch;
            Extent = span * 0.5f + BlockPitch;

            CreateGround(span);
            CreateRoadGrid(span);
            CreateBlocks(span, rng);
            CreateVerticalPlatforms(span, rng);

            SetStatic(parent.gameObject);
            return true;
        }

        /// <summary>
        /// Reads one straight road module to learn the pack's grid pitch and which axis
        /// the road runs along, then sets the uniform scale that makes it BlockPitch wide.
        /// </summary>
        private static bool CalibrateRoadModule()
        {
            var probe = ArtAssets.LoadModel(Roads, "road-straight");
            if (probe == null)
            {
                Debug.LogError("[NullFrontier] Kenney road kit is not imported; " +
                               "city will fall back to greybox.");
                return false;
            }

            var instance = Object.Instantiate(probe);
            instance.name = "probe";
            try
            {
                Bounds bounds = ArtAssets.CombinedBounds(instance);
                float longAxis = Mathf.Max(bounds.size.x, bounds.size.z);
                if (longAxis <= 0.001f)
                {
                    Debug.LogError("[NullFrontier] Road modules report zero size; " +
                                   "check Assets/Art import settings.");
                    return false;
                }
                _roadScaleLongAxisX = bounds.size.x >= bounds.size.z;
                _roadScale = BlockPitch / longAxis;
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Restyle one city-only kit material copy and keep the asset in sync.</summary>
        private static void Tint(Material material, Color color)
        {
            if (material == null) return;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            UnityEditor.EditorUtility.SetDirty(material);
        }

        // ------------------------------------------------------------------- layout

        private static void CreateGround(float span)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_cityRoot, false);
            ground.transform.localScale = new Vector3((span + BlockPitch * 3f) / 10f, 1f,
                (span + BlockPitch * 3f) / 10f);
            ground.layer = _groundLayer;
            ground.GetComponent<Renderer>().sharedMaterial = _asphaltMat;
        }

        private static void CreateRoadGrid(float span)
        {
            var roads = Child("Roads");
            var props = Child("Props");
            float half = span * 0.5f;
            int segment = 0;

            for (int i = 0; i < IntersectionsPerAxis; i++)
            {
                for (int j = 0; j < IntersectionsPerAxis; j++)
                {
                    float x = -half + i * BlockPitch;
                    float z = -half + j * BlockPitch;

                    // 2 cm above the asphalt: road tiles are thin, and a coplanar
                    // surface would flicker wherever two of them meet.
                    PlaceKit(roads, Roads, PickModel(Roads, "road-intersection", "road-crossroad"),
                        new Vector3(x, 0.02f, z), Quaternion.identity, _roadScale, _roadMat,
                        _artLayer, false, true);

                    if (i < IntersectionsPerAxis - 1)
                        PlaceSegment(roads, props, new Vector3(x + BlockPitch * 0.5f, 0.02f, z), true, segment++);
                    if (j < IntersectionsPerAxis - 1)
                        PlaceSegment(roads, props, new Vector3(x, 0.02f, z + BlockPitch * 0.5f), false, segment++);
                }
            }
        }

        /// <summary>Straight road between two intersections, with a lamp on every third block.</summary>
        private static void PlaceSegment(Transform roads, Transform props, Vector3 midpoint,
            bool alongX, int index)
        {
            Quaternion rotation = Quaternion.identity;
            if (alongX == !_roadScaleLongAxisX) rotation = Quaternion.Euler(0f, 90f, 0f);

            PlaceKit(roads, Roads, PickModel(Roads, "road-straight", "road-square"),
                midpoint, rotation, _roadScale, _roadMat, _artLayer, false, true);

            if (index % 3 != 0) return;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            Vector3 side = alongX ? Vector3.forward : Vector3.right;
            PlaceLamp(props, midpoint - axis * (BlockPitch * 0.5f) + side * (BlockPitch * 0.34f),
                Quaternion.LookRotation(-side));
        }

        private static void CreateBlocks(float span, System.Random rng)
        {
            var buildings = Child("Buildings");
            var signage = Child("Signage");
            var props = Child("Props");
            var cover = Child("Cover");
            float half = span * 0.5f;

            for (int i = 0; i < IntersectionsPerAxis - 1; i++)
            {
                for (int j = 0; j < IntersectionsPerAxis - 1; j++)
                {
                    float x = -half + (i + 0.5f) * BlockPitch;
                    float z = -half + (j + 0.5f) * BlockPitch;

                    // Leave a plaza near spawn so the player is not walled in on load.
                    // Cover is deliberately excluded: a shipping container in the first
                    // cell filled the whole opening frame at eye level. Small site props
                    // keep the plaza from reading as an empty car park.
                    if (Mathf.Abs(x) < BlockPitch * 0.75f && Mathf.Abs(z) < BlockPitch * 0.75f)
                    {
                        if (rng.NextDouble() < 0.5)
                            PlaceDetail(props, new Vector3(x, 0f, z), rng);
                        continue;
                    }

                    if (rng.NextDouble() < 0.18)
                    {
                        PlaceCover(cover, new Vector3(x, 0f, z), rng);
                        PlaceDetail(props, new Vector3(x, 0f, z), rng);
                        continue;
                    }

                    string model = BuildingModels[rng.Next(BuildingModels.Length)];
                    Vector3 native = Extents(Industrial, model);
                    if (Mathf.Max(native.x, native.z) <= 0.001f) continue;

                    float cellRoom = BlockPitch * (0.62f + (float)rng.NextDouble() * 0.2f);

                    // The tallest tower in the kit is authored much taller than a city
                    // block, so height caps the fit to keep the skyline readable.
                    float scale = Mathf.Min(cellRoom / Mathf.Max(native.x, native.z),
                        MaxBuildingHeight / Mathf.Max(0.5f, native.y));
                    float yaw = rng.Next(4) * 90f;

                    var go = PlaceKit(buildings, Industrial, model, new Vector3(x, 0f, z),
                        Quaternion.Euler(0f, yaw, 0f), scale, _kitMat, _coverLayer, true, true);
                    if (go == null) continue;

                    AddRooftopDetails(signage, go, NeonColors[rng.Next(NeonColors.Length)], rng);
                    AddFacadePanel(signage, go, rng);
                    if (rng.NextDouble() < 0.55)
                        PlaceCover(cover, new Vector3(x + cellRoom * 0.6f, 0f, z), rng);
                }
            }
        }

        private static Vector3 ProbeExtents(GameObject model)
        {
            var instance = Object.Instantiate(model);
            try
            {
                return ArtAssets.CombinedBounds(instance).size;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Water tank or chimney on the roof plus a band of emissive signage. These are
        /// placed under an unscaled root and positioned from world bounds, so the
        /// building's fit-to-cell scale never distorts them.
        /// </summary>
        private static void AddRooftopDetails(Transform root, GameObject building, Color neon,
            System.Random rng)
        {
            Bounds b = ArtAssets.CombinedBounds(building);
            float radius = Mathf.Min(b.size.x, b.size.z) * 0.5f;

            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "Signage";
            band.transform.SetParent(root, false);
            band.transform.position = new Vector3(b.center.x, b.max.y - 0.7f, b.max.z - 0.06f);
            band.transform.localScale = new Vector3(radius * 1.6f, 0.4f, 0.14f);
            band.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetNeonMaterial("Signage", neon, 3.2f);
            band.layer = _artLayer;
            Object.DestroyImmediate(band.GetComponent<Collider>());

            string detail = rng.NextDouble() < 0.5 ? "detail-tank" : "chimney-medium";
            var prefab = ArtAssets.LoadModel(Industrial, detail);
            if (prefab == null) return;

            Vector3 native = Extents(Industrial, detail);
            if (Mathf.Max(native.x, native.z) <= 0.001f) return;

            var roofItem = Object.Instantiate(prefab, root);
            roofItem.name = "Roof_" + detail;
            roofItem.transform.localScale = Vector3.one * (radius * 0.6f / Mathf.Max(native.x, native.z));
            roofItem.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            SetLayerRecursive(roofItem, _artLayer);
        }

        /// <summary>
        /// A window grid and, on some blocks, a holo screen on one facade. The kit models
        /// are flat-shaded painted boxes; the additive panel supplies the office windows
        /// that make a block read as a tower rather than a painted crate, at one draw call
        /// each and no change to the collider.
        /// </summary>
        private static void AddFacadePanel(Transform root, GameObject building,
            System.Random rng)
        {
            // Compound kit models are a tower plus low wings; the combined bounds are
            // much larger than any single wall, so panels measured from them floated off
            // the facade. The largest-volume renderer is the main mass the windows belong
            // on.
            Bounds b = MainMassBounds(building);
            var windowMaterial =
                ArtAssets.GetWindowMaterial("CityFacade", new Color(0.24f, 0.55f, 1f), 0.4f);
            // The lit-window shader is additive, so on its own it left the wall as a flat
            // painted panel with faint stripes. A dark, smooth glazing sheet underneath
            // gives the facade a readable glass plane by day and a base for the lit cells
            // to sit on at night.
            var glassMaterial = ArtAssets.GetSolidMaterial("CityGlass",
                new Color(0.07f, 0.09f, 0.13f), 0.9f);
            float y = b.center.y;
            float height = b.size.y * 0.72f;

            // One panel on one random side left every other approach to a block a blank
            // painted wall - the opening shot looked at exactly such a face. Wrapping all
            // four sides makes the district read as offices from any heading.
            AddWindowQuad(root, new Vector3(b.max.x + 0.04f, y, b.center.z),
                -90f, b.size.z * 0.80f, height, Vector3.right, glassMaterial, windowMaterial);
            AddWindowQuad(root, new Vector3(b.min.x - 0.04f, y, b.center.z),
                90f, b.size.z * 0.80f, height, Vector3.left, glassMaterial, windowMaterial);
            AddWindowQuad(root, new Vector3(b.center.x, y, b.max.z + 0.04f),
                180f, b.size.x * 0.80f, height, Vector3.forward, glassMaterial, windowMaterial);
            AddWindowQuad(root, new Vector3(b.center.x, y, b.min.z - 0.04f),
                0f, b.size.x * 0.80f, height, Vector3.back, glassMaterial, windowMaterial);

            if (rng.NextDouble() > 0.2) return;

            bool alongX = rng.Next(2) == 0;
            float x = alongX
                ? (rng.Next(2) == 0 ? b.max.x + 0.25f : b.min.x - 0.25f)
                : b.center.x;
            float z = alongX
                ? b.center.z
                : (rng.Next(2) == 0 ? b.max.z + 0.25f : b.min.z - 0.25f);
            float yaw = alongX ? (x > b.center.x ? -90f : 90f) : (z > b.center.z ? 180f : 0f);

            Color holo = NeonColors[rng.Next(NeonColors.Length)];
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "HoloSign";
            panel.transform.SetParent(root, false);
            panel.transform.position = new Vector3(x, b.center.y + b.size.y * 0.18f, z);
            panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            panel.transform.localScale = new Vector3(4.4f, 7.6f, 1f);
            var renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial =
                ArtAssets.GetHoloMaterial("CitySign", holo, 0.9f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            panel.layer = _artLayer;
            Object.DestroyImmediate(panel.GetComponent<Collider>());
        }

        /// <summary>The renderer with the most volume: the block's main mass, not its wings.</summary>
        private static Bounds MainMassBounds(GameObject building)
        {
            Bounds best = ArtAssets.CombinedBounds(building);
            float bestVolume = 0f;
            foreach (var renderer in building.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                float volume = bounds.size.x * bounds.size.y * bounds.size.z;
                if (volume <= bestVolume) continue;
                bestVolume = volume;
                best = bounds;
            }
            return best;
        }

        /// <summary>Glazing plus lit windows on one wall; yaw makes the quad face outward.</summary>
        private static void AddWindowQuad(Transform root, Vector3 position, float yaw,
            float width, float height, Vector3 outward, Material glass, Material windows)
        {
            AddFacadeQuad(root, "FacadeGlass", position, yaw, width, height, glass);
            // 2 cm proud of the glazing: coplanar quads would z-fight into a flicker.
            AddFacadeQuad(root, "FacadeWindows", position + outward * 0.02f,
                yaw, width, height, windows);
        }

        private static void AddFacadeQuad(Transform root, string name, Vector3 position,
            float yaw, float width, float height, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(root, false);
            quad.transform.position = position;
            quad.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            quad.transform.localScale = new Vector3(width, height, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            quad.layer = _artLayer;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
        }

        // -------------------------------------------------------------- set dressing

        private static void CreateVerticalPlatforms(float span, System.Random rng)
        {
            var platforms = Child("Platforms");
            var cover = Child("Cover");
            float half = span * 0.5f * 0.55f;

            Vector3[] sites =
            {
                new Vector3(-half, 0f, half), new Vector3(half, 0f, -half),
                new Vector3(half, 0f, half), new Vector3(-half, 0f, -half)
            };

            foreach (Vector3 site in sites)
            {
                Quaternion turn = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
                BuildPlatform(platforms, site, turn, rng);
            }
        }

        /// <summary>
        /// A 1.4 m deck with a stepped ramp: stair treads bake reliably into the NavMesh
        /// and stay within the agent's climb limit, which a single steep ramp would not.
        /// </summary>
        private static void BuildPlatform(Transform parent, Vector3 site, Quaternion turn,
            System.Random rng)
        {
            var group = new GameObject("Platform").transform;
            group.SetParent(parent, false);
            group.position = site;
            group.rotation = turn;

            const float deckTop = 1.4f;
            const float deckSize = 7f;
            const int steps = 4;

            Box(group, "Deck", new Vector3(0f, deckTop * 0.5f, 0f),
                new Vector3(deckSize, deckTop, deckSize), _concreteMat, _coverLayer);

            for (int s = 0; s < steps; s++)
            {
                float height = deckTop * (s + 1) / steps;
                float z = deckSize * 0.5f + (steps - s) * 0.9f;
                Box(group, "Step", new Vector3(0f, height * 0.5f, z),
                    new Vector3(3f, height, 0.9f), _concreteMat, _coverLayer);
            }

            Color neon = NeonColors[rng.Next(NeonColors.Length)];
            var strip = ArtAssets.GetNeonMaterial("DeckEdge", neon, 2.6f);
            for (int side = 0; side < 4; side++)
            {
                if (side == 0) continue; // +Z is where the ramp meets the deck
                float yaw = side * 90f;
                float angle = yaw * Mathf.Deg2Rad;
                var edge = Box(group, "DeckNeon",
                    new Vector3(Mathf.Sin(angle) * (deckSize * 0.5f), deckTop + 0.06f,
                        Mathf.Cos(angle) * (deckSize * 0.5f)),
                    new Vector3(deckSize * 0.96f, 0.12f, 0.12f), strip, _artLayer);
                edge.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Object.DestroyImmediate(edge.GetComponent<Collider>());
            }

            CoverMarker(group, new Vector3(-2f, deckTop, -2f));
            CoverMarker(group, new Vector3(2f, deckTop, 2f));
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPos,
            Vector3 size, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.layer = layer;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void PlaceLamp(Transform props, Vector3 position, Quaternion rotation)
        {
            // Cover, not Art: a lamp with no collider is a six-metre pole the player can
            // see and the bots walk straight through, which is what reads as clipping.
            var go = PlaceKit(props, Roads, PickModel(Roads, "light-curved", "light-square"),
                position, rotation, _roadScale, _roadMat, _coverLayer, true, true);
            if (go == null || _lampLights >= MaxLampLights) return;

            Bounds bounds = ArtAssets.CombinedBounds(go);
            var head = new GameObject("LampLight");
            head.transform.SetParent(props, false);
            head.transform.position = new Vector3(position.x, bounds.max.y - 0.2f, position.z);

            var light = head.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.65f, 0.85f, 1f);
            light.range = 11f;
            light.intensity = 2.1f;
            light.shadows = LightShadows.None;
            // Vertex lighting keeps additional lights off the mobile fragment budget.
            light.renderMode = LightRenderMode.ForceVertex;
            _lampLights++;
        }

        private static void PlaceDetail(Transform props, Vector3 position, System.Random rng)
        {
            string model = DetailModels[rng.Next(DetailModels.Length)];
            // 2 m reads as believable street clutter whatever units the pack shipped in.
            // Solid clutter is baked and collided for the same reason lamps are: bots
            // walking through a dumpster is the single most obvious tell that the AI
            // does not live in the world it is standing in.
            PlaceSized(props, model, position + new Vector3(2.4f, 0f, -1.8f),
                Quaternion.Euler(0f, rng.Next(360), 0f), 2f, _coverLayer, true);
        }

        /// <summary>
        /// A waist-high prop agents can shoot over and hide behind, plus the CoverPoint
        /// the soldier AI retreats to. The marker goes on bare ground beside it, because
        /// a marker inside the prop's collider is unreachable on the NavMesh.
        /// </summary>
        private static void PlaceCover(Transform cover, Vector3 position, System.Random rng)
        {
            string model = CoverModels[rng.Next(CoverModels.Length)];
            var go = PlaceSized(cover, model, position, Quaternion.Euler(0f, rng.Next(4) * 90f, 0f),
                2.8f, _coverLayer, true);
            if (go == null) return;

            Bounds bounds = ArtAssets.CombinedBounds(go);
            float reach = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f + 0.9f;
            Vector3 side = rng.NextDouble() < 0.5 ? Vector3.right : Vector3.forward;
            CoverMarker(cover, new Vector3(bounds.center.x + side.x * reach, 0.1f,
                bounds.center.z + side.z * reach));
        }

        /// <summary>
        /// The Transform DigitalSoldier.FindNearestCover() looks for. Must sit on the
        /// NavMesh at ground level, so it is placed beside the prop rather than on top.
        /// </summary>
        private static void CoverMarker(Transform parent, Vector3 worldPosition)
        {
            var marker = new GameObject("CoverPoint");
            marker.transform.SetParent(parent, false);
            marker.transform.position = worldPosition;
            marker.tag = "CoverPoint";
            marker.layer = _artLayer;
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>Unscaled mesh extents, probed once per model.</summary>
        private static Vector3 Extents(string packDir, string model)
        {
            string key = packDir + "/" + model;
            if (_extents.TryGetValue(key, out Vector3 cached)) return cached;

            var prefab = ArtAssets.LoadModel(packDir, model);
            Vector3 value = prefab == null ? Vector3.zero : ProbeExtents(prefab);
            _extents[key] = value;
            return value;
        }

        /// <summary>
        /// Places a model scaled so its footprint is the target size in metres. The two
        /// packs were authored in different units, so one shared scale factor would make
        /// one kit's props tower over the other's; measuring each model keeps the whole
        /// street at human scale.
        /// </summary>
        private static GameObject PlaceSized(Transform parent, string model, Vector3 position,
            Quaternion rotation, float targetFootprint, int layer, bool addCollider)
        {
            string pack = ResolvePack(model);
            if (pack == null) return null;

            Vector3 native = Extents(pack, model);
            if (Mathf.Max(native.x, native.z) <= 0.001f) return null;

            var material = pack == Industrial ? _kitMat : _roadMat;
            return PlaceKit(parent, pack, model, position, rotation,
                targetFootprint / Mathf.Max(native.x, native.z), material, layer, addCollider, true);
        }

        private static string ResolvePack(string model)
        {
            if (ArtAssets.HasModel(Roads, model)) return Roads;
            if (ArtAssets.HasModel(Industrial, model)) return Industrial;
            Debug.LogWarning($"[NullFrontier] City prop \"{model}\" is not in either kit.");
            return null;
        }

        private static GameObject PlaceKit(Transform parent, string packDir, string model,
            Vector3 position, Quaternion rotation, float scale, Material material,
            int layer, bool addCollider, bool snapToGround = false)
        {
            var prefab = ArtAssets.LoadModel(packDir, model);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, parent);
            go.name = model;
            go.transform.localScale = Vector3.one * scale;

            go.transform.position = position;
            go.transform.rotation = rotation;

            // Meshes are not authored around their origin, so resting them on the
            // pavement has to be measured rather than assumed.
            if (snapToGround)
            {
                Bounds placed = ArtAssets.CombinedBounds(go);
                go.transform.position += Vector3.up * (position.y - placed.min.y);
            }

            if (addCollider)
            {
                Bounds bounds = ArtAssets.CombinedBounds(go);
                var collider = go.AddComponent<BoxCollider>();
                float s = Mathf.Max(0.0001f, scale);
                collider.center = go.transform.InverseTransformPoint(bounds.center);
                collider.size = new Vector3(bounds.size.x / s, bounds.size.y / s, bounds.size.z / s);
            }

            if (material != null)
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = material;

            SetLayerRecursive(go, layer);
            return go;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private static string PickModel(string packDir, string preferred, string fallback)
        {
            if (ArtAssets.LoadModel(packDir, preferred) != null) return preferred;
            var names = ArtAssets.ModelNames(packDir);
            if (names.Contains(fallback)) return fallback;
            return names.Count > 0 ? names[0] : preferred;
        }

        private static Transform Child(string name)
        {
            var existing = _cityRoot.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(_cityRoot, false);
            return go.transform;
        }

        private static void SetStatic(GameObject root)
        {
            // Batching only: the city never moves, and the two kit materials otherwise
            // mean hundreds of individual draw calls on a mid-range phone.
            foreach (Transform child in root.transform) SetStatic(child.gameObject);
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
        }
    }
}
