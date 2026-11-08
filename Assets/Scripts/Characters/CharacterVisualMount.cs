using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Characters
{
    /// <summary>
    /// Replaces the greybox capsule with the selected character's rigged model at
    /// runtime. The model is loaded from Resources/Characters and then aligned by
    /// measuring its rendered bounds, so no assumption is made about the FBX pivot:
    /// whatever the author exported, the feet land on the controller's base.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class CharacterVisualMount : MonoBehaviour
    {
        [SerializeField] private string modelFolder = "Characters";
        [SerializeField] private bool hideHostPrimitives = true;
        [SerializeField] private float groundGap = 0.02f;

        private Transform _visual;
        private string _loadedModel;
        private int _realignFrames;

        private void Awake()
        {
            // Falling back to the roster default rather than null: with no id the mount
            // does nothing and the player walks around as the 2 m greybox capsule, which is
            // half a head taller than every 1.75 m bot.
            Mount(GameManager.Instance != null
                ? GameManager.Instance.SelectedCharacterId
                : "iidreuspade");
        }

        private void Start()
        {
            // Skinned-mesh bounds are not trustworthy until the animator has written a
            // frame, so measure again once the first pose exists.
            if (_visual != null) _realignFrames = 2;
        }

        private void LateUpdate()
        {
            // LateUpdate, not Update: the Animator writes its pose between the two, and
            // measuring in Update kept using the unposed bounds. That put the city player
            // ten metres above his controller and left him out of every frame.
            if (_realignFrames <= 0) return;
            _realignFrames--;
            AlignFeetToController();
        }

        /// <summary>Load and mount the model for a roster id. Safe to call again on swap.</summary>
        public void Mount(string characterId)
        {
            var definition = characterId == null ? null : CharacterRegistry.Get(characterId);
            if (definition == null || string.IsNullOrEmpty(definition.ModelPrefab)) return;
            if (definition.ModelPrefab == _loadedModel && _visual != null) return;

            var prefab = Resources.Load<GameObject>($"{modelFolder}/{definition.ModelPrefab}");
            if (prefab == null)
            {
                Debug.LogWarning($"[NullFrontier] No visual prefab at Resources/{modelFolder}/" +
                                 $"{definition.ModelPrefab}. Run Null Frontier > Rebuild Character And Enemy Prefabs.");
                return;
            }

            if (_visual != null)
            {
                // Deactivate before Destroy: Destroy is deferred to the end of the frame,
                // and an active old model keeps answering GetComponentInChildren, which
                // made the rig cache the animator that was about to be destroyed.
                _visual.gameObject.SetActive(false);
                Destroy(_visual.gameObject);
            }
            if (hideHostPrimitives) HideHostPrimitives();

            var instance = Instantiate(prefab, transform);
            instance.name = "CharacterVisual";
            // Instantiate(original, parent) keeps the asset's world position; the visual
            // belongs at the host's feet, and the alignment below measures from there.
            instance.transform.localPosition = Vector3.zero;
            _visual = instance.transform;
            _loadedModel = definition.ModelPrefab;

            // Movement comes from the CharacterController / NavMeshAgent. Left on, the
            // clip's root motion drove the visual away from its owner - the city player
            // was rendering ten metres above the camera within seconds of Play.
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                // Write the default pose immediately; unposed skinned bounds are what the
                // alignment used to measure, and they report a box around the origin.
                animator.Update(0f);
            }

            AlignFeetToController();
            EnsureRig(definition.AccentColor);
            _realignFrames = 2;
        }

        private void EnsureRig(Color accent)
        {
            var rig = GetComponent<CharacterVisualRig>();
            if (rig == null)
            {
                // Added while the model already exists so its Awake caches the real renderers.
                rig = gameObject.AddComponent<CharacterVisualRig>();
            }
            else rig.RefreshRenderers();

            rig.SetFlashColor(Color.Lerp(accent, Color.white, 0.35f));
        }

        private void HideHostPrimitives()
        {
            foreach (var renderer in GetComponents<Renderer>())
                renderer.enabled = false;
        }

        private void AlignFeetToController()
        {
            if (_visual == null) return;

            var controller = GetComponent<CharacterController>();
            float floor = (transform.position + (controller != null
                ? controller.center - Vector3.up * (controller.height * 0.5f)
                : -Vector3.up * 0.9f)).y + groundGap;

            // Humanoid rigs answer this exactly: the ankles are where the feet are, no
            // matter what the skinned bounds claim. The bounds path below is the fallback
            // for generic rigs and is why the character was once mounted ten metres up -
            // unposed skinned bounds are a box around the skeleton origin, not the body.
            var animator = _visual.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (left != null && right != null)
                {
                    // The ankle bone sits a hand's width above the sole.
                    float lowest = Mathf.Min(left.position.y, right.position.y);
                    _visual.position += Vector3.up * (floor + 0.08f - lowest);
                    return;
                }
            }

            var renderers = _visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float shift = floor - bounds.min.y;
            // A correction bigger than the character is garbage, not a pose.
            if (Mathf.Abs(shift) < 4f) _visual.position += Vector3.up * shift;
        }
    }
}
