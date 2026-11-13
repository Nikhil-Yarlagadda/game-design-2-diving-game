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
    public static bool deliveryTutorialShown;
    public static int days;
    public static int credits;
    public static int tellurium;
    public static HashSet<string> npcsTalks;
    public static Dictionary<string, int> itemsBought;
    public static Dictionary<string, int> inventory;
    public static Dictionary<string, int> dailyPurchases;
    public static int shopDay;
    public static bool medicineDelivered;
    public static DiveLoadout lastLoadout;
    public static bool excursionIncapacitated;
    public static int oreLost;
    

    // Also initialize safely when a scene is opened directly in the editor.
    static GameState() => reset();

    public static void reset()
    {
        miningSpots = new Dictionary<string, MiningSpotState>();
        days = 1;
        credits = 0;
        deliveryTutorialShown = false;
        tellurium = 0;
        npcsTalks = new HashSet<string>();
        itemsBought = new Dictionary<string, int>();
        itemsBought[MiningMachine] = 0;
        inventory = new Dictionary<string, int>();
        inventory[MiningMachine] = 4;
        inventory[ItemCatalog.LightTank] = 1;
        dailyPurchases = new Dictionary<string, int>();
        shopDay = 0;
        medicineDelivered = false;
        lastLoadout = new DiveLoadout { Tank = ItemCatalog.LightTank, Machines = 4 };
        excursionIncapacitated = false;
        oreLost = 0;
    }
}
