using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.AI
{
    public class DigitalSoldier : EnemyBase
    {
        [Header("Patrol")]
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private float waypointReachDistance = 0.6f;

        [Header("Combat")]
        [SerializeField] private float shotDamage = 10f;
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float burstShotInterval = 0.12f;
        [SerializeField] private float burstCooldown = 1.2f;
        [SerializeField] private float aimHeight = 1.2f;

        [Header("Retreat")]
        [SerializeField, Range(0f, 1f)] private float retreatHealthFraction = 0.4f;
        [SerializeField, Range(0f, 1f)] private float recoverHealthFraction = 0.6f;

        protected override float AttackRange => 14f;

        private int _waypointIndex;
        private Transform _coverPoint;
        private int _shotsRemaining;
        private float _nextBurstShotTime;
        private float _nextBurstStartTime;

        protected override void Awake()
        {
            base.Awake();
            if (agent != null) agent.stoppingDistance = waypointReachDistance;
        }

        /// <summary>
        /// Route handed over by EnemySpawner after instantiation, so patrol
        /// waypoints can be sampled onto the baked NavMesh instead of guessed.
        /// </summary>
        public void SetPatrolRoute(Transform[] waypoints)
        {
            patrolWaypoints = waypoints;
            _waypointIndex = 0;
            if (waypoints != null && waypoints.Length > 0
                && agent != null && agent.isOnNavMesh && waypoints[0] != null)
            {
                agent.SetDestination(waypoints[0].position);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (_state == AIState.Retreat || _stunTimer > 0f) return;
            if (health == null || !health.IsAlive) return;
            if (health.CurrentHealth <= health.MaxHealth * retreatHealthFraction)
                EnterRetreat();
        }

        protected override void TickPatrol()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;
            if (agent.pathPending) return;

            if (agent.remainingDistance <= waypointReachDistance || !agent.hasPath)
            {
                _waypointIndex = (_waypointIndex + 1) % patrolWaypoints.Length;
                var waypoint = patrolWaypoints[_waypointIndex];
                if (waypoint != null) agent.SetDestination(waypoint.position);
            }
        }

        protected override void TickRetreat()
        {
            if (health != null && health.CurrentHealth >= health.MaxHealth * recoverHealthFraction)
            {
                _state = AIState.Investigate;
                if (agent != null && agent.isOnNavMesh)
                    agent.SetDestination(_lastKnownPosition);
                return;
            }

            if (agent != null && agent.isOnNavMesh && _coverPoint != null)
            {
                agent.isStopped = false;
                if (!agent.pathPending && agent.remainingDistance > waypointReachDistance + 0.2f)
                    agent.SetDestination(_coverPoint.position);
            }
        }

        protected override void PerformAttack()
        {
            if (_target == null) return;
            FaceTarget(_target);

            if (_shotsRemaining <= 0)
            {
                if (Time.time < _nextBurstStartTime) return;
                _shotsRemaining = Mathf.Max(1, burstCount);
            }

            if (Time.time < _nextBurstShotTime) return;

            _nextBurstShotTime = Time.time + burstShotInterval;
            Vector3 origin = transform.position + Vector3.up * 1.4f;
            FireHitscan(origin, _target.position + Vector3.up * aimHeight,
                shotDamage, DamageType.Ballistic);

            _shotsRemaining--;
            if (_shotsRemaining <= 0)
                _nextBurstStartTime = Time.time + burstCooldown;
        }

        private void EnterRetreat()
        {
            _state = AIState.Retreat;
            _coverPoint = FindNearestCover();
            if (agent != null && agent.isOnNavMesh && _coverPoint != null)
            {
                agent.isStopped = false;
                agent.SetDestination(_coverPoint.position);
            }
        }

        private Transform FindNearestCover()
        {
            var covers = GameObject.FindGameObjectsWithTag("CoverPoint");
            Transform best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < covers.Length; i++)
            {
                float d = (covers[i].transform.position - transform.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = covers[i].transform;
                }
            }
            return best;
        }
    }
}
