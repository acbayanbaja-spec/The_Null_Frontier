using System;
using System.Collections.Generic;
using NullFrontier.Characters;
using NullFrontier.Core;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class AbilityController : MonoBehaviour
    {
        [SerializeField] private string fallbackAbilityId = TacticalDash.Id;

        public Ability CurrentAbility { get; private set; }
        public float CooldownNormalized =>
            CurrentAbility != null ? CurrentAbility.CooldownNormalized : 0f;

        public event Action<float> OnCooldownChanged;

        private readonly Dictionary<string, Ability> _abilities = new Dictionary<string, Ability>();
        private readonly PlayerInputState _input = new PlayerInputState();
        private IInputProvider _provider;

        private void Start()
        {
            foreach (var ability in GetComponentsInChildren<Ability>(true))
            {
                if (ability == null || string.IsNullOrEmpty(ability.AbilityId)) continue;
                _abilities[ability.AbilityId] = ability;
                ability.OnCooldownChanged += HandleAbilityCooldown;
            }

            string selectedId = GameManager.Instance != null
                ? GameManager.Instance.SelectedCharacterId
                : "iidreuspade";
            var definition = CharacterRegistry.Get(selectedId);
            string abilityId = definition != null && !string.IsNullOrEmpty(definition.AbilityId)
                ? definition.AbilityId
                : fallbackAbilityId;
            if (_abilities.TryGetValue(abilityId, out var matched)) CurrentAbility = matched;

            _provider = FindInputProvider();
        }

        private void HandleAbilityCooldown(float normalized) => OnCooldownChanged?.Invoke(normalized);

        private void Update()
        {
            _input.Reset();
            _provider?.Poll(_input);

            if (_input.AbilityPressed)
                ActivateCurrentAbility();
        }

        public void ActivateCurrentAbility()
        {
            if (CurrentAbility != null)
                CurrentAbility.Activate();
        }

        private IInputProvider FindInputProvider()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IInputProvider provider && provider.IsActive)
                    return provider;
            }
            return GetComponent<IInputProvider>();
        }
    }
}
