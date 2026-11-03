using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DivingPrototype;

[System.Serializable]
public class ItemInfo
{
    public string itemName;   // must match the key in GameState.inventory exactly
    public Sprite sprite;
    public int maxStack = 64;
}

public class ExcursionInventory : MonoBehaviour
{
    public GameObject panel;            // InventoryPanel
    public Transform gridParent;        // the Grid object
    public InventorySlotUI slotPrefab;
    public int columns = 8;
    public int rows = 6;
    public int defaultMaxStack = 64;
    public List<ItemInfo> itemInfos;
    private int slotIndex;
    private OceanInteractions ocean;

    private List<InventorySlotUI> slots = new List<InventorySlotUI>();
    private Dictionary<string, ItemInfo> lookup = new Dictionary<string, ItemInfo>();

    void Awake()
    {
        ocean = FindAnyObjectByType<OceanInteractions>();

        foreach (var info in itemInfos)
            lookup[info.itemName] = info;

        for (int i = 0; i < columns * rows; i++)
        {
            var slot = Instantiate(slotPrefab, gridParent);
            slot.onClicked = OnSlotClicked;
            slots.Add(slot);
        }
    }

    void OnSlotClicked(InventorySlotUI slot)
    {
        Debug.Log("OnSlotClicked: " + slot.ItemName + ", ocean is " + (ocean == null ? "NULL" : "set"));
        if (string.IsNullOrEmpty(slot.ItemName)) return;

        ocean.DropItem(slot.ItemName);
        Refresh();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            Toggle();
    }

    public void Open()
    {
        panel.SetActive(true);
        Refresh();
        Time.timeScale = 0f;   // uncomment to freeze the game while open
    }

    public void Close()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void Toggle()
    {
        if (panel.activeSelf) Close();
        else Open();
    }

    public void Refresh()
{
    slotIndex = 0;
    bool hasRoom = true;

    // Tellurium first, so it always gets space
    if (GameState.tellurium > 0)
        hasRoom = AddStacks("Tellurium", (int)GameState.tellurium);

    // Then everything in the inventory dictionary
    foreach (var pair in GameState.inventory)
    {
        if (!hasRoom) break;
        hasRoom = AddStacks(pair.Key, pair.Value);
    }

    // Clear whatever slots are left over
    for (; slotIndex < slots.Count; slotIndex++)
        slots[slotIndex].Clear();
}

// Splits a count into stacks and fills slots. Returns false if the grid ran out of room.
private bool AddStacks(string itemName, int count)
{
    lookup.TryGetValue(itemName, out ItemInfo info);
    int maxStack = info != null ? Mathf.Max(1, info.maxStack) : defaultMaxStack;
    Sprite sprite = info != null ? info.sprite : null;

    int remaining = count;
    while (remaining > 0)
    {
        if (slotIndex >= slots.Count)
        {
            Debug.LogWarning("Inventory is full, some items can't be shown.");
            return false;
        }

        int stack = Mathf.Min(remaining, maxStack);
        slots[slotIndex].Set(itemName, stack, sprite);
        slotIndex++;
        remaining -= stack;
    }

    return true;
}
}