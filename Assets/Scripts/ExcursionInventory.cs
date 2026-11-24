using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
    private TMP_Text details, dropLabel, tankHeader, transferButtonLabel;
    private RectTransform dropControls, helpPanel;
    private ScrollRect helpScroll;
    private UnityEngine.UI.Button less, more, max;
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
        var header = hud.Panel("Equipped Diving Gear", new Vector2(0, 671), new Vector2(850, 23));
        header.SetParent(panel.transform, false);
        tankHeader = hud.PanelText(header, 18);
        tankHeader.rectTransform.offsetMin = new Vector2(16, 0);
        tankHeader.rectTransform.offsetMax = new Vector2(-16, 0);
        var info = hud.Panel("Inventory Item Details", new Vector2(0, 56), new Vector2(850, 64));
        info.SetParent(panel.transform, false);
        details = hud.PanelText(info, 19);
        details.rectTransform.offsetMin = new Vector2(10, 1);
        details.rectTransform.offsetMax = new Vector2(-10, -1);
        details.enableAutoSizing = true;
        details.fontSizeMin = 14;
        details.fontSizeMax = 19;
        dropControls = hud.Panel("Drop Quantity", new Vector2(0, 8), new Vector2(850, 47));
        dropControls.SetParent(panel.transform, false);
        dropLabel = hud.PanelText(dropControls, 19);
        dropLabel.rectTransform.offsetMax = new Vector2(-500, -10);
        less = hud.Button(dropControls, "−", new Vector2(-66, 3), new Vector2(46, 41), () => { dropCount--; RefreshDrop(); });
        more = hud.Button(dropControls, "+", new Vector2(-12, 3), new Vector2(46, 41), () => { dropCount++; RefreshDrop(); });
        max = hud.Button(dropControls, "Max", new Vector2(50, 3), new Vector2(64, 41),
            () => { dropCount = dropLimit; RefreshDrop(); });
        transferButtonLabel = hud.Button(dropControls, "Drop", new Vector2(143, 3), new Vector2(108, 41), ConfirmDrop)
            .GetComponentInChildren<TMP_Text>();
        hud.Button(dropControls, "Cancel", new Vector2(268, 3), new Vector2(112, 41), CancelDrop);
        CancelDrop();
        BuildHelp(hud);
    }

    /// <summary>Builds a scrollable tutorial reference inside the paused inventory.</summary>
    private void BuildHelp(OceanHud hud)
    {
        var helpButton = (RectTransform)hud.Button((RectTransform)panel.transform, "Help", Vector2.zero,
            new Vector2(82, 34), OpenHelp).transform;
        helpButton.anchorMin = helpButton.anchorMax = helpButton.pivot = Vector2.one;
        helpButton.anchoredPosition = new Vector2(-22, -23);

        helpPanel = hud.Panel("Excursion Help", Vector2.zero, Vector2.zero);
        helpPanel.SetParent(panel.transform, false);
        helpPanel.anchorMin = Vector2.zero;
        helpPanel.anchorMax = Vector2.one;
        helpPanel.offsetMin = helpPanel.offsetMax = Vector2.zero;
        var background = helpPanel.GetComponent<Image>();
        background.color = new Color(.015f, .04f, .07f);
        background.raycastTarget = true;

        var title = hud.PanelText(helpPanel, 28);
        title.text = "Excursion Help";
        title.rectTransform.anchorMin = new Vector2(0, 1);
        title.rectTransform.offsetMin = new Vector2(30, -68);
        title.rectTransform.offsetMax = new Vector2(-30, -14);

        var back = (RectTransform)hud.Button(helpPanel, "Back", Vector2.zero, new Vector2(110, 42), CloseHelp).transform;
        back.anchorMin = back.anchorMax = back.pivot = Vector2.one;
        back.anchoredPosition = new Vector2(-28, -22);

        var viewport = hud.Panel("Tutorial viewport", Vector2.zero, Vector2.zero);
        viewport.SetParent(helpPanel, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(32, 25);
        viewport.offsetMax = new Vector2(-54, -77);
        var viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(.04f, .09f, .13f);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var text = hud.PanelText(viewport, 19);
        text.text = OceanTutorial.ReferenceText;
        text.alignment = TextAlignmentOptions.TopLeft;
        var content = text.rectTransform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.offsetMin = new Vector2(16, 0);
        content.offsetMax = new Vector2(-16, -12);
        var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        helpScroll = helpPanel.gameObject.AddComponent<ScrollRect>();
        helpScroll.viewport = viewport;
        helpScroll.content = content;
        helpScroll.horizontal = false;
        helpScroll.movementType = ScrollRect.MovementType.Clamped;
        helpScroll.scrollSensitivity = 36;
        helpScroll.verticalScrollbar = BuildHelpScrollbar(hud);
        helpPanel.gameObject.SetActive(false);
    }

    /// <summary>Creates a visible, draggable scroll control beside the tutorial text.</summary>
    private Scrollbar BuildHelpScrollbar(OceanHud hud)
    {
        var track = hud.Panel("Tutorial scrollbar", Vector2.zero, Vector2.zero);
        track.SetParent(helpPanel, false);
        track.anchorMin = new Vector2(1, 0);
        track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(-45, 25);
        track.offsetMax = new Vector2(-33, -77);
        track.GetComponent<Image>().raycastTarget = true;
        var handle = hud.Panel("Scroll handle", Vector2.zero, Vector2.zero);
        handle.SetParent(track, false);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        var handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(.34f, .63f, .71f);
        handleImage.raycastTarget = true;
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle;
        bar.targetGraphic = handleImage;
        return bar;
    }

    /// <summary>Opens the help page at the beginning without releasing the inventory pause.</summary>
    private void OpenHelp()
    {
        if (PauseManager.BlocksInput) return;
        helpPanel.SetAsLastSibling();
        helpPanel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        helpScroll.StopMovement();
        helpScroll.verticalNormalizedPosition = 1;
    }

    /// <summary>Returns to the inventory without changing simulation time.</summary>
    private void CloseHelp() => helpPanel.gameObject.SetActive(false);

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
        int limit = ocean.DepositOpen ? ocean.DepositableCount(item)
            : item?.Droppable == true ? ocean.CarriedCount(item.Id) : 0;
        if (dropControls == null || limit == 0)
        {
            CancelDrop();
            return;
        }
        dropItem = item.Id;
        dropLimit = limit;
        dropCount = 1;
        dropControls.gameObject.SetActive(true);
        RefreshDrop();
    }

    private void RefreshDrop()
    {
        dropCount = Mathf.Clamp(dropCount, 1, dropLimit);
        dropLabel.text = $"{(ocean.DepositOpen ? "Deposit" : "Drop")} {dropCount} / {dropLimit}";
        less.interactable = dropCount > 1;
        more.interactable = max.interactable = dropCount < dropLimit;
    }

    private void ConfirmDrop()
    {
        bool transferred = ocean.DepositOpen ? ocean.DepositItem(dropItem, dropCount) : ocean.DropItem(dropItem, dropCount);
        if (transferred) CancelDrop();
        else if (details != null) details.text = ocean.Message;
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
            || ocean.TutorialBlocksInput
            || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return;
        OpenPanel();
    }

    public void OpenDeposit()
    {
        if (ocean != null && ocean.BeginDeposit()) OpenPanel();
    }

    private void OpenPanel()
    {
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        if (tankHeader != null) tankHeader.text = ocean.DepositOpen
            ? "Deposit items on the submarine  •  TAB to continue exploring"
            : ItemCatalog.Find(ocean.Loadout.Tank).Name;
        if (transferButtonLabel != null) transferButtonLabel.text = ocean.DepositOpen ? "Deposit" : "Drop";
        if (details != null) details.text = ocean.DepositOpen
            ? "Deposited items cannot be retrieved during this excursion."
            : "Hover over an item for details.";
        Refresh();
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!panel.activeSelf || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return;
        CancelDrop();
        if (helpPanel != null) CloseHelp();
        panel.SetActive(false);
        if (ocean.DepositOpen) ocean.CancelReturn();
        else Time.timeScale = 1f;
    }

    public void Toggle()
    {
        if (PauseManager.BlocksInput || SceneChanger.IsTransitioning) return;
        if (helpPanel != null && helpPanel.gameObject.activeInHierarchy) CloseHelp();
        else if (panel.activeSelf) Close();
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
