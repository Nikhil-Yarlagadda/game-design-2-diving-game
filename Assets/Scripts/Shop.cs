using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

public class Shop : MonoBehaviour
{
    public TextMeshProUGUI description;
    public TextMeshProUGUI buttonText;
    public Button button;
    public Button tab;
    private static ItemCatalog.Definition item;
    private static Shop instance;

    public GameObject notification;   
    public Button notificationButton; 

    public static Dictionary<string, int> cart = new Dictionary<string, int>();

    public TextMeshProUGUI cartText;    // the text between the two lines
    public TextMeshProUGUI totalText;
    public RectTransform cartContentRect; 
    
    void Start()
    {
        item = null;
        notification.SetActive(false);
    notificationButton.onClick.AddListener(() => notification.SetActive(false));
        instance = this;
        cart.Clear();
        RefreshCart();

        if (ColorUtility.TryParseHtmlString("#A4DDDB", out Color temp))
                temp.a = Mathf.Clamp01(0.9f);
                tab.image.color = temp;
                if (ColorUtility.TryParseHtmlString("#04302D", out Color text))
                {
                    buttonText.color = text;
                }
    }

    void Update()
    {
        int stock = ItemCatalog.RemainingStock(item);
        button.interactable = CanBuy();
        if (item != null)
            description.text = $"{item.Name}\nCost: {item.Price} credits    Weight: {item.Weight}    Stock: {stock}\n\n{item.Description}";
    }

    public void RefreshCart()
    {
        var sb = new StringBuilder();
        int total = 0;

        foreach (var pair in cart)
        {
            var def = ItemCatalog.Find(pair.Key);
            if (def == null) continue;

            int cost = def.Price * pair.Value;
            total += cost;
            sb.AppendLine(def.Name + " x" + pair.Value
                + "<pos=75%>C " + cost
                + "<pos=92%><link=\"" + pair.Key + "\"><color=#FF6B6B>X</color></link>");
        }

        cartText.text = sb.ToString();
        totalText.text = "C " + total;
        LayoutRebuilder.ForceRebuildLayoutImmediate(cartContentRect);
    }

    public void RemoveFromCart(string id)
    {
        if (!cart.ContainsKey(id)) return;
        cart.Remove(id);
        RefreshCart();
    }

    public void buyClicked()
    {
        if (!CanBuy()) return;

        // the daily-purchase counter resets on a new day
        if (GameState.shopDay != GameState.days)
        {
            GameState.shopDay = GameState.days;
            GameState.dailyPurchases.Clear();
        }

        foreach (var pair in cart)
        {
            var def = ItemCatalog.Find(pair.Key);
            if (def == null) continue;

            for (int i = 0; i < pair.Value; i++)
            {
                GameState.credits -= def.Price;
                GameState.Playtest.Purchase(def.Id, def.Price, GameState.credits);
                ItemCatalog.ChangeCount(GameState.itemsBought, def.Id, 1);
                if (def.DailyStock)
                    ItemCatalog.ChangeCount(GameState.dailyPurchases, def.Id, 1);
            }
        }

        cart.Clear();
        RefreshCart();
        notification.SetActive(true);
    }

    private static int CartTotal()
    {
        int total = 0;
        foreach (var pair in cart)
        {
            var def = ItemCatalog.Find(pair.Key);
            if (def != null) total += def.Price * pair.Value;
        }
        return total;
    }

    private static bool CanBuy() => cart.Count > 0 && GameState.credits >= CartTotal();

    public static void itemClicked(string name)
    {
        var def = ItemCatalog.Find(name);
        if (def == null) return;

        int remaining = ItemCatalog.RemainingStock(def, cart);
        Debug.Log(def.Id + " remaining=" + remaining);

        if (remaining <= 0) return;   // out of stock, don't add

        cart[def.Id] = (cart.TryGetValue(def.Id, out int n) ? n : 0) + 1;
        instance?.RefreshCart();
    }
}

