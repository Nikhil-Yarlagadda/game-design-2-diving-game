using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopFilter : MonoBehaviour
{
    public Transform itemsRoot;          // the parent that holds all Item objects
    public Button allButton;
    public Button toolsButton;
    public Button equipmentButton;
    public Button consumablesButton;

    private Item[] items;

    public float selectedAlpha = 0.9f;
    public float unselectedAlpha = 0f;

    private Color selectedTextColor;
    private Color normalTextColor;
    private Button[] buttons;

    void Start()
{
    items = itemsRoot.GetComponentsInChildren<Item>(true);

    ColorUtility.TryParseHtmlString("#042825", out selectedTextColor);
    ColorUtility.TryParseHtmlString("#A4DDDB", out normalTextColor);
    buttons = new[] { allButton, toolsButton, equipmentButton, consumablesButton };

    allButton.onClick.AddListener(() =>
    {
        Apply(null);
        Highlight(allButton);
    });
    toolsButton.onClick.AddListener(() =>
    {
        Apply(Item.Category.Tool);
        Highlight(toolsButton);
    });
    equipmentButton.onClick.AddListener(() =>
    {
        Apply(Item.Category.Equipment);
        Highlight(equipmentButton);
    });
    consumablesButton.onClick.AddListener(() =>
    {
        Apply(Item.Category.Consumable);
        Highlight(consumablesButton);
    });

    Apply(null);
    Highlight(allButton);   // start on All Items
}

    void Apply(Item.Category? filter)
    {
        Debug.Log("Filter clicked: " + (filter == null ? "All" : filter.ToString()));
        foreach (var item in items)
        {
            bool show = item.Available && (filter == null || item.category == filter);
            item.gameObject.SetActive(show);
        }
    }

    void Highlight(Button selected)
    {
        foreach (var b in buttons)
        {
            bool on = b == selected;

            // background: keep the existing color, change only the alpha
            Color bg = b.image.color;
            bg.a = on ? selectedAlpha : unselectedAlpha;
            b.image.color = bg;

            // label text
            var label = b.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = on ? selectedTextColor : normalTextColor;
        }
    }
}