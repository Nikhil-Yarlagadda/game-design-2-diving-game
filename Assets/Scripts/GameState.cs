using System.Collections.Generic;
using UnityEngine;

public static class GameState
{
    public struct MiningSpotState
    {
        public bool IsProspected;
        public int DisplayedTier, ActualTier, TierOutput, WreckOre;
        public bool HasWreck;
    }
    public struct DiveLoadout
    {
        public string Tank;
        public int Machines, Stimulants;
        public bool Flippers, Camera;
    }

    public static Dictionary<string, MiningSpotState> miningSpots;
    public const string MiningMachine = ItemCatalog.Machine;
    public const int MinimumCredits = -100;
    public static bool deliveryTutorialShown;
    public static int days;
    public static int credits;
    internal static PlaytestReport Playtest { get; private set; }
    public static int tellurium;
    internal static bool interiorDayResults;
    public static HashSet<string> npcsTalks;
    private static readonly Dictionary<string, float> crewConversationReadyTimes = new();
    public static Dictionary<string, int> itemsBought;
    public static Dictionary<string, int> inventory;
    public static Dictionary<string, int> dailyPurchases;
    public static int shopDay;
    public static bool medicineDelivered;
    public static DiveLoadout lastLoadout;
    public static Dictionary<string, int> pendingGearReminder;
    public static bool excursionIncapacitated;
    public static int oreLost;
    

    public static bool checkTerminal;
    public static bool draftMessage;
    public static bool fgfsSpread;
    public static bool adminUnlocked;
    public static bool infectionPhoto;
    public static bool abyssPhoto;
    public static bool passkeyHintSeen;
    public static int evidenceRequestDay;
    public static int dayOfKirstenCured;
    public static int dayOfColeCured;
    public static int dayFgfsCollected;

    public const int LastDay = 20;

    /// <summary>Returns whether this crew member is still aboard on the current day.</summary>
    public static bool IsCrewAboard(string id) => id switch
    {
        "Mineral Processor" => days < 13,
        "Medic" => days < 17 || (dayOfKirstenCured >= 0 && dayOfKirstenCured < 17),
        "Apprentice Diver" => true,
        _ => false
    };

    /// <summary>Returns the room of a crew member currently available to talk, or null while they are away.</summary>
    internal static string GetAvailableCrewRoom(string id)
    {
        if (!IsCrewAboard(id)) return null;
        return id switch
        {
            "Apprentice Diver" when days < 15 && !HasSpokenToOtherCrew() => null,
            "Apprentice Diver" => days switch
            {
                15 or 19 => "Mineral Lab",
                17 => "Storage Closet",
                _ => "Moonpool"
            },
            "Medic" => days switch
            {
                5 or 13 or 17 => "Mineral Lab",
                3 or 7 or 11 or 19 => "Shared Space",
                _ => "Storage Closet"
            },
            "Mineral Processor" => days is 3 or 5 or 7 ? "Storage Closet" : "Mineral Lab",
            _ => null
        };
    }

    /// <summary>Returns whether the foreman has spoken with each crew member still aboard.</summary>
    public static bool HasSpokenToLivingCrew() =>
        HasSpokenToOtherCrew() && npcsTalks.Contains("Apprentice Diver");

    /// <summary>Returns whether Cole can return from his dive.</summary>
    public static bool HasSpokenToOtherCrew() =>
        (!IsCrewAboard("Mineral Processor") || npcsTalks.Contains("Mineral Processor")) &&
        (!IsCrewAboard("Medic") || npcsTalks.Contains("Medic"));

    /// <summary>Returns whether the character’s post-conversation cooldown has elapsed.</summary>
    public static bool CanTalkToCrew(string id) =>
        !crewConversationReadyTimes.TryGetValue(id, out float readyTime) || Time.unscaledTime >= readyTime;

    /// <summary>Prevents another conversation with this character for one second, across room changes.</summary>
    public static void StartCrewConversationCooldown(string id) =>
        crewConversationReadyTimes[id] = Time.unscaledTime + 1f;

    // Also initialize safely when a scene is opened directly in the editor.
    static GameState() => reset();

    public static void reset()
    {
        miningSpots = new Dictionary<string, MiningSpotState>();
        days = 1;
        credits = 0;
        Playtest = new PlaytestReport();
        Playtest.StartDay(days, credits);
        deliveryTutorialShown = false;
        tellurium = 0;
        interiorDayResults = false;
        npcsTalks = new HashSet<string>();
        crewConversationReadyTimes.Clear();
        itemsBought = new Dictionary<string, int>();
        itemsBought[MiningMachine] = 0;
        inventory = new Dictionary<string, int>();
        const int startingMachines = 3;
        inventory[MiningMachine] = startingMachines;
        inventory[ItemCatalog.LightTank] = 1;
        dailyPurchases = new Dictionary<string, int>();
        shopDay = 0;
        medicineDelivered = false;
        lastLoadout = new DiveLoadout { Tank = ItemCatalog.LightTank, Machines = startingMachines };
        pendingGearReminder = new Dictionary<string, int>();
        excursionIncapacitated = false;
        oreLost = 0;
        checkTerminal = false;
        draftMessage = false;
        fgfsSpread = false;
        adminUnlocked = false;
        infectionPhoto = false;
        abyssPhoto = false;
        passkeyHintSeen = false;
        evidenceRequestDay = -1;
        dayOfKirstenCured = -1;
        dayOfColeCured = -1;
        dayFgfsCollected = -1;
    }
}
