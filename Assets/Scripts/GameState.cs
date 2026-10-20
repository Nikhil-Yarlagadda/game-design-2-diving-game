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
    

    // Also initialize safely when a scene is opened directly in the editor.
    static GameState() => reset();

    public static void reset()
    {
        miningSpots = new Dictionary<string, MiningSpotState>();
        days = 1;
        credits = 100;
        deliveryTutorialShown = false;
        tellurium = 0;
        npcsTalks = new HashSet<string>();
        itemsBought = new Dictionary<string, int>();
        itemsBought[MiningMachine] = 0;
    }
}