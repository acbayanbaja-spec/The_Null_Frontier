using NullFrontier.Characters;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Player
{
    /// <summary>
    /// Runtime character instance: applies the selected CharacterDefinition stats to
    /// Health/Stamina and handles death/respawn at the last checkpoint.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Stamina))]
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 3f;

        public CharacterDefinition Definition { get; private set; }
        public Vector3 LastCheckpoint { get; set; }

        private Health _health;
        private bool _respawning;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void Start()
        {
            Definition = CharacterRegistry.Get(GameManager.Instance?.SelectedCharacterId ?? "iidreuspade");
            _health.SetMaxHealth(Definition.MaxHealth, true);
            GetComponent<Stamina>().SetMax(Definition.MaxStamina, true);
            LastCheckpoint = transform.position;
            _health.OnDied += HandleDeath;
        }

        private void HandleDeath()
        {
            if (_respawning) return;
            _respawning = true;
            GameEvents.RaisePlayerDied();
            Invoke(nameof(Respawn), respawnDelay);
        }

        private void Respawn()
        {
            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.position = LastCheckpoint;
            if (controller != null) controller.enabled = true;

            _health.Heal(_health.MaxHealth);
            _respawning = false;
            GameEvents.RaisePlayerRespawned();
        }
    }
}
