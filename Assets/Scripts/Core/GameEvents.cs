using System;
using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// Game-wide event hub. Static events decouple systems (combat, missions, UI, saves).
    /// </summary>
    public static class GameEvents
    {
        public static event Action PlayerDied;
        public static event Action PlayerRespawned;
        public static event Action SaveRequested;
        /// <summary>Raised after SaveSystem.Data has been replaced by loaded data.</summary>
        public static event Action SaveLoaded;
        public static event Action<GameMode> GameModeChanged;

        /// <summary>Raised with the player position when the use key/button is pressed.</summary>
        public static event Action<Vector3> PlayerInteracted;

        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        public static void RaiseSaveRequested() => SaveRequested?.Invoke();
        public static void RaiseSaveLoaded() => SaveLoaded?.Invoke();
        public static void RaiseGameModeChanged(GameMode mode) => GameModeChanged?.Invoke(mode);
        public static void RaisePlayerInteracted(Vector3 playerPosition) => PlayerInteracted?.Invoke(playerPosition);
    }

    public enum GameMode
    {
        None,
        Story,
        MazeSurvival,
        Exploration,
        Cooperative,
        Arena
    }
}
