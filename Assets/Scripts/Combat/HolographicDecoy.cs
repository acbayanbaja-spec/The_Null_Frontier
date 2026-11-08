using System.Collections.Generic;
using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class Decoy : MonoBehaviour, IDamageable
    {
        public static readonly List<Decoy> ActiveInstances = new List<Decoy>();

        [SerializeField] private float maxHealth = 40f;
        [SerializeField] private float lifetime = 8f;

        public bool IsAlive { get; private set; }
        public Transform Transform => transform;

        private float _health;
        private float _age;

        private void OnEnable()
        {
            _health = maxHealth;
            _age = 0f;
            IsAlive = true;
            ActiveInstances.Add(this);
        }

        private void OnDisable()
        {
            IsAlive = false;
            ActiveInstances.Remove(this);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= lifetime) Destroy(gameObject);
        }

        public void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive) return;
            _health -= damage.Amount;
            if (_health <= 0f) Destroy(gameObject);
        }

        public void Heal(float amount) { }
    }

    public class HolographicDecoy : Ability
    {
        public const string Id = "holographic_decoy";
        public override string AbilityId => Id;

        [SerializeField] private Decoy decoyPrefab;
        [SerializeField] private float decoyLifetime = 8f;

        private Decoy _activeDecoy;

        public override bool Activate()
        {
            if (!base.Activate()) return false;

            if (_activeDecoy != null) Destroy(_activeDecoy.gameObject);

            Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.1f;
            if (decoyPrefab != null)
            {
                _activeDecoy = Instantiate(decoyPrefab, spawnPos, transform.rotation);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "HolographicDecoy";
                go.transform.SetPositionAndRotation(spawnPos, transform.rotation);
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.3f, 0.8f, 1f, 0.4f);
                _activeDecoy = go.AddComponent<Decoy>();
            }

            return true;
        }
    }
}
