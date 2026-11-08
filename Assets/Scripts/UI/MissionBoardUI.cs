using System.Collections.Generic;
using System.Text;
using NullFrontier.Items;
using NullFrontier.Missions;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Contract board for the district. The database holds every mission; the save plus
    /// the active slot decide which ones are still on the board. Cards are rebuilt on each
    /// refresh rather than cached, because accepting, completing or abandoning all change
    /// the set - and the progress numbers have to follow the live mission state.
    /// </summary>
    public class MissionBoardUI : MonoBehaviour
    {
        [SerializeField] private string regionId = "vertical_city";

        [Header("List")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private Button cardPrefab;

        [Header("Detail")]
        [SerializeField] private UnityEngine.UI.Text detailName;
        [SerializeField] private UnityEngine.UI.Text detailDescription;
        [SerializeField] private UnityEngine.UI.Text detailObjectives;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button abandonButton;

        private readonly List<GameObject> _cards = new();
        private MissionDefinition _selected;

        private void Awake()
        {
            if (acceptButton != null) acceptButton.onClick.AddListener(AcceptSelected);
            if (abandonButton != null) abandonButton.onClick.AddListener(AbandonActive);
        }

        private void OnEnable()
        {
            var manager = MissionManager.Instance;
            if (manager != null)
            {
                manager.OnMissionUpdated += Refresh;
                manager.OnMissionCompleted += HandleCompleted;
            }
            Refresh();
        }

        private void OnDisable()
        {
            var manager = MissionManager.Instance;
            if (manager != null)
            {
                manager.OnMissionUpdated -= Refresh;
                manager.OnMissionCompleted -= HandleCompleted;
            }
        }

        /// <summary>A finished contract clears the active slot, so the board reopens it for hire.</summary>
        private void HandleCompleted(MissionDefinition mission)
        {
            if (mission != null && _selected != null && _selected.id == mission.id) _selected = null;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = _cards.Count - 1; i >= 0; i--)
                if (_cards[i] != null) Destroy(_cards[i].gameObject);
            _cards.Clear();

            var manager = MissionManager.Instance;
            if (manager == null)
            {
                SetDetail(null);
                return;
            }

            if (cardContainer != null && cardPrefab != null)
            {
                // AvailableMissions leaves out whatever is already accepted, so the active
                // contract is added by hand: the board doubles as the mission tracker.
                var active = manager.ActiveMission;
                if (active != null && active.regionId == regionId) AddCard(active, true);
                foreach (var def in manager.AvailableMissions(regionId)) AddCard(def, false);
            }

            SetDetail(_selected != null ? _selected : manager.ActiveMission);
        }

        private void AddCard(MissionDefinition def, bool isActive)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            var label = card.GetComponentInChildren<UnityEngine.UI.Text>();
            if (label != null)
            {
                label.text = isActive
                    ? $"{def.title}\nACTIVE"
                    : $"{def.title}\n{def.objectives.Count} objective(s) · {def.xpReward} XP";
            }

            var captured = def;
            card.onClick.AddListener(() => SetDetail(captured));
            _cards.Add(card.gameObject);
        }

        private void SetDetail(MissionDefinition def)
        {
            _selected = def;
            var manager = MissionManager.Instance;
            bool isActive = def != null && manager != null && manager.ActiveMission != null
                && manager.ActiveMission.id == def.id;

            if (detailName != null)
                detailName.text = def == null ? "NO CONTRACT" : def.title;

            if (detailDescription != null)
                detailDescription.text = def == null
                    ? "Take a contract from the board."
                    : def.description;

            if (detailObjectives != null) detailObjectives.text = Describe(def, isActive);

            if (acceptButton != null)
            {
                acceptButton.interactable = def != null && !isActive
                    && (manager == null || manager.ActiveMission == null);
                var text = acceptButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (text != null) text.text = isActive ? "IN PROGRESS" : "ACCEPT";
            }

            if (abandonButton != null)
                abandonButton.interactable = manager != null && manager.ActiveMission != null;
        }

        /// <summary>Objective list with live progress when this contract is the active one.</summary>
        private static string Describe(MissionDefinition def, bool isActive)
        {
            if (def == null) return string.Empty;

            var builder = new StringBuilder();
            var manager = MissionManager.Instance;
            for (int i = 0; i < def.objectives.Count; i++)
            {
                var objective = def.objectives[i];
                string line = MissionText.Describe(objective);
                if (isActive && manager != null)
                    line = $"[ ] {line}  {manager.GetProgress(i)}/{objective.requiredCount}";
                else if (!objective.isOptional)
                    line = $"[ ] {line}";
                else
                    line = $"[o] {line}  (optional)";

                builder.AppendLine(line);
            }

            if (def.itemIds != null && def.itemIds.Count > 0)
            {
                builder.Append("Reward: ");
                for (int i = 0; i < def.itemIds.Count; i++)
                {
                    var item = ItemDatabase.Get(def.itemIds[i]);
                    builder.Append(item != null ? item.displayName : def.itemIds[i]);
                    if (i < def.itemIds.Count - 1) builder.Append(", ");
                }
            }
            return builder.ToString().TrimEnd();
        }

        private void AcceptSelected()
        {
            if (_selected == null) return;
            MissionManager.Instance?.AcceptMission(_selected.id);
            Refresh();
        }

        private void AbandonActive()
        {
            MissionManager.Instance?.AbandonMission();
            Refresh();
        }
    }
}
