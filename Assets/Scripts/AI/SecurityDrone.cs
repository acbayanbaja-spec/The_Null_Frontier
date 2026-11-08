using NullFrontier.Core;
using UnityEngine;
using UnityEngine.AI;

namespace NullFrontier.AI
{
    public class SecurityDrone : EnemyBase
    {
        [Header("Drone")]
        [SerializeField] private float hoverHeight = 2.5f;
        [SerializeField] private float preferredDistance = 6f;
        [SerializeField] private float engageDistance = 12f;
        [SerializeField] private float strafeInterval = 1.2f;
        [SerializeField] private float strafeOffset = 3f;

        [Header("Weapon")]
        [SerializeField] private float shotDamage = 8f;
        [SerializeField] private float fireInterval = 0.8f;
        [SerializeField] private float aimHeight = 1.2f;

        protected override float AttackRange => engageDistance;

        private float _nextShotTime;
        private float _strafeTimer;
        private int _strafeSign = 1;

        protected override void Awake()
        {
            base.Awake();
            if (agent != null)
            {
                // NavMeshAgent applies no gravity; baseOffset makes the drone hover above the surface.
                agent.baseOffset = hoverHeight;
                agent.stoppingDistance = preferredDistance;
            }
        }

        protected override void PerformAttack()
        {
            if (_target == null) return;

            float dist = Vector3.Distance(transform.position, _target.position);

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                Vector3 toTarget = (_target.position - transform.position).normalized;
                if (dist > preferredDistance + 0.5f)
                {
                    agent.SetDestination(_target.position);
                }
                else
                {
                    _strafeTimer -= Time.deltaTime;
                    if (_strafeTimer <= 0f)
                    {
                        _strafeSign = -_strafeSign;
                        _strafeTimer = strafeInterval;
                    }
                    Vector3 side = Vector3.Cross(Vector3.up, toTarget) * (_strafeSign * strafeOffset);
                    Vector3 hold = toTarget * (preferredDistance - dist);

                    // A raw offset handed to SetDestination goes wherever it lands, and for
                    // a hovering bot those points are at chest height inside the neighbouring
                    // building. The drone is the kind that visibly walks through walls, so
                    // its strafe target has to come off the mesh, not off arithmetic.
                    Vector3 wanted = transform.position + side + hold;
                    if (NavMesh.SamplePosition(wanted, out NavMeshHit nav, 2.5f, NavMesh.AllAreas))
                        agent.SetDestination(nav.position);
                    else agent.ResetPath();
                }
            }

            if (Time.time >= _nextShotTime)
            {
                _nextShotTime = Time.time + fireInterval;
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                FireHitscan(origin, _target.position + Vector3.up * aimHeight,
                    shotDamage, DamageType.Energy);
            }
        }
    }
}
