using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DivingPrototype;
using TMPro;

[System.Serializable]
public class ItemInfo
{
    public string itemName;
    public Sprite sprite;
}

public class ExcursionInventory : MonoBehaviour
{
    public GameObject panel;
    public Transform gridParent;
    public InventorySlotUI slotPrefab;
    public int columns = 8;
    public int rows = 6;
    public List<ItemInfo> itemInfos;

    private OceanInteractions ocean;
    private TMP_Text details, dropLabel, tankHeader;
    private RectTransform dropControls;
    private UnityEngine.UI.Button less, more;
    private string dropItem;
    private int dropCount, dropLimit;
    private readonly List<InventorySlotUI> slots = new();
    private readonly Dictionary<string, Sprite> icons = new();

    void Awake()
    {
        ocean = FindAnyObjectByType<OceanInteractions>();
        foreach (var info in itemInfos)
        {
            var definition = ItemCatalog.Find(info.itemName);
            if (definition != null) icons[definition.Id] = info.sprite;
        }
        for (int i = 0; i < columns * rows; i++)
        {
            var slot = Instantiate(slotPrefab, gridParent);
            slot.onClicked = OnSlotClicked;
            slot.onHovered = ShowItem;
            slots.Add(slot);
        }
    }

    void Start()
    {
        var hud = FindAnyObjectByType<OceanHud>();
        if (hud == null) return;
        var header = hud.Panel("Equipped Hydrox", new Vector2(0, 671), new Vector2(850, 23));
        header.SetParent(panel.transform, false);
        tankHeader = hud.PanelText(header, 18);
        tankHeader.rectTransform.offsetMin = new Vector2(16, 0);
        tankHeader.rectTransform.offsetMax = new Vector2(-16, 0);
        var info = hud.Panel("Inventory Item Details", new Vector2(0, 55), new Vector2(850, 57));
        info.SetParent(panel.transform, false);
        details = hud.PanelText(info, 19);
        dropControls = hud.Panel("Drop Quantity", new Vector2(0, 8), new Vector2(850, 47));
        dropControls.SetParent(panel.transform, false);
        dropLabel = hud.PanelText(dropControls, 19);
        dropLabel.rectTransform.offsetMax = new Vector2(-450, -10);
        less = hud.Button(dropControls, "−", new Vector2(0, 3), new Vector2(46, 41), () => { dropCount--; RefreshDrop(); });
        more = hud.Button(dropControls, "+", new Vector2(54, 3), new Vector2(46, 41), () => { dropCount++; RefreshDrop(); });
        hud.Button(dropControls, "Drop", new Vector2(143, 3), new Vector2(108, 41), ConfirmDrop);
        hud.Button(dropControls, "Cancel", new Vector2(268, 3), new Vector2(112, 41), CancelDrop);
        CancelDrop();
    }

    private void ShowItem(InventorySlotUI slot)
    {
        var item = ItemCatalog.Find(slot.ItemName);
        if (details != null && item != null)
            details.text = $"{item.Name}  •  Weight {item.Weight}\n{item.Description}";
    }

    private void OnSlotClicked(InventorySlotUI slot)
    {
        ShowItem(slot);
        var item = ItemCatalog.Find(slot.ItemName);
        if (item == null || !item.Droppable || dropControls == null)
        {
            CancelDrop();
            return;
        }
        dropItem = item.Id;
        dropLimit = ocean.CarriedCount(item.Id);
        dropCount = 1;
        dropControls.gameObject.SetActive(true);
        RefreshDrop();
    }

    private void RefreshDrop()
    {
        dropCount = Mathf.Clamp(dropCount, 1, dropLimit);
        dropLabel.text = $"Drop {dropCount} / {dropLimit}";
        less.interactable = dropCount > 1;
        more.interactable = dropCount < dropLimit;
    }

    private void ConfirmDrop()
    {
        if (ocean.DropItem(dropItem, dropCount)) CancelDrop();
        Refresh();
    }

    private void CancelDrop()
    {
        dropItem = null;
        if (dropControls != null) dropControls.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            Toggle();
    }

    public void Open()
    {
        if (ocean == null || ocean.LoadoutOpen || ocean.Incapacitated || ocean.IsTransitioning || ocean.ReturnConfirmationOpen
            || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        if (tankHeader != null) tankHeader.text = ocean.Loadout.Tank;
        if (details != null) details.text = "Hover over an item for details.";
        Refresh();
        Time.timeScale = 0f;
    }

    public void Close()
    {
        CancelDrop();
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void Toggle()
    {
        if (PauseManager.BlocksInput || SceneChanger.IsTransitioning) return;
        if (panel.activeSelf) Close();
        else Open();
    }

    public void Refresh()
    {
        int slot = 0;
        foreach (var pair in ocean.CarriedItems)
        {
            int remaining = pair.Value;
            int stack = ItemCatalog.Find(pair.Key).Stack;
            icons.TryGetValue(pair.Key, out var icon);
            while (remaining > 0 && slot < slots.Count)
            {
                int count = Mathf.Min(remaining, stack);
                slots[slot++].Set(pair.Key, count, icon);
                remaining -= count;
            }
        }
        for (; slot < slots.Count; slot++) slots[slot].Clear();
    }
}
