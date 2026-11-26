using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Storage backend abstraction for cross-platform saves (Desktop files, WebGL PlayerPrefs, and tests).</summary>
public interface ISaveStorage
{
    bool Exists(int slot);
    string Read(int slot);
    void Write(int slot, string json);
    void Delete(int slot);
}

/// <summary>Manages the 3 save slots, frozen day-start checkpoints, pending results, and loading.</summary>
public static class SaveManager
{
    public const int SlotCount = 3;
    public const string TitleScene = "Title Screen";
    public const string IntroductionScene = "Introduction";
    public const string InteriorScene = "Interior";
    public const string OceanScene = "Ocean Interactables";
    public const string ResultScene = "Result";
    public const string EndingScene = "Ending";

    private static ISaveStorage storage = CreateDefaultStorage();
    private static SaveData currentCheckpoint;

    /// <summary>Currently active save slot (1..3), or 0 when running a scene directly without a slot.</summary>
    public static int ActiveSlot { get; private set; }

    /// <summary>Whether the last persisted checkpoint for the active slot is a pending result screen.</summary>
    public static bool ActiveCheckpointIsPendingResult => currentCheckpoint?.isPendingResult == true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ActiveSlot = 0;
        currentCheckpoint = null;
        storage = CreateDefaultStorage();
    }

#if UNITY_EDITOR
    public static void SetStorageForTesting(ISaveStorage customStorage)
    {
        storage = customStorage ?? CreateDefaultStorage();
        ActiveSlot = 0;
        currentCheckpoint = null;
    }

    public static void ResetStorageToDefault()
    {
        storage = CreateDefaultStorage();
        ActiveSlot = 0;
        currentCheckpoint = null;
    }
#endif

    /// <summary>Returns metadata for all 3 slots (indices 1..3).</summary>
    public static SaveSlotSummary[] GetAllSummaries()
    {
        var summaries = new SaveSlotSummary[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            summaries[i] = GetSummary(i + 1);
        return summaries;
    }

    /// <summary>Returns metadata for a single 1-based save slot.</summary>
    public static SaveSlotSummary GetSummary(int slot)
    {
        var summary = new SaveSlotSummary { SlotIndex = slot };
        if (!IsValidSlot(slot)) return summary;
        if (!storage.Exists(slot)) return summary;

        if (TryReadSlot(slot, out SaveData data, out bool corrupted))
        {
            summary.HasSave = true;
            summary.Days = data.days;
            summary.Credits = data.credits;
            summary.TargetScene = data.targetScene;
            summary.SavedAtUtc = data.savedAtUtc;
            summary.IsPendingResult = data.isPendingResult;
            summary.IsCompleted = data.isCompleted;
        }
        else
        {
            summary.IsCorrupted = corrupted;
        }
        return summary;
    }

    /// <summary>Returns true if at least one slot contains a valid, loadable save.</summary>
    public static bool HasAnyValidSave()
    {
        for (int slot = 1; slot <= SlotCount; slot++)
            if (TryReadSlot(slot, out _, out _)) return true;
        return false;
    }

    /// <summary>Returns the first empty slot (1..3), or 0 if all slots are occupied.</summary>
    public static int FindFirstEmptySlot()
    {
        for (int slot = 1; slot <= SlotCount; slot++)
            if (!storage.Exists(slot)) return slot;
        return 0;
    }

    /// <summary>Starts a fresh run in the given slot (1..3) and writes the Day 1 day-start checkpoint.</summary>
    public static bool StartNewGameInSlot(int slot)
    {
        if (!IsValidSlot(slot)) return false;
        PauseManager.ResetState();
        GameState.reset();
        var fresh = SaveData.CaptureFromGameState(slot, InteriorScene, pendingResult: false);
        if (!WriteToSlot(slot, fresh)) return false;
        ActiveSlot = slot;
        currentCheckpoint = fresh;
        return true;
    }

    /// <summary>Captures and saves a clean start-of-day checkpoint for the active slot.</summary>
    public static bool CaptureAndSaveDayStart(string targetScene)
    {
        if (!IsValidSlot(ActiveSlot)) return false;
        currentCheckpoint = SaveData.CaptureFromGameState(ActiveSlot, targetScene, pendingResult: false);
        return WriteToSlot(ActiveSlot, currentCheckpoint);
    }

    /// <summary>Saves the settled outcome waiting on the Result screen (prevents failure SL and protects completed dives).</summary>
    public static bool SavePendingResult()
    {
        if (!IsValidSlot(ActiveSlot)) return false;
        currentCheckpoint = SaveData.CaptureFromGameState(ActiveSlot, ResultScene, pendingResult: true);
        return WriteToSlot(ActiveSlot, currentCheckpoint);
    }

    /// <summary>Saves the completed run state when reaching the Ending scene.</summary>
    public static bool SaveEnding()
    {
        if (!IsValidSlot(ActiveSlot)) return false;
        currentCheckpoint = SaveData.CaptureFromGameState(ActiveSlot, EndingScene, pendingResult: false, completed: true);
        return WriteToSlot(ActiveSlot, currentCheckpoint);
    }

    /// <summary>Ensures the frozen checkpoint (never dirty mid-day state) is persisted for the active slot.</summary>
    public static bool PersistActiveCheckpoint()
    {
        if (!IsValidSlot(ActiveSlot) || currentCheckpoint == null) return true;
        return WriteToSlot(ActiveSlot, currentCheckpoint);
    }

    /// <summary>Loads the specified slot into GameState and outputs the scene to open.</summary>
    public static bool LoadSlot(int slot, out string targetScene)
    {
        targetScene = null;
        if (!TryReadSlot(slot, out SaveData data, out _)) return false;

        PauseManager.ResetState();
        data.ApplyToGameState();
        ActiveSlot = slot;
        currentCheckpoint = data;
        targetScene = data.targetScene;
#if UNITY_EDITOR
        GameStateDebugger.ClearDayStart();
#endif
        return true;
    }

    /// <summary>Loads the specified slot and fades directly into its saved scene.</summary>
    public static bool LoadSlotAndTransition(int slot)
    {
        if (!TryReadSlot(slot, out SaveData data, out _)) return false;
        if (!Application.CanStreamedLevelBeLoaded(data.targetScene))
        {
            Debug.LogError($"Saved scene '{data.targetScene}' is not in Build Settings.");
            return false;
        }
        return SceneChanger.ChangeSceneWithFade(data.targetScene, () =>
        {
            PauseManager.ResetState();
            data.ApplyToGameState();
            ActiveSlot = slot;
            currentCheckpoint = data;
#if UNITY_EDITOR
            GameStateDebugger.ClearDayStart();
#endif
        });
    }

    /// <summary>Returns from gameplay or pause to the Title Screen without overwriting the clean checkpoint with mid-day state.</summary>
    public static bool ReturnToTitleMenu()
    {
        if (!PersistActiveCheckpoint()) return false;
        PauseManager.ResetState();
        Time.timeScale = 1f;
        return SceneChanger.ChangeSceneWithFade(TitleScene, () =>
        {
            PauseManager.ResetState();
            ActiveSlot = 0;
            currentCheckpoint = null;
        }, ignoreOverlays: true);
    }

    /// <summary>Reads and validates a slot without mutating active GameState.</summary>
    public static bool TryReadSlot(int slot, out SaveData data, out bool corrupted)
    {
        data = null;
        corrupted = false;
        if (!IsValidSlot(slot) || !storage.Exists(slot)) return false;

        try
        {
            string json = storage.Read(slot);
            if (string.IsNullOrWhiteSpace(json))
            {
                corrupted = true;
                return false;
            }
            data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || !data.IsValid() || (Application.isPlaying && !Application.CanStreamedLevelBeLoaded(data.targetScene)))
            {
                data = null;
                corrupted = true;
                return false;
            }
            data.slotIndex = slot;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read save slot {slot}: {ex.Message}");
            data = null;
            corrupted = true;
            return false;
        }
    }

    /// <summary>Returns true if the current active scene is an Excursion ocean scene.</summary>
    public static bool IsInExcursionScene() => SceneManager.GetActiveScene().name == OceanScene;

    /// <summary>Returns true if the current active scene is the Result settlement scene.</summary>
    public static bool IsInResultScene() => SceneManager.GetActiveScene().name == ResultScene;

    private static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;

    private static bool WriteToSlot(int slot, SaveData data)
    {
        try
        {
            data.slotIndex = slot;
            data.savedAtUtc = DateTime.UtcNow.ToString("O");
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            storage.Write(slot, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write save slot {slot}: {ex.Message}");
            return false;
        }
    }

    private static ISaveStorage CreateDefaultStorage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return new PlayerPrefsSaveStorage();
#else
        return new FileSaveStorage();
#endif
    }

    private sealed class FileSaveStorage : ISaveStorage
    {
        private static string DirectoryPath => Path.Combine(Application.persistentDataPath, "saves");
        private static string SlotPath(int slot) => Path.Combine(DirectoryPath, $"slot_{slot}.json");

        public bool Exists(int slot) => File.Exists(SlotPath(slot));

        public string Read(int slot) => File.ReadAllText(SlotPath(slot));

        public void Write(int slot, string json)
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = SlotPath(slot);
            string temp = path + ".tmp";
            string backup = path + ".bak";
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                File.Replace(temp, path, backup);
                try { if (File.Exists(backup)) File.Delete(backup); } catch { }
            }
            else
            {
                File.Move(temp, path);
            }
        }

        public void Delete(int slot)
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        private static string Key(int slot) => $"sunken_contract_save_slot_{slot}";

        public bool Exists(int slot) => PlayerPrefs.HasKey(Key(slot));
        public string Read(int slot) => PlayerPrefs.GetString(Key(slot), "");

        public void Write(int slot, string json)
        {
            PlayerPrefs.SetString(Key(slot), json);
            PlayerPrefs.Save();
        }

        public void Delete(int slot)
        {
            PlayerPrefs.DeleteKey(Key(slot));
            PlayerPrefs.Save();
        }
    }
}
