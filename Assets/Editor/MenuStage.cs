using UnityEditor;
using UnityEngine;
using NullFrontier.UI;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// The menu's backdrop: a rain-soaked avenue built from the same licensed kits as
    /// the city, so the first frame the player sees is the world they are about to
    /// enter rather than a plinth floating in fog. Everything here is static geometry
    /// plus sprite glows - no extra cameras, no realtime reflections, no particle
    /// systems - because the menu has to open instantly on a phone.
    /// </summary>
    public static class MenuStage
    {
        private const string Roads = ArtAssets.KitRoadDir;
        private const string Industrial = ArtAssets.KitIndustrialDir;

        private const float Street = 13f;      // building face distance from the centre line
        private const float BlockPitch = 15f;
        // Height fitting alone lets a short, wide kit block balloon into the road; this
        // caps either horizontal side so the silhouette still reads as a street wall.
        private const float MaxBlockFootprint = 22f;
        private const int BlocksPerSide = 5;
        private const float TileLength = 15f;

        private static readonly string[] BuildingModels =
        {
            "building-a", "building-c", "building-e", "building-g", "building-i",
            "building-k", "building-m", "building-o", "building-q", "building-t"
        };

        private static readonly Color[] SignColors =
        {
            new Color(0.15f, 0.85f, 1f),
            new Color(1f, 0.25f, 0.6f),
            new Color(0.6f, 0.35f, 1f),
            new Color(0.2f, 1f, 0.65f),
        };

        // The gate is the subject of the shot: the avenue runs toward it, the skyline
        // stops short of it and the planet hangs behind it. At 72 m it read as a distant
        // hoop; it now stands 38 m out, tall enough to arch over the operative, so the
        // first frame is "a man about to walk through a gate", which is the reference's
        // composition. The camera sits about 5 m back and 1.3 m up inside a 50 degree
        // cone, which keeps the whole ring inside the top edge.
        private const string Generated = ArtAssets.Root + "/Generated";
        private const float GateZ = -38f;
        private const float GateY = 9.6f;
        private const float GateRadius = 10.6f;

        // The world seen through the gate. 30 m across at 88 m subtends about 18 degrees,
        // which fills the 21 degree opening without becoming a wall behind it.
        private const float PlanetZ = -80f;
        private const float PlanetDiameter = 36f;

        /// <summary>Builds the stage and wires the runtime ambience onto the menu camera.</summary>
        public static bool Build(Transform menuRoot, Transform focus, int artLayer, int groundLayer)
        {
            var rng = new System.Random(20261106);

            var stage = new GameObject("MenuStage");
            stage.transform.SetParent(menuRoot, false);

            bool hasKits = ArtAssets.HasModel(Industrial, "building-a");
            if (hasKits)
            {
                BuildAvenue(stage.transform, artLayer);
                BuildSkyline(stage.transform, artLayer, rng);
            }
            else
            {
                Debug.LogWarning("[NullFrontier] City kits unavailable: menu stage falls " +
                                 "back to an empty plaza with rain and signage only.");
            }

            StreetDressing(stage.transform, artLayer);
            OverheadCables(stage.transform, artLayer);
            Ground(stage.transform, groundLayer);
            Puddles(stage.transform, groundLayer, rng);
            var ring = BuildGate(stage.transform, artLayer);
            WetFloorProbe(stage.transform, artLayer);
            var traffic = BuildTraffic(stage.transform, artLayer, rng);
            var ambience = WireAmbience(focus, traffic);
            WireMotion(stage, ring.transform);

            Debug.Log($"[NullFrontier] Menu stage built: kits={hasKits}, " +
                      $"traffic={traffic.Length}, rain={(ambience != null ? "on" : "off")}.");
            return hasKits;
        }

        /// <summary>
        /// Hands the stage's emissive hardware to the motion component: the gate rim and
        /// its tilt, every neon panel that can carry an emission colour, and all of the
        /// glow sprites. Names are the contract with the builders below, not a search of
        /// the scene - only objects created by this file are ever collected.
        /// </summary>
        private static void WireMotion(GameObject stage, Transform ring)
        {
            var neon = new System.Collections.Generic.List<Renderer>();
            var arcs = new System.Collections.Generic.List<Transform>();
            var lights = new System.Collections.Generic.List<Light>();
            Transform earth = null, clouds = null, cirrus = null;

            foreach (var renderer in stage.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name;
                if (name == "Ring" || name == "Signage" || name == "PortalArc")
                    neon.Add(renderer);
            }

            foreach (var candidate in stage.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "PortalArc") arcs.Add(candidate);
                else if (candidate.name == "Earth") earth = candidate;
                else if (candidate.name == "CloudShell") clouds = candidate;
                else if (candidate.name == "CirrusShell") cirrus = candidate;
            }
            foreach (var candidate in stage.GetComponentsInChildren<Light>(true))
            {
                if (candidate.name.StartsWith("GateLight")) lights.Add(candidate);
            }

            var motion = stage.AddComponent<NullFrontier.UI.MenuStageMotion>();
            UiFactory.SetField(motion, "gateRing", ring);
            UiFactory.SetField(motion, "emitters", neon.ToArray());
            UiFactory.SetField(motion, "glows",
                stage.GetComponentsInChildren<SpriteRenderer>(true));
            UiFactory.SetField(motion, "earth", earth);
            UiFactory.SetField(motion, "clouds", clouds);
            UiFactory.SetField(motion, "cirrus", cirrus);
            UiFactory.SetField(motion, "portalArcs", arcs.ToArray());
            UiFactory.SetField(motion, "gateLights", lights.ToArray());
        }

        // ------------------------------------------------------------------ ground

        private static void Ground(Transform parent, int layer)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "WetAsphalt";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(24f, 1f, 24f);
            ground.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetSolidMaterial("MenuAsphalt", new Color(0.045f, 0.05f, 0.07f), 0.78f);
            ground.layer = layer;
        }

        // ------------------------------------------------------------------- avenue

        private static void BuildAvenue(Transform parent, int layer)
        {
            // Tiles run away from the camera so the shot has a vanishing point to sit in.
            for (int i = 0; i < 7; i++)
            {
                float z = 6f - i * TileLength;
                Place(Roads, "road-straight", new Vector3(0f, 0.012f, z), TileLength, layer);

                // Kerb light strips: the only thing that sells "wet street" without a
                // reflection pass is light pooling on the asphalt itself.
                for (int side = -1; side <= 1; side += 2)
                {
                    Spill(parent, "Kerb", new Vector3(side * 5.6f, 0.02f, z),
                        new Vector2(2.6f, 9f), new Color(0.2f, 0.6f, 0.95f, 0.46f),
                        0.55f, layer);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Place(Roads, "electricity-pole",
                    new Vector3((i % 2 == 0 ? -1f : 1f) * 7.4f, 0f, -4f - i * 18f), 7f, layer);
            }
        }

        // ----------------------------------------------------------------- skyline

        private static void BuildSkyline(Transform parent, int layer, System.Random rng)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < BlocksPerSide; i++)
                {
                    string model = BuildingModels[rng.Next(BuildingModels.Length)];
                    var building = Place(Industrial, model,
                        new Vector3(side * Street, 0f, -8f - i * BlockPitch), 0f, layer);
                    if (building == null) continue;

                    float height = Mathf.Lerp(11f, 27f, (float)rng.NextDouble());
                    ArtAssets.FitToHeight(building, height, MaxBlockFootprint);
                    height = ArtAssets.CombinedBounds(building).size.y;
                    building.name = model;

                    // Fitting the height alone leaves the inner facade wherever the kit
                    // pivot happens to put it; slide the block outward until its street
                    // face sits on the kerb line, so no building can reach across the
                    // avenue and hide the gate, the planet or the sky.
                    Bounds bounds = ArtAssets.CombinedBounds(building);
                    float faceLine = Street - 1.2f;
                    float shift = side > 0 ? faceLine - bounds.min.x
                                           : -faceLine - bounds.max.x;
                    building.transform.position += new Vector3(shift, 0f, 0f);

                    // The kit ships facades on all sides, but only the street-facing one
                    // is ever in shot, so signage goes there and nowhere else.
                    Vector3 face = new Vector3(side * faceLine, 0f,
                        building.transform.position.z);
                    Signage(parent, face, side, height, layer, rng, i == 0);
                    FacadeDetail(parent, face, building.transform.position.z, side, height,
                        layer, rng, i);
                }
            }
        }

        /// <summary>
        /// Breaks up the flat kit facade with a lit-window grid, a mounted holo screen on
        /// the midground blocks and a mast with a beacon on every third roof. The grid is
        /// one additive quad whose shader draws the cells, so the whole avenue gains
        /// hundreds of windows for one draw call per building.
        /// </summary>
        private static void FacadeDetail(Transform parent, Vector3 face, float buildingZ,
            int side, float height, int layer, System.Random rng, int index)
        {
            var windows = GameObject.CreatePrimitive(PrimitiveType.Quad);
            windows.name = "FacadeWindows";
            windows.transform.SetParent(parent, false);
            windows.transform.position = new Vector3(face.x - side * 0.55f, height * 0.52f,
                buildingZ);
            windows.transform.rotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
            windows.transform.localScale = new Vector3(8.4f, height * 0.8f, 1f);
            var windowRenderer = windows.GetComponent<MeshRenderer>();
            windowRenderer.sharedMaterial = ArtAssets.GetWindowMaterial("MenuFacade",
                new Color(0.20f, 0.55f, 1f), 0.9f);
            windowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            windowRenderer.receiveShadows = false;
            windows.layer = layer;
            Object.DestroyImmediate(windows.GetComponent<Collider>());

            if (index == 1 || index == 3)
            {
                Color holo = index == 1
                    ? new Color(1f, 0.22f, 0.62f)
                    : new Color(0.2f, 0.95f, 1f);
                HoloBillboard(parent, face, buildingZ - 2.2f, side, height, layer, holo);
            }

            if (index % 3 != 0) return;

            // Mast and beacon: a vertical silhouette breaking the roof line so the skyline
            // is not ten identical boxes.
            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Antenna";
            mast.transform.SetParent(parent, false);
            mast.transform.position = new Vector3(face.x - side * 1.8f, height + 1.7f,
                buildingZ - 1.5f);
            mast.transform.localScale = new Vector3(0.07f, 1.7f, 0.07f);
            mast.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetSolidMaterial("Mast", new Color(0.06f, 0.07f, 0.09f), 0.35f);
            mast.layer = layer;
            Object.DestroyImmediate(mast.GetComponent<Collider>());

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "AntennaBeacon";
            beacon.transform.SetParent(parent, false);
            beacon.transform.position = new Vector3(face.x - side * 1.8f, height + 3.5f,
                buildingZ - 1.5f);
            beacon.transform.localScale = Vector3.one * 0.22f;
            beacon.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetNeonMaterial("AntennaBeacon", new Color(1f, 0.25f, 0.3f), 4.5f);
            beacon.layer = layer;
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
        }

        private static void HoloBillboard(Transform parent, Vector3 face, float z, int side,
            float height, int layer, Color color)
        {
            // A dark backing box first, so the additive glyphs are mounted on a screen
            // rather than floating in front of the brickwork.
            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "HoloMount";
            backing.transform.SetParent(parent, false);
            backing.transform.position = new Vector3(face.x - side * 0.62f, height * 0.63f, z);
            backing.transform.localScale = new Vector3(0.14f, 9.6f, 5.2f);
            backing.GetComponent<Renderer>().sharedMaterial =
                ArtAssets.GetSolidMaterial("HoloFrame", new Color(0.03f, 0.035f, 0.05f), 0.4f);
            backing.layer = layer;
            Object.DestroyImmediate(backing.GetComponent<Collider>());

            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "HoloSign";
            panel.transform.SetParent(parent, false);
            panel.transform.position = new Vector3(face.x - side * 0.72f, height * 0.63f, z);
            panel.transform.rotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
            panel.transform.localScale = new Vector3(4.6f, 8.8f, 1f);
            var renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ArtAssets.GetHoloMaterial("MenuSign", color, 1.15f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            panel.layer = layer;
            Object.DestroyImmediate(panel.GetComponent<Collider>());
        }

        /// <summary>Cable bundles sagging across the avenue, silhouetted against the gate.</summary>
        private static void OverheadCables(Transform parent, int layer)
        {
            var material = ArtAssets.GetNeonMaterial("MenuCable",
                new Color(0.025f, 0.035f, 0.055f), 1f);
            float[] runs = { -7f, -20f, -33f, -48f };
            foreach (float z in runs)
            {
                for (int bundle = 0; bundle < 3; bundle++)
                {
                    var go = new GameObject("Cable");
                    go.transform.SetParent(parent, false);
                    go.layer = layer;

                    var line = go.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = 13;
                    line.widthMultiplier = 0.035f;
                    line.numCapVertices = 2;
                    line.sharedMaterial = material;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;

                    float y = 8.4f + bundle * 0.7f;
                    for (int i = 0; i < 13; i++)
                    {
                        float t = i / 12f;
                        float x = Mathf.Lerp(-12.6f, 12.6f, t);
                        float sag = -1.5f * Mathf.Sin(Mathf.PI * t);
                        line.SetPosition(i, new Vector3(x, y + sag, z + bundle * 0.4f));
                    }
                }
            }
        }

        /// <summary>Ground-level clutter from the same CC0 kit, kept out of the lane.</summary>
        private static void StreetDressing(Transform parent, int layer)
        {
            Prop(parent, "dumpster", new Vector3(-8.3f, 0f, -12f), 2.2f, layer);
            Prop(parent, "dumpster", new Vector3(8.4f, 0f, -29f), 2.2f, layer);
            Prop(parent, "shipping-container-a", new Vector3(-9.2f, 0f, -47f), 5f, layer);
            Prop(parent, "detail-tank", new Vector3(8.5f, 0f, -53f), 2.2f, layer);
            Prop(parent, "construction-barrier", new Vector3(5.7f, 0f, -4f), 2.4f, layer);
            Prop(parent, "construction-cone", new Vector3(-5.3f, 0f, -6f), 0.7f, layer);
            Prop(parent, "construction-cone", new Vector3(5.4f, 0f, -10f), 0.7f, layer);
        }

        private static void Prop(Transform parent, string model, Vector3 position,
            float length, int layer)
        {
            // Street clutter (dumpsters, barriers, cones) ships in the roads kit, not the
            // industrial one; the stage's prop list mixes the two, so resolve per name.
            string pack = ArtAssets.HasModel(Industrial, model) ? Industrial : Roads;
            var prop = Place(pack, model, position, length, layer);
            if (prop != null) prop.transform.SetParent(parent, true);
        }

        /// <summary>
        /// Flat puddles of near-mirror asphalt. The reflection probe supplies the neon; the
        /// puddles only have to break the ground into wet sheets so the street is not one
        /// uniform matte plane.
        /// </summary>
        private static void Puddles(Transform parent, int layer, System.Random rng)
        {
            var material = ArtAssets.GetWetMaterial("MenuPuddle",
                new Color(0.02f, 0.028f, 0.045f), 0.97f, 0.55f);

            for (int i = 0; i < 7; i++)
            {
                var puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puddle.name = "Puddle";
                puddle.transform.SetParent(parent, false);
                // The road tiles are laid at y = 0.012, so a puddle authored on the ground
                // plane was entirely underneath them. It sits a couple of millimetres above
                // the asphalt, and still below the kerb spill sprites at 0.02.
                puddle.transform.position = new Vector3(
                    Mathf.Lerp(-5.2f, 5.2f, (float)rng.NextDouble()),
                    0.014f + i * 0.0004f,
                    Mathf.Lerp(4f, -56f, (float)rng.NextDouble()));
                puddle.transform.rotation = Quaternion.Euler(90f, 0f, rng.Next(360));
                puddle.transform.localScale = new Vector3(
                    Mathf.Lerp(2.2f, 5.2f, (float)rng.NextDouble()),
                    Mathf.Lerp(1.4f, 3.6f, (float)rng.NextDouble()), 1f);
                var renderer = puddle.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                puddle.layer = layer;
                Object.DestroyImmediate(puddle.GetComponent<Collider>());
            }
        }

        private static void Signage(Transform parent, Vector3 face, int side, float height,
            int layer, System.Random rng, bool nearShot)
        {
            Color neon = SignColors[rng.Next(SignColors.Length)];

            int bands = 2 + rng.Next(2);
            for (int b = 0; b < bands; b++)
            {
                float y = height * (0.35f + 0.5f * b / Mathf.Max(1, bands));
                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = "Signage";
                band.transform.SetParent(parent, false);
                band.transform.position = new Vector3(face.x - side * 0.4f, y, face.z + 2.5f * (b - 0.5f));
                band.transform.localScale = new Vector3(0.25f, 0.5f, 5.5f);
                band.GetComponent<Renderer>().sharedMaterial =
                    ArtAssets.GetNeonMaterial("MenuSign", neon, 1.8f);
                band.layer = layer;
                Object.DestroyImmediate(band.GetComponent<Collider>());

                // The glow is a light pool on the wet asphalt, not a second subject: the
                // blocks nearest the shot get the strip only, or the bloom eats the frame.
                if (nearShot) continue;
                Spill(parent, "SignSpill", new Vector3(face.x - side * 1.6f, 0.03f,
                        band.transform.position.z),
                    new Vector2(1.5f, 4f), new Color(neon.r, neon.g, neon.b, 0.22f), 1f, layer);
            }
        }

        // -------------------------------------------------------------------- portal

        /// <summary>
        /// The thing the whole shot is built around: a ring gate at the end of the avenue
        /// with a planet behind it. No realtime light is added for it - the mobile pipeline
        /// is already spending its additional-light budget on the showcase - so the flood
        /// is emissive geometry plus unlit sprites, which fog cannot wash out.
        /// </summary>
        private static GameObject BuildGate(Transform parent, int layer)
        {
            var gate = new GameObject("SkyGate");
            gate.transform.SetParent(parent, false);

            var ring = new GameObject("Ring");
            ring.transform.SetParent(gate.transform, false);
            ring.transform.position = new Vector3(0f, GateY, GateZ);
            ring.AddComponent<MeshFilter>().sharedMesh = GateMesh();
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            // Saturated blue, not pale cyan: a gain that pushes all three channels far
            // over 1 tonemaps to white and the gate reads as a blown-out hoop. This keeps
            // red and green low enough that the ring stays blue while still crossing the
            // bloom threshold of 1.05.
            ringRenderer.sharedMaterial =
                ArtAssets.GetNeonMaterial("SkyGate", new Color(0.10f, 0.42f, 1f), 2.2f);
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.layer = layer;

            // Only the rim is lit: a filled disc would hide the planet the ring exists to
            // frame. This is the event horizon, kept under a tenth of alpha.
            Billboard(gate.transform, "Threshold", new Vector3(0f, GateY, GateZ + 0.5f),
                new Vector2(19f, 19f), UiFactory.GlowSprite,
                new Color(0.30f, 0.78f, 1f, 0.13f), -18, layer);

            BuildPortalMembrane(gate.transform, layer);
            BuildPortalParticles(gate.transform, layer);
            BuildGateLights(gate.transform, layer);
            BuildPortalArcs(gate.transform, layer);

            Sky(parent, layer);
            Planet(parent, layer);

            Spill(parent, "GateFlood", new Vector3(0f, 0.024f, GateZ + 9f),
                new Vector2(13f, 9f), new Color(0.22f, 0.66f, 1f, 0.34f), 1f, layer);
            Spill(parent, "GateFlood", new Vector3(0f, 0.024f, GateZ + 22f),
                new Vector2(8f, 6f), new Color(0.22f, 0.66f, 1f, 0.20f), 1f, layer);

            for (int side = -1; side <= 1; side += 2)
            {
                Billboard(gate.transform, "Shaft",
                    new Vector3(side * 5f, GateY * 0.6f, GateZ + 2f),
                    new Vector2(4.5f, 24f), UiFactory.GlowSprite,
                    new Color(0.35f, 0.8f, 1f, 0.09f), -17, layer);
            }

            return ring;
        }

        /// <summary>
        /// One wide sprite of seeded dots instead of a hundred star billboards: unlit, so
        /// the menu's exponential fog cannot erase it, and a single draw call.
        /// </summary>
        private static void Sky(Transform parent, int layer)
        {
            Billboard(parent, "Starfield", new Vector3(0f, 26f, -150f),
                new Vector2(280f, 140f), UiFactory.StarFieldSprite,
                new Color(0.85f, 0.92f, 1f, 0.92f), -30, layer);
        }

        /// <summary>
        /// The planet behind the gate, built as three shells: a real albedo sphere, a
        /// coverage-mapped cloud sphere just outside it, and a glow ring for the
        /// atmosphere. The pivot carries the 23.4 degree axial tilt, so both shells spin
        /// about the planet's own axis rather than the world's.
        /// </summary>
        private static void Planet(Transform parent, int layer)
        {
            var pivot = new GameObject("EarthPivot");
            pivot.transform.SetParent(parent, false);
            // Hung just behind the ring so the world fills the opening the player is being
            // invited through; any further back and it shrinks to a dot inside the arch.
            pivot.transform.position = new Vector3(1.5f, 16f, PlanetZ);
            // The camera sits below the planet, so the tilt is biased a few degrees toward
            // it: unbiassed, the shot looked up at Antarctica.
            pivot.transform.rotation = Quaternion.Euler(-6f, 0f, 23.4f);

            // Diameter is authored in metres on the pivot's children, and the mesh itself
            // is a one-metre sphere, so localScale reads directly as the planet's size.
            var earth = new GameObject("Earth");
            earth.transform.SetParent(pivot.transform, false);
            earth.transform.localScale = Vector3.one * PlanetDiameter;
            earth.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
            var body = earth.AddComponent<MeshRenderer>();
            body.sharedMaterial = ArtAssets.GetEarthMaterial();
            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            body.receiveShadows = false;
            earth.layer = layer;

            var clouds = new GameObject("CloudShell");
            clouds.transform.SetParent(pivot.transform, false);
            // A fifth of a percent clear of the surface: enough that the two spheres never
            // z-fight at 90 m, small enough that the weather still reads as sitting on it.
            clouds.transform.localScale = Vector3.one * (PlanetDiameter * 1.006f);
            clouds.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
            var shell = clouds.AddComponent<MeshRenderer>();
            shell.sharedMaterial = ArtAssets.GetCloudMaterial();
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell.receiveShadows = false;
            clouds.layer = layer;

            // A second, thinner deck above the first. Two speeds at different scales are
            // what separate weather from paint on the surface.
            var cirrus = new GameObject("CirrusShell");
            cirrus.transform.SetParent(pivot.transform, false);
            cirrus.transform.localScale = Vector3.one * (PlanetDiameter * 1.014f);
            cirrus.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
            var high = cirrus.AddComponent<MeshRenderer>();
            high.sharedMaterial = ArtAssets.GetCloudMaterial("Space_Cirrus", 0.38f);
            high.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            high.receiveShadows = false;
            cirrus.layer = layer;

            // A real fresnel shell around the body, in addition to the sprite glow behind
            // it: the sprite supplies the bloom, this supplies the thin blue limb.
            var air = new GameObject("AtmosphereShell");
            air.transform.SetParent(pivot.transform, false);
            air.transform.localScale = Vector3.one * (PlanetDiameter * 1.045f);
            air.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
            var haze = air.AddComponent<MeshRenderer>();
            haze.sharedMaterial = ArtAssets.GetAtmosphereMaterial();
            haze.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            haze.receiveShadows = false;
            air.layer = layer;

            // Outer scatter: wide, very faint, and behind the world so it only ever shows
            // as the bloom the atmosphere puts around itself.
            Billboard(parent, "AtmosphereHalo", new Vector3(1.5f, 16f, PlanetZ - 8f),
                new Vector2(62f, 62f), UiFactory.GlowSprite,
                new Color(0.16f, 0.40f, 0.85f, 0.10f), -28, layer);

            // The rim itself. Laid behind the far surface so the sphere occludes its middle
            // and only the band outside the silhouette survives - a glow in front would
            // read as a fog patch sitting on the planet.
            Billboard(parent, "Atmosphere", new Vector3(1.5f, 16f, PlanetZ - 16f),
                new Vector2(40f, 40f), UiFactory.AtmosphereSprite,
                new Color(0.34f, 0.64f, 1f, 0.80f), -25, layer);
        }

        /// <summary>
        /// Three partial rings inside the gate, each on its own arc, spinning at its own
        /// rate. Full rings read as decoration; arcs read as energy that is still moving.
        /// </summary>
        private static void BuildPortalArcs(Transform parent, int layer)
        {
            var material = ArtAssets.GetNeonMaterial("PortalArc",
                new Color(0.35f, 0.75f, 1f), 1.9f);

            float[] radii = { 6.4f, 7.9f, 9.3f };
            float[] spans = { 258f, 212f, 302f };
            float[] minors = { 0.062f, 0.050f, 0.042f };

            for (int i = 0; i < radii.Length; i++)
            {
                var arc = new GameObject("PortalArc");
                arc.transform.SetParent(parent, false);
                arc.transform.position = new Vector3(0f, GateY, GateZ);
                // Stagger the starting angles so the three never line up into one ring.
                arc.transform.localRotation = Quaternion.Euler(0f, 0f, 24f + 118f * i);
                arc.AddComponent<MeshFilter>().sharedMesh =
                    ArcMesh(i, radii[i], minors[i], spans[i]);
                var renderer = arc.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arc.layer = layer;
            }
        }

        /// <summary>
        /// A quad of animated energy filling the gate opening. Additive so the planet and
        /// the starfield still read through it; the shader's own time uniform does the
        /// swirl, which keeps the runtime cost to one draw call and no CPU work.
        /// </summary>
        private static void BuildPortalMembrane(Transform parent, int layer)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PortalMembrane";
            quad.transform.SetParent(parent, false);
            quad.transform.position = new Vector3(0f, GateY, GateZ + 0.7f);
            // 1.78x filled the ring but bleached the planet behind it to a flat cyan disc;
            // 1.5x keeps the energy at the rim and lets the world read through the middle.
            quad.transform.localScale = new Vector3(GateRadius * 1.5f, GateRadius * 1.5f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ArtAssets.GetPortalMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            quad.layer = layer;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
        }

        /// <summary>
        /// Two particle systems rather than one: motes drifting out of the aperture, and
        /// embers climbing off the wet street. Both simulate on the CPU at these counts,
        /// which is the only VFX cost the menu adds to the first frame.
        /// </summary>
        private static void BuildPortalParticles(Transform parent, int layer)
        {
            var motes = new GameObject("PortalMotes");
            motes.transform.SetParent(parent, false);
            motes.transform.position = new Vector3(0f, GateY, GateZ + 0.9f);
            motes.layer = layer;

            var system = motes.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            main.startColor = new Color(0.55f, 0.86f, 1f, 0.9f);
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.rateOverTime = 24f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(GateRadius * 1.7f, GateRadius * 1.7f, 0.4f);

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Unity requires all three axes to share one curve mode; leaving x and y at
            // their default constants next to a two-constant z logs a per-frame warning.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.35f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            AddParticleFade(system);
            FinishParticleRenderer(system, "PortalMote", new Color(0.5f, 0.82f, 1f, 1f));

            var embers = new GameObject("PortalEmbers");
            embers.transform.SetParent(parent, false);
            embers.transform.position = new Vector3(0f, 0.3f, GateZ + 14f);
            embers.layer = layer;

            var rising = embers.AddComponent<ParticleSystem>();
            var emberMain = rising.main;
            emberMain.loop = true;
            emberMain.playOnAwake = true;
            emberMain.duration = 7f;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
            emberMain.startColor = new Color(0.4f, 0.78f, 1f, 0.85f);
            emberMain.maxParticles = 90;
            emberMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            emberMain.gravityModifier = -0.02f;

            var emberEmission = rising.emission;
            emberEmission.rateOverTime = 12f;

            var emberShape = rising.shape;
            emberShape.shapeType = ParticleSystemShapeType.Box;
            emberShape.scale = new Vector3(22f, 0.2f, 10f);
            // A box emits along its local +Z; tipping it onto its back points the spray at
            // the sky, which is what "embers climbing off the wet street" actually needs.
            emberShape.rotation = new Vector3(-90f, 0f, 0f);

            var emberNoise = rising.noise;
            emberNoise.enabled = true;
            emberNoise.strength = 0.5f;
            emberNoise.frequency = 0.5f;
            emberNoise.scrollSpeed = 0.25f;

            AddParticleFade(rising);
            FinishParticleRenderer(rising, "PortalEmber", new Color(0.35f, 0.7f, 1f, 1f));
        }

        private static void AddParticleFade(ParticleSystem system)
        {
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            fade.color = gradient;
        }

        private static void FinishParticleRenderer(ParticleSystem system, string label, Color tint)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ArtAssets.GetParticleMaterial(label, tint);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Real light on the street around the gate. The original stage faked this with
        /// sprites only; three shadowless points cost a fraction of one shadowed sun and
        /// give the wet road and the operative's gear actual rim light.
        /// </summary>
        private static void BuildGateLights(Transform parent, int layer)
        {
            GateLight(parent, "GateLightLeft", new Vector3(-7.4f, 5.2f, GateZ + 3f),
                7.5f, 24f, layer);
            GateLight(parent, "GateLightRight", new Vector3(7.4f, 5.2f, GateZ + 3f),
                7.5f, 24f, layer);
            GateLight(parent, "GateLightCore", new Vector3(0f, GateY, GateZ + 2f),
                4.5f, 30f, layer);
            // Two cool fill lights down the avenue: the gate lit its own plaza, but the
            // foreground where the operative stands stayed a navy silhouette.
            GateLight(parent, "AvenueFillNear", new Vector3(-1.6f, 3.6f, -9f),
                4.2f, 18f, layer);
            GateLight(parent, "AvenueFillMid", new Vector3(1.8f, 3.4f, -22f),
                3.6f, 20f, layer);
        }

        private static void GateLight(Transform parent, string name, Vector3 position,
            float intensity, float range, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = layer;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.35f, 0.75f, 1f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        /// <summary>A quad of any sprite, square to the menu camera's viewing axis.</summary>
        private static void Billboard(Transform parent, string label, Vector3 position,
            Vector2 worldSize, Sprite sprite, Color tint, int sortingOrder, int layer)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = layer;

            // Sprite bounds are in PPU units, not metres: a 128 px glow is 1.28 m at scale
            // one, so every call site below is written in metres and converted here.
            Vector3 spriteSize = sprite.bounds.size;
            go.transform.localScale = new Vector3(worldSize.x / spriteSize.x,
                worldSize.y / spriteSize.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// Torus for the gate rim. Saved as an asset so the rebuilt scene references a mesh
        /// rather than one that only exists in the scene's memory and is stripped at build.
        /// </summary>
        private static Mesh GateMesh()
        {
            ArtAssets.EnsureFolder(Generated);
            string path = $"{Generated}/SkyGate_Ring.asset";

            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) return saved;

            const float minor = 0.85f;
            const int ringSegments = 64, tubeSegments = 12;

            var mesh = new Mesh { name = "SkyGate_Ring" };
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            for (int i = 0; i <= ringSegments; i++)
            {
                float u = i / (float)ringSegments * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
                Vector3 centre = outward * GateRadius;

                for (int j = 0; j <= tubeSegments; j++)
                {
                    float v = j / (float)tubeSegments * Mathf.PI * 2f;
                    float cosV = Mathf.Cos(v), sinV = Mathf.Sin(v);

                    vertices.Add(centre + outward * (minor * cosV) + Vector3.forward * (minor * sinV));
                    normals.Add((outward * cosV + Vector3.forward * sinV).normalized);
                }
            }

            int stride = tubeSegments + 1;
            for (int i = 0; i < ringSegments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * stride + j;
                    int b = a + stride;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// A one-metre UV sphere with a documented seam. The built-in sphere does not
        /// promise which meridian faces the camera, and an Earth showing the middle of the
        /// Pacific instead of Africa is the first thing anyone notices; here u = 0.5 is +Z,
        /// so Greenwich faces the menu camera and the map reads the right way round.
        /// </summary>
        private static Mesh SphereMesh()
        {
            ArtAssets.EnsureFolder(Generated);
            string path = $"{Generated}/Earth_Sphere.asset";

            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) return saved;

            const int lon = 64, lat = 32;
            int stride = lon + 1;
            int count = stride * (lat + 1);

            var vertices = new System.Collections.Generic.List<Vector3>(count);
            var normals = new System.Collections.Generic.List<Vector3>(count);
            var uvs = new System.Collections.Generic.List<Vector2>(count);
            var triangles = new System.Collections.Generic.List<int>(lon * lat * 6);

            for (int j = 0; j <= lat; j++)
            {
                float v = j / (float)lat;
                float phi = (v - 0.5f) * Mathf.PI;
                float cosPhi = Mathf.Cos(phi), sinPhi = Mathf.Sin(phi);

                for (int i = 0; i <= lon; i++)
                {
                    float u = i / (float)lon;
                    float theta = u * Mathf.PI * 2f;
                    var normal = new Vector3(
                        cosPhi * Mathf.Sin(theta), sinPhi, -cosPhi * Mathf.Cos(theta));

                    normals.Add(normal);
                    vertices.Add(normal * 0.5f);   // unit diameter: scale reads as metres
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int j = 0; j < lat; j++)
            {
                for (int i = 0; i < lon; i++)
                {
                    int a = j * stride + i;
                    int b = a + stride;
                    AddTriangle(triangles, vertices, normals, a, b, a + 1);
                    AddTriangle(triangles, vertices, normals, a + 1, b, b + 1);
                }
            }

            var mesh = new Mesh { name = "Earth_Sphere" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Partial torus for one portal arc, saved per size so a rebuild reuses it.</summary>
        private static Mesh ArcMesh(int index, float radius, float minor, float degrees)
        {
            ArtAssets.EnsureFolder(Generated);
            string path = $"{Generated}/PortalArc_{index}.asset";

            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) return saved;

            const int segments = 72, tubeSegments = 8;
            float span = degrees * Mathf.Deg2Rad;
            int stride = tubeSegments + 1;

            var vertices = new System.Collections.Generic.List<Vector3>((segments + 1) * stride);
            var normals = new System.Collections.Generic.List<Vector3>((segments + 1) * stride);
            var triangles = new System.Collections.Generic.List<int>(segments * tubeSegments * 6);

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments * span;
                var outward = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
                Vector3 centre = outward * radius;

                for (int j = 0; j <= tubeSegments; j++)
                {
                    float v = j / (float)tubeSegments * Mathf.PI * 2f;
                    float cosV = Mathf.Cos(v), sinV = Mathf.Sin(v);

                    vertices.Add(centre + outward * (minor * cosV) + Vector3.forward * (minor * sinV));
                    normals.Add((outward * cosV + Vector3.forward * sinV).normalized);
                }
            }

            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * stride + j;
                    int b = a + stride;
                    AddTriangle(triangles, vertices, normals, a, b, a + 1);
                    AddTriangle(triangles, vertices, normals, a + 1, b, b + 1);
                }
            }

            var mesh = new Mesh { name = $"PortalArc_{index}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Emits a triangle with the winding Unity treats as front-facing. Which of the two
        /// index orders is correct depends on how the grid was parameterised, so the test is
        /// made against the vertex normal rather than assumed from the loop.
        /// </summary>
        private static void AddTriangle(System.Collections.Generic.List<int> triangles,
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Vector3> normals, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            Vector3 normal = normals[a] + normals[b] + normals[c];

            if (Vector3.Dot(face, normal) < 0f)
            {
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }
            else
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
        }

        /// <summary>
        /// The wet asphalt was reflecting the skybox, which is why the road read as matte
        /// grey between the neon pools. One box-projected probe over the avenue, rendered a
        /// single time at startup, puts the signage and the gate into the road for 0.4 MB.
        /// </summary>
        private static void WetFloorProbe(Transform parent, int layer)
        {
            var go = new GameObject("WetFloorProbe");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 7f, -26f);
            go.layer = layer;

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.hdr = false;
            probe.size = new Vector3(46f, 26f, 104f);
            probe.center = Vector3.zero;
            probe.boxProjection = true;
            probe.intensity = 1.25f;
            probe.shadowDistance = 0f;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;

            go.AddComponent<NullFrontier.UI.MenuReflectionProbe>();
        }

        /// <summary>
        /// A soft sprite laid flat on the asphalt. Alpha-blended rather than additive:
        /// the built-in sprite material is the one shader guaranteed to be in the build.
        /// </summary>
        private static void Spill(Transform parent, string label, Vector3 position,
            Vector2 size, Color tint, float scale, int layer)
        {
            var glow = new GameObject(label);
            glow.transform.SetParent(parent, false);
            glow.transform.position = position;
            glow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = new Vector3(size.x * scale, size.y * scale, 1f);
            glow.layer = layer;

            var sprite = glow.AddComponent<SpriteRenderer>();
            sprite.sprite = UiFactory.GlowSprite;
            sprite.color = tint;
            sprite.sortingOrder = -10;
        }

        // ------------------------------------------------------------------ traffic

        private static Transform[] BuildTraffic(Transform parent, int layer, System.Random rng)
        {
            var root = new GameObject("Traffic");
            root.transform.SetParent(parent, false);

            var lanes = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                float height = Mathf.Lerp(13f, 26f, (float)rng.NextDouble());
                int direction = i % 2 == 0 ? 1 : -1;

                var vehicle = new GameObject("Vehicle");
                vehicle.transform.SetParent(root.transform, false);
                vehicle.transform.localPosition = new Vector3(direction * -60f, height,
                    -34f + i * 17f);
                vehicle.transform.forward = new Vector3(direction, 0f, 0f);
                vehicle.layer = layer;

                var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                hull.name = "Hull";
                hull.transform.SetParent(vehicle.transform, false);
                hull.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                hull.transform.localScale = new Vector3(0.4f, 1.1f, 0.5f);
                hull.GetComponent<Renderer>().sharedMaterial =
                    ArtAssets.GetSolidMaterial("VehicleHull", new Color(0.08f, 0.09f, 0.12f), 0.6f);
                hull.layer = layer;
                Object.DestroyImmediate(hull.GetComponent<Collider>());

                Color light = i % 3 == 0 ? new Color(1f, 0.45f, 0.2f) : new Color(0.35f, 0.9f, 1f);
                var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beacon.name = "Beacon";
                beacon.transform.SetParent(vehicle.transform, false);
                beacon.transform.localPosition = new Vector3(direction * 0.9f, 0f, 0f);
                beacon.transform.localScale = new Vector3(0.5f, 0.14f, 0.3f);
                beacon.GetComponent<Renderer>().sharedMaterial =
                    ArtAssets.GetNeonMaterial("VehicleBeacon", light, 5f);
                beacon.layer = layer;
                Object.DestroyImmediate(beacon.GetComponent<Collider>());

                lanes.Add(vehicle.transform);
            }

            return lanes.ToArray();
        }

        // ----------------------------------------------------------------- ambience

        private static MenuStageAmbience WireAmbience(Transform focus, Transform[] traffic)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[NullFrontier] No main camera in MainMenu - menu ambience skipped.");
                return null;
            }

            var ambience = camera.gameObject.AddComponent<NullFrontier.UI.MenuStageAmbience>();
            UiFactory.SetField(ambience, "lookAt", focus);
            UiFactory.SetField(ambience, "streakSprite", UiFactory.StreakSprite);
            UiFactory.SetField(ambience, "vehicles", traffic);
            UiFactory.SetField(ambience, "volume", new Vector3(30f, 16f, 48f));
            UiFactory.SetField(ambience, "drops", 180);
            UiFactory.SetField(ambience, "baseFieldOfView", camera.fieldOfView);

            // This component drives the transform every frame, so whatever the camera was
            // authored at is discarded on the first tick. The camera now looks at the gate
            // instead of the operative's face: the old downward angle cropped the arch,
            // the planet and the sky behind them. From 6 m with the gaze on the gate he
            // stands in the lower third and the whole portal is in frame.
            var gaze = new GameObject("GateFocus");
            gaze.transform.position = new Vector3(0f, GateY - 0.6f, GateZ);
            UiFactory.SetField(ambience, "gazePoint", gaze.transform);

            UiFactory.SetField(ambience, "radiusRange", new Vector2(5.8f, 6.4f));
            UiFactory.SetField(ambience, "heightRange", new Vector2(0.9f, 1.15f));
            UiFactory.SetField(ambience, "sweepDegrees", new Vector2(-7f, 7f));
            UiFactory.SetField(ambience, "sweepSeconds", 40f);
            return ambience;
        }

        // -------------------------------------------------------------------- placing

        /// <summary>
        /// Instantiates a kit model. A targetLength of zero leaves the model at native
        /// scale so the caller can drive height with FitToHeight instead.
        /// </summary>
        private static GameObject Place(string packDir, string model, Vector3 position,
            float targetLength, int layer)
        {
            var prefab = ArtAssets.LoadModel(packDir, model);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = model;
            go.transform.position = position;

            if (targetLength > 0.001f)
            {
                Bounds native = ArtAssets.CombinedBounds(go);
                float longest = Mathf.Max(0.001f, Mathf.Max(native.size.x, native.size.z));
                go.transform.localScale = Vector3.one * (targetLength / longest);
            }

            foreach (var transform in go.GetComponentsInChildren<Transform>())
                transform.gameObject.layer = layer;

            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return go;
        }
    }
}
