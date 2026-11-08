using System.Collections.Generic;

namespace NullFrontier.Missions
{
    /// <summary>
    /// Turns raw objective ids into the text a player reads. The database stores machine
    /// ids because that is what gameplay systems notify with; this is the display layer.
    /// </summary>
    public static class MissionText
    {
        private static readonly Dictionary<string, string> TargetNames = new()
        {
            { "vertical_city_spawn", "the spawn gate" },
            { "terminal_7", "Terminal 7" },
            { "hidden_terminal", "the access terminal" },
            { "maze_exit", "the maze exit" },
            { "relay_1", "the relay" },
            { "core_fragment", "the Core Fragment" },
            { "data_cache", "encrypted data caches" },
            { "scrap_alloy", "alloy scrap" },
            { "digital_soldier", "Digital Soldiers" },
            { "maze_stalker", "Maze Stalkers" },
            { "security_drone", "Security Drones" },
            { "security_commander", "the Security Commander" },
        };

        private static readonly Dictionary<MissionManager.MissionEventType, string> Verbs = new()
        {
            { MissionManager.MissionEventType.Reach, "Reach" },
            { MissionManager.MissionEventType.Interact, "Investigate" },
            { MissionManager.MissionEventType.Kill, "Eliminate" },
            { MissionManager.MissionEventType.Collect, "Collect" },
            { MissionManager.MissionEventType.Escape, "Escape to" },
            { MissionManager.MissionEventType.Defend, "Defend" },
        };

        public static string Target(string targetId) =>
            TargetNames.TryGetValue(targetId, out string name) ? name : targetId;

        public static string Describe(MissionDefinition.Objective objective)
        {
            string verb = Verbs.TryGetValue(objective.type, out string v) ? v : objective.type.ToString();
            string target = Target(objective.targetId);
            return objective.requiredCount > 1
                ? $"{verb} {target} x{objective.requiredCount}"
                : $"{verb} {target}";
        }
    }
}
