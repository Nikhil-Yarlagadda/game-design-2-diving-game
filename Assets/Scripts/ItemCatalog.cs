using System;
using System.Collections.Generic;

public static class ItemCatalog
{
    public const string Machine = "Mining Machine";
    public const string LightTank = "Light Hydrox Tank";
    public const string MediumTank = "Medium Hydrox Tank";
    public const string HeavyTank = "Heavy Hydrox Tank";
    public const string Flippers = "Advanced flippers";
    public const string Stimulant = "Stimulant Canister";
    public const string Medicine = "Antimicrobial Medicine";
    public const string Camera = "Camera Module";
    public const string Ore = "Tellurium";
    public const string Fungus = "Full-grown Fungus Sample";
    public const string CrewPhoto = "Photograph of Infected Crewmate";
    public const string EyePhoto = "Photograph of the Eye of the Abyss";

    public sealed class Definition
    {
        public readonly string Id, Name, Description;
        public readonly int Weight, Stack, Price, Stock, TankSeconds, WeightCapacity;
        public readonly bool Droppable, DailyStock, InteriorOnly;

        internal Definition(string id, string description, int weight, int stack = 1, bool droppable = false,
            int price = 0, int stock = 0, bool dailyStock = false, bool interiorOnly = false, int tankSeconds = 0,
            string name = null, int weightCapacity = 100)
        {
            Id = id;
            Name = name ?? id;
            Description = description;
            Weight = weight;
            Stack = stack;
            Droppable = droppable;
            Price = price;
            Stock = stock;
            DailyStock = dailyStock;
            InteriorOnly = interiorOnly;
            TankSeconds = tankSeconds;
            WeightCapacity = weightCapacity;
        }
    }

    private static readonly Dictionary<string, Definition> definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        { Machine, new(Machine, "Attach to prospected mining spots to extract tellurium. Breaks if left out overnight.", 15, 4, droppable: true, price: 50, stock: 5, dailyStock: true) },
        { MediumTank, new(MediumTank, "A medium hydrox tank paired with a sturdy carrying harness. A substantial upgrade from the Novice set; the tank contains 5 minutes worth of hydrox, and the harness increases your carrying capacity twofold.", 20, price: 200, stock: 1, tankSeconds: 300, name: "Advanced Diver’s Equipment", weightCapacity: 200) },
        { HeavyTank, new(HeavyTank, "A large hydrox tank paired with a heavy-duty carrying harness. A colossal upgrade from the Novice set; the tank contains 8 minutes worth of hydrox, and the harness increases your carrying capacity threefold.", 30, price: 400, stock: 1, tankSeconds: 480, name: "Intrepid Diver’s Equipment", weightCapacity: 300) },
        { Flippers, new(Flippers, "Large and cumbersome on dry land, but a great help in the water. Increases swimming speed.", 4, price: 300, stock: 1) },
        { Stimulant, new(Stimulant, "Consumable item. A small canister of envigorating gas. Can be inhaled during excursions for a boost of energy. It’s a wonder how eager the Company is to supply these.", 2, 4, droppable: true, price: 30, stock: 5, dailyStock: true) },
        { Medicine, new(Medicine, "Should cure a mild infection. The Company is only providing a single pill.", 0, price: 100, stock: 1, interiorOnly: true) },
        { Camera, new(Camera, "A pressure-resistant waterproof camera with a spotlight. Widens the flashlight beam when equipped.", 10, price: 500, stock: 1) },
        { LightTank, new(LightTank, "Trident Excavation’s standard issue diving set: a small hydrox tank and lightweight carrying harness. The tank contains 3 minutes worth of hydrox, while the harness affords no extra carrying capacity.", 10, tankSeconds: 180, name: "Novice Diver’s Equipment") },
        { Ore, new(Ore, "Dirty with sediment and biotic matter. The Company grants 10 credits per sample.", 2, 16, droppable: true, name: "Raw Tellurium Sample") },
        { Fungus, new(Fungus, "If the encrypted files are to be trusted, this is the key to your survival.", 5, 4) },
        { CrewPhoto, new(CrewPhoto, "Upload this to the terminal to ask the Company for aid.", 0, interiorOnly: true) },
        { EyePhoto, new(EyePhoto, "Meet the void’s gaze, somewhere on the abyssal plain.", 0) }
    };

    /// <summary>Enumerates the item definitions in the catalog.</summary>
    public static IEnumerable<Definition> All => definitions.Values;

    public static Definition Find(string id) =>
        id != null && definitions.TryGetValue(id.Trim(), out var definition) ? definition : null;

    public static int Count(IReadOnlyDictionary<string, int> items, string id) =>
        items.TryGetValue(id, out int count) ? count : 0;

    public static void ChangeCount(Dictionary<string, int> items, string id, int delta)
    {
        int remaining = Count(items, id) + delta;
        if (remaining > 0) items[id] = remaining;
        else items.Remove(id);
    }

    public static int RemainingStock(Definition item, Dictionary<string, int> cart = null)
    {
        if (item == null || item.Stock == 0) return 0;
        if (item.Id == Medicine && (GameState.days < 7 || GameState.medicineDelivered)) return 0;

        int remaining;
        if (item.DailyStock)
            remaining = GameState.shopDay == GameState.days
                ? Math.Max(0, item.Stock - Count(GameState.dailyPurchases, item.Id))
                : item.Stock;
        else
            remaining = Math.Max(0, item.Stock - Count(GameState.inventory, item.Id) - Count(GameState.itemsBought, item.Id));

        if (cart != null)
            remaining = Math.Max(0, remaining - Count(cart, item.Id));

        return remaining;
    }
}
