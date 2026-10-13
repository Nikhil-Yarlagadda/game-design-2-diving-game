using System.Collections.Generic;
using UnityEngine;

public static class GameState
{
    public static int days;
    public static int credits;
    public static Dictionary<string, int> inventory;

    public static void reset()
    {
        days = 1;
        credits = 0;
        inventory = new Dictionary<string, int>();
    }
}