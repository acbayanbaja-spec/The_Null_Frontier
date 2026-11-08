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
        private const float PlanetZ = -88f;
        private const float PlanetDiameter = 30f;

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

            Ground(stage.transform, groundLayer);
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
            Transform earth = null, clouds = null;

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
            }

            var motion = stage.AddComponent<NullFrontier.UI.MenuStageMotion>();
            UiFactory.SetField(motion, "gateRing", ring);
            UiFactory.SetField(motion, "emitters", neon.ToArray());
            UiFactory.SetField(motion, "glows",
                stage.GetComponentsInChildren<SpriteRenderer>(true));
            UiFactory.SetField(motion, "earth", earth);
            UiFactory.SetField(motion, "clouds", clouds);
            UiFactory.SetField(motion, "portalArcs", arcs.ToArray());
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
                }
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
            probe.intensity = 1f;
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
