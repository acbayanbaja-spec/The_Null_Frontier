using System;
using System.Collections.Generic;
using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Missions;
using NullFrontier.Player;
using NullFrontier.Progression;
using UnityEngine;

namespace NullFrontier.Maze
{
    /// <summary>
    /// Scene singleton owning the escape-maze flow: builds a seeded maze, runs the
    /// countdown, ping-pongs moving walls, and resolves escape (rewards + mission
    /// event) or timeout (teleport back to the entrance).
    /// </summary>
    public class MazeManager : MonoBehaviour
    {
        public static MazeManager Instance { get; private set; }

        public enum MazeDifficulty
        {
            Easy,
            Medium,
            Hard
        }

        [Serializable]
        public class MovingWall
        {
            public Transform wall;
            public Vector3 axis = Vector3.forward;
            public float distance = 4f;
            public float speed = 1f;

            private Vector3 _origin;
            private float _t;

            internal void Begin()
            {
                if (wall == null) return;
                _origin = wall.position;
                _t = 0f;
            }

            internal void Tick(float dt)
            {
                if (wall == null || distance <= 0f || speed <= 0f) return;
                _t += dt * speed;
                float offset = Mathf.PingPong(_t, distance);
                wall.position = _origin + axis.normalized * offset;
            }
        }

        [SerializeField] private MazeDifficulty difficulty = MazeDifficulty.Medium;
        [SerializeField] private int seed;
        [SerializeField] private float countdownSeconds = 120f;
        [SerializeField] private MazeBuilder builder;
        [SerializeField] private List<MovingWall> movingWalls = new();
        [SerializeField] private int escapeXp = 150;
        [SerializeField] private string escapeItemId;

        public event Action OnMazeEscaped;
        public event Action OnMazeTimedOut;

        public float TimeRemaining { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 EntranceWorldPosition => builder != null ? builder.EntranceWorldPosition : transform.position;
        public Vector3 ExitWorldPosition => builder != null ? builder.ExitWorldPosition : transform.position;

        private BoxCollider _exitTrigger;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Begin();
        }

        public void Begin()
        {
            if (builder == null) builder = GetComponentInChildren<MazeBuilder>();
            if (builder == null)
            {
                Debug.LogWarning("[MazeManager] No MazeBuilder assigned; maze disabled.");
                return;
            }

            (int w, int h) = DifficultyToSize(difficulty);
            builder.Build(w, h, new System.Random(seed));

            SetupExitTrigger();

            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.transform.position = EntranceWorldPosition;
                if (controller != null) controller.enabled = true;
                player.LastCheckpoint = EntranceWorldPosition;
            }

            foreach (var mw in movingWalls) mw?.Begin();

            TimeRemaining = countdownSeconds;
            IsRunning = true;
        }

        private void Update()
        {
            if (!IsRunning) return;

            foreach (var mw in movingWalls) mw?.Tick(Time.deltaTime);

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                HandleTimeout();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsRunning) return;
            if (other.GetComponentInParent<PlayerCharacter>() == null) return;

            IsRunning = false;
            MissionManager.Instance?.NotifyEvent(MissionManager.MissionEventType.Escape, "maze_exit");

            var progression = ProgressionSystem.Instance;
            if (progression != null && escapeXp > 0) progression.AddXp(escapeXp);

            var inventory = InventorySystem.Instance;
            if (inventory != null && !string.IsNullOrEmpty(escapeItemId))
            {
                inventory.AddItem(escapeItemId, 1);
            }

            OnMazeEscaped?.Invoke();
            GameEvents.RaiseSaveRequested();
        }

        private void HandleTimeout()
        {
            IsRunning = false;

            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.transform.position = EntranceWorldPosition;
                player.LastCheckpoint = EntranceWorldPosition;
                if (controller != null) controller.enabled = true;
            }

            OnMazeTimedOut?.Invoke();
        }

        public void Restart()
        {
            seed++;
            Begin();
        }

        private void SetupExitTrigger()
        {
            if (_exitTrigger == null)
            {
                _exitTrigger = gameObject.AddComponent<BoxCollider>();
                _exitTrigger.isTrigger = true;
            }
            _exitTrigger.center = transform.InverseTransformPoint(ExitWorldPosition) + Vector3.up;
            _exitTrigger.size = new Vector3(builder.CellSize * 1.5f, 3f, builder.CellSize * 1.5f);
        }

        private static (int, int) DifficultyToSize(MazeDifficulty d) => d switch
        {
            MazeDifficulty.Easy => (11, 11),
            MazeDifficulty.Medium => (21, 21),
            MazeDifficulty.Hard => (31, 31),
            _ => (21, 21)
        };
    }
}
