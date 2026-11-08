using System.Collections.Generic;

namespace NullFrontier.Missions
{
    /// <summary>
    /// Plain C# mission definition with objectives matched against
    /// MissionManager.MissionEventType notifications. Seeded in code, no assets.
    /// </summary>
    public class MissionDefinition
    {
        public class Objective
        {
            public MissionManager.MissionEventType type;
            public string targetId;
            public int requiredCount;
            public bool isOptional;

            public Objective(MissionManager.MissionEventType type, string targetId,
                int requiredCount = 1, bool isOptional = false)
            {
                this.type = type;
                this.targetId = targetId;
                this.requiredCount = requiredCount;
                this.isOptional = isOptional;
            }
        }

        public string id;
        public string title;
        public string description;
        public string regionId;
        public List<Objective> objectives = new();
        public int xpReward;
        public List<string> itemIds = new();

        public MissionDefinition(string id, string title, string description, string regionId,
            int xpReward, IEnumerable<Objective> objectives, IEnumerable<string> itemIds = null)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.regionId = regionId;
            this.xpReward = xpReward;
            this.objectives.AddRange(objectives);
            if (itemIds != null) this.itemIds.AddRange(itemIds);
        }
    }

    public static class MissionDatabase
    {
        public static readonly IReadOnlyList<MissionDefinition> All = new List<MissionDefinition>
        {
            new("enter_vertical_city", "Enter the Vertical City", 
                "Reach the Vertical City spawn gate and establish a foothold.",
                "vertical_city", 100,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Reach, "vertical_city_spawn")
                }),
            new("missing_players", "Missing Players",
                "Investigate terminal 7 and deal with the digital soldiers guarding it.",
                "vertical_city", 250,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Interact, "terminal_7"),
                    new(MissionManager.MissionEventType.Kill, "digital_soldier", 3)
                },
                new[] { "med_gel" }),
            new("find_access_terminal", "Find the Access Terminal",
                "Locate the hidden terminal before security locks the sector down.",
                "vertical_city", 200,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Reach, "hidden_terminal")
                }),
            new("escape_shifting_maze", "Escape the Shifting Maze",
                "The maze rearranges itself. Find the exit before the timer runs out.",
                "shifting_maze", 400,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Escape, "maze_exit")
                },
                new[] { "shield_cell" }),
            new("defeat_security_commander", "Security Commander",
                "The sector's security commander stands between you and the core.",
                "vertical_city", 600,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Kill, "security_commander")
                },
                new[] { "nf_energy" }),
            new("recover_core_fragment", "Recover the Core Fragment",
                "A fragment of the Null Core has surfaced. Bring it back intact.",
                "vertical_city", 500,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Collect, "core_fragment")
                }),
            new("defend_the_relay", "Defend the Relay",
                "Stand in the relay pylon and cycle it three times while the district fights back.",
                "vertical_city", 350,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Defend, "relay_1", 3)
                },
                new[] { "stim", "med_gel" }),
            new("cache_hunter", "Cache Hunter",
                "Recover encrypted data caches scattered through the district.",
                "vertical_city", 300,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Collect, "data_cache", 5)
                },
                new[] { "acc_targeter" }),
            new("deep_scavenge", "Deep Scavenge",
                "Stockpile alloy scrap for the workshop.",
                "shifting_maze", 150,
                new List<MissionDefinition.Objective>
                {
                    new(MissionManager.MissionEventType.Collect, "scrap_alloy", 10)
                })
        };

        public static MissionDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].id == id) return All[i];
            }
            return null;
        }
    }
}
