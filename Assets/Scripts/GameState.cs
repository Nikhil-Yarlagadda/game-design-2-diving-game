using System.Collections.Generic;
using UnityEngine;

public static class GameState
{
    public struct MiningSpotState
    {
        public bool IsProspected;
        public int DisplayedTier, ActualTier, TierOutput;
    }
    public static Dictionary<string, MiningSpotState> miningSpots;
    public const string MiningMachine = "Mining Machine";
    public static bool deliveryTutorialShown;
    public static int days;
    public static int credits;
    public static int tellurium;
    public static HashSet<string> npcsTalks;
    public static Dictionary<string, int> itemsBought;
    public static Dictionary<string, int> inventory;
    

    // Also initialize safely when a scene is opened directly in the editor.
    static GameState() => reset();
    public static ItemStack[] layout;
    public static Dictionary<string, int> excursionInventory;

    // Moves up to `amount` of an item between two inventories. Returns how many actually moved.
    public static int Move(Dictionary<string, int> from, Dictionary<string, int> to, string name, int amount)
    {
        if (!from.TryGetValue(name, out int have) || have <= 0) return 0;

        int moved = Mathf.Min(amount, have);
        if (have - moved <= 0) from.Remove(name);
        else from[name] = have - moved;

        to[name] = (to.TryGetValue(name, out int c) ? c : 0) + moved;
        return moved;
    }

    // Call whenever an excursion ends and the player keeps what they found
    public static void ReturnFromExcursion()
    {
        itemsBought = new Dictionary<string, int>();
        foreach (var pair in new List<KeyValuePair<string, int>>(excursionInventory))
            Move(excursionInventory, inventory, pair.Key, pair.Value);

        layout = new ItemStack[48]; // the grid arrangement belongs to the trip
    }

    public static void reset()
    {
        miningSpots = new Dictionary<string, MiningSpotState>();
        days = 1;
        credits = 0;
        deliveryTutorialShown = false;
        tellurium = 0;
        npcsTalks = new HashSet<string>();
        itemsBought = new Dictionary<string, int>();
        itemsBought["Mining Machine"] = 0;
        inventory = new Dictionary<string, int>();
        inventory["Mining Machine"] = 3;
        inventory["Light Hydrox Tank"] = 1;
        excursionInventory = new Dictionary<string, int>();
        layout = new ItemStack[48];
    }
}