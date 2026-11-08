using UnityEngine;

namespace NullFrontier.Missions
{
    /// <summary>
    /// World-space objective target. Its id must match a mission objective targetId;
    /// when the player enters the trigger, the serialized event type is notified.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MissionMarker : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private MissionManager.MissionEventType eventType = MissionManager.MissionEventType.Reach;
        [SerializeField] private bool notifyOnce = true;

        private bool _notified;

        public string Id => id;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_notified) return;
            if (other.GetComponentInParent<Player.PlayerCharacter>() == null) return;

            var manager = MissionManager.Instance;
            if (manager == null || string.IsNullOrEmpty(id)) return;

            manager.NotifyEvent(eventType, id);
            if (notifyOnce) _notified = true;
        }
    }
}
