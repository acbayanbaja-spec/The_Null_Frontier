using System.Collections.Generic;
using NullFrontier.AI;
using NullFrontier.Missions;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Top-down tactical radar. Everything within Range metres of the operative is
    /// projected into the panel and rotated so the direction the player is facing is
    /// always up, which is the one convention mobile players already know.
    ///
    /// The dots are created by the editor build pass and pooled here: a radar that
    /// instantiates during a firefight is a radar that stutters during a firefight.
    /// </summary>
    public class RadarController : MonoBehaviour
    {
        [Header("Pools")]
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private RectTransform hostileLayer;
        [SerializeField] private RectTransform objectiveLayer;
        [SerializeField] private RectTransform compass;

        [Header("Tuning")]
        [SerializeField] private float range = 55f;
        [SerializeField] private float refreshSeconds = 0.05f;
        [SerializeField] private float edgeInset = 0.86f;

        private readonly List<Image> _hostiles = new();
        private readonly List<Image> _objectives = new();
        private Transform _player;
        private RectTransform _panel;
        private float _nextRefresh;

        private void Awake()
        {
            _panel = hostileLayer != null ? hostileLayer : transform as RectTransform;
            Collect(hostileLayer, _hostiles);
            Collect(objectiveLayer, _objectives);
        }

        private void Start()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            _player = tagged != null ? tagged.transform : null;
        }

        private static void Collect(RectTransform layer, List<Image> output)
        {
            if (layer == null) return;
            for (int i = 0; i < layer.childCount; i++)
            {
                var image = layer.GetChild(i).GetComponent<Image>();
                if (image != null) output.Add(image);
            }
        }

        private void LateUpdate()
        {
            // LateUpdate: the dots must land where the world is at the end of the frame,
            // not where it was when the physics step ran.
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.01f, refreshSeconds);

            if (_player == null || _panel == null) return;

            Vector3 facing = _player.forward;
            facing.y = 0f;
            float yaw = facing.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg
                : 0f;

            if (compass != null) compass.localEulerAngles = new Vector3(0f, 0f, -yaw);
            if (playerMarker != null)
            {
                playerMarker.localEulerAngles = Vector3.zero;
                playerMarker.gameObject.SetActive(true);
            }

            int used = Plot(_hostiles, yaw, null);
            for (int i = used; i < _hostiles.Count; i++) _hostiles[i].gameObject.SetActive(false);

            var targets = ActiveTargets();
            int pinned = Plot(_objectives, yaw, targets);
            for (int i = pinned; i < _objectives.Count; i++) _objectives[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Projects a source set into the panel. Returns how many dots were placed, so
        /// the caller can park the rest of the pool.
        /// </summary>
        private int Plot(List<Image> pool, float yaw, List<Vector3> points)
        {
            if (pool.Count == 0) return 0;

            if (points != null)
            {
                int n = 0;
                for (int i = 0; i < points.Count && n < pool.Count; i++)
                {
                    if (!Place(pool[n], yaw, points[i])) continue;
                    pool[n].color = new Color(1f, 0.78f, 0.28f, 0.95f);
                    n++;
                }
                return n;
            }

            int live = 0;
            var hostiles = EnemyBase.Hostiles;
            for (int i = 0; i < hostiles.Count && live < pool.Count; i++)
            {
                var enemy = hostiles[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if (!Place(pool[live], yaw, enemy.transform.position)) continue;

                // A nearly-dead hostile reads red so the player can pick the next target
                // without looking at the world.
                pool[live].color = enemy.HealthFraction < 0.34f
                    ? new Color(1f, 0.32f, 0.28f, 0.98f)
                    : new Color(1f, 0.46f, 0.34f, 0.9f);
                live++;
            }
            return live;
        }

        private bool Place(Image dot, float yaw, Vector3 world)
        {
            Vector3 offset = world - _player.position;
            offset = Quaternion.Euler(0f, yaw, 0f) * offset;

            Vector2 normalized = new Vector2(offset.x, offset.z) / Mathf.Max(1f, range);
            float length = normalized.magnitude;
            if (length > 1f)
            {
                dot.gameObject.SetActive(false);
                return false;
            }

            Vector2 size = _panel.rect.size * 0.5f * edgeInset;
            var rect = dot.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(normalized.x * size.x, normalized.y * size.y);
            dot.gameObject.SetActive(true);
            return true;
        }

        /// <summary>
        /// World positions the active contract points at. Only ids that exist as
        /// interactables in the scene are pinned - a radar that invents markers is worse
        /// than one that shows none.
        /// </summary>
        private readonly List<Vector3> _targetPoints = new();

        private List<Vector3> ActiveTargets()
        {
            var result = _targetPoints;
            result.Clear();
            var mission = MissionManager.Instance != null ? MissionManager.Instance.ActiveMission : null;
            if (mission == null || mission.objectives == null) return result;

            var markers = InteractableObject.All;
            for (int i = 0; i < mission.objectives.Count; i++)
            {
                string targetId = mission.objectives[i].targetId;
                if (string.IsNullOrEmpty(targetId)) continue;

                for (int m = 0; m < markers.Count; m++)
                {
                    var marker = markers[m];
                    if (marker == null || marker.MarkerId != targetId) continue;
                    result.Add(marker.transform.position);
                    break;
                }
            }
            return result;
        }
    }
}
