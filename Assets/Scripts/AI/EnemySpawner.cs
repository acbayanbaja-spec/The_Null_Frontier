using System.Collections.Generic;
using NullFrontier.Core;
using UnityEngine;
using UnityEngine.AI;

namespace NullFrontier.AI
{
    /// <summary>
    /// Keeps the streets dangerous. Spawns the enemy prefabs that
    /// Null Frontier > Build All Scenes writes into Resources/Enemies, samples
    /// legal NavMesh positions around the player, hands each patrol bot a route,
    /// and refills the population after a delay so a mission can always be
    /// completed even if the player kills everything first.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [System.Serializable]
        public class EnemyKind
        {
            public string prefabResourcePath = "Enemies/DigitalSoldier";
            public int population = 3;
            public float health = 60f;
            public float moveSpeed = 3.4f;
            public float innerRadius = 16f;
            public float outerRadius = 52f;
            public float hoverOffset;
        }

        [Header("Roster")]
        [SerializeField] private EnemyKind[] kinds;

        [Header("Population")]
        [SerializeField] private int hardCap = 14;
        [SerializeField] private float respawnDelay = 16f;
        [SerializeField] private float initialSpawnInterval = 0.35f;

        [Header("Patrol")]
        [SerializeField] private bool buildPatrolRoutes = true;
        [SerializeField] private float waypointSpacing = 7f;
        [SerializeField] private int waypointCount = 4;

        [Header("Anchor")]
        [SerializeField] private Transform spawnAnchor;

        private class Pending
        {
            public EnemyKind kind;
            public float readyAt;
        }

        private readonly List<GameObject> _alive = new();
        private readonly List<Pending> _pending = new();
        private readonly Dictionary<EnemyKind, int> _targetCount = new();
        private readonly Dictionary<EnemyKind, int> _aliveCount = new();
        private float _nextInitialSpawn;
        private bool _missingReported;

        private void Start()
        {
            if (kinds == null || kinds.Length == 0)
            {
                Debug.LogWarning("[NullFrontier] EnemySpawner has no kinds configured.");
                enabled = false;
                return;
            }

            if (spawnAnchor == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) spawnAnchor = player.transform;
            }

            int total = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                if (kind == null || string.IsNullOrEmpty(kind.prefabResourcePath)) continue;
                int count = Mathf.Max(0, kind.population);
                if (total + count > hardCap) count = Mathf.Max(0, hardCap - total);
                _targetCount[kind] = count;
                _aliveCount[kind] = 0;
                total += count;
                for (int n = 0; n < count; n++)
                    _pending.Add(new Pending { kind = kind, readyAt = _nextInitialSpawn });
                _nextInitialSpawn += initialSpawnInterval;
            }

            if (total == 0)
            {
                Debug.LogWarning("[NullFrontier] EnemySpawner resolved an empty population.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (_pending.Count == 0) return;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_alive.Count >= hardCap) return;
                var pending = _pending[i];
                if (Time.time < pending.readyAt) continue;
                _pending.RemoveAt(i);
                Spawn(pending.kind);
            }
        }

        private void Spawn(EnemyKind kind)
        {
            var prefab = Resources.Load<GameObject>(kind.prefabResourcePath);
            if (prefab == null)
            {
                if (!_missingReported)
                {
                    _missingReported = true;
                    Debug.LogError($"[NullFrontier] Enemy prefab missing: Resources/{kind.prefabResourcePath}. " +
                                   "Run Null Frontier > Build All Scenes.");
                }
                return;
            }

            Vector3 basePoint = RandomPointOnRing(kind);
            if (!NavMesh.SamplePosition(basePoint, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                // No walkable surface near the requested ring: try again later.
                _pending.Add(new Pending { kind = kind, readyAt = Time.time + 1.5f });
                return;
            }

            var instance = Instantiate(prefab, hit.position,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            instance.name = prefab.name + "_" + _alive.Count;
            _alive.Add(instance);
            _aliveCount[kind] = _aliveCount.TryGetValue(kind, out int running) ? running + 1 : 1;

            var health = instance.GetComponent<Health>();
            if (health != null)
            {
                health.SetMaxHealth(kind.health, true);
                var captured = instance;
                health.OnDied += () => ScheduleRespawn(captured, kind);
            }

            var agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = kind.moveSpeed;
                agent.baseOffset = kind.hoverOffset;
                if (agent.isOnNavMesh) agent.Warp(hit.position);
            }

            if (buildPatrolRoutes) AssignPatrolRoute(instance, kind);
        }

        private Vector3 RandomPointOnRing(EnemyKind kind)
        {
            Vector3 origin = spawnAnchor != null ? spawnAnchor.position : Vector3.zero;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(kind.innerRadius, Mathf.Max(kind.innerRadius + 1f, kind.outerRadius));
            return origin + new Vector3(Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius);
        }

        private void AssignPatrolRoute(GameObject instance, EnemyKind kind)
        {
            var soldier = instance.GetComponent<DigitalSoldier>();
            if (soldier == null) return;

            var route = new Transform[Mathf.Max(2, waypointCount)];
            for (int i = 0; i < route.Length; i++)
            {
                float angle = (Mathf.PI * 2f * i) / route.Length + Random.Range(-0.3f, 0.3f);
                float radius = Random.Range(waypointSpacing * 0.6f, waypointSpacing * 1.4f);
                Vector3 wanted = instance.transform.position
                    + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var node = new GameObject($"Waypoint_{i}");
                node.transform.SetParent(instance.transform, false);
                if (NavMesh.SamplePosition(wanted, out NavMeshHit nodeHit, waypointSpacing, NavMesh.AllAreas))
                    node.transform.position = nodeHit.position;
                else
                    node.transform.position = wanted;
                node.SetActive(false);
                route[i] = node.transform;
            }
            soldier.SetPatrolRoute(route);
        }

        private void ScheduleRespawn(GameObject deadInstance, EnemyKind kind)
        {
            _alive.Remove(deadInstance);
            int remaining = _aliveCount.TryGetValue(kind, out int running)
                ? Mathf.Max(0, running - 1)
                : 0;
            _aliveCount[kind] = remaining;

            int target = _targetCount.TryGetValue(kind, out int n) ? n : 0;
            if (remaining >= target) return;

            _pending.Add(new Pending { kind = kind, readyAt = Time.time + respawnDelay });
        }
    }
}
