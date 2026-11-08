using UnityEngine;

namespace NullFrontier.Missions
{
    /// <summary>
    /// Hands the player the region's opening mission on load, so the HUD tracker shows
    /// something to do instead of an empty panel. AcceptMission ignores the request if a
    /// mission is already active or the save says it was completed.
    /// </summary>
    public class OpeningMission : MonoBehaviour
    {
        [SerializeField] private string missionId = "missing_players";

        private void Start()
        {
            var manager = MissionManager.Instance;
            if (manager == null || manager.ActiveMission != null) return;
            manager.AcceptMission(missionId);
        }
    }
}
