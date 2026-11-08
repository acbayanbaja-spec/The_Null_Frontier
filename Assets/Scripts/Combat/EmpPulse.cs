using System.Collections.Generic;
using NullFrontier.AI;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class EmpPulse : Ability
    {
        public const string Id = "emp_pulse";
        public override string AbilityId => Id;

        [SerializeField] private float radius = 8f;
        [SerializeField] private float stunDuration = 3f;
        [SerializeField] private LayerMask enemyMask = ~0;

        public override bool Activate()
        {
            if (!base.Activate()) return false;

            var hits = Physics.OverlapSphere(transform.position, radius, enemyMask,
                QueryTriggerInteraction.Ignore);
            var stunned = new HashSet<EnemyBase>();
            for (int i = 0; i < hits.Length; i++)
            {
                var enemy = hits[i].GetComponentInParent<EnemyBase>();
                if (enemy != null && stunned.Add(enemy))
                    enemy.Stun(stunDuration);
            }
            return true;
        }
    }
}
