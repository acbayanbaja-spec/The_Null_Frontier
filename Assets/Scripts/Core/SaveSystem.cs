using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// JSON save system. Writes to Application.persistentDataPath, debounces saves,
    /// and forwards to the cloud backend when an auth token exists. Never overwrites
    /// a valid save with empty data — deserialization failures keep the last good file.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [Serializable]
        public class SaveData
        {
            public int version = 1;
            public string selectedCharacterId = "iidreuspade";
            public string currentRegionId = "vertical_city";
            public float playerX, playerY, playerZ;
            public int level = 1;
            public long xp;
            public int skillPoints;
            public List<string> unlockedCharacters = new() { "iidreuspade" };
            public List<string> unlockedRegions = new() { "vertical_city" };
            public List<string> completedMissions = new();
            public List<InventoryEntry> inventory = new();
            public List<EquippedEntry> equipped = new();
            public List<string> achievements = new();
            /// <summary>Lifetime counters the badges read from. Kept as pairs because
            /// JsonUtility cannot serialise a dictionary.</summary>
            public List<CounterEntry> counters = new();
            public List<string> claimedRewards = new();
            public long lastSavedUtcTicks;
            public SettingsData settings = new();
        }

        /// <summary>
        /// Player-adjustable options. A negative level or frame rate means "never
        /// chosen", so the first run keeps whatever the platform defaulted to.
        /// </summary>
        [Serializable]
        public class SettingsData
        {
            public int qualityLevel = -1;
            public int targetFrameRate = -1;
            public float renderScale = 1f;
            public float lookSensitivity = 1f;
            public float masterVolume = 0.75f;
            public float ambientVolume = 0.28f;
            public float effectsVolume = 1f;
        }

        [Serializable]
        public class InventoryEntry
        {
            public string itemId;
            public int quantity;
        }

        [Serializable]
        public class EquippedEntry
        {
            public string slot;
            public string itemId;
        }

        [Serializable]
        public class CounterEntry
        {
            public string key;
            public long value;
        }

        public SaveData Data { get; private set; } = new();
        public bool HasLocalSave => File.Exists(SavePath);

        /// <summary>
        /// Drops the loaded run and writes the empty save immediately, so a restarted
        /// app cannot reload the previous run's inventory/levels from disk.
        /// </summary>
        public void StartNewSave()
        {
            Data = new SaveData();
            SaveNow();
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, "nullfrontier_save.json");

        private const float SaveDebounceSeconds = 2f;
        private float _pendingSaveTimer = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            GameEvents.SaveRequested += QueueSave;
        }

        private void OnDisable()
        {
            GameEvents.SaveRequested -= QueueSave;
        }

        private void Update()
        {
            if (_pendingSaveTimer >= 0f)
            {
                _pendingSaveTimer -= Time.unscaledDeltaTime;
                if (_pendingSaveTimer < 0f) WriteSave();
            }
        }

        public void Load()
        {
            if (!HasLocalSave)
            {
                Data = new SaveData();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var loaded = JsonUtility.FromJson<SaveData>(json);
                // Guard: a truncated/corrupt file can deserialize to null or an empty shell.
                if (loaded == null || loaded.version <= 0)
                {
                    Debug.LogWarning("[SaveSystem] Save file invalid; keeping defaults.");
                    BackupCorruptSave();
                    Data = new SaveData();
                    return;
                }
                Data = loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Load failed: {e.Message}. Keeping defaults.");
                BackupCorruptSave();
                Data = new SaveData();
            }
        }

        public void QueueSave()
        {
            _pendingSaveTimer = SaveDebounceSeconds;
        }

        public void SaveNow()
        {
            _pendingSaveTimer = -1f;
            WriteSave();
        }

        private void WriteSave()
        {
            Data.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
            try
            {
                string tempPath = SavePath + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(Data, true));
                if (File.Exists(SavePath)) File.Copy(SavePath, SavePath + ".bak", true);
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tempPath, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
            }

            BackendClient.Instance?.UploadCloudSave(Data);
        }

        private void BackupCorruptSave()
        {
            try
            {
                if (File.Exists(SavePath))
                    File.Copy(SavePath, SavePath + ".corrupt", true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not back up corrupt save: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) SaveNow();
        }
    }
}
