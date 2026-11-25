using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace DivingPrototype
{
    /// <summary>
    /// Excursion HUD. Split across partial files by screen:
    /// Loadout (equip screen and its pre-dive prompts), Status (tank, weight, item slots, low hydrox),
    /// Markers (minimap and off-screen prospect arrows), Return (submarine confirmation),
    /// Tutorial (tutorial boxes and interaction prompt), Failure (incapacitation screen).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed partial class OceanHud : MonoBehaviour
    {
        public OceanInteractions interactions;
        public TMP_Text status, prompt, message, interior;
        public RectTransform hydroxRoot, hydroxFill;
        [FormerlySerializedAs("radarRoot")] public RectTransform minimapRoot;
        public Sprite arrowSprite, circleSprite, squareSprite;
        public TMP_FontAsset font;
        [Min(1f)] public float minimapRange = 25f;
        private OceanTutorial tutorial;

        /// <summary>True while the excursion inventory (or deposit) panel covers the HUD.</summary>
        private bool InventoryOpen => interactions.panel != null && interactions.panel.activeSelf;

        internal UnityEngine.UI.Button Button(RectTransform parent, string caption, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(caption, position, size);
            rect.SetParent(parent, false);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.08f, .22f, .27f, 1f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(action);
            var label = PanelText(rect, 20);
            label.alignment = TextAlignmentOptions.Center;
            label.text = caption;
            label.rectTransform.offsetMin = new Vector2(4, 2);
            label.rectTransform.offsetMax = new Vector2(-4, -2);
            return button;
        }

        /// <summary>A full-screen panel under the HUD canvas.</summary>
        private RectTransform Overlay(string name)
        {
            var rect = Panel(name, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Creates a centered dialog and a backdrop that captures input across the viewport.</summary>
        private RectTransform BuildCenteredModal(string title, Vector2 size, float backdropOpacity, out RectTransform overlay)
        {
            overlay = Overlay(title);
            var shade = overlay.GetComponent<UnityEngine.UI.Image>();
            shade.color = new Color(0, 0, 0, backdropOpacity);
            shade.raycastTarget = true;
            var dialog = Panel("Dialog", Vector2.zero, size);
            dialog.SetParent(overlay, false);
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            overlay.gameObject.SetActive(false);
            return dialog;
        }

        internal RectTransform Panel(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(status.transform.parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.015f,.04f,.07f,.95f);
            image.raycastTarget = false;
            return rect;
        }

        internal TMP_Text PanelText(RectTransform panel, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var text = go.GetComponent<TMP_Text>();
            text.rectTransform.SetParent(panel, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16, 10);
            text.rectTransform.offsetMax = new Vector2(-16, -10);
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>A full-screen container for themed screens, parented like the legacy panels.</summary>
        private RectTransform ThemedScreen(string name)
        {
            var rect = OceanUiTheme.Node(name, status.transform.parent).Fill();
            rect.gameObject.SetActive(false);
            return rect;
        }

        /// <summary>
        /// A Figma-style alert: dimmed backdrop that captures input, rounded slate dialog with a cyan outline,
        /// and an optional "!" warning triangle centered at the top. Returns the dialog; children are placed from its top.
        /// </summary>
        private RectTransform BuildAlert(string name, Vector2 size, bool warningIcon, out RectTransform overlay, float backdropOpacity = .55f,
            float warningSize = 62f, float warningTop = 18f)
        {
            overlay = ThemedScreen(name);
            var shade = overlay.gameObject.AddComponent<UnityEngine.UI.Image>();
            shade.color = new Color(0, 0, 0, backdropOpacity);
            shade.raycastTarget = true;
            var dialog = OceanUiTheme.Box("Dialog", overlay, OceanUiTheme.PopupFill, OceanUiTheme.Cyan, 10f).rectTransform;
            dialog.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, size);
            dialog.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            if (warningIcon)
                OceanUiTheme.WarningIcon(dialog, font, warningSize, OceanUiTheme.Cyan, OceanUiTheme.Navy)
                    .Place(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -warningTop), Vector2.one * warningSize);
            return dialog;
        }

        /// <summary>Adds a text block to an alert, measured from the dialog's top edge.</summary>
        private TMP_Text AlertText(RectTransform dialog, float top, float height, float size, string value = "")
        {
            var text = OceanUiTheme.Text("Text", dialog, font, size, OceanUiTheme.White, TextAlignmentOptions.Center, value);
            text.rectTransform.anchorMin = new Vector2(0, 1);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(.5f, 1);
            text.rectTransform.offsetMin = new Vector2(28, -top - height);
            text.rectTransform.offsetMax = new Vector2(-28, -top);
            return text;
        }

        /// <summary>Adds a centered button to an alert, measured from the dialog's top edge.</summary>
        private UnityEngine.UI.Button AlertButton(RectTransform dialog, string caption, float top, Vector2 size,
            OceanUiTheme.ButtonStyle style, UnityEngine.Events.UnityAction action, float x = 0f)
        {
            var button = OceanUiTheme.Button(caption, dialog, font, 24f, style, action);
            ((RectTransform)button.transform).Place(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(x, -top), size);
            return button;
        }

        private void Awake() => font = OceanUiTheme.Font(font);

        private void Start()
        {
            tutorial = interactions.GetComponent<OceanTutorial>();
            BuildStatus();
            BuildTutorial();
            BuildInventoryFull();
            BuildMinimap();
            BuildProspectMarkers();
            BuildReturnConfirmation();
            BuildLoadout();
            BuildSafetyOffer();
            BuildTutorialPrompt();
            BuildFailure();
        }

        private void LateUpdate()
        {
            if (!MinimapBuilt) return;
            UpdateProspectMarkers();
            if (PauseManager.BlocksInput) return;
            UpdateReturnConfirmation();
            UpdateFailure();
            if (interactions.IsTransitioning) return;
            UpdatePreDivePrompts();
            UpdateStatus();
            UpdateTutorial();
            UpdatePrompt();
            UpdateInventoryFull();
            if (!interactions.InsideSubmarine) UpdateMinimap();
        }
    }
}
