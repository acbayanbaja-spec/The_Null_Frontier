using UnityEditor;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Adds a futuristic tactical kit to a rigged roster model at prefab-build time:
    /// chest armour, pauldrons, belt pouches, a backpack with tanks and an antenna, a
    /// lit visor and a slung rifle. The parts hang off the model's real bones, so they
    /// follow the idle and locomotion clips instead of floating at a fixed height.
    ///
    /// This is a gear kit over the existing low-poly Quaternius body - it improves the
    /// silhouette and the read at mobile distance, and is not a substitute for a
    /// realistic character mesh.
    /// </summary>
    public static class TacticalGear
    {
        public static void Attach(GameObject visual, Color accent, int layer = 0)
        {
            if (visual == null) return;

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float height = Mathf.Max(1f, bounds.size.y);

            Transform torso = FindBone(visual, "Torso", "Spine2", "Spine1", "Spine", "Hips");
            Transform head = FindBone(visual, "Head", "Neck");
            Transform leftShoulder = FindBoneBySide(visual, "Shoulder", "UpperArm", true);
            Transform rightShoulder = FindBoneBySide(visual, "Shoulder", "UpperArm", false);
            if (torso == null) torso = visual.transform;

            var armour = ArtAssets.GetSolidMaterial("GearArmour",
                new Color(0.085f, 0.095f, 0.12f), 0.32f);
            var plate = ArtAssets.GetWetMaterial("GearPlate",
                new Color(0.20f, 0.23f, 0.28f), 0.58f, 0.55f);
            var strap = ArtAssets.GetSolidMaterial("GearStrap",
                new Color(0.045f, 0.05f, 0.06f), 0.24f);
            var glow = ArtAssets.GetNeonMaterial("GearAccent", accent, 1.7f);

            float h = height;

            // Torso: soft vest under a hard chest plate, both a little proud of the body.
            Part(torso, "GearVest", new Vector3(0f, 0.02f * h, 0f),
                new Vector3(0.40f, 0.36f, 0.26f) * (h / 1.75f),
                Quaternion.identity, armour, PrimitiveType.Cube, layer);
            Part(torso, "GearChestPlate", new Vector3(0f, 0.06f * h, 0.14f * (h / 1.75f)),
                new Vector3(0.30f, 0.20f, 0.06f) * (h / 1.75f),
                Quaternion.identity, plate, PrimitiveType.Cube, layer);
            Part(torso, "GearChestLight", new Vector3(0f, 0.08f * h, 0.175f * (h / 1.75f)),
                new Vector3(0.05f, 0.02f, 0.012f) * (h / 1.75f),
                Quaternion.identity, glow, PrimitiveType.Cube, layer);

            // Backpack + twin coolant tanks + whip antenna.
            Part(torso, "GearPack", new Vector3(0f, 0.02f * h, -0.17f * (h / 1.75f)),
                new Vector3(0.32f, 0.32f, 0.16f) * (h / 1.75f),
                Quaternion.identity, armour, PrimitiveType.Cube, layer);
            Part(torso, "GearTankL", new Vector3(-0.09f * (h / 1.75f), 0.10f * h,
                -0.245f * (h / 1.75f)), new Vector3(0.075f, 0.22f, 0.075f) * (h / 1.75f),
                Quaternion.identity, plate, PrimitiveType.Cylinder, layer);
            Part(torso, "GearTankR", new Vector3(0.09f * (h / 1.75f), 0.10f * h,
                -0.245f * (h / 1.75f)), new Vector3(0.075f, 0.22f, 0.075f) * (h / 1.75f),
                Quaternion.identity, plate, PrimitiveType.Cylinder, layer);
            Part(torso, "GearAntenna", new Vector3(0.13f * (h / 1.75f), 0.22f * h,
                -0.20f * (h / 1.75f)), new Vector3(0.012f, 0.26f, 0.012f) * (h / 1.75f),
                Quaternion.identity, strap, PrimitiveType.Cylinder, layer);
            Part(torso, "GearAntennaTip", new Vector3(0.13f * (h / 1.75f), 0.355f * h,
                -0.20f * (h / 1.75f)), Vector3.one * (0.035f * (h / 1.75f)),
                Quaternion.identity, glow, PrimitiveType.Sphere, layer);
            Part(torso, "GearPackLight", new Vector3(0f, 0.02f * h, -0.255f * (h / 1.75f)),
                new Vector3(0.18f, 0.018f, 0.012f) * (h / 1.75f),
                Quaternion.identity, glow, PrimitiveType.Cube, layer);

            // Belt and forward pouches: reads in third person when the camera looks at
            // his back and again when the roster turntable brings the front around.
            Part(torso, "GearBelt", new Vector3(0f, -0.20f * h, 0f),
                new Vector3(0.38f, 0.055f, 0.26f) * (h / 1.75f),
                Quaternion.identity, strap, PrimitiveType.Cube, layer);
            Part(torso, "GearPouchL", new Vector3(-0.13f * (h / 1.75f), -0.25f * h,
                0.10f * (h / 1.75f)), new Vector3(0.10f, 0.11f, 0.07f) * (h / 1.75f),
                Quaternion.identity, armour, PrimitiveType.Cube, layer);
            Part(torso, "GearPouchR", new Vector3(0.13f * (h / 1.75f), -0.25f * h,
                0.10f * (h / 1.75f)), new Vector3(0.10f, 0.11f, 0.07f) * (h / 1.75f),
                Quaternion.identity, armour, PrimitiveType.Cube, layer);

            // Pauldrons ride the shoulder bones; without a matching bone they fall back
            // to the torso and sit slightly high rather than being skipped.
            Transform leftAnchor = leftShoulder != null ? leftShoulder : torso;
            Transform rightAnchor = rightShoulder != null ? rightShoulder : torso;
            Part(leftAnchor, "GearPauldronL",
                leftShoulder != null ? Vector3.zero : new Vector3(-0.21f * (h / 1.75f),
                    0.12f * h, 0f),
                new Vector3(0.17f, 0.10f, 0.20f) * (h / 1.75f),
                Quaternion.Euler(0f, 0f, 8f), plate, PrimitiveType.Sphere, layer);
            Part(rightAnchor, "GearPauldronR",
                rightShoulder != null ? Vector3.zero : new Vector3(0.21f * (h / 1.75f),
                    0.12f * h, 0f),
                new Vector3(0.17f, 0.10f, 0.20f) * (h / 1.75f),
                Quaternion.Euler(0f, 0f, -8f), plate, PrimitiveType.Sphere, layer);

            // Visor band on the head bone. The menu faces him at the gate, so this is seen
            // mostly in gameplay and in the roster portrait, not from behind on the plinth.
            if (head != null)
            {
                Part(head, "GearVisor", new Vector3(0f, 0.045f * h, 0.10f * (h / 1.75f)),
                    new Vector3(0.21f, 0.035f, 0.055f) * (h / 1.75f),
                    Quaternion.identity, glow, PrimitiveType.Cube, layer);
            }

            // A rifle slung diagonally across the pack.
            Part(torso, "GearRifle", new Vector3(0.02f * (h / 1.75f), 0f,
                -0.27f * (h / 1.75f)), new Vector3(0.60f, 0.05f, 0.06f) * (h / 1.75f),
                Quaternion.Euler(0f, 0f, 34f), strap, PrimitiveType.Cube, layer);
            Part(torso, "GearRifleMag", new Vector3(0.10f * (h / 1.75f), -0.06f * h,
                -0.27f * (h / 1.75f)), new Vector3(0.09f, 0.13f, 0.05f) * (h / 1.75f),
                Quaternion.Euler(0f, 0f, 34f), plate, PrimitiveType.Cube, layer);
        }

        private static Transform FindBone(GameObject visual, params string[] names)
        {
            var skinned = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                foreach (var name in names)
                {
                    foreach (var bone in skinned.bones)
                    {
                        if (bone != null && bone.name == name) return bone;
                    }
                }
            }

            foreach (var candidate in visual.GetComponentsInChildren<Transform>(true))
            {
                foreach (var name in names)
                {
                    if (candidate.name == name) return candidate;
                }
            }
            return null;
        }

        private static Transform FindBoneBySide(GameObject visual, string name, string fallback,
            bool left)
        {
            var skinned = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned == null) return null;

            float centre = visual.transform.position.x;
            Transform best = null;
            float bestDistance = 0f;

            foreach (var bone in skinned.bones)
            {
                if (bone == null) continue;
                if (bone.name != name && bone.name != fallback) continue;

                float side = bone.position.x - centre;
                if (left ? side >= 0f : side <= 0f) continue;

                // Multiple bones can share a name (left and right limbs); take the one
                // furthest out along X, which is the shoulder rather than the elbow.
                if (best == null || Mathf.Abs(side) > bestDistance)
                {
                    best = bone;
                    bestDistance = Mathf.Abs(side);
                }
            }
            return best;
        }

        private static GameObject Part(Transform bone, string name, Vector3 worldOffset,
            Vector3 worldSize, Quaternion worldRotation, Material material,
            PrimitiveType type, int layer)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.layer = layer;

            part.transform.SetParent(bone, false);
            Vector3 anchor = bone.position;
            part.transform.position = anchor + worldOffset;
            part.transform.rotation = worldRotation;

            Vector3 scale = bone.lossyScale;
            part.transform.localScale = new Vector3(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part;
        }
    }
}
