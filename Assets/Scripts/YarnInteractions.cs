using UnityEngine;
using Yarn.Unity;

public class WorldInteractions : MonoBehaviour
{
    /// <summary>Opens the sleeping day's results with Cole's earnings ready to settle.</summary>
    [YarnCommand("NewDay")]
    public static void NewDay()
    {
        SceneChanger.ChangeSceneWithFade("Result", () =>
        {
            GameState.interiorDayResults = true;
            GameState.tellurium = GameState.days switch
            {
                1 => 10,
                15 or 17 or 19 => 0,
                _ => Random.Range(5, 11)
            };
            GameState.oreLost = 0;
            GameState.excursionIncapacitated = false;
            SaveManager.SavePendingResult();
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

    /// <summary>Records an answered medicine prompt and consumes a dose when one is given.</summary>
    [YarnCommand("medicine_choice")]
    public static void MedicineChoice(string crewmate, bool give)
    {
        GameState.Playtest.Choice("Medicine choice", give ? "Give" : "Decline", crewmate);
        if (!give || !HasItem(ItemCatalog.Medicine)) return;
        RemoveItem(ItemCatalog.Medicine);
        GameState.Playtest.Choice("Medicine dose consumed", "", crewmate);
    }

    [YarnCommand("add_item")]
    public static void AddItem(string itemName)
    {
        string id = ItemCatalog.Find(itemName)?.Id ?? itemName;
        if (id != ItemCatalog.CrewPhoto || !HasItem(id))
        {
            ItemCatalog.ChangeCount(GameState.inventory, id, 1);
            if (id == ItemCatalog.CrewPhoto) GameState.Playtest.Milestone("Infected crewmate photo captured");
        }
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

    /// <summary>Requires another conversation with this crew member before the day can end.</summary>
    [YarnCommand("unspeak")]
    public static void Unspeak(string name) => GameState.npcsTalks.Remove(name);

    /// <summary>Records Kirsten’s first cure for the dialogue and crew schedule.</summary>
    [YarnCommand("cure_Kirsten")]
    public static void CureKristen()
    {
        if (GameState.dayOfKirstenCured >= 0) return;
        GameState.dayOfKirstenCured = GameState.days;
        GameState.Playtest.Choice("Crewmate cured", "", "Kirsten");
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

    [YarnFunction("kirsten_aboard")]
    public static bool KirstenAboard() => GameState.IsCrewAboard("Medic");

    /// <summary>Records Cole’s first cure for the dialogue.</summary>
    [YarnCommand("cure_Cole")]
    public static void CureCole()
    {
        if (GameState.dayOfColeCured >= 0) return;
        GameState.dayOfColeCured = GameState.days;
        GameState.Playtest.Choice("Crewmate cured", "", "Cole");
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
