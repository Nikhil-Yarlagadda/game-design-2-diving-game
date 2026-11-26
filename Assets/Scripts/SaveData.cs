using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Serializable snapshot of a single save slot.</summary>
[Serializable]
public sealed class SaveData
{
    public const int CurrentSchemaVersion = 1;

    [Serializable]
    public struct ItemCountEntry
    {
        public string id;
        public int count;
    }

    [Serializable]
    public struct MiningSpotEntry
    {
        public string id;
        public GameState.MiningSpotState state;
    }

    public int schemaVersion = CurrentSchemaVersion;
    public int slotIndex;
    public string savedAtUtc;
    public string targetScene;
    public bool isPendingResult;
    public bool isCompleted;

    public int days;
    public int credits;
    public bool deliveryTutorialShown;
    public int shopDay;
    public bool medicineDelivered;
    public GameState.DiveLoadout lastLoadout;

    public bool checkTerminal;
    public bool draftMessage;
    public bool fgfsSpread;
    public bool adminUnlocked;
    public bool infectionPhoto;
    public bool abyssPhoto;
    public bool passkeyHintSeen;
    public int evidenceRequestDay;
    public int dayOfKirstenCured;
    public int dayOfColeCured;
    public int dayFgfsCollected;

    // Only populated when isPendingResult is true (e.g., regular failure anti-SL or pending day results).
    public int tellurium;
    public int oreLost;
    public bool excursionIncapacitated;
    public bool interiorDayResults;

    public List<string> npcsTalks = new();
    public List<ItemCountEntry> itemsBought = new();
    public List<ItemCountEntry> inventory = new();
    public List<ItemCountEntry> dailyPurchases = new();
    public List<ItemCountEntry> pendingGearReminder = new();
    public List<MiningSpotEntry> miningSpots = new();

    [SerializeField] internal PlaytestReport playtest;

    /// <summary>Captures a clean snapshot from live GameState.</summary>
    public static SaveData CaptureFromGameState(int slot, string scene, bool pendingResult, bool completed = false)
    {
        var data = new SaveData
        {
            schemaVersion = CurrentSchemaVersion,
            slotIndex = slot,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            targetScene = scene,
            isPendingResult = pendingResult,
            isCompleted = completed,
            days = GameState.days,
            credits = GameState.credits,
            deliveryTutorialShown = GameState.deliveryTutorialShown,
            shopDay = GameState.shopDay,
            medicineDelivered = GameState.medicineDelivered,
            lastLoadout = GameState.lastLoadout,
            checkTerminal = GameState.checkTerminal,
            draftMessage = GameState.draftMessage,
            fgfsSpread = GameState.fgfsSpread,
            adminUnlocked = GameState.adminUnlocked,
            infectionPhoto = GameState.infectionPhoto,
            abyssPhoto = GameState.abyssPhoto,
            passkeyHintSeen = GameState.passkeyHintSeen,
            evidenceRequestDay = GameState.evidenceRequestDay,
            dayOfKirstenCured = GameState.dayOfKirstenCured,
            dayOfColeCured = GameState.dayOfColeCured,
            dayFgfsCollected = GameState.dayFgfsCollected,
            tellurium = pendingResult ? GameState.tellurium : 0,
            oreLost = pendingResult ? GameState.oreLost : 0,
            excursionIncapacitated = pendingResult && GameState.excursionIncapacitated,
            interiorDayResults = pendingResult && GameState.interiorDayResults,
            npcsTalks = pendingResult ? new List<string>(GameState.npcsTalks) : new List<string>(),
            itemsBought = ToList(GameState.itemsBought),
            inventory = ToList(GameState.inventory),
            dailyPurchases = ToList(GameState.dailyPurchases),
            pendingGearReminder = ToList(GameState.pendingGearReminder),
            miningSpots = ToMiningList(GameState.miningSpots),
            playtest = ClonePlaytest(GameState.Playtest)
        };
        return data;
    }

    /// <summary>Applies this snapshot onto GameState after resetting transient runtime fields.</summary>
    public void ApplyToGameState()
    {
        GameState.reset();
        GameState.days = Mathf.Clamp(days, 1, GameState.LastDay + 1);
        GameState.credits = credits;
        GameState.deliveryTutorialShown = deliveryTutorialShown;
        GameState.shopDay = shopDay;
        GameState.medicineDelivered = medicineDelivered;
        GameState.lastLoadout = lastLoadout;

        GameState.checkTerminal = checkTerminal;
        GameState.draftMessage = draftMessage;
        GameState.fgfsSpread = fgfsSpread;
        GameState.adminUnlocked = adminUnlocked;
        GameState.infectionPhoto = infectionPhoto;
        GameState.abyssPhoto = abyssPhoto;
        GameState.passkeyHintSeen = passkeyHintSeen;
        GameState.evidenceRequestDay = evidenceRequestDay;
        GameState.dayOfKirstenCured = dayOfKirstenCured;
        GameState.dayOfColeCured = dayOfColeCured;
        GameState.dayFgfsCollected = dayFgfsCollected;

        GameState.tellurium = isPendingResult ? tellurium : 0;
        GameState.oreLost = isPendingResult ? oreLost : 0;
        GameState.excursionIncapacitated = isPendingResult && excursionIncapacitated;
        GameState.interiorDayResults = isPendingResult && interiorDayResults;

        GameState.npcsTalks = npcsTalks != null ? new HashSet<string>(npcsTalks) : new HashSet<string>();
        GameState.itemsBought = FromList(itemsBought);
        GameState.inventory = FromList(inventory);
        GameState.dailyPurchases = FromList(dailyPurchases);
        GameState.pendingGearReminder = FromList(pendingGearReminder);
        GameState.miningSpots = FromMiningList(miningSpots);

        PlaytestReport restored = ClonePlaytest(playtest);
        if (restored == null || restored.days == null || restored.days.Count == 0)
        {
            restored = new PlaytestReport();
            restored.StartDay(GameState.days, GameState.credits);
        }
        GameState.RestorePlaytest(restored);
    }

    /// <summary>Validates deserialized fields before loading or displaying a save slot.</summary>
    public bool IsValid()
    {
        if (schemaVersion != CurrentSchemaVersion) return false;
        if (days < 1 || days > GameState.LastDay) return false;
        if (inventory == null) return false;
        bool knownScene =
            targetScene == SaveManager.InteriorScene ||
            targetScene == SaveManager.OceanScene ||
            targetScene == SaveManager.ResultScene ||
            targetScene == SaveManager.EndingScene;
        if (!knownScene) return false;
        if (isPendingResult != (targetScene == SaveManager.ResultScene)) return false;
        if (isCompleted != (targetScene == SaveManager.EndingScene)) return false;
        return true;
    }

    private static PlaytestReport ClonePlaytest(PlaytestReport source)
    {
        if (source == null) return null;
        string json = JsonUtility.ToJson(source);
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PlaytestReport>(json);
    }

    private static List<ItemCountEntry> ToList(Dictionary<string, int> dict)
    {
        var list = new List<ItemCountEntry>();
        if (dict == null) return list;
        foreach (var pair in dict)
        {
            if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0)
                list.Add(new ItemCountEntry { id = pair.Key, count = pair.Value });
        }
        return list;
    }

    private static Dictionary<string, int> FromList(List<ItemCountEntry> list)
    {
        var dict = new Dictionary<string, int>();
        if (list == null) return dict;
        foreach (var entry in list)
        {
            if (!string.IsNullOrEmpty(entry.id) && entry.count > 0)
                dict[entry.id] = entry.count;
        }
        return dict;
    }

    private static List<MiningSpotEntry> ToMiningList(Dictionary<string, GameState.MiningSpotState> dict)
    {
        var list = new List<MiningSpotEntry>();
        if (dict == null) return list;
        foreach (var pair in dict)
        {
            if (!string.IsNullOrEmpty(pair.Key))
                list.Add(new MiningSpotEntry { id = pair.Key, state = pair.Value });
        }
        return list;
    }

    private static Dictionary<string, GameState.MiningSpotState> FromMiningList(List<MiningSpotEntry> list)
    {
        var dict = new Dictionary<string, GameState.MiningSpotState>();
        if (list == null) return dict;
        foreach (var entry in list)
        {
            if (!string.IsNullOrEmpty(entry.id))
                dict[entry.id] = entry.state;
        }
        return dict;
    }
}

/// <summary>Lightweight summary shown on Load Game and Overwrite Slot cards.</summary>
public struct SaveSlotSummary
{
    public int SlotIndex;
    public bool HasSave;
    public bool IsCorrupted;
    public bool IsPendingResult;
    public bool IsCompleted;
    public int Days;
    public int Credits;
    public string TargetScene;
    public string SavedAtUtc;
}
