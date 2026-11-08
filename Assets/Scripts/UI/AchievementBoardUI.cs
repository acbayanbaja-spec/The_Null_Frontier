using System.Collections.Generic;
using NullFrontier.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Badge wall and supply outbox. One component serves both pages: the wall lists every
    /// badge with its live progress, the outbox lists only what has been earned and not yet
    /// taken. Rows are rebuilt on refresh rather than cached, because a claim removes the
    /// row it belongs to.
    /// </summary>
    public class AchievementBoardUI : MonoBehaviour
    {
        public enum Mode { Badges, Rewards }

        [SerializeField] private Mode mode = Mode.Badges;

        [Header("List")]
        [SerializeField] private Transform rowContainer;
        [SerializeField] private Button rowPrefab;

        [Header("Summary")]
        [SerializeField] private UnityEngine.UI.Text summary;
        [SerializeField] private UnityEngine.UI.Text emptyLabel;

        private readonly List<GameObject> _rows = new();

        private void OnEnable()
        {
            var system = AchievementSystem.Instance;
            if (system != null) system.OnAchievementsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            var system = AchievementSystem.Instance;
            if (system != null) system.OnAchievementsChanged -= Refresh;
        }

        private void Refresh()
        {
            for (int i = _rows.Count - 1; i >= 0; i--)
                if (_rows[i] != null) Destroy(_rows[i].gameObject);
            _rows.Clear();

            var system = AchievementSystem.Instance;
            if (system == null || rowContainer == null || rowPrefab == null)
            {
                SetEmpty("Progress is not loaded yet.");
                return;
            }

            int shown = 0;
            foreach (var row in system.Snapshot())
            {
                if (mode == Mode.Rewards && !row.Claimable) continue;
                AddRow(row);
                shown++;
            }

            if (summary != null)
                summary.text = mode == Mode.Badges
                    ? $"{system.UnlockedCount} of {system.TotalCount} badges earned"
                    : $"{system.ClaimableCount} drop(s) waiting to be taken";

            SetEmpty(mode == Mode.Badges
                ? null
                : shown == 0
                    ? "Nothing to claim. Earn a badge on the wall and its supply drop lands here."
                    : null);
        }

        private void AddRow(AchievementSystem.Row row)
        {
            var instance = Instantiate(rowPrefab, rowContainer);
            var go = instance.gameObject;
            _rows.Add(go);

            var def = row.Definition;
            int progress = Mathf.Min(row.Progress, def.target);

            SetText(go, "Title", def.title);
            SetText(go, "Blurb", def.blurb);
            SetText(go, "Reward", row.Reward);
            SetText(go, "State", StateText(row));

            var bar = FindChild<Slider>(go.transform, "Bar");
            if (bar != null)
            {
                bar.value = def.target > 0 ? Mathf.Clamp01(progress / (float)def.target) : 1f;
                bar.interactable = false;
            }

            var tint = FindChild<Image>(go.transform, "Rim");
            if (tint != null)
                tint.color = row.Unlocked
                    ? new Color(0.10f, 0.78f, 0.92f, 0.9f)
                    : new Color(1f, 1f, 1f, 0.16f);

            var claim = FindChild<Button>(go.transform, "Claim");
            if (claim == null) return;

            claim.gameObject.SetActive(mode == Mode.Rewards);
            if (mode != Mode.Rewards) return;

            var captured = def.id;
            claim.onClick.AddListener(() =>
            {
                // A refused claim must not redraw the row as taken: Refresh reads the
                // inventory-backed state, so an item that never landed keeps its row up.
                if (AchievementSystem.Instance != null && AchievementSystem.Instance.Claim(captured))
                    Debug.Log($"[AchievementBoard] Claimed {captured}.");
                Refresh();
            });
        }

        private static string StateText(AchievementSystem.Row row)
        {
            if (row.Unlocked) return row.Claimed ? "TAKEN" : "READY";
            return $"{row.Progress} / {row.Definition.target}";
        }

        private void SetEmpty(string message)
        {
            if (emptyLabel == null) return;
            emptyLabel.text = message ?? string.Empty;
            emptyLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private static void SetText(GameObject root, string child, string value)
        {
            var label = FindChild<UnityEngine.UI.Text>(root.transform, child);
            if (label != null) label.text = value;
        }

        private static T FindChild<T>(Transform root, string name) where T : Component
        {
            foreach (var candidate in root.GetComponentsInChildren<T>(true))
                if (candidate.name == name) return candidate;
            return null;
        }
    }
}
