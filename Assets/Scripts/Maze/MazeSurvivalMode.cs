using NullFrontier.Core;
using NullFrontier.Missions;
using UnityEngine;

namespace NullFrontier.Maze
{
    /// <summary>
    /// MazeSurvival entry point: sets the game mode, seeds the maze run, scores
    /// (time remaining + kills), and submits to the leaderboard when signed in.
    /// </summary>
    public class MazeSurvivalMode : MonoBehaviour
    {
        [SerializeField] private int seed = 12345;
        [SerializeField] private int pointsPerKill = 25;

        private int _kills;
        private bool _ended;

        private void OnEnable()
        {
            GameManager.Instance?.SetGameMode(GameMode.MazeSurvival);

            if (MazeManager.Instance != null)
            {
                MazeManager.Instance.OnMazeEscaped += HandleRunEnded;
                MazeManager.Instance.OnMazeTimedOut += HandleRunEnded;
            }
            GameEvents.PlayerDied += HandleRunEnded;
            MissionManager.OnMissionEvent += HandleMissionEvent;
        }

        private void OnDisable()
        {
            if (MazeManager.Instance != null)
            {
                MazeManager.Instance.OnMazeEscaped -= HandleRunEnded;
                MazeManager.Instance.OnMazeTimedOut -= HandleRunEnded;
            }
            GameEvents.PlayerDied -= HandleRunEnded;
            MissionManager.OnMissionEvent -= HandleMissionEvent;
        }

        private void Start()
        {
            if (MazeManager.Instance != null)
            {
                MazeManager.Instance.Begin();
            }
        }

        private void HandleMissionEvent(MissionManager.MissionEventType type, string targetId, int count)
        {
            if (_ended) return;
            if (type == MissionManager.MissionEventType.Kill) _kills += count;
        }

        private void HandleRunEnded()
        {
            if (_ended) return;
            _ended = true;

            float timeRemaining = MazeManager.Instance != null ? MazeManager.Instance.TimeRemaining : 0f;
            long score = (long)(timeRemaining * 10f) + (long)_kills * pointsPerKill;

            BackendClient.Instance?.SubmitLeaderboardScore("maze_survival", score, null);
        }
    }
}
