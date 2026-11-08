using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Generates one AnimatorController per character model from the takes that
    /// ship inside its FBX. States are discrete (Idle / Walk / Run / Shoot / Hit /
    /// Death) rather than a blend tree, because the packs only carry a walk cycle -
    /// Run is the same clip played faster through AnimatorState.speed.
    /// </summary>
    public static class CharacterRigBuilder
    {
        private const float WalkEnter = 0.4f;
        private const float WalkExit = 0.3f;
        private const float RunEnter = 5.6f;
        private const float RunExit = 5.2f;
        private const float RunSpeedScale = 1.75f;

        public static RuntimeAnimatorController GetOrCreate(GameObject model)
        {
            if (model == null) return null;

            string modelPath = AssetDatabase.GetAssetPath(model);
            string name = Path.GetFileNameWithoutExtension(modelPath);
            string controllerPath = $"{ArtAssets.RigDir}/{name}.controller";

            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null) return existing;

            ArtAssets.EnsureFolder(ArtAssets.RigDir);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MotionY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            var idleClip = ArtAssets.FindClip(model, "Idle");
            var walkClip = ArtAssets.FindClip(model, "Walk", "WalkCarry");
            var shootClip = ArtAssets.FindClip(model, "ShootOnehanded", "Shoot", "Punch");
            var hitClip = ArtAssets.FindClip(model, "RecieveHit", "ReceiveHit", "Hit");
            var deathClip = ArtAssets.FindClip(model, "Defeat", "Death", "Die");

            if (idleClip == null || walkClip == null)
            {
                Debug.LogWarning($"[NullFrontier] {name}: no Idle/Walk take found - " +
                                 "locomotion will play the pose from the model only. " +
                                 "Clips: " + ClipListing(model));
            }

            // Locomotion: the safe core of the rig. Both states drive the same clip
            // assets; Run just plays Walk quicker.
            var idle = sm.AddState("Idle", new Vector3(-320f, 60f));
            idle.motion = idleClip != null ? idleClip : walkClip;
            sm.defaultState = idle;

            var walk = sm.AddState("Walk", new Vector3(-320f, 200f));
            walk.motion = walkClip != null ? walkClip : idleClip;

            var run = sm.AddState("Run", new Vector3(-320f, 340f));
            run.motion = walkClip != null ? walkClip : idleClip;
            run.speed = RunSpeedScale;

            To(idle, walk, AnimatorConditionMode.Greater, WalkEnter);
            To(walk, idle, AnimatorConditionMode.Less, WalkExit);
            To(walk, run, AnimatorConditionMode.Greater, RunEnter);
            To(run, walk, AnimatorConditionMode.Less, RunExit);

            // One-shot reactions, entered from anywhere and self-exiting back to Idle.
            if (shootClip != null) AddOneShot(sm, idle, "Shoot", "Shoot", shootClip, 0.55f);
            if (hitClip != null) AddOneShot(sm, idle, "Hit", "Hit", hitClip, 0.35f);
            if (deathClip != null) AddTerminal(sm, "Death", "Die", deathClip);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NullFrontier] Rig built for {name}: " +
                      $"idle={Name(idleClip)} walk={Name(walkClip)} shoot={Name(shootClip)} " +
                      $"hit={Name(hitClip)} death={Name(deathClip)}");
            return controller;
        }

        private static void To(AnimatorState from, AnimatorState to,
            AnimatorConditionMode mode, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.14f;
            transition.conditions = new[]
            {
                new AnimatorCondition { mode = mode, parameter = "Speed", threshold = threshold }
            };
        }

        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState exitTarget,
            string stateName, string trigger, AnimationClip clip, float exitPoint)
        {
            var state = sm.AddState(stateName, new Vector3(60f, 60f));
            state.motion = clip;
            state.writeDefaultValues = true;

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.08f;
            enter.canTransitionToSelf = false;
            enter.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = trigger, threshold = 0f }
            };

            var back = state.AddTransition(exitTarget);
            back.hasExitTime = true;
            back.exitTime = exitPoint;
            back.duration = 0.12f;
        }

        private static void AddTerminal(AnimatorStateMachine sm, string stateName,
            string trigger, AnimationClip clip)
        {
            var state = sm.AddState(stateName, new Vector3(60f, 300f));
            state.motion = clip;
            state.writeDefaultValues = true;
            state.speed = 1f;

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.1f;
            enter.canTransitionToSelf = false;
            enter.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = trigger, threshold = 0f }
            };
        }

        private static string ClipListing(GameObject model)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var clip in ArtAssets.AllClips(model))
                sb.Append(clip.name).Append(' ');
            return sb.Length == 0 ? "(none)" : sb.ToString();
        }

        private static string Name(AnimationClip clip) => clip == null ? "missing" : clip.name;
    }
}
