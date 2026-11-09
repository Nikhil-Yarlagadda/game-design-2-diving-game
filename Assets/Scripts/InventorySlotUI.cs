using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public Image icon;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI nameText;

    public string ItemName { get; private set; }
    [HideInInspector] public int Index;
    [HideInInspector] public ExcursionInventory owner;
    public System.Action<InventorySlotUI> onClick; // used by screens that have no owner

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (owner != null) owner.SlotClicked(this);
            else onClick?.Invoke(this);
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

    public void OnBeginDrag(PointerEventData e) { if (owner != null) owner.BeginDrag(this, e); }
    public void OnDrag(PointerEventData e)      { if (owner != null) owner.Drag(e); }
    public void OnEndDrag(PointerEventData e)   { if (owner != null) owner.EndDrag(); }

    public void OnDrop(PointerEventData e)
    {
        if (owner == null) return;
        var source = e.pointerDrag != null ? e.pointerDrag.GetComponent<InventorySlotUI>() : null;
        if (source != null) owner.MoveStack(source.Index, Index);
    }
}