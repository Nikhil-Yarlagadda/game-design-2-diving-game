using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class EquipScreen : MonoBehaviour
{

    public int defaultMaxStack = 64;
    public Transform storageList;      // list of rows, as before
    public Transform loadoutGrid;      // the new grid
    public Button rowPrefab;
    public InventorySlotUI slotPrefab;
    public int columns = 8;
    public int rows = 6;
    public List<string> equippableItems;
    public List<string> hydroxTanks;
    public List<ItemInfo> itemInfos;

    private List<InventorySlotUI> slots = new List<InventorySlotUI>();
    private Dictionary<string, ItemInfo> lookup = new Dictionary<string, ItemInfo>();

    int Capacity { get { return columns * rows; } }

    void Awake()
    {
        foreach (var info in itemInfos)
            lookup[info.itemName] = info;

        for (int i = 0; i < Capacity; i++)
        {
            var slot = Instantiate(slotPrefab, loadoutGrid);
            slot.Index = i;
            slot.onClick = OnLoadoutSlotClicked;
            slots.Add(slot);
        }
    }

    bool IsEquippable(string name)
    {
        return equippableItems.Contains(name);
    }

    void OnEnable() { Refresh(); }

    void Refresh()
    {
        RefreshStorage();
        RefreshLoadout();
    }

    int MaxStack(string name)
    {
        return lookup.TryGetValue(name, out ItemInfo info) ? Mathf.Max(1, info.maxStack) : defaultMaxStack;
    }

    Sprite GetSprite(string name)
    {
        return lookup.TryGetValue(name, out ItemInfo info) ? info.sprite : null;
    }

    bool IsTank(string name)
    {
        return hydroxTanks.Contains(name);
    }

    bool OtherTankPacked(string name)
    {
        foreach (string tank in hydroxTanks)
        {
            if (tank == name) continue;
            if (GameState.excursionInventory.TryGetValue(tank, out int count) && count > 0)
                return true;
        }
        return false;
    }

    void RefreshStorage()
    {
        foreach (Transform child in storageList) Destroy(child.gameObject);

        foreach (var pair in new List<KeyValuePair<string, int>>(GameState.inventory))
        {
            if (pair.Value <= 0) continue;
            if (!IsEquippable(pair.Key)) continue;

            string name = pair.Key;
            var row = Instantiate(rowPrefab, storageList);
            row.GetComponentInChildren<TextMeshProUGUI>().text = name + " x" + pair.Value;
            if (IsTank(name) && OtherTankPacked(name))
                row.interactable = false;
            else
                row.onClick.AddListener(() => ToLoadout(name));
        }
    }

    void RefreshLoadout()
    {
        int slotIndex = 0;

        foreach (var pair in GameState.excursionInventory)
        {
            int max = MaxStack(pair.Key);
            Sprite sprite = GetSprite(pair.Key);

            int remaining = pair.Value;
            while (remaining > 0 && slotIndex < slots.Count)
            {
                int stack = Mathf.Min(remaining, max);
                slots[slotIndex].Set(pair.Key, stack, sprite);
                slotIndex++;
                remaining -= stack;
            }
        }

        for (; slotIndex < slots.Count; slotIndex++)
            slots[slotIndex].Clear();
    }

    void ToLoadout(string name)
    {
        if (IsTank(name) && OtherTankPacked(name)) return;
        if (!IsEquippable(name)) return;
        int times = ShiftHeld() ? int.MaxValue : 1;

        for (int i = 0; i < times; i++)
        {
            if (!LoadoutHasRoom(name)) break;
            if (GameState.Move(GameState.inventory, GameState.excursionInventory, name, 1) == 0) break;
        }

        Refresh();
    }

    void OnLoadoutSlotClicked(InventorySlotUI slot)
    {
        if (string.IsNullOrEmpty(slot.ItemName)) return;

        int amount = ShiftHeld() ? int.MaxValue : 1;
        GameState.Move(GameState.excursionInventory, GameState.inventory, slot.ItemName, amount);

        Refresh();
    }

    bool LoadoutHasRoom(string name)
    {
        int used = 0;
        foreach (var pair in GameState.excursionInventory)
            used += Mathf.CeilToInt(pair.Value / (float)MaxStack(pair.Key));

        GameState.excursionInventory.TryGetValue(name, out int have);
        if (have > 0 && have % MaxStack(name) != 0) return true; // a partly filled stack exists
        return used < Capacity;
    }

    bool ShiftHeld()
    {
        return Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
    }
}