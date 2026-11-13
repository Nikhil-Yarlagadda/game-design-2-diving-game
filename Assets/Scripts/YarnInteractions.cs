using UnityEngine;
using Yarn.Unity;
using System.Collections.Generic;

public class WorldInteractions : MonoBehaviour
{
    [YarnCommand("NewDay")]
    public static void NewDay()
    {
        GameState.days++;
        GameState.npcsTalks = new HashSet<string>();
        SceneChanger.ChangeScene("Equip");
    }    

    [YarnFunction("has_item")]
    public static bool HasItem(string itemName)
    {
        return GameState.inventory.TryGetValue(itemName, out int count) && count > 0;
    } 

    [YarnCommand("remove_item")]
    public static void RemoveItem(string itemName)
    {
        if (!GameState.inventory.TryGetValue(itemName, out int count)) return;

        if (count <= 1) GameState.inventory.Remove(itemName);
        else GameState.inventory[itemName] = count - 1;
    }

    [YarnCommand("add_item")]
    public static void AddItem(string itemName)
    {
        if (!GameState.inventory.ContainsKey(itemName)){
            GameState.inventory[itemName] = 0;
        }
        GameState.inventory[itemName]++;
    }

    [YarnFunction("check_terminal")]
    public static bool CheckTerminal()
    {
        return GameState.checkTerminal;
    }

    [YarnFunction("spoken_to")]
    public static bool SpokenTo(string name)
    {
        return GameState.npcsTalks.Contains(name);
    }

    [YarnCommand("cure_Kirsten")]
    public static void CureKristen(string name)
    {
        GameState.dayOfKirstenCured = GameState.days;
    }

    [YarnFunction("time_Kirsten")]
    public static bool TimeSinceKristenCured(int days)
    {
        if(GameState.dayOfKirstenCured < 0){
            return false;
        }
        return (GameState.days - GameState.dayOfKirstenCured) == days;
    }

    [YarnFunction("Kirsten_sick")]
    public static bool isKristenSick()
    {
        return GameState.dayOfKirstenCured < 0;
    }

    [YarnCommand("cure_Cole")]
    public static void CureCole(string name)
    {
        GameState.dayOfColeCured = GameState.days;
    }

    [YarnFunction("time_Cole")]
    public static bool TimeSinceColeCured(int days)
    {
        if(GameState.dayOfColeCured < 0){
            return false;
        }
        return (GameState.days - GameState.dayOfColeCured) == days;
    }

    [YarnFunction("Cole_sick")]
    public static bool isColeSick()
    {
        return GameState.dayOfColeCured < 0;
    }

    [YarnFunction("days_since_fgfs")]
    public static int DaysSinceFgfs()
    {
        if (GameState.dayFgfsCollected < 0) return -1;
        return GameState.days - GameState.dayFgfsCollected;
    }
}
