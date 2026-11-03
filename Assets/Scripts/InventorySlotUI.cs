using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI nameText;

    public string ItemName { get; private set; }
    public Action<InventorySlotUI> onClicked;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            Debug.Log("Slot clicked: " + ItemName);
            onClicked?.Invoke(this);
        });
    }

    public void Set(string itemName, int count, Sprite sprite)
    {
        ItemName = itemName;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        nameText.text = sprite == null ? itemName : "";
        countText.text = count > 1 ? count.ToString() : "";
    }

    public void Clear()
    {
        ItemName = null;
        icon.enabled = false;
        nameText.text = "";
        countText.text = "";
    }
}