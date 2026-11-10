using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DivingPrototype;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class ItemInfo
{
    public string itemName;   
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
    private OceanInteractions ocean;
    public Image dragGhost;
    private InventorySlotUI dragSource;

    private List<InventorySlotUI> slots = new List<InventorySlotUI>();
    private Dictionary<string, ItemInfo> lookup = new Dictionary<string, ItemInfo>();

    void Awake()
    {
        ocean = FindAnyObjectByType<OceanInteractions>();

        foreach (var info in itemInfos)
        {
            Debug.Log("Logging item " + info.itemName + " with stack size " + info.maxStack);   
            lookup[info.itemName] = info;
        }
        for (int i = 0; i < columns * rows; i++)
        {
            var slot = Instantiate(slotPrefab, gridParent);
            slot.Index = i;
            slot.owner = this;
            slots.Add(slot);
        }

        if (GameState.layout == null || GameState.layout.Length != slots.Count)
            GameState.layout = new ItemStack[slots.Count];
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
        Reconcile();
        Redraw();
    }

    public void BeginDrag(InventorySlotUI slot, PointerEventData e)
    {

        Debug.Log("BeginDrag on slot " + slot.Index + ", item: " + slot.ItemName);

        if (string.IsNullOrEmpty(slot.ItemName)) { dragSource = null; return; }

        dragSource = slot;
        dragGhost.sprite = slot.icon.sprite;
        dragGhost.gameObject.SetActive(true);
        dragGhost.transform.position = e.position;
    }

    public void Drag(PointerEventData e)
    {
        Debug.Log("Drag, source is " + (dragSource == null ? "null" : "set"));
        if (dragSource != null) dragGhost.transform.position = e.position;
        Debug.Log("UI: " + e.position + "  Mouse: " + Mouse.current.position.ReadValue() +
          "  Screen: " + Screen.width + "x" + Screen.height);
    }

    public void EndDrag()
    {
        Debug.Log("EndDrag");
        dragSource = null;
        dragGhost.gameObject.SetActive(false);
    }

    public void MoveStack(int from, int to)
    {
        var layout = GameState.layout;
        if (from == to || layout[from] == null) return;

        var a = layout[from];
        var b = layout[to];

        if (b == null)                           // move into an empty slot
        {
            layout[to] = a;
            layout[from] = null;
        }
        else if (b.itemName == a.itemName)       // merge stacks
        {
            int moved = Mathf.Min(MaxStack(a.itemName) - b.count, a.count);
            b.count += moved;
            a.count -= moved;
            if (a.count <= 0) layout[from] = null;
        }
        else                                     // swap
        {
            layout[to] = a;
            layout[from] = b;
        }

        Redraw();
    }

    // ---------- click to drop ----------

    public void SlotClicked(InventorySlotUI slot)
    {
        var stack = GameState.layout[slot.Index];
        if (stack == null) return;

        string name = stack.itemName;

         if (!ocean.CanDrop(name))
        {
            ocean.Notify("You can't drop that.");   // only if Notify is public
            return;
        }

        // remove from the clicked stack first, so the right slot shrinks
        stack.count--;
        if (stack.count <= 0) GameState.layout[slot.Index] = null;

        ocean.DropItem(name); // lowers the totals in GameState
        Refresh();
    }

    // ---------- layout syncing ----------

    int MaxStack(string name)
    {
        Debug.Log("Looking up name: " + name);
        int tbr = lookup.TryGetValue(name, out ItemInfo info) ? Mathf.Max(1, info.maxStack) : defaultMaxStack;
        Debug.Log(name +" has stack size: " + tbr);
        return tbr;
    }

    int GetTotal(string name)
    {
        if (name == "Tellurium") return (int)GameState.tellurium;
        return GameState.excursionInventory.TryGetValue(name, out int c) ? c : 0;
    }

    void Reconcile()
    {
        var layout = GameState.layout;

        // loaded saves can contain empty placeholders, treat them as empty
        for (int i = 0; i < layout.Length; i++)
            if (layout[i] != null && (string.IsNullOrEmpty(layout[i].itemName) || layout[i].count <= 0))
                layout[i] = null;

        var names = new HashSet<string>(GameState.excursionInventory.Keys) { "Tellurium" };
        foreach (var s in layout)
            if (s != null) names.Add(s.itemName);

        foreach (string name in names)
        {
            int want = GetTotal(name);
            int have = 0;
            foreach (var s in layout)
                if (s != null && s.itemName == name) have += s.count;

            if (have > want)
            {
                int excess = have - want;
                for (int i = layout.Length - 1; i >= 0 && excess > 0; i--)
                {
                    if (layout[i] == null || layout[i].itemName != name) continue;
                    int take = Mathf.Min(excess, layout[i].count);
                    layout[i].count -= take;
                    excess -= take;
                    if (layout[i].count <= 0) layout[i] = null;
                }
            }
            else if (have < want)
            {
                int toAdd = want - have;
                int max = MaxStack(name);

                foreach (var s in layout)                    // top up existing stacks
                {
                    if (toAdd <= 0) break;
                    if (s == null || s.itemName != name || s.count >= max) continue;
                    int add = Mathf.Min(max - s.count, toAdd);
                    s.count += add;
                    toAdd -= add;
                }

                for (int i = 0; i < layout.Length && toAdd > 0; i++)   // then fill empty slots
                {
                    if (layout[i] != null) continue;
                    int add = Mathf.Min(max, toAdd);
                    layout[i] = new ItemStack { itemName = name, count = add };
                    toAdd -= add;
                }

                if (toAdd > 0) Debug.LogWarning("Inventory is full, " + toAdd + " " + name + " can't be shown.");
            }
        }
    }

    void Redraw()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var s = GameState.layout[i];
            if (s == null) { slots[i].Clear(); continue; }

            lookup.TryGetValue(s.itemName, out ItemInfo info);
            slots[i].Set(s.itemName, s.count, info != null ? info.sprite : null);
        }
    }

    public bool CanFit(string itemName)
    {
        Reconcile(); // make sure the layout matches the current totals first

        int max = MaxStack(itemName);
        foreach (var s in GameState.layout)
        {
            if (s == null) return true;                                    // empty slot
            if (s.itemName == itemName && s.count < max) return true;      // room in an existing stack
        }
        return false;
    }

    public int RoomFor(string itemName)
    {
        Reconcile(); // make the layout match the current totals first

        int max = MaxStack(itemName);
        int room = 0;

        foreach (var s in GameState.layout)
        {
            if (s == null) room += max;                                  // empty slot
            else if (s.itemName == itemName) room += max - s.count;      // space left in an existing stack
        }

        return room;
    }
}