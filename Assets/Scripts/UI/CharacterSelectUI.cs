using System.Collections.Generic;
using NullFrontier.Characters;
using NullFrontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NullFrontier.UI
{
    /// <summary>
    /// Scrollable roster built from CharacterRegistry: each row shows the rendered
    /// portrait of that operative's actual model, and the live 3D body on the menu
    /// turntable is the preview. Locked characters stay visible but unselectable.
    /// Selection writes to GameManager and the save file, then loads the game scene.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private Button cardPrefab;
        [SerializeField] private UnityEngine.UI.Text detailName;
        [SerializeField] private UnityEngine.UI.Text detailRole;
        [SerializeField] private UnityEngine.UI.Text detailDescription;
        [SerializeField] private UnityEngine.UI.Text detailStats;
        [SerializeField] private Slider vitalityBar;
        [SerializeField] private Slider mobilityBar;
        [SerializeField] private Slider firepowerBar;
        [SerializeField] private UnityEngine.UI.Text vitalityValue;
        [SerializeField] private UnityEngine.UI.Text mobilityValue;
        [SerializeField] private UnityEngine.UI.Text firepowerValue;
        [SerializeField] private UnityEngine.UI.Text caption;
        [SerializeField] private Button confirmButton;

        [Header("Roster preview")]
        [SerializeField] private CharacterVisualMount showcase;
        [SerializeField] private string portraitFolder = "Portraits";

        [Header("Scene")]
        [SerializeField] private string gameSceneName = "VerticalCity";

        private class Row
        {
            public Button Card;
            public Image Plate;
            public Image Rim;
            public Transform Halo;
            public GameObject Tab;
            public Image Portrait;
            public UnityEngine.UI.Text Name;
            public UnityEngine.UI.Text Role;
            public UnityEngine.UI.Text Status;
            public UnityEngine.UI.Text Lock;
        }

        private readonly List<Row> _rows = new();
        private CharacterDefinition _selected;

        // Mirrors the palette UiFactory painted the tile with; the runtime cannot read
        // the editor class, so the three plate states are restated here.
        private static readonly Color RestPlate = new Color(0.09f, 0.11f, 0.16f, 0.95f);
        private static readonly Color LitPlate = new Color(0.17f, 0.21f, 0.30f, 0.97f);
        private static readonly Color DisabledPlate = new Color(0.06f, 0.07f, 0.10f, 0.92f);

        private void Start()
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = false;
                confirmButton.onClick.AddListener(ConfirmSelection);
            }
            showcase?.Mount(GameManager.Instance?.SelectedCharacterId);
            BuildCards();
        }

        private void BuildCards()
        {
            if (cardContainer == null || cardPrefab == null) return;

            var unlocked = SaveSystem.Instance?.Data.unlockedCharacters;
            foreach (var def in CharacterRegistry.All)
            {
                bool isUnlocked = unlocked == null || unlocked.Contains(def.Id);

                var card = Instantiate(cardPrefab, cardContainer);
                var row = new Row
                {
                    Card = card,
                    Plate = card.targetGraphic as Image,
                    Rim = Find<Image>(card.transform, "Rim"),
                    Halo = Find<Transform>(card.transform, "Halo"),
                    Tab = card.transform.Find("Tab") != null
                        ? card.transform.Find("Tab").gameObject
                        : null,
                    Portrait = Find<Image>(card.transform, "Portrait"),
                    Name = Find<UnityEngine.UI.Text>(card.transform, "Label"),
                    Role = Find<UnityEngine.UI.Text>(card.transform, "Role"),
                    Status = Find<UnityEngine.UI.Text>(card.transform, "Status"),
                    Lock = Find<UnityEngine.UI.Text>(card.transform, "Lock"),
                };

                if (row.Name != null) row.Name.text = def.DisplayName;
                if (row.Role != null) row.Role.text = def.Role.ToUpperInvariant();
                if (row.Status != null) row.Status.text = Handle(def.Id);
                if (row.Portrait != null)
                {
                    var sprite = LoadPortrait(def);
                    row.Portrait.sprite = sprite;
                    // An unrendered portrait is a null sprite: left white it paints a
                    // glaring empty box, so it falls back to the cell's own shade.
                    row.Portrait.color = sprite == null
                        ? new Color(0.22f, 0.26f, 0.34f, 1f)
                        : isUnlocked
                            ? new Color(1f, 1f, 1f, 0.94f)
                            : new Color(0.32f, 0.36f, 0.44f, 0.9f);
                }
                if (row.Lock != null) row.Lock.enabled = !isUnlocked;
                if (row.Plate != null && !isUnlocked) row.Plate.color = DisabledPlate;

                // Each operative owns one colour and the tile edge carries it, so the roster
                // reads as six identities instead of six copies of the same plate.
                if (row.Rim != null)
                    row.Rim.color = new Color(def.AccentColor.r, def.AccentColor.g,
                        def.AccentColor.b, isUnlocked ? 0.8f : 0.32f);

                card.interactable = isUnlocked;

                var captured = def;
                card.onClick.AddListener(() => Select(captured, row));
                _rows.Add(row);
            }
        }

        private void Select(CharacterDefinition def, Row row)
        {
            _selected = def;

            foreach (var other in _rows)
                Highlight(other, other == row);

            showcase?.Mount(def.Id);
            if (detailName != null) detailName.text = def.DisplayName;
            if (detailRole != null) detailRole.text = $"{def.Role.ToUpperInvariant()}  ·  {Handle(def.Id)}";
            if (detailDescription != null) detailDescription.text = def.Description;
            SetBars(def);
            if (detailStats != null)
                detailStats.text = Label(def.AbilityId);
            if (caption != null)
                caption.text = $"{def.DisplayName.ToUpperInvariant()}  ·  {def.Role.ToUpperInvariant()}";
            if (confirmButton != null) confirmButton.interactable = true;
        }

        private void ConfirmSelection()
        {
            if (_selected == null) return;

            var game = GameManager.Instance;
            if (game != null) game.SelectedCharacterId = _selected.Id;

            var save = SaveSystem.Instance;
            if (save != null)
            {
                save.Data.selectedCharacterId = _selected.Id;
                save.SaveNow();
            }

            SceneLoader.Instance?.LoadSceneAsync(gameSceneName, additive: false);
        }

        // -------------------------------------------------------------- presentation

        /// <summary>
        /// Portraits are rendered by the editor (CharacterPortraits) straight from the
        /// prefab, so a missing file means the rebuild has not run yet - the row still
        /// works, it just falls back to the accent colour.
        /// </summary>
        private Sprite LoadPortrait(CharacterDefinition def)
        {
            if (string.IsNullOrEmpty(def.ModelPrefab)) return null;
            return Resources.Load<Sprite>($"{portraitFolder}/{def.ModelPrefab}");
        }

        private static void Highlight(Row row, bool selected)
        {
            if (row.Halo != null) row.Halo.gameObject.SetActive(selected);
            if (row.Tab != null) row.Tab.SetActive(selected);
            if (row.Plate == null || row.Plate.color == DisabledPlate) return;
            row.Plate.color = selected ? LitPlate : RestPlate;
        }

        // Meter lengths are relative to the roster, not to a made-up cap: 1.0 means
        // "highest of the six", which is what a comparison bar actually promises.
        private static float Norm(float value, float floor, float ceiling)
            => ceiling > floor ? Mathf.Clamp01((value - floor) / (ceiling - floor)) : 0.5f;

        private void SetBars(CharacterDefinition def)
        {
            float minHealth = float.MaxValue, maxHealth = float.MinValue;
            float minSpeed = float.MaxValue, maxSpeed = float.MinValue;
            float minPower = float.MaxValue, maxPower = float.MinValue;

            foreach (var other in CharacterRegistry.All)
            {
                minHealth = Mathf.Min(minHealth, other.MaxHealth);
                maxHealth = Mathf.Max(maxHealth, other.MaxHealth);
                minSpeed = Mathf.Min(minSpeed, other.MoveSpeed);
                maxSpeed = Mathf.Max(maxSpeed, other.MoveSpeed);
                minPower = Mathf.Min(minPower, other.AttackMultiplier);
                maxPower = Mathf.Max(maxPower, other.AttackMultiplier);
            }

            if (vitalityBar != null)
                vitalityBar.value = Norm(def.MaxHealth, minHealth, maxHealth);
            if (mobilityBar != null)
                mobilityBar.value = Norm(def.MoveSpeed, minSpeed, maxSpeed);
            if (firepowerBar != null)
                firepowerBar.value = Norm(def.AttackMultiplier, minPower, maxPower);

            // The meter shows the comparison; the readout shows the number behind it,
            // so nothing in the dossier is relative-only.
            if (vitalityValue != null)
                vitalityValue.text = $"{def.MaxHealth:0} HP  ·  {def.MaxShield:0} SH";
            if (mobilityValue != null)
                mobilityValue.text = $"{def.MoveSpeed:0.0} m/s";
            if (firepowerValue != null)
                firepowerValue.text = $"x{def.AttackMultiplier:0.00}  ·  x{def.DefenseMultiplier:0.00}";
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            var child = root.Find(name);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static string Handle(string id) => $"@{id}";

        /// <summary>"tactical_dash" reads as "Tactical Dash" in the dossier.</summary>
        private static string Label(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var text = new System.Text.StringBuilder(raw.Length + 4);
            bool atWordStart = true;
            foreach (char c in raw)
            {
                if (c == '_' || c == '-')
                {
                    text.Append(' ');
                    atWordStart = true;
                    continue;
                }
                text.Append(atWordStart ? char.ToUpperInvariant(c) : c);
                atWordStart = false;
            }
            return text.ToString();
        }
    }
}
