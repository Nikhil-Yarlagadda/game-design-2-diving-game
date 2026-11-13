using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class Shop : MonoBehaviour
{
    public TextMeshProUGUI description;
    public Button button;
    private static ItemCatalog.Definition item;
    
    void Start()
    {
        item = null;
    }
    void Update()
    {
        int stock = ItemCatalog.RemainingStock(item);
        button.interactable = CanBuy(stock);
        if (item != null)
            description.text = $"{item.Name}\nCost: {item.Price} credits    Weight: {item.Weight}    Stock: {stock}\n\n{item.Description}";
    }

    public void buyClicked()
    {
        if (!CanBuy(ItemCatalog.RemainingStock(item))) return;
        GameState.credits -= item.Price;
        ItemCatalog.ChangeCount(GameState.itemsBought, item.Id, 1);
        if (!item.DailyStock) return;
        if (GameState.shopDay != GameState.days)
        {
            GameState.shopDay = GameState.days;
            GameState.dailyPurchases.Clear();
        }
        ItemCatalog.ChangeCount(GameState.dailyPurchases, item.Id, 1);
    }

    private static bool CanBuy(int stock) => stock > 0 && item != null && GameState.credits >= item.Price;

    public static void itemClicked(string name) => item = ItemCatalog.Find(name);
}

