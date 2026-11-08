using UnityEngine;

namespace NullFrontier.UI
{
    /// <summary>
    /// Keeps the menu frame alive: a slow camera dolly around the operative, rain
    /// falling through the shot and distant traffic overhead. Everything runs off one
    /// Update so the menu costs a handful of transform writes per frame - no particles,
    /// no extra cameras, no realtime reflections.
    /// </summary>
    public class MenuStageAmbience : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Transform lookAt;
        // The shot is composed on the gate, not on the operative's face: aiming at the
        // face pitched the camera down far enough that the arch, the planet and the sky
        // above them were all cropped. With the gaze on the gate the operative stands in
        // the lower third and the whole portal reads behind them.
        [SerializeField] private Transform gazePoint;
        [SerializeField] private Vector2 radiusRange = new Vector2(3.1f, 4.3f);
        [SerializeField] private Vector2 heightRange = new Vector2(0.08f, 0.52f);
        [SerializeField] private Vector2 sweepDegrees = new Vector2(-15f, 15f);
        [SerializeField] private float sweepSeconds = 26f;
        [SerializeField] private float baseFieldOfView = 42f;

        [Header("Rain")]
        [SerializeField] private Sprite streakSprite;
        [SerializeField] private int drops = 170;
        [SerializeField] private Vector3 rainCenter = new Vector3(0f, 9f, -6f);
        [SerializeField] private Vector3 volume = new Vector3(26f, 15f, 44f);
        [SerializeField] private Vector2 fallSpeed = new Vector2(12f, 19f);
        [SerializeField] private Color dropColor = new Color(0.62f, 0.82f, 1f, 0.30f);

        [Header("Traffic")]
        [SerializeField] private Transform[] vehicles;
        [SerializeField] private float laneHalfLength = 95f;

        private struct Lane
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float Offset;
        }

        private Camera _camera;
        private Lane[] _lanes;
        private Transform[] _dropShapes;
        private float[] _dropSpeeds;
        private float[] _dropSizes;

        private void Awake()
        {
            _camera = Camera.main;
            BuildRain();
            BuildLanes();
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            DriveCamera(t);
            DriveRain();
            DriveLanes(Time.deltaTime);
        }

        // The sweep is a sine, so the shot eases to a stop at each end instead of
        // snapping back around the plinth.
        private void DriveCamera(float t)
        {
            if (_camera == null) return;

            float phase = Mathf.Sin(2f * Mathf.PI * (t / Mathf.Max(4f, sweepSeconds)));
            float angle = Mathf.Lerp(sweepDegrees.x, sweepDegrees.y, 0.5f + 0.5f * phase);
            float height = Mathf.Lerp(heightRange.x, heightRange.y, 0.5f + 0.5f * phase);
            float radius = Mathf.Lerp(radiusRange.x, radiusRange.y,
                0.5f + 0.5f * Mathf.Sin(t * 0.11f));

            Vector3 focus = lookAt != null ? lookAt.position : new Vector3(0f, 1.05f, 0f);
            Vector3 aim = gazePoint != null ? gazePoint.position : focus;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height, radius);

            _camera.transform.position = focus + offset;
            _camera.transform.LookAt(aim + Vector3.up * (0.05f * Mathf.Sin(t * 0.6f)));
            _camera.fieldOfView = baseFieldOfView + 1.4f * Mathf.Sin(t * 0.19f);
        }

        private void BuildRain()
        {
            if (streakSprite == null || drops <= 0) return;

            // World space, not camera space: the shot moves, the weather does not.
            var root = new GameObject("Rain");
            root.transform.position = rainCenter;

            int count = Mathf.Clamp(drops, 1, 600);
            _dropShapes = new Transform[count];
            _dropSpeeds = new float[count];
            _dropSizes = new float[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Drop");
                go.transform.SetParent(root.transform, false);
                var sprite = go.AddComponent<SpriteRenderer>();
                sprite.sprite = streakSprite;
                sprite.color = dropColor;
                sprite.sortingOrder = 100;

                _dropShapes[i] = go.transform;
                _dropSpeeds[i] = Random.Range(fallSpeed.x, fallSpeed.y);
                _dropSizes[i] = Random.Range(0.7f, 1.5f);
                Respawn(_dropShapes[i], true);
            }
        }

        private void DriveRain()
        {
            if (_dropShapes == null) return;

            float dt = Time.deltaTime;
            float floor = -volume.y * 0.5f;

            for (int i = 0; i < _dropShapes.Length; i++)
            {
                var drop = _dropShapes[i];
                if (drop == null) continue;

                Vector3 p = drop.localPosition;
                p.y -= _dropSpeeds[i] * dt;
                drop.localPosition = p;
                if (p.y <= floor) Respawn(drop, false);
            }
        }

        private void Respawn(Transform drop, bool anywhereInVolume)
        {
            Vector3 p = new Vector3(
                Random.Range(-volume.x * 0.5f, volume.x * 0.5f),
                anywhereInVolume ? Random.Range(-volume.y * 0.5f, volume.y * 0.5f)
                                 : volume.y * 0.5f,
                Random.Range(-volume.z * 0.5f, volume.z * 0.5f));
            drop.localPosition = p;

            int index = drop.GetSiblingIndex();
            float size = _dropSizes[index];
            drop.localScale = new Vector3(size, size * Random.Range(0.8f, 1.6f), 1f);
        }

        private void BuildLanes()
        {
            if (vehicles == null || vehicles.Length == 0) return;

            _lanes = new Lane[vehicles.Length];
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] == null) continue;
                _lanes[i] = new Lane
                {
                    Origin = vehicles[i].localPosition,
                    Direction = vehicles[i].forward,
                    Speed = Random.Range(9f, 17f),
                    Offset = Random.Range(-laneHalfLength, laneHalfLength),
                };
            }
        }

        private void DriveLanes(float dt)
        {
            if (_lanes == null) return;

            for (int i = 0; i < _lanes.Length; i++)
            {
                var vehicle = vehicles[i];
                if (vehicle == null) continue;

                var lane = _lanes[i];
                lane.Offset += lane.Speed * dt;
                if (lane.Offset > laneHalfLength) lane.Offset = -laneHalfLength;

                vehicle.localPosition = lane.Origin + lane.Direction * lane.Offset;
                _lanes[i] = lane;
            }
        }
    }
}
