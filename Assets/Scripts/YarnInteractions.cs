using UnityEngine;
using Yarn.Unity;

public class WorldInteractions : MonoBehaviour
{
    [YarnCommand("NewDay")]
    public static void NewDay()
    {
        SceneChanger.ChangeSceneWithFade("Ocean Interactables", () =>
        {
            GameState.days++;
            GameState.npcsTalks.Clear();
#if UNITY_EDITOR
            GameStateDebugger.CaptureDayStart("Ocean Interactables");
#endif
        });
    }

    [YarnFunction("has_item")]
    public static bool HasItem(string itemName)
    {
        return ItemCatalog.Count(GameState.inventory, ItemCatalog.Find(itemName)?.Id ?? itemName) > 0;
    }

    [YarnCommand("remove_item")]
    public static void RemoveItem(string itemName)
    {
        ItemCatalog.ChangeCount(GameState.inventory, ItemCatalog.Find(itemName)?.Id ?? itemName, -1);
    }

    [YarnCommand("add_item")]
    public static void AddItem(string itemName)
    {
        string id = ItemCatalog.Find(itemName)?.Id ?? itemName;
        if (id != ItemCatalog.CrewPhoto || !HasItem(id))
            ItemCatalog.ChangeCount(GameState.inventory, id, 1);
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

    /// <summary>Records Kirsten’s first cure for the dialogue and crew schedule.</summary>
    [YarnCommand("cure_Kirsten")]
    public static void CureKristen()
    {
        if (GameState.dayOfKirstenCured < 0) GameState.dayOfKirstenCured = GameState.days;
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

    /// <summary>Records Cole’s first cure for the dialogue.</summary>
    [YarnCommand("cure_Cole")]
    public static void CureCole()
    {
        if (GameState.dayOfColeCured < 0) GameState.dayOfColeCured = GameState.days;
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
