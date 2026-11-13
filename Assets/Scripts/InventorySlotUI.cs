using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler
{
    public Image icon;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI nameText;

    public string ItemName { get; private set; }
    public Action<InventorySlotUI> onClicked;
    public Action<InventorySlotUI> onHovered;
    public void OnPointerEnter(PointerEventData eventData) => onHovered?.Invoke(this);
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button.targetGraphic != null) button.targetGraphic.color = new Color(.08f, .16f, .20f);
        nameText.color = countText.color = Color.white;
        nameText.fontSize = 15f;
        countText.fontSize = 16f;
        button.onClick.AddListener(() =>
        {
            onClicked?.Invoke(this);
        });
    }

    public void Set(string itemName, int count, Sprite sprite)
    {
        ItemName = itemName;
        button.interactable = true;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        nameText.text = sprite == null ? itemName : "";
        countText.text = count > 1 ? count.ToString() : "";
    }

    public void Clear()
    {
        ItemName = null;
        button.interactable = false;
        icon.enabled = false;
        nameText.text = "";
        countText.text = "";
    }
}
