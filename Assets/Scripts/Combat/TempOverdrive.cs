using System.Collections;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class TempOverdrive : Ability
    {
        public const string Id = "temp_overdrive";
        public override string AbilityId => Id;

        [SerializeField] private float overdriveDuration = 3f;
        [SerializeField] private float fireRateBonus = 0.5f;
        [SerializeField] private float moveSpeedBonus = 0.5f;

        // Weapon reads FireRateScale; PlayerController can read MoveSpeedScale for the speed buff.
        public static float FireRateScale { get; private set; } = 1f;
        public static float MoveSpeedScale { get; private set; } = 1f;

        private Coroutine _active;

        public override bool Activate()
        {
            if (_active != null) return false;
            if (!base.Activate()) return false;

            _active = StartCoroutine(OverdriveRoutine());
            return true;
        }

        private IEnumerator OverdriveRoutine()
        {
            FireRateScale = 1f + fireRateBonus;
            MoveSpeedScale = 1f + moveSpeedBonus;
            yield return new WaitForSeconds(overdriveDuration);
            FireRateScale = 1f;
            MoveSpeedScale = 1f;
            _active = null;
        }
    }
}
