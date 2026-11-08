using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Builds the weapon meshes and the prefabs the player carries them in.
    ///
    /// The roster ships bodies but no guns, and the loadout was equipping an empty
    /// GameObject - the operative could fire a weapon that did not exist. These are
    /// authored here, out of boxes, for the same reason the rest of the project's art is:
    /// nothing has to be downloaded, nothing has a licence to track, and every mesh is
    /// one draw call with two submeshes (body metal and emissive parts).
    ///
    /// Convention: +Z is down the barrel, +Y is up the sight line, the origin sits in the
    /// web of the hand. WeaponVisualMount overrides the world rotation from aim input, so
    /// the guns do not depend on how the rig's hand bone happens to be oriented.
    /// </summary>
    public static class WeaponModels
    {
        public const string PrefabDir = "Assets/Resources/Weapons";

        private const string MeshDir = ArtAssets.Root + "/Generated/Weapons";
        private const string MatDir = ArtAssets.Root + "/Materials/Weapons";

        private struct Gun
        {
            public readonly string Id;
            public readonly Color Accent;
            public readonly Vector3 Muzzle;
            public readonly Vector3[] Solid;
            public readonly Vector3[] Glow;

            public Gun(string id, Color accent, Vector3 muzzle, Vector3[] solid, Vector3[] glow)
            {
                Id = id; Accent = accent; Muzzle = muzzle; Solid = solid; Glow = glow;
            }
        }

        // A box is stored as centre + size in one Vector4-shaped pair: two Vector3s per
        // box, position then extent, keeps the tables below readable.
        private static readonly Gun[] Guns =
        {
            new Gun("nf_pistol", new Color(0.25f, 0.85f, 1f), new Vector3(0f, 0.055f, 0.17f),
                new[]
                {
                    V(0f, 0.055f, 0.06f), S(0.036f, 0.036f, 0.16f),
                    V(0f, 0.02f, 0.03f), S(0.032f, 0.026f, 0.13f),
                    V(0f, -0.05f, -0.02f), S(0.03f, 0.11f, 0.052f),
                    V(0f, 0.03f, -0.055f), S(0.028f, 0.03f, 0.05f),
                },
                new[]
                {
                    V(0f, 0.055f, 0.148f), S(0.028f, 0.028f, 0.02f),
                    V(0f, 0.079f, 0.13f), S(0.009f, 0.014f, 0.014f),
                    V(0f, 0.079f, -0.01f), S(0.02f, 0.01f, 0.05f),
                    V(0f, -0.02f, 0.03f), S(0.034f, 0.012f, 0.06f),
                }),

            new Gun("nf_ar", new Color(1f, 0.62f, 0.18f), new Vector3(0f, 0.03f, 0.46f),
                new[]
                {
                    V(0f, 0.03f, 0.02f), S(0.05f, 0.07f, 0.34f),
                    V(0f, 0.03f, 0.3f), S(0.028f, 0.028f, 0.26f),
                    V(0f, 0.03f, 0.22f), S(0.044f, 0.052f, 0.2f),
                    V(0f, 0.02f, -0.22f), S(0.046f, 0.09f, 0.16f),
                    V(0f, -0.062f, 0.02f), S(0.036f, 0.14f, 0.072f),
                    V(0f, -0.06f, -0.08f), S(0.032f, 0.12f, 0.052f),
                    V(0f, 0.092f, 0.02f), S(0.036f, 0.042f, 0.09f),
                    V(0f, 0.07f, 0.14f), S(0.03f, 0.02f, 0.06f),
                },
                new[]
                {
                    V(0f, 0.092f, 0.066f), S(0.028f, 0.028f, 0.012f),
                    V(0.024f, 0.03f, 0.22f), S(0.006f, 0.02f, 0.16f),
                    V(-0.024f, 0.03f, 0.22f), S(0.006f, 0.02f, 0.16f),
                    V(0f, -0.13f, 0.02f), S(0.03f, 0.012f, 0.05f),
                    V(0f, 0.03f, 0.43f), S(0.034f, 0.034f, 0.03f),
                }),

            new Gun("nf_energy", new Color(0.62f, 0.35f, 1f), new Vector3(0f, 0.03f, 0.31f),
                new[]
                {
                    V(0f, 0.03f, 0f), S(0.05f, 0.062f, 0.3f),
                    V(0f, 0.03f, 0.22f), S(0.062f, 0.062f, 0.1f),
                    V(0f, -0.06f, -0.06f), S(0.032f, 0.12f, 0.052f),
                    V(0f, 0.085f, -0.16f), S(0.014f, 0.08f, 0.12f),
                    V(0f, 0.03f, -0.19f), S(0.044f, 0.05f, 0.08f),
                },
                new[]
                {
                    V(0f, 0.03f, 0.13f), S(0.072f, 0.072f, 0.014f),
                    V(0f, 0.03f, 0.19f), S(0.072f, 0.072f, 0.014f),
                    V(0f, 0.03f, 0.25f), S(0.072f, 0.072f, 0.014f),
                    V(0f, 0.03f, -0.02f), S(0.032f, 0.032f, 0.12f),
                    V(0f, 0.03f, 0.275f), S(0.03f, 0.03f, 0.02f),
                }),

            new Gun("nf_shotgun", new Color(1f, 0.28f, 0.3f), new Vector3(0f, 0.025f, 0.41f),
                new[]
                {
                    V(0f, 0.03f, 0f), S(0.056f, 0.076f, 0.26f),
                    V(0f, 0.055f, 0.26f), S(0.03f, 0.03f, 0.26f),
                    V(0f, -0.005f, 0.26f), S(0.03f, 0.03f, 0.26f),
                    V(0f, -0.005f, 0.16f), S(0.052f, 0.05f, 0.1f),
                    V(0f, 0.01f, -0.2f), S(0.05f, 0.1f, 0.18f),
                    V(0f, -0.06f, -0.06f), S(0.032f, 0.11f, 0.052f),
                },
                new[]
                {
                    V(0f, 0.078f, 0.05f), S(0.022f, 0.014f, 0.14f),
                    V(0f, 0.055f, 0.39f), S(0.034f, 0.034f, 0.02f),
                    V(0f, -0.005f, 0.39f), S(0.034f, 0.034f, 0.02f),
                    V(0f, -0.03f, -0.13f), S(0.04f, 0.02f, 0.06f),
                }),
        };

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        private static Vector3 S(float x, float y, float z) => new Vector3(x, y, z);

        [MenuItem("Null Frontier/Rebuild Weapon Models")]
        public static void RebuildAll()
        {
            ArtAssets.EnsureFolder(MeshDir);
            ArtAssets.EnsureFolder(MatDir);
            ArtAssets.EnsureFolder(PrefabDir);

            int built = 0;
            foreach (Gun gun in Guns)
                if (Build(gun)) built++;

            AssetDatabase.SaveAssets();
            Debug.Log($"[NullFrontier] {built}/{Guns.Length} weapon models written to {PrefabDir}.");
        }

        /// <summary>Prefab for a weapon id, built on first use so the runtime never has to
        /// ask whether the editor has run yet.</summary>
        public static GameObject Get(string weaponId)
        {
            string path = $"{PrefabDir}/{weaponId}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            foreach (Gun gun in Guns)
                if (gun.Id == weaponId) return Build(gun) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;

            Debug.LogError($"[NullFrontier] No weapon model spec for '{weaponId}'.");
            return null;
        }

        private static bool Build(Gun gun)
        {
            Mesh mesh = BuildMesh(gun);
            if (mesh == null) return false;

            var root = new GameObject(gun.Id);

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                ArtAssets.GetSolidMaterial("WeaponBody_" + gun.Id, new Color(0.15f, 0.17f, 0.21f), 0.62f),
                ArtAssets.GetNeonMaterial("WeaponGlow_" + gun.Id, gun.Accent, 2.4f),
            };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // The mount keeps this node aimed down the firing line, so a shot that starts
            // at the muzzle is a shot that leaves the barrel.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = gun.Muzzle;

            string path = $"{PrefabDir}/{gun.Id}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"[NullFrontier] {path} was not written.");
                return false;
            }
            return true;
        }

        private static Mesh BuildMesh(Gun gun)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var body = new List<int>();
            var glow = new List<int>();

            AppendBoxes(verts, norms, body, gun.Solid);
            AppendBoxes(verts, norms, glow, gun.Glow);

            string path = $"{MeshDir}/{gun.Id}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.subMeshCount = 2;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(glow, 1);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AppendBoxes(List<Vector3> verts, List<Vector3> norms, List<int> tris,
            Vector3[] pairs)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                AppendBox(verts, norms, tris, pairs[i], pairs[i + 1]);
        }

        /// <summary>Flat-shaded box: four vertices per face so every edge stays a crease.
        /// A smooth-recalculated cube would read as a ball of metal.</summary>
        private static void AppendBox(List<Vector3> verts, List<Vector3> norms, List<int> tris,
            Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;

            AddFace(verts, norms, tris, centre, h, new Vector3(0f, 0f, 1f),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z));
            AddFace(verts, norms, tris, centre, h, new Vector3(0f, 0f, -1f),
                new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z),
                new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z));
            AddFace(verts, norms, tris, centre, h, new Vector3(1f, 0f, 0f),
                new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z));
            AddFace(verts, norms, tris, centre, h, new Vector3(-1f, 0f, 0f),
                new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z),
                new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z));
            AddFace(verts, norms, tris, centre, h, new Vector3(0f, 1f, 0f),
                new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z),
                new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z));
            AddFace(verts, norms, tris, centre, h, new Vector3(0f, -1f, 0f),
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z));
        }

        private static void AddFace(List<Vector3> verts, List<Vector3> norms, List<int> tris,
            Vector3 centre, Vector3 h, Vector3 normal,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int baseIndex = verts.Count;
            verts.Add(centre + a); norms.Add(normal);
            verts.Add(centre + b); norms.Add(normal);
            verts.Add(centre + c); norms.Add(normal);
            verts.Add(centre + d); norms.Add(normal);

            tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
            tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
        }
    }
}
