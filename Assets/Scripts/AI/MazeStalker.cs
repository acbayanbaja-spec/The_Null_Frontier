using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.AI
{
    public class MazeStalker : EnemyBase
    {
        [Header("Charge")]
        [SerializeField] private float chargeSpeedMultiplier = 1.6f;

        [Header("Lunge")]
        [SerializeField] private float lungeRange = 2.2f;
        [SerializeField] private float lungeDamage = 25f;
        [SerializeField] private float lungeWindup = 0.35f;
        [SerializeField] private float lungeDistance = 2.5f;
        [SerializeField] private float lungeCooldown = 1.5f;

        protected override float AttackRange => lungeRange;

        private float _baseSpeed;
        private float _nextLungeTime;
        private float _windupRemaining = -1f;

        protected override void Awake()
        {
            base.Awake();
            if (agent != null) _baseSpeed = agent.speed;
        }

        protected override void TickChase()
        {
            if (agent != null) agent.speed = _baseSpeed * chargeSpeedMultiplier;
            base.TickChase();
        }

        protected override void TickPatrol()
        {
            RestoreSpeed();
            base.TickPatrol();
        }

        protected override void TickInvestigate()
        {
            RestoreSpeed();
            base.TickInvestigate();
        }

        protected override void TickRetreat()
        {
            RestoreSpeed();
            base.TickRetreat();
        }

        private void RestoreSpeed()
        {
            if (agent != null && agent.speed != _baseSpeed)
                agent.speed = _baseSpeed;
        }

        protected override void PerformAttack()
        {
            if (_target == null) return;
            FaceTarget(_target);

            if (_windupRemaining >= 0f)
            {
                _windupRemaining -= Time.deltaTime;
                if (_windupRemaining < 0f) ExecuteLunge();
                return;
            }

            if (Time.time >= _nextLungeTime)
            {
                _windupRemaining = lungeWindup;
                PlayAttackAnim();
            }
        }

        private void ExecuteLunge()
        {
            _nextLungeTime = Time.time + lungeCooldown;

            Vector3 dir = _target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();

            if (agent != null && agent.isOnNavMesh)
                agent.Move(dir * lungeDistance);

            if (_target != null
                && Vector3.Distance(transform.position, _target.position) <= lungeRange + 1f)
            {
                var damageable = _target.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(new DamageInfo(lungeDamage, _target.position,
                        dir, gameObject, DamageType.Melee));
                }
            }
        }
    }
}
