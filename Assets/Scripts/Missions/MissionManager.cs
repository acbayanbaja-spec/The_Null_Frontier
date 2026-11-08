using System;
using System.Collections.Generic;
using NullFrontier.Core;
using NullFrontier.Items;
using NullFrontier.Progression;
using UnityEngine;

namespace NullFrontier.Missions
{
    /// <summary>
    /// Tracks one active mission at a time. Gameplay systems call NotifyEvent with a
    /// MissionEventType + targetId; matching objectives advance, completion pays
    /// rewards and records the mission in the save.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        public enum MissionEventType
        {
            Kill,
            Collect,
            Reach,
            Interact,
            Escape,
            Defend
        }

        public event Action OnMissionUpdated;
        public event Action<MissionDefinition> OnMissionCompleted;

        public static event Action<MissionEventType, string, int> OnMissionEvent;

        public MissionDefinition ActiveMission { get; private set; }

        private int[] _progress = Array.Empty<int>();

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

        public void NotifyEvent(MissionEventType type, string targetId, int count = 1)
        {
            OnMissionEvent?.Invoke(type, targetId, count);

            var mission = ActiveMission;
            if (mission == null || string.IsNullOrEmpty(targetId)) return;

            bool changed = false;
            for (int i = 0; i < mission.objectives.Count; i++)
            {
                var obj = mission.objectives[i];
                if (obj.isOptional) continue;
                if (obj.type != type || obj.targetId != targetId) continue;
                if (_progress[i] >= obj.requiredCount) continue;

                _progress[i] = Mathf.Min(obj.requiredCount, _progress[i] + count);
                changed = true;
            }

            if (!changed) return;

            if (IsComplete(mission))
            {
                CompleteMission(mission);
            }
            else
            {
                OnMissionUpdated?.Invoke();
            }
        }

        public bool AcceptMission(string id)
        {
            var def = MissionDatabase.Get(id);
            if (def == null || ActiveMission != null) return false;

            var data = SaveSystem.Instance?.Data;
            if (data?.completedMissions != null && data.completedMissions.Contains(id)) return false;

            ActiveMission = def;
            _progress = new int[def.objectives.Count];
            OnMissionUpdated?.Invoke();
            return true;
        }

        public void AbandonMission()
        {
            if (ActiveMission == null) return;
            ActiveMission = null;
            _progress = Array.Empty<int>();
            OnMissionUpdated?.Invoke();
        }

        public List<MissionDefinition> AvailableMissions(string regionId)
        {
            var result = new List<MissionDefinition>();
            var data = SaveSystem.Instance?.Data;
            for (int i = 0; i < MissionDatabase.All.Count; i++)
            {
                var m = MissionDatabase.All[i];
                if (!string.IsNullOrEmpty(regionId) && m.regionId != regionId) continue;
                if (data?.completedMissions != null && data.completedMissions.Contains(m.id)) continue;
                if (ActiveMission != null && ActiveMission.id == m.id) continue;
                result.Add(m);
            }
            return result;
        }

        public int GetProgress(int objectiveIndex)
        {
            if (_progress == null || objectiveIndex < 0 || objectiveIndex >= _progress.Length) return 0;
            return _progress[objectiveIndex];
        }

        public bool IsObjectiveComplete(int objectiveIndex)
        {
            var mission = ActiveMission;
            if (mission == null || objectiveIndex < 0 || objectiveIndex >= mission.objectives.Count) return false;
            var obj = mission.objectives[objectiveIndex];
            return obj.isOptional || GetProgress(objectiveIndex) >= obj.requiredCount;
        }

        private static bool IsComplete(MissionDefinition mission)
        {
            for (int i = 0; i < mission.objectives.Count; i++)
            {
                var obj = mission.objectives[i];
                if (obj.isOptional) continue;
                var progress = Instance != null ? Instance._progress : null;
                if (progress == null || i >= progress.Length || progress[i] < obj.requiredCount) return false;
            }
            return true;
        }

        private void CompleteMission(MissionDefinition mission)
        {
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                data.completedMissions ??= new List<string>();
                if (!data.completedMissions.Contains(mission.id)) data.completedMissions.Add(mission.id);
            }

            var progression = ProgressionSystem.Instance;
            if (progression != null && mission.xpReward > 0)
            {
                progression.AddXp(mission.xpReward);
            }

            var inventory = InventorySystem.Instance;
            if (inventory != null)
            {
                foreach (var itemId in mission.itemIds)
                {
                    inventory.AddItem(itemId, 1);
                }
            }

            ActiveMission = null;
            _progress = Array.Empty<int>();

            OnMissionCompleted?.Invoke(mission);
            GameEvents.RaiseSaveRequested();
        }
    }
}
