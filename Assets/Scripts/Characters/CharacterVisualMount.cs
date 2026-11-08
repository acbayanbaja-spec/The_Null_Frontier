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
        private bool _pendingRealign;

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
            if (_visual != null) _pendingRealign = true;
        }

        private void Update()
        {
            if (!_pendingRealign) return;
            _pendingRealign = false;
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

            if (_visual != null) Destroy(_visual.gameObject);
            if (hideHostPrimitives) HideHostPrimitives();

            var instance = Instantiate(prefab, transform);
            instance.name = "CharacterVisual";
            _visual = instance.transform;
            _loadedModel = definition.ModelPrefab;

            AlignFeetToController();
            EnsureRig(definition.AccentColor);
            _pendingRealign = true;
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
            var renderers = _visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Vector3 pivot = _visual.position;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.0001f) return;

            Vector3 footTarget = transform.position
                + (controller != null
                    ? controller.center - Vector3.up * (controller.height * 0.5f)
                    : -Vector3.up * 0.9f)
                + Vector3.up * groundGap;

            // Bounds are world-space; convert the world correction into the local frame
            // so it survives the parent's rotation.
            Vector3 shift = new Vector3(pivot.x - bounds.center.x, footTarget.y - bounds.min.y,
                pivot.z - bounds.center.z);
            _visual.localPosition += transform.InverseTransformDirection(shift);
        }
    }
}
