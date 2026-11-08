using System.Collections.Generic;
using NullFrontier.Characters;
using NullFrontier.Combat;
using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Missions;
using NullFrontier.Player;
using NullFrontier.Progression;
using UnityEngine;
using UnityEngine.AI;

namespace NullFrontier.AI
{
    public abstract class EnemyBase : MonoBehaviour
    {
        public enum AIState { Patrol, Investigate, Chase, Attack, Retreat, Stunned }

        [Header("References")]
        [SerializeField] protected NavMeshAgent agent;
        [SerializeField] protected Health health;

        [Header("Detection")]
        [SerializeField] protected float viewDistance = 18f;
        [SerializeField, Range(0f, 360f)] protected float viewAngle = 110f;
        [SerializeField] protected float memorySeconds = 5f;

        [Header("Rewards")]
        [SerializeField] protected float xpReward = 25f;
        [SerializeField] protected string dropItemId = "";
        [SerializeField] protected string enemyTypeId = "enemy";

        [Header("Scan")]
        [SerializeField] protected Renderer scanIndicator;
        [SerializeField] private Color revealedColor = new Color(1f, 0.25f, 0.2f);

        [Header("Death")]
        [SerializeField] private float deathFadeSeconds = 1.6f;

        public AIState State => _state;

        public bool RevealedByScan { get; set; }

        protected abstract float AttackRange { get; }

        protected static readonly List<EnemyBase> Instances = new List<EnemyBase>();

        /// <summary>Every live enemy in the scene. The HUD radar reads this; callers
        /// still have to skip the dead, which stay listed until they are destroyed.</summary>
        public static IReadOnlyList<EnemyBase> Hostiles => Instances;

        public bool IsAlive => health != null && health.IsAlive;

        public float HealthFraction => health != null && health.MaxHealth > 0f
            ? health.CurrentHealth / health.MaxHealth
            : 0f;

        protected Transform _player;
        protected Health _playerHealth;
        protected Transform _target;
        protected Vector3 _lastKnownPosition;
        protected float _memoryTimer;
        protected float _stunTimer;
        protected AIState _state = AIState.Patrol;

        private MaterialPropertyBlock _scanBlock;
        private bool _scanApplied;
        private CharacterVisualRig _rig;
        private EnemyWeapon _weapon;

        // Ground and Cover are the two layers the NavMesh was baked from, so they are the
        // only ones whose volumes a body has no business being inside. Resolved in Awake:
        // LayerMask.GetMask calls NameToLayer, and Unity throws if that happens in a field
        // initializer - it took the whole EnemyBase type down with a TypeInitialization.
        private readonly Collider[] _embedProbe = new Collider[4];
        private float _recoveryCooldown;

        // Bots used to deal damage with nothing drawn at all, so the only feedback was a
        // health bar. A round is a thin hot line, and the colour says what threw it - the
        // same two tints the player's own weapon traces in, so both sides read alike.
        private const float TracerWidth = 0.05f;
        private static readonly Color BallisticTracer = new Color(1f, 0.82f, 0.45f);
        private static readonly Color EnergyTracer = new Color(0.35f, 0.9f, 1f);
        private int _worldMask;

        protected virtual void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (health == null) health = GetComponent<Health>();
            _rig = GetComponentInChildren<CharacterVisualRig>();
            _weapon = GetComponent<EnemyWeapon>();
            _worldMask = LayerMask.GetMask("Ground", "Cover");
            Instances.Add(this);
        }

        protected virtual void OnDestroy() => Instances.Remove(this);

        protected virtual void Start()
        {
            var playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                _player = playerController.transform;
                _playerHealth = playerController.GetComponent<Health>();
            }
            if (health != null) health.OnDied += HandleDeath;
        }

        public static void AlertNearby(Vector3 position, float radius, Vector3 lastKnownPosition)
        {
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                var enemy = Instances[i];
                if (enemy == null) continue;
                if ((enemy.transform.position - position).sqrMagnitude > radius * radius) continue;
                enemy.HearNoise(lastKnownPosition);
            }
        }

        public void Stun(float duration)
        {
            if (health != null && !health.IsAlive) return;
            _stunTimer = Mathf.Max(_stunTimer, duration);
            _state = AIState.Stunned;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        protected virtual void Update()
        {
            if (health != null && !health.IsAlive) return;

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                if (_stunTimer > 0f)
                {
                    UpdateScanIndicator();
                    return;
                }
                _state = AIState.Investigate;
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(_lastKnownPosition);
                }
            }

            UpdateDetection();
            TickState();
            KeepOutOfGeometry();
            UpdateScanIndicator();
        }

        /// <summary>
        /// The NavMesh is a two-dimensional sheet and no bot has a Rigidbody, so nothing
        /// in the engine pushes one back out of a wall it has entered - a hovering drone
        /// crosses into the volume of a building and simply stays there, which is exactly
        /// the "bots walk inside walls" the player sees. This is the guarantee instead: a
        /// body whose chest is inside world geometry is put back on the nearest walkable
        /// point rather than left standing in a wall.
        /// </summary>
        private void KeepOutOfGeometry()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            if (_recoveryCooldown > 0f)
            {
                _recoveryCooldown -= Time.deltaTime;
                return;
            }

            Vector3 chest = transform.position + Vector3.up * 0.9f;
            int count = Physics.OverlapBoxNonAlloc(chest, Vector3.one * 0.15f, _embedProbe,
                Quaternion.identity, _worldMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var other = _embedProbe[i];
                if (other == null || other.transform.IsChildOf(transform)
                    || transform.root == other.transform.root) continue;

                agent.ResetPath();
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit nav, 2.5f,
                        NavMesh.AllAreas))
                    agent.Warp(nav.position);
                _recoveryCooldown = 0.4f;
                return;
            }
        }

        protected virtual void UpdateDetection()
        {
            _target = PickTarget();
            if (_target != null)
            {
                _lastKnownPosition = _target.position;
                _memoryTimer = memorySeconds;

                float dist = Vector3.Distance(transform.position, _target.position);
                if (dist <= AttackRange)
                {
                    if (_state != AIState.Attack && _state != AIState.Retreat
                        && agent != null && agent.isOnNavMesh)
                        agent.isStopped = true;
                    if (_state != AIState.Retreat) _state = AIState.Attack;
                }
                else
                {
                    if (_state == AIState.Attack && agent != null && agent.isOnNavMesh)
                        agent.isStopped = false;
                    if (_state != AIState.Retreat) _state = AIState.Chase;
                }
            }
            else if (_memoryTimer > 0f)
            {
                _memoryTimer -= Time.deltaTime;
                if (_state == AIState.Attack || _state == AIState.Chase)
                {
                    _state = AIState.Investigate;
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = false;
                        agent.SetDestination(_lastKnownPosition);
                    }
                }
                if (_memoryTimer <= 0f) _state = AIState.Patrol;
            }
        }

        protected virtual Transform PickTarget()
        {
            Transform best = null;
            float bestDist = float.MaxValue;

            if (_player != null && _playerHealth != null && _playerHealth.IsAlive && CanSee(_player))
            {
                best = _player;
                bestDist = Vector3.Distance(transform.position, _player.position);
            }

            for (int i = 0; i < Decoy.ActiveInstances.Count; i++)
            {
                var decoy = Decoy.ActiveInstances[i];
                if (decoy == null || !decoy.IsAlive || !CanSee(decoy.transform)) continue;
                float d = Vector3.Distance(transform.position, decoy.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = decoy.transform;
                }
            }
            return best;
        }

        protected virtual bool CanSee(Transform target)
        {
            Vector3 to = target.position - transform.position;
            float dist = to.magnitude;
            if (dist > viewDistance) return false;

            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 0.001f
                && Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;

            Vector3 eye = transform.position + Vector3.up * 1.4f;
            Vector3 aimPoint = target.position + Vector3.up * 1.2f;
            Vector3 dir = aimPoint - eye;
            if (dir.sqrMagnitude < 0.0001f) return true;
            if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, viewDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == target
                    || hit.transform.IsChildOf(target)
                    || target.IsChildOf(hit.transform);
            }
            return true;
        }

        protected virtual void HearNoise(Vector3 position)
        {
            if (health != null && !health.IsAlive) return;
            if (_stunTimer > 0f) return;
            _lastKnownPosition = position;
            _memoryTimer = memorySeconds;
            if (_state == AIState.Patrol || _state == AIState.Investigate)
            {
                _state = AIState.Investigate;
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(position);
                }
            }
        }

        protected virtual void TickState()
        {
            switch (_state)
            {
                case AIState.Patrol: TickPatrol(); break;
                case AIState.Investigate: TickInvestigate(); break;
                case AIState.Chase: TickChase(); break;
                case AIState.Attack: TickAttack(); break;
                case AIState.Retreat: TickRetreat(); break;
                case AIState.Stunned: break;
            }
        }

        protected virtual void TickPatrol() { }

        protected virtual void TickInvestigate()
        {
            if (agent == null || !agent.isOnNavMesh || agent.pathPending) return;
            if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                _memoryTimer = 0f;
                _state = AIState.Patrol;
            }
        }

        protected virtual void TickChase()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.SetDestination(_lastKnownPosition);
        }

        protected virtual void TickAttack()
        {
            FaceTarget(_target);
            PerformAttack();
        }

        protected virtual void TickRetreat() { }

        protected abstract void PerformAttack();

        protected void FaceTarget(Transform target)        {
            if (target == null) return;
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), Time.deltaTime * 8f);
        }

        /// <summary>Plays the attack take on the model. Safe when no rig is present.</summary>
        protected void PlayAttackAnim()
        {
            if (_rig != null) _rig.PlayAttack();
        }

        protected bool FireHitscan(Vector3 origin, Vector3 aimPoint, float damage, DamageType type)
        {
            Vector3 dir = (aimPoint - origin).normalized;
            if (dir.sqrMagnitude < 0.0001f) return false;
            AudioDirector.Instance?.PlayShot(origin);
            _rig?.PlayAttack();

            // The gameplay ray starts at the chest, where the aim maths put it, but the
            // streak is drawn from the barrel the player can actually see. Without this a
            // bot did damage out of thin air with nothing travelling between the two.
            Vector3 muzzle = _weapon != null && _weapon.Muzzle != null
                ? _weapon.Muzzle.position : origin;
            float range = viewDistance * 1.5f;
            Color tint = type == DamageType.Energy ? EnergyTracer : BallisticTracer;

            if (!Physics.Raycast(origin, dir, out RaycastHit hit, range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                CombatEffects.Shot(muzzle, origin + dir * range, tint, TracerWidth);
                return false;
            }

            CombatEffects.Shot(muzzle, hit.point, tint, TracerWidth);
            CombatEffects.Impact(hit.point, hit.normal, tint);

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                damageable.TakeDamage(new DamageInfo(damage, hit.point, dir, gameObject, type));
                return true;
            }
            return false;
        }

        private void UpdateScanIndicator()
        {
            if (scanIndicator == null) return;
            bool revealed = RevealedByScan || EnhancedScanning.IsScanning;
            if (!revealed && !_scanApplied) return;

            _scanApplied = revealed;
            if (revealed)
            {
                if (_scanBlock == null) _scanBlock = new MaterialPropertyBlock();
                _scanBlock.SetColor("_EmissionColor", revealedColor * 2f);
                scanIndicator.SetPropertyBlock(_scanBlock);
            }
            else
            {
                scanIndicator.SetPropertyBlock(new MaterialPropertyBlock());
            }
        }

        protected virtual void HandleDeath()
        {
            var progression = ProgressionSystem.Instance;
            if (progression != null) progression.AddXp(xpReward);

            if (!string.IsNullOrEmpty(dropItemId))
            {
                var inventory = InventorySystem.Instance;
                if (inventory != null) inventory.AddItem(dropItemId, 1);
            }

            var missions = MissionManager.Instance;
            if (missions != null)
                missions.NotifyEvent(MissionManager.MissionEventType.Kill, enemyTypeId, 1);

            if (agent != null) agent.enabled = false;
            enabled = false;

            // The body stays in the scene long enough for the death take to play;
            // CharacterVisualRig keys off Health.OnDied and is not disabled here.
            Destroy(gameObject, deathFadeSeconds);
        }
    }
}
