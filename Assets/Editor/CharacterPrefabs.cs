using System.IO;
using System.Reflection;
using NullFrontier.AI;
using NullFrontier.Characters;
using NullFrontier.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Turns the licensed FBX models into runtime-loadable prefabs:
    ///   Resources/Characters/<Model>.prefab  visual-only rigs for the player roster
    ///   Resources/Enemies/<Kind>.prefab      whole enemies, ready to be spawned
    /// CharacterVisualMount and EnemySpawner load these by name at runtime.
    /// </summary>
    public static class CharacterPrefabs
    {
        private const float CharacterVisualHeight = 1.75f;

        [MenuItem("Null Frontier/Rebuild Character And Enemy Prefabs")]
        public static void RebuildAll()
        {
            ArtAssets.ResetCaches();
            ReimportCharacterModels();
            var generated = BuildCharacterVisuals();
            generated += BuildEnemyPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log($"[NullFrontier] {generated} runtime prefabs written to Assets/Resources.");
        }

        /// <summary>
        /// The vertex-colour import rule only lands on a reimport, and the meshes were
        /// first brought in without it. Forcing the update here is what turns the roster
        /// from black silhouettes into coloured characters.
        /// </summary>
        private static void ReimportCharacterModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ArtAssets.CharacterDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var before = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var mesh = before != null ? before.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                bool hadColours = mesh != null && mesh.sharedMesh != null
                    && mesh.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                if (!hadColours)
                {
                    var after = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var afterMesh = after != null ? after.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                    bool nowHas = afterMesh != null && afterMesh.sharedMesh != null
                        && afterMesh.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color);
                    Debug.Log($"[NullFrontier] {path}: vertex colours {(nowHas ? "imported" : "ABSENT - this model has no colour channel")}.");
                }
            }
            ArtAssets.ResetCaches();
        }

        /// <summary>One visual prefab per roster entry, de-duplicated by model name.</summary>
        public static int BuildCharacterVisuals()
        {
            ArtAssets.EnsureFolder(ArtAssets.CharacterPrefabDir);
            var built = new System.Collections.Generic.HashSet<string>();

            foreach (var def in CharacterRegistry.All)
            {
                if (string.IsNullOrEmpty(def.ModelPrefab)) continue;
                if (!built.Add(def.ModelPrefab)) continue;
                if (BuildVisual(def.ModelPrefab, def.AccentColor) != null) continue;
                Debug.LogError($"[NullFrontier] Could not build visual prefab for {def.DisplayName} " +
                               $"({def.ModelPrefab}). Is Assets/Art/Quaternius/Characters imported?");
            }
            return built.Count;
        }

        private static GameObject BuildVisual(string modelName, Color accent)
        {
            var model = ArtAssets.LoadModel(ArtAssets.CharacterDir, modelName);
            if (model == null) return null;

            var root = new GameObject(modelName);
            var visual = Object.Instantiate(model, root.transform);
            visual.name = "Visual";
            ArtAssets.FitToHeight(visual, CharacterVisualHeight);
            visual.transform.localPosition = Vector3.zero;
            ArtAssets.BakeCharacterMaterials(visual, modelName, accent);

            var animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = CharacterRigBuilder.GetOrCreate(model);
                animator.applyRootMotion = false;
            }

            string path = $"{ArtAssets.CharacterPrefabDir}/{modelName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static int BuildEnemyPrefabs()
        {
            ArtAssets.EnsureFolder(ArtAssets.EnemyPrefabDir);
            int count = 0;
            // enemyTypeId must match a mission objective's targetId, otherwise the kills
            // the spawner hands out can never be counted by MissionManager.
            count += BuildEnemy("DigitalSoldier", "BlueSoldier_Male", typeof(DigitalSoldier),
                new EnemyTuning(90f, 3.6f, 0f, 30f, "digital_soldier", "", "nf_ar")) ? 1 : 0;
            count += BuildEnemy("MazeStalker", "Zombie_Male", typeof(MazeStalker),
                new EnemyTuning(70f, 4.4f, 0f, 25f, "maze_stalker", "med_gel", "")) ? 1 : 0;
            count += BuildEnemy("SecurityDrone", "Goblin_Male", typeof(SecurityDrone),
                new EnemyTuning(55f, 3.0f, 2.5f, 40f, "security_drone", "scrap_alloy",
                    "nf_energy")) ? 1 : 0;
            // The commander is the kill target of a mission, so it has to exist as its own
            // prefab with its own enemyTypeId.
            count += BuildEnemy("SecurityCommander", "Soldier_Male", typeof(DigitalSoldier),
                new EnemyTuning(240f, 3.2f, 0f, 120f, "security_commander", "shield_cell",
                    "nf_shotgun")) ? 1 : 0;
            return count;
        }

        private readonly struct EnemyTuning
        {
            public readonly float Health;
            public readonly float Speed;
            public readonly float Hover;
            public readonly float Xp;
            public readonly string TypeId;
            public readonly string DropItemId;
            public readonly string WeaponId;

            public EnemyTuning(float health, float speed, float hover, float xp,
                string typeId, string dropItemId, string weaponId)
            {
                Health = health; Speed = speed; Hover = hover; Xp = xp;
                TypeId = typeId; DropItemId = dropItemId; WeaponId = weaponId;
            }
        }

        private static bool BuildEnemy(string kindName, string modelName, System.Type behaviour,
            EnemyTuning tuning)
        {
            var root = new GameObject(kindName);
            root.layer = LayerMask.NameToLayer("Enemy");

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = tuning.Speed;
            agent.angularSpeed = 320f;
            agent.acceleration = 12f;
            agent.radius = 0.36f;
            agent.height = 1.8f;
            agent.baseOffset = tuning.Hover;
            agent.autoTraverseOffMeshLink = true;
            // Bots with no avoidance shoulder straight through each other into whatever
            // is behind them, which reads to the player as walking into a wall.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            var health = root.AddComponent<Health>();
            SetField(health, "maxHealth", tuning.Health);

            var ai = root.AddComponent(behaviour);
            SetField(ai, "xpReward", tuning.Xp);
            SetField(ai, "enemyTypeId", tuning.TypeId);
            SetField(ai, "dropItemId", tuning.DropItemId);

            var rig = root.AddComponent<CharacterVisualRig>();
            // A hovering unit is a machine: it throws sparks when it is hit and comes
            // apart when it dies. Only the ones that walk the ground bleed.
            SetField(rig, "biological", tuning.Hover <= 0f);

            // Empty hands that shoot are the oldest cheat in the game, and the player
            // could see it: every bot now carries one of the same models the player holds.
            if (!string.IsNullOrEmpty(tuning.WeaponId))
            {
                var weapon = root.AddComponent<NullFrontier.AI.EnemyWeapon>();
                SetField(weapon, "weaponId", tuning.WeaponId);
            }

            var model = ArtAssets.LoadModel(ArtAssets.CharacterDir, modelName);
            if (model != null)
            {
                var visual = Object.Instantiate(model, root.transform);
                visual.name = "Visual";
                ArtAssets.FitToHeight(visual, CharacterVisualHeight);

                // The root is feet-anchored - the capsule spans 0 to 1.8 and the agent's
                // baseOffset sits that plane on the NavMesh - so the model hangs off the
                // root at zero. The -0.9 this used to write assumed a capsule-centre root
                // and buried every ground enemy to the shoulders; hover is already paid
                // for by baseOffset, so the drone must not be lifted twice.
                visual.transform.localPosition = Vector3.zero;
                ArtAssets.BakeCharacterMaterials(visual, modelName, new Color(1f, 0.32f, 0.24f));

                var animator = visual.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = CharacterRigBuilder.GetOrCreate(model);
                    animator.applyRootMotion = false;
                }

                var scanRenderer = visual.GetComponentInChildren<Renderer>(true);
                if (scanRenderer != null) SetField(ai, "scanIndicator", scanRenderer);
            }
            else
            {
                Debug.LogWarning($"[NullFrontier] Enemy {kindName}: model {modelName} missing; " +
                                 "spawning collider-only.");
            }

            string path = $"{ArtAssets.EnemyPrefabDir}/{kindName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            // protected [SerializeField] fields live on base classes (EnemyBase), and
            // GetField never walks non-public inherited members.
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            Debug.LogError($"[NullFrontier] {target.GetType().Name}.{fieldName} not found.");
        }
    }
}
