using System.Collections.Generic;
using UnityEngine;

public static class GameState
{
    public static int days;
    public static int credits;
    public static int tellurium;
    public static HashSet<string> npcsTalks;
    public static Dictionary<string, int> inventory;
    

    public static void reset()
    {
        days = 1;
        credits = 0;
        tellurium = 0;
        npcsTalks = new HashSet<string>();
        inventory = new Dictionary<string, int>();
    }
}