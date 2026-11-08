using System;
using NullFrontier.Player;
using UnityEngine;

namespace NullFrontier.Combat
{
    public abstract class Ability : MonoBehaviour
    {
        [SerializeField] protected float cooldown = 8f;
        [SerializeField] protected float staminaCost;

        public abstract string AbilityId { get; }
        public float CooldownRemaining { get; protected set; }
        public float CooldownNormalized => cooldown > 0f ? CooldownRemaining / cooldown : 0f;
        public bool IsReady => CooldownRemaining <= 0f;

        public event Action<float> OnCooldownChanged;

        protected virtual void Update()
        {
            if (CooldownRemaining <= 0f) return;
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            RaiseCooldownChanged();
        }

        public virtual bool CanActivate()
        {
            if (CooldownRemaining > 0f) return false;
            if (staminaCost > 0f)
            {
                var stamina = GetComponentInParent<Stamina>();
                if (stamina != null && stamina.Current < staminaCost) return false;
            }
            return true;
        }

        public virtual bool Activate()
        {
            if (!CanActivate()) return false;
            if (staminaCost > 0f)
            {
                var stamina = GetComponentInParent<Stamina>();
                if (stamina != null && !stamina.TrySpend(staminaCost))
                    return false;
            }
            CooldownRemaining = cooldown;
            RaiseCooldownChanged();
            return true;
        }

        protected void RaiseCooldownChanged() => OnCooldownChanged?.Invoke(CooldownNormalized);
    }
}
