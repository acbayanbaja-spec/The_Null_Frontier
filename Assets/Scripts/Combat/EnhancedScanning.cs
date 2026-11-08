using System.Collections;
using NullFrontier.AI;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class EnhancedScanning : Ability
    {
        public const string Id = "enhanced_scanning";
        public override string AbilityId => Id;

        [SerializeField] private float scanDuration = 3f;

        // EnemyBase reads this flag (plus per-enemy RevealedByScan) to drive scanIndicator emission.
        public static bool IsScanning { get; private set; }

        private Coroutine _scan;

        public override bool Activate()
        {
            if (_scan != null) return false;
            if (!base.Activate()) return false;

            _scan = StartCoroutine(ScanRoutine());
            return true;
        }

        private IEnumerator ScanRoutine()
        {
            IsScanning = true;
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy != null) enemy.RevealedByScan = true;
            }

            float elapsed = 0f;
            while (elapsed < scanDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            foreach (var enemy in enemies)
            {
                if (enemy != null) enemy.RevealedByScan = false;
            }
            IsScanning = false;
            _scan = null;
        }
    }
}
