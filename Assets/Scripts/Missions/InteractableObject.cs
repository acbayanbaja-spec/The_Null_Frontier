using System.Collections.Generic;
using NullFrontier.Core;
using NullFrontier.Items;
using UnityEngine;

namespace NullFrontier.Missions
{
    /// <summary>
    /// Something the player can use: a terminal, a supply crate, a data cache.
    /// Pressing use inside the trigger reports the event to the mission system, pays out
    /// an item and can consume the object. The HUD reads NearbyPrompt so the player can
    /// see what is in reach.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InteractableObject : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private MissionManager.MissionEventType eventType = MissionManager.MissionEventType.Interact;
        [SerializeField] private string prompt = "USE";
        [SerializeField] private string grantItemId;
        [SerializeField] private int grantQuantity = 1;
        [SerializeField] private bool consumeOnUse;
        [SerializeField] private GameObject spawnOnUse;

        [Tooltip("How many times the object has to be used before the objective counts as "
            + "done. A relay uses this for its spin-up cycles.")]
        [SerializeField] private int requiredUses = 1;

        [Tooltip("Seconds the object has to sit out between uses when requiredUses > 1.")]
        [SerializeField] private float useCooldown = 6f;

        private static readonly List<InteractableObject> InReach = new();
        private static readonly List<InteractableObject> Markers = new();

        /// <summary>Every interactable in the scene. The radar uses this to pin the
        /// objects a contract is actually pointing at.</summary>
        public static IReadOnlyList<InteractableObject> All => Markers;

        public string MarkerId => id;
        public bool IsComplete => _used;

        private bool _tracked;
        private bool _used;
        private int _uses;
        private float _readyAt;

        /// <summary>Prompt text for whatever the player is currently standing on, or empty.</summary>
        public static string NearbyPrompt { get; private set; } = string.Empty;

        private void Awake()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
        }

        private void OnEnable()
        {
            GameEvents.PlayerInteracted += HandleInteract;
            if (!Markers.Contains(this)) Markers.Add(this);
        }

        private void OnDisable()
        {
            GameEvents.PlayerInteracted -= HandleInteract;
            Markers.Remove(this);
            SetTracked(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_used && IsPlayer(other)) SetTracked(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other)) SetTracked(false);
        }

        private static bool IsPlayer(Collider collider) =>
            collider.GetComponentInParent<Player.PlayerCharacter>() != null;

        private void SetTracked(bool tracked)
        {
            if (tracked == _tracked) return;
            _tracked = tracked;
            if (tracked) InReach.Add(this);
            else InReach.Remove(this);
            RecomputePrompt();
        }

        private static void RecomputePrompt() =>
            NearbyPrompt = InReach.Count > 0 ? InReach[InReach.Count - 1].BuildPrompt() : string.Empty;

        /// <summary>A multi-use object changes state without the player leaving the zone,
        /// so its prompt is refreshed rather than only set on entry and exit.</summary>
        private void Update()
        {
            if (_tracked && Mathf.Max(1, requiredUses) > 1) RecomputePrompt();
        }

        private string BuildPrompt()
        {
            if (_used) return string.Empty;
            string label = string.IsNullOrEmpty(prompt) ? $"USE {name}" : prompt.ToUpperInvariant();
            if (Time.time < _readyAt) return $"{label}  ·  CYCLING";
            int remaining = Mathf.Max(1, requiredUses) - _uses;
            return remaining > 1 ? $"{label}  ·  x{remaining}" : label;
        }

        private void HandleInteract(Vector3 playerPosition)
        {
            if (_used || !_tracked || Time.time < _readyAt) return;

            if (!string.IsNullOrEmpty(id)) MissionManager.Instance?.NotifyEvent(eventType, id);

            if (!string.IsNullOrEmpty(grantItemId))
            {
                InventorySystem.Instance?.AddItem(grantItemId, Mathf.Max(1, grantQuantity));
                MissionManager.Instance?.NotifyEvent(MissionManager.MissionEventType.Collect,
                    grantItemId, Mathf.Max(1, grantQuantity));
            }

            if (spawnOnUse != null)
                Object.Instantiate(spawnOnUse, transform.position, transform.rotation);

            AudioDirector.Instance?.PlayUiClick();

            _uses++;
            if (_uses >= Mathf.Max(1, requiredUses))
            {
                _used = true;
                SetTracked(false);
                if (consumeOnUse) StopRendering();
            }
            else
            {
                _readyAt = Time.time + Mathf.Max(0.5f, useCooldown);
                RecomputePrompt();
            }
        }

        private void StopRendering()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var light in GetComponentsInChildren<Light>(true))
                light.enabled = false;
            enabled = false;
        }
    }
}
