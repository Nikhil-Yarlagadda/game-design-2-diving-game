using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Item : MonoBehaviour
{
    public string title;
    public int remaining;
    public int price;
    public TextMeshProUGUI stock;
    public TextMeshProUGUI cost;
    public Button button;
    public enum Category { Equipment, Consumable, Tool }
    public Category category;

    private void Start()
    {
        if (!Available) gameObject.SetActive(false);

        price = ItemCatalog.Find(title).Price;
        remaining = ItemCatalog.RemainingStock(ItemCatalog.Find(title), Shop.cart);
    }
    
    void Update()
    {
        remaining = ItemCatalog.RemainingStock(ItemCatalog.Find(title), Shop.cart);

        stock.text = "in stock: " + remaining;
        cost.text = "C " + price;

        button.interactable = remaining > 0;
    }

    public void onClicked()
    {
        Shop.itemClicked(title);
    }

    // true unless something else says this item shouldn't exist yet
    public bool Available
    {
        get { return !(ItemCatalog.Find(title)?.Id == ItemCatalog.Medicine && GameState.days < 7); }
    }
}
