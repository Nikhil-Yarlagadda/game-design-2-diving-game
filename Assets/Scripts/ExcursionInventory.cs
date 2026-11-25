using System.Collections.Generic;
using DivingPrototype;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

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

    private static readonly Color DepositAccent = Theme.Hex("EC5CFF");
    private static readonly Color DepositIdleFill = Theme.Hex("59507C");
    private static readonly Color DropIdleFill = Theme.Hex("5D5963");
    private static readonly Color StepFill = Theme.Hex("4B6A76");
    private static readonly Color ControlBoxFill = Theme.Hex("354F5C");

    private OceanInteractions ocean;
    private TMP_FontAsset font;
    private TMP_Text titleLabel, tankHeader, weightLabel;
    private TMP_Text detailName, details, detailWeight, detailQuantity, detailArtPlaceholder;
    private TMP_Text dropCountText, transferButtonLabel, lessLabel, moreLabel, maxLabel;
    private Image weightBarFill, detailIcon, detailArtBg;
    private RectTransform detailCard, detailWeightRow, detailArtBox, dropControls, helpPanel;
    private RectTransform countBoxRect, lessRect, moreRect, maxRect, transferRect, cancelRect;
    private ScrollRect gridScroll, helpScroll;
    private UnityEngine.UI.Button tutorialButton, less, more, max, transferButton, cancelButton;
    private FrameButtonStyle transferVisual, lessVisual, moreVisual, maxVisual;
    private InventorySlotUI selectedSlot;
    private string dropItem, inspectedItem;
    private int dropCount, dropLimit;
    private bool uiBuilt;
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

    void Start() => EnsureUiBuilt();

    private void EnsureUiBuilt()
    {
        if (uiBuilt || panel == null) return;
        uiBuilt = true;

        var hud = FindAnyObjectByType<OceanHud>();
        font = Theme.Font(hud != null ? hud.font : null);

        var panelRect = (RectTransform)panel.transform;
        panelRect.Fill();
        var panelBg = panel.GetComponent<Image>();
        if (panelBg != null)
        {
            panelBg.sprite = null;
            panelBg.type = Image.Type.Simple;
            panelBg.color = Theme.Navy;
            panelBg.raycastTarget = true;
        }

        var layout = Theme.Node("Inventory Layout", panelRect)
            .Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Theme.Fig(1440, 1024));

        BuildTopBar(layout);
        BuildItemsPanel(layout);
        BuildDetailCard(layout);
        BuildHelp(panelRect);
        CancelDrop();
    }

    private void BuildTopBar(RectTransform layout)
    {
        var titleFrame = At(Theme.Frame("Title", layout, Theme.PanelFill, Theme.Cyan, 2f).rectTransform, 42, 50, 422, 131);
        titleLabel = Theme.Text("Text", titleFrame, font, Theme.Fig(64), Theme.White, TextAlignmentOptions.Center, "INVENTORY");
        titleLabel.rectTransform.Fill(12, 4, 12, 4);

        var infoBox = At(Theme.Node("Equipped Diving Gear", layout), 498, 67, 728, 97);
        var infoBg = infoBox.gameObject.AddComponent<Image>();
        infoBg.color = Theme.PanelFill.WithAlpha(.45f);
        infoBg.raycastTarget = false;
        HorizontalRule(infoBox, true, 728f, 2f, Theme.Cyan);
        HorizontalRule(infoBox, false, 728f, 2f, Theme.Cyan);
        tankHeader = Theme.Text("Text", infoBox, font, Theme.Fig(34), Theme.White, TextAlignmentOptions.MidlineLeft);
        tankHeader.rectTransform.Fill(Theme.Fig(20), 4, Theme.Fig(20), 4);
        tankHeader.enableAutoSizing = true;
        tankHeader.fontSizeMin = 14f;
        tankHeader.fontSizeMax = Theme.Fig(34);

        var back = FrameButton("Back", layout, "BACK", Theme.PanelFill, Theme.Cyan, Theme.White, Theme.Fig(34), Close);
        At((RectTransform)back.transform, 1264, 67, 156, 97);
    }

    private void BuildItemsPanel(RectTransform layout)
    {
        At(Theme.Frame("Items Panel", layout, Theme.PanelFill.WithAlpha(.55f), Theme.Cyan, 2f).rectTransform, 59, 239, 890, 710);
        var tab = At(Theme.Frame("Items Tab", layout, Theme.Cyan, Theme.Cyan, 2f).rectTransform, 86, 257, 185, 90);
        var tabText = Theme.Text("Label", tab, font, Theme.Fig(36), Theme.Navy, TextAlignmentOptions.Center, "ITEMS");
        tabText.rectTransform.Fill(6, 4, 6, 4);

        // Weight bar matching Figma "Weight design" and OceanUiTheme.WeightColor.
        var track = At(Theme.Box("Weight Track", layout, Theme.Navy, Theme.Cyan, Theme.Fig(17.5f), 2f).rectTransform, 509, 270, 414, 35);
        var fillArea = Theme.Node("Fill Area", track).Fill(Theme.Fig(4), Theme.Fig(4), Theme.Fig(4), Theme.Fig(4));
        weightBarFill = Theme.Box("Weight Fill", fillArea, Theme.TankFull, null, Theme.Fig(13.5f));
        weightBarFill.rectTransform.anchorMin = Vector2.zero;
        weightBarFill.rectTransform.anchorMax = Vector2.one;
        weightBarFill.rectTransform.offsetMin = Vector2.zero;
        weightBarFill.rectTransform.offsetMax = Vector2.zero;

        weightLabel = Theme.Text("Weight Value", layout, font, Theme.Fig(44), Theme.White, TextAlignmentOptions.Center);
        At(weightLabel.rectTransform, 509, 306, 414, 52);
        weightLabel.textWrappingMode = TextWrappingModes.NoWrap;

        // Scrollable 6-column slot grid (displays all 48 slots in 8 rows of 6, 4 rows visible at once).
        var viewport = At(Theme.Node("Items Viewport", layout), 86, 386, 816, 536);
        var viewportBg = viewport.gameObject.AddComponent<Image>();
        viewportBg.color = Color.clear;
        viewportBg.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var gridRect = (RectTransform)gridParent;
        gridRect.SetParent(viewport, false);
        gridRect.anchorMin = new Vector2(0, 1);
        gridRect.anchorMax = Vector2.one;
        gridRect.pivot = new Vector2(.5f, 1);
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = Vector2.zero;

        var grid = gridParent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.cellSize = Theme.Fig(106, 112);
            grid.spacing = Theme.Fig(35, 29);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.UpperLeft;
        }
        var fitter = gridParent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gridParent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        gridScroll = viewport.gameObject.AddComponent<ScrollRect>();
        gridScroll.viewport = viewport;
        gridScroll.content = gridRect;
        gridScroll.horizontal = false;
        gridScroll.vertical = true;
        gridScroll.movementType = ScrollRect.MovementType.Clamped;
        gridScroll.scrollSensitivity = 36f;
        gridScroll.verticalScrollbar = BuildScrollbar("Items Scrollbar", layout, 916, 389, 18, 534);
    }

    private void BuildDetailCard(RectTransform layout)
    {
        tutorialButton = FrameButton("Tutorial", layout, "TUTORIAL", Theme.Navy, Theme.Cyan, Theme.Cyan, Theme.Fig(36), OpenHelp);
        At((RectTransform)tutorialButton.transform, 970, 239, 438, 84);

        var cardFrame = Theme.Frame("Inventory Item Details", layout, Theme.PanelFill.WithAlpha(.75f), Theme.Cyan, 2f);
        detailCard = At(cardFrame.rectTransform, 970, 342, 438, 607);

        detailArtBox = At(Theme.Node("Art", detailCard), 52, 40, 141, 134);
        detailArtBg = detailArtBox.gameObject.AddComponent<Image>();
        detailArtBg.color = Theme.Cyan;
        detailArtBg.raycastTarget = false;
        detailArtPlaceholder = Theme.Text("Placeholder", detailArtBox, font, Theme.Fig(20), Theme.Navy, TextAlignmentOptions.Center, "image");
        detailArtPlaceholder.rectTransform.Fill();
        detailIcon = Theme.Node("Sprite", detailArtBox).Fill().gameObject.AddComponent<Image>();
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;
        detailIcon.enabled = false;

        detailName = Theme.Text("Name", detailCard, font, Theme.Fig(44), Theme.White, TextAlignmentOptions.TopLeft);
        At(detailName.rectTransform, 210, 40, 206, 134);
        detailName.enableAutoSizing = true;
        detailName.fontSizeMin = 14f;
        detailName.fontSizeMax = Theme.Fig(44);

        details = Theme.Text("Description", detailCard, font, Theme.Fig(32), Theme.White, TextAlignmentOptions.TopLeft);
        At(details.rectTransform, 36, 186, 366, 198);
        details.enableAutoSizing = true;
        details.fontSizeMin = 12f;
        details.fontSizeMax = Theme.Fig(32);

        detailWeight = Theme.WeightLabel(detailCard, font, Theme.Fig(36), Theme.White, TextAlignmentOptions.MidlineLeft);
        detailWeightRow = (RectTransform)detailWeight.transform.parent;
        At(detailWeightRow, 46, 392, 180, 40);

        detailQuantity = Theme.Text("Quantity", detailCard, font, Theme.Fig(22), Theme.White, TextAlignmentOptions.MidlineRight);
        At(detailQuantity.rectTransform, 228, 392, 164, 40);
        detailQuantity.textWrappingMode = TextWrappingModes.NoWrap;

        var ctrlFrame = Theme.Frame("Drop Quantity", detailCard, ControlBoxFill.WithAlpha(.6f), Theme.Cyan, 2f);
        dropControls = At(ctrlFrame.rectTransform, 28, 462, 385, 126);

        less = FrameButton("Less", dropControls, "−", StepFill, Theme.Cyan, Theme.White, Theme.Fig(48), () => { dropCount--; RefreshDrop(); }, out lessVisual);
        lessRect = (RectTransform)less.transform;
        lessLabel = less.GetComponentInChildren<TMP_Text>();

        var countImg = Theme.Node("Count", dropControls).gameObject.AddComponent<Image>();
        countImg.color = StepFill;
        countImg.raycastTarget = false;
        countBoxRect = countImg.rectTransform;
        dropCountText = Theme.Text("Value", countBoxRect, font, Theme.Fig(34), Theme.White, TextAlignmentOptions.Center, "1");
        dropCountText.rectTransform.Fill(2, 2, 2, 2);
        dropCountText.textWrappingMode = TextWrappingModes.NoWrap;

        more = FrameButton("More", dropControls, "+", StepFill, Theme.Cyan, Theme.White, Theme.Fig(48), () => { dropCount++; RefreshDrop(); }, out moreVisual);
        moreRect = (RectTransform)more.transform;
        moreLabel = more.GetComponentInChildren<TMP_Text>();

        max = FrameButton("Max", dropControls, "MAX", ControlBoxFill, Theme.Cyan, Theme.White, Theme.Fig(28), () => { dropCount = dropLimit; RefreshDrop(); }, out maxVisual);
        maxRect = (RectTransform)max.transform;
        maxLabel = max.GetComponentInChildren<TMP_Text>();

        transferButton = FrameButton("Transfer", dropControls, "DROP", DropIdleFill, Theme.Pink, Theme.Pink, Theme.Fig(32), ConfirmDrop, out transferVisual);
        transferRect = (RectTransform)transferButton.transform;
        transferButtonLabel = transferButton.GetComponentInChildren<TMP_Text>();

        cancelButton = FrameButton("Cancel", dropControls, "CANCEL", Theme.Navy.WithAlpha(.8f), Theme.Cyan, Theme.White, Theme.Fig(22), CancelDrop);
        cancelRect = (RectTransform)cancelButton.transform;
    }

    /// <summary>Switches between Excavation Inventory and Deposit layouts without changing rules or wiring.</summary>
    private void ApplyModeLayout(bool deposit)
    {
        if (titleLabel != null) titleLabel.text = deposit ? "DEPOSIT" : "INVENTORY";
        if (tutorialButton != null) tutorialButton.gameObject.SetActive(!deposit);

        At(detailCard, 970, deposit ? 239 : 342, 438, deposit ? 710 : 607);

        if (deposit)
        {
            At(dropControls, 28, 477, 385, 190);
            At(lessRect, 14, 16, 56, 56);
            At(countBoxRect, 78, 16, 88, 56);
            At(moreRect, 174, 16, 56, 56);
            At(maxRect, 240, 16, 131, 56);
            At(transferRect, 128, 86, 129, 56);
            At(cancelRect, 128, 150, 129, 30);
            maxLabel.fontSize = Theme.Fig(32);
            transferButtonLabel.text = "DEPOSIT";
            transferVisual.SetPalette(DepositIdleFill, DepositAccent, DepositAccent, DepositAccent.WithAlpha(.8f), Theme.White, Theme.White);
        }
        else
        {
            At(dropControls, 28, 462, 385, 126);
            At(lessRect, 14, 14, 57, 56);
            At(countBoxRect, 80, 14, 88, 56);
            At(moreRect, 178, 14, 57, 56);
            At(transferRect, 245, 14, 128, 56);
            At(maxRect, 14, 80, 174, 34);
            At(cancelRect, 198, 80, 175, 34);
            maxLabel.fontSize = Theme.Fig(24);
            transferButtonLabel.text = "DROP";
            // Frame 9 DROP swatches: hover fills #FE8D8D at 90% with a white outline and white text.
            transferVisual.SetPalette(DropIdleFill, Theme.Pink, Theme.Pink, Theme.Pink.WithAlpha(.9f), Theme.White, Theme.White);
        }
    }

    /// <summary>Builds a scrollable tutorial reference matching the Figma TUTORIAL modal.</summary>
    private void BuildHelp(RectTransform panelRect)
    {
        helpPanel = Theme.Node("Excursion Help", panelRect).Fill();
        var backdrop = helpPanel.gameObject.AddComponent<Image>();
        backdrop.color = Theme.Navy.WithAlpha(.88f);
        backdrop.raycastTarget = true;

        var helpLayout = Theme.Node("Help Layout", helpPanel)
            .Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Theme.Fig(1440, 1024));

        var dialog = At(Theme.Box("Tutorial Dialog", helpLayout, Theme.Navy, Theme.Cyan, Theme.Fig(20), 2f).rectTransform,
            205, 83, 1031, 866);
        dialog.GetComponent<Image>().raycastTarget = true;

        var title = Theme.Text("Title", helpLayout, font, Theme.Fig(60), Theme.Cyan, TextAlignmentOptions.Center, "TUTORIAL");
        At(title.rectTransform, 205, 102, 1031, 70);

        var viewport = At(Theme.Node("Tutorial viewport", helpLayout), 245, 185, 932, 748);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Theme.Node("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
        int sidePadding = Mathf.RoundToInt(Theme.Fig(16));
        layoutGroup.padding = new RectOffset(sidePadding, sidePadding, 20, 16);
        layoutGroup.childControlWidth = layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var text = Theme.Text("Text", content, font, Theme.Fig(40), Theme.White, TextAlignmentOptions.TopLeft, OceanTutorial.ReferenceText);
        text.richText = true;
        text.fontStyle = FontStyles.Normal;
        text.outlineWidth = 0f;
        if (text.fontMaterial != null && text.fontMaterial.HasProperty(ShaderUtilities.ID_FaceDilate))
            text.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);

        helpScroll = viewport.gameObject.AddComponent<ScrollRect>();
        helpScroll.viewport = viewport;
        helpScroll.content = content;
        helpScroll.horizontal = false;
        helpScroll.vertical = true;
        helpScroll.movementType = ScrollRect.MovementType.Clamped;
        helpScroll.scrollSensitivity = 36f;
        helpScroll.verticalScrollbar = BuildScrollbar("Tutorial scrollbar", helpLayout, 1196, 189, 18, 742);

        var back = FrameButton("Back", helpLayout, "BACK", Theme.PanelFill, Theme.Cyan, Theme.White, Theme.Fig(34), CloseHelp);
        At((RectTransform)back.transform, 1264, 67, 156, 97);

        helpPanel.gameObject.SetActive(false);
    }

    private static Scrollbar BuildScrollbar(string name, RectTransform parent, float x, float y, float width, float height)
    {
        var track = At(Theme.Box(name, parent, Theme.Navy, Theme.Cyan, Theme.Fig(width * .5f), 1.5f).rectTransform, x, y, width, height);
        track.GetComponent<Image>().raycastTarget = true;
        var area = Theme.Node("Sliding Area", track).Fill(Theme.Fig(2), Theme.Fig(2), Theme.Fig(2), Theme.Fig(2));
        var handle = Theme.Box("Scroll handle", area, Theme.Cyan, null, Theme.Fig((width - 4f) * .5f)).rectTransform.Fill();
        var handleImage = handle.GetComponent<Image>();
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
        helpScroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>Returns to the inventory without changing simulation time.</summary>
    private void CloseHelp() => helpPanel.gameObject.SetActive(false);

    private void ShowItem(InventorySlotUI slot)
    {
        var item = ItemCatalog.Find(slot.ItemName);
        if (item == null) return;
        inspectedItem = item.Id;
        if (detailName != null) detailName.text = item.Name;
        if (details != null)
        {
            details.color = Theme.White;
            details.text = item.Description;
        }
        if (detailWeightRow != null) detailWeightRow.gameObject.SetActive(true);
        if (detailWeight != null) detailWeight.text = $"{item.Weight} lbs";
        if (detailQuantity != null) detailQuantity.text = $"Quantity: {ocean.CarriedCount(item.Id)}";
        if (detailArtBox != null)
        {
            detailArtBox.gameObject.SetActive(true);
            icons.TryGetValue(item.Id, out var sprite);
            detailIcon.sprite = sprite;
            detailIcon.enabled = sprite != null;
            detailArtBg.enabled = sprite == null;
            detailArtPlaceholder.gameObject.SetActive(sprite == null);
        }
    }

    private void ShowEmptyDetails()
    {
        inspectedItem = null;
        if (detailArtBox != null) detailArtBox.gameObject.SetActive(false);
        if (detailName != null) detailName.text = ocean.DepositOpen ? "DEPOSIT ITEMS" : "INVENTORY";
        if (details != null)
        {
            details.color = Theme.White;
            details.text = ocean.DepositOpen
                ? "Deposited items cannot be retrieved during this excursion.\nClick an item to deposit  •  TAB to continue exploring"
                : "Hover over an item for details.\nClick an item to drop.";
        }
        if (detailWeightRow != null) detailWeightRow.gameObject.SetActive(false);
        if (detailQuantity != null) detailQuantity.text = "";
    }

    private void OnSlotClicked(InventorySlotUI slot)
    {
        ShowItem(slot);
        SelectSlot(slot);
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

    private void SelectSlot(InventorySlotUI slot)
    {
        if (selectedSlot != null && selectedSlot != slot) selectedSlot.SetSelected(false);
        selectedSlot = slot;
        if (selectedSlot != null) selectedSlot.SetSelected(!string.IsNullOrEmpty(selectedSlot.ItemName));
    }

    private void RefreshDrop()
    {
        dropCount = Mathf.Clamp(dropCount, 1, dropLimit);
        if (dropCountText != null) dropCountText.text = dropCount.ToString();
        less.interactable = dropCount > 1;
        more.interactable = max.interactable = dropCount < dropLimit;
        lessVisual?.Refresh();
        moreVisual?.Refresh();
        maxVisual?.Refresh();
    }

    private void ConfirmDrop()
    {
        bool transferred = ocean.DepositOpen ? ocean.DepositItem(dropItem, dropCount) : ocean.DropItem(dropItem, dropCount);
        if (transferred) CancelDrop();
        else if (details != null)
        {
            details.color = Theme.Pink;
            details.text = ocean.Message;
        }
        Refresh();
    }

    private void CancelDrop()
    {
        dropItem = null;
        if (dropControls != null) dropControls.gameObject.SetActive(false);
        SelectSlot(null);
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
        EnsureUiBuilt();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        ApplyModeLayout(ocean.DepositOpen);
        CancelDrop();
        if (tankHeader != null)
        {
            string tankName = ItemCatalog.Find(ocean.Loadout.Tank)?.Name ?? ocean.Loadout.Tank;
            tankHeader.text = $"CURRENT HYDROX TANK: {tankName.ToUpperInvariant()}";
        }
        ShowEmptyDetails();
        Refresh();
        if (gridScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            gridScroll.StopMovement();
            gridScroll.verticalNormalizedPosition = 1f;
        }
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

        if (weightLabel != null && weightBarFill != null)
        {
            int weight = ocean.CarriedWeight;
            int capacity = ocean.WeightCapacity;
            weightLabel.text = $"{weight} / {capacity} lbs";
            weightBarFill.rectTransform.anchorMax = new Vector2(capacity > 0 ? Mathf.Clamp01((float)weight / capacity) : 1f, 1f);
            weightBarFill.color = Theme.WeightColor(weight, capacity);
            weightBarFill.enabled = weight > 0;
        }

        if (inspectedItem != null && detailQuantity != null)
        {
            int carried = ocean.CarriedCount(inspectedItem);
            if (carried > 0) detailQuantity.text = $"Quantity: {carried}";
            else ShowEmptyDetails();
        }
    }

    private static RectTransform At(RectTransform rect, float x, float y, float width, float height)
        => rect.Place(new Vector2(0, 1), new Vector2(0, 1), Theme.Fig(x, -y), Theme.Fig(width, height));

    private static void HorizontalRule(RectTransform parent, bool topEdge, float width, float thickness, Color color)
    {
        var line = Theme.Node("Rule", parent).gameObject.AddComponent<Image>();
        var anchor = topEdge ? new Vector2(0, 1) : Vector2.zero;
        line.rectTransform.Place(anchor, anchor, Vector2.zero, new Vector2(Theme.Fig(width), thickness));
        line.color = color;
        line.raycastTarget = false;
    }

    private UnityEngine.UI.Button FrameButton(string name, RectTransform parent, string caption, Color fill, Color border,
        Color textColor, float fontSize, UnityEngine.Events.UnityAction action)
        => FrameButton(name, parent, caption, fill, border, textColor, fontSize, action, out _);

    private UnityEngine.UI.Button FrameButton(string name, RectTransform parent, string caption, Color fill, Color border,
        Color textColor, float fontSize, UnityEngine.Events.UnityAction action, out FrameButtonStyle visual)
    {
        var frame = Theme.Frame(name, parent, fill, border, 2f);
        frame.raycastTarget = true;
        var button = frame.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = frame;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);
        var label = Theme.Text("Label", frame.transform, font, fontSize, textColor, TextAlignmentOptions.Center, caption);
        label.rectTransform.Fill(4, 2, 4, 2);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        visual = frame.gameObject.AddComponent<FrameButtonStyle>();
        visual.Initialize(button, frame, label, fill, border, textColor);
        return button;
    }

    /// <summary>Handles hover and normal color palettes for square-cornered Figma buttons.</summary>
    private sealed class FrameButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private UnityEngine.UI.Button button;
        private Image frame;
        private TMP_Text label;
        private Color idleFill, idleBorder, idleText, hoverFill, hoverBorder, hoverText;
        private bool hovered;

        public void Initialize(UnityEngine.UI.Button button, Image frame, TMP_Text label,
            Color idleFill, Color idleBorder, Color idleText)
        {
            this.button = button;
            this.frame = frame;
            this.label = label;
            SetPalette(idleFill, idleBorder, idleText, Color.Lerp(idleFill, idleBorder, .28f), idleBorder, idleText);
        }

        public void SetPalette(Color idleFill, Color idleBorder, Color idleText,
            Color hoverFill, Color hoverBorder, Color hoverText)
        {
            this.idleFill = idleFill;
            this.idleBorder = idleBorder;
            this.idleText = idleText;
            this.hoverFill = hoverFill;
            this.hoverBorder = hoverBorder;
            this.hoverText = hoverText;
            Apply();
        }

        public void Refresh() => Apply();
        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; Apply(); }
        private void OnDisable() { hovered = false; Apply(); }

        private void Apply()
        {
            if (frame == null) return;
            bool interactable = button == null || button.interactable;
            if (!interactable)
            {
                frame.color = idleFill.WithAlpha(idleFill.a * .5f);
                Theme.SetFrameColor(frame, idleBorder.WithAlpha(.35f));
                if (label != null) label.color = Theme.DisabledText;
                return;
            }
            frame.color = hovered ? hoverFill : idleFill;
            Theme.SetFrameColor(frame, hovered ? hoverBorder : idleBorder);
            if (label != null) label.color = hovered ? hoverText : idleText;
        }
    }
}
