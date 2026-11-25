using System;
using DivingPrototype;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler
{
    public Image icon;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI nameText;

    public string ItemName { get; private set; }
    public Action<InventorySlotUI> onClicked;
    public Action<InventorySlotUI> onHovered;
    public void OnPointerEnter(PointerEventData eventData) => onHovered?.Invoke(this);

    private static readonly Color SlotFill = Theme.Hex("354E5B");
    private static readonly Color SlotSelectedFill = Theme.Hex("88B9BA");
    private static readonly Color EmptyFill = Theme.PanelFill.WithAlpha(.55f);
    private static readonly Color EmptyBorder = Theme.Cyan.WithAlpha(.35f);

    private Button button;
    private Image frameImage, artBg;
    private Image[] borderEdges;
    private RectTransform artRect;
    private bool initialized, selected;

    void Awake() => EnsureInitialized();

    /// <summary>Applies the Figma slot frame, inner art square, Jersey 20 font, and count layout at runtime.</summary>
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        button = GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(.88f, .88f, .88f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => onClicked?.Invoke(this));

        frameImage = button.targetGraphic as Image;
        if (frameImage != null)
        {
            frameImage.sprite = null;
            frameImage.type = Image.Type.Simple;
        }

        artRect = Theme.Node("Art", transform).Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Theme.Fig(68, 68));
        artBg = artRect.gameObject.AddComponent<Image>();
        artBg.color = Theme.Cyan;
        artBg.raycastTarget = false;

        if (icon != null)
        {
            icon.rectTransform.SetParent(artRect, false);
            icon.rectTransform.Fill();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        var font = Theme.Font(countText != null ? countText.font : null);
        if (nameText != null)
        {
            nameText.rectTransform.SetParent(artRect, false);
            nameText.rectTransform.Fill(3, 3, 3, 3);
            nameText.font = font;
            nameText.color = Theme.Navy;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 7f;
            nameText.fontSizeMax = 13f;
            nameText.textWrappingMode = TextWrappingModes.Normal;
            nameText.raycastTarget = false;
        }

        borderEdges = CreateBorder(transform, 2f);

        if (countText != null)
        {
            countText.rectTransform.SetAsLastSibling();
            countText.rectTransform.Fill(4, 1, 5, 4);
            countText.font = font;
            countText.color = Theme.White;
            countText.fontSize = Theme.Fig(34);
            countText.alignment = TextAlignmentOptions.BottomRight;
            countText.textWrappingMode = TextWrappingModes.NoWrap;
            countText.raycastTarget = false;
        }

        ApplyVisual();
    }

    public void Set(string itemName, int count, Sprite sprite)
    {
        EnsureInitialized();
        ItemName = itemName;
        button.interactable = true;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        artRect.gameObject.SetActive(true);
        artBg.enabled = sprite == null;
        nameText.text = sprite == null ? ItemCatalog.Find(itemName)?.Name ?? itemName : "";
        countText.text = count > 1 ? count.ToString() : "";
        ApplyVisual();
    }

    public void Clear()
    {
        EnsureInitialized();
        ItemName = null;
        selected = false;
        button.interactable = false;
        icon.enabled = false;
        artBg.enabled = false;
        artRect.gameObject.SetActive(false);
        nameText.text = "";
        countText.text = "";
        ApplyVisual();
    }

    /// <summary>Highlights the slot currently targeted for drop or deposit.</summary>
    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        if (initialized) ApplyVisual();
    }

    private void ApplyVisual()
    {
        bool occupied = !string.IsNullOrEmpty(ItemName);
        if (frameImage != null)
            frameImage.color = !occupied ? EmptyFill : selected ? SlotSelectedFill : SlotFill;
        var borderColor = !occupied ? EmptyBorder : selected ? Theme.White : Theme.Cyan;
        if (borderEdges != null)
            for (int i = 0; i < borderEdges.Length; i++)
                borderEdges[i].color = borderColor;
    }

    private static Image[] CreateBorder(Transform parent, float thickness)
    {
        var border = Theme.Node("Border", parent).Fill();
        return new[]
        {
            BorderEdge(border, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, thickness)),
            BorderEdge(border, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, thickness)),
            BorderEdge(border, new Vector2(0, 0), new Vector2(0, 1), new Vector2(thickness, 0)),
            BorderEdge(border, new Vector2(1, 0), new Vector2(1, 1), new Vector2(thickness, 0))
        };
    }

    private static Image BorderEdge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var edge = Theme.Node("Edge", parent).gameObject.AddComponent<Image>();
        edge.rectTransform.anchorMin = anchorMin;
        edge.rectTransform.anchorMax = anchorMax;
        edge.rectTransform.pivot = anchorMin;
        edge.rectTransform.sizeDelta = size;
        edge.rectTransform.anchoredPosition = Vector2.zero;
        edge.raycastTarget = false;
        return edge;
    }
}
