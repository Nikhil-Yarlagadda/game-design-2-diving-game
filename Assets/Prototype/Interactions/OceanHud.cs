using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace DivingPrototype
{
    [DefaultExecutionOrder(200)]
    public sealed class OceanHud : MonoBehaviour
    {
        public OceanInteractions interactions;
        public TMP_Text status, prompt, message, interior;
        public RectTransform hydroxRoot, hydroxFill;
        [FormerlySerializedAs("radarRoot")] public RectTransform minimapRoot;
        public Sprite arrowSprite, circleSprite, squareSprite;
        public TMP_FontAsset font;
        [Min(1f)] public float minimapRange = 25f;
        private UnityEngine.UI.Image[] markers;
        private RectTransform playerDirection;
        private TMP_Text submarineDistance;
        private OceanTutorial tutorial;
        private TMP_Text tutorialText, scanText;
        private RectTransform tutorialPanel, scanPanel, scanFill;
        private UnityEngine.UI.Outline hydroxHighlight;
        private RectTransform returnOverlay;
        private TMP_Text returnWarning;
        private UnityEngine.UI.Button endExcursionButton;
        private RectTransform safetyOverlay, prospectOverlay;
        private TMP_Text safetyText;
        private readonly List<(OceanTarget target, RectTransform root, UnityEngine.UI.Image arrow, TMP_Text label)> prospectMarkers = new();
        private readonly HashSet<Vector2Int> occupiedMarkerCells = new();
        private readonly List<Rect> markerExclusions = new();
        private RectTransform loadoutOverlay, weightFill;
        private UnityEngine.UI.Image lowGasTint, weightColor;
        private TMP_Text loadoutWeight, tankLabel, weightLabel, lowGasText;
        private UnityEngine.UI.Button diveButton;
        private readonly System.Collections.Generic.List<System.Action> loadoutLabels = new();

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

        private RectTransform Overlay(string name)
        {
            var rect = Panel(name, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void BuildReturnConfirmation()
        {
            returnOverlay = Overlay("Return Confirmation");
            var backdrop = returnOverlay.GetComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(0, 0, 0, .75f);
            backdrop.raycastTarget = true;
            var dialog = Panel("Dialog", Vector2.zero, new Vector2(650, 260));
            dialog.SetParent(returnOverlay, false);
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            dialog.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var question = PanelText(dialog, 24);
            question.rectTransform.offsetMin = new Vector2(24, 168);
            question.rectTransform.offsetMax = new Vector2(-24, -24);
            question.text = "Return to the submarine";
            returnWarning = PanelText(dialog, 20);
            returnWarning.rectTransform.offsetMin = new Vector2(24, 84);
            returnWarning.rectTransform.offsetMax = new Vector2(-24, -96);
            var inventory = FindAnyObjectByType<ExcursionInventory>();
            Button(dialog, "Continue exploring", new Vector2(-205, 24), new Vector2(194, 48), () => interactions.CancelReturn());
            Button(dialog, "Deposit items", new Vector2(0, 24), new Vector2(194, 48), () => inventory?.OpenDeposit())
                .interactable = inventory != null;
            endExcursionButton = Button(dialog, "End excursion", new Vector2(205, 24), new Vector2(194, 48), () => interactions.ConfirmReturn());
            returnOverlay.gameObject.SetActive(false);
        }

        private void BuildSafetyOffer()
        {
            if (!interactions.SafetyOfferOpen) return;
            safetyOverlay = Overlay("Safety Mining Machines");
            var backdrop = safetyOverlay.GetComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(0, 0, 0, .82f);
            backdrop.raycastTarget = true;
            var dialog = Panel("Dialog", Vector2.zero, new Vector2(510, 250));
            dialog.SetParent(safetyOverlay, false);
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            safetyText = PanelText(dialog, 22);
            safetyText.enableAutoSizing = true;
            safetyText.fontSizeMin = 17;
            safetyText.fontSizeMax = 22;
            safetyText.rectTransform.offsetMin = new Vector2(28, 70);
            safetyText.rectTransform.offsetMax = new Vector2(-28, -14);
            Button(dialog, "Yes", new Vector2(-85, 20), new Vector2(145, 45), () => interactions.ResolveSafetyOffer(true));
            Button(dialog, "No", new Vector2(85, 20), new Vector2(145, 45), () => interactions.ResolveSafetyOffer(false));
        }

        private void BuildLoadout()
        {
            loadoutOverlay = Overlay("Excursion Loadout");
            var backdrop = loadoutOverlay.GetComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(0, 0, 0, .82f);
            backdrop.raycastTarget = true;
            var dialog = Panel("Equipment", new Vector2(0, -220), new Vector2(680, 440));
            dialog.SetParent(loadoutOverlay, false);
            dialog.anchorMin = dialog.anchorMax = new Vector2(.5f, .5f);
            var title = PanelText(dialog, 28);
            title.rectTransform.offsetMin = new Vector2(24, 376);
            title.rectTransform.offsetMax = new Vector2(-24, -12);
            title.text = "PREPARE FOR EXCURSION";
            string[] tanks = { ItemCatalog.LightTank, ItemCatalog.MediumTank, ItemCatalog.HeavyTank };
            for (int i = 0; i < tanks.Length; i++)
            {
                string tank = tanks[i];
                var gear = ItemCatalog.Find(tank);
                string caption = $"{gear.Name.Split(' ')[0]}  •  {gear.TankSeconds / 60} min\nCapacity {gear.WeightCapacity}";
                var button = Button(dialog, caption, new Vector2((i - 1) * 204, 318), new Vector2(192, 46),
                    () => { var pick = interactions.Loadout; pick.Tank = tank; interactions.SetLoadout(pick); });
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 16;
                loadoutLabels.Add(() =>
                {
                    bool selected = interactions.Loadout.Tank == tank;
                    button.interactable = !selected && ItemCatalog.Count(GameState.inventory, tank) > 0;
                    label.text = selected ? caption + "  [Equipped]" : caption;
                });
            }
            BuildLoadoutCount(dialog, ItemCatalog.Machine, 252);
            BuildLoadoutCount(dialog, ItemCatalog.Stimulant, 194);
            BuildLoadoutToggle(dialog, ItemCatalog.Flippers, -153);
            BuildLoadoutToggle(dialog, ItemCatalog.Camera, 153);
            loadoutWeight = PanelText(dialog, 20);
            loadoutWeight.rectTransform.offsetMin = new Vector2(34, 83);
            loadoutWeight.rectTransform.offsetMax = new Vector2(-34, -318);
            diveButton = Button(dialog, "Begin dive", new Vector2(0, 24), new Vector2(244, 48),
                () => interactions.BeginDive());
        }

        private void BuildLoadoutCount(RectTransform dialog, string item, float y)
        {
            bool machine = item == ItemCatalog.Machine;
            var row = Panel(item, new Vector2(-65, y), new Vector2(460, 44));
            row.SetParent(dialog, false);
            var label = PanelText(row, 20);
            void Adjust(int delta)
            {
                var pick = interactions.Loadout;
                if (machine) pick.Machines += delta;
                else pick.Stimulants += delta;
                interactions.SetLoadout(pick);
            }
            var minus = Button(dialog, "−", new Vector2(202, y), new Vector2(55, 44), () => Adjust(-1));
            var plus = Button(dialog, "+", new Vector2(270, y), new Vector2(55, 44), () => Adjust(1));
            loadoutLabels.Add(() =>
            {
                int count = machine ? interactions.Loadout.Machines : interactions.Loadout.Stimulants;
                int owned = ItemCatalog.Count(GameState.inventory, item);
                label.text = $"{ItemCatalog.Find(item).Name}    {count} / {owned}";
                minus.interactable = count > 0;
                plus.interactable = count < owned;
            });
        }

        private void BuildLoadoutToggle(RectTransform dialog, string item, float x)
        {
            bool flippers = item == ItemCatalog.Flippers;
            var button = Button(dialog, item, new Vector2(x, 134), new Vector2(294, 44), () =>
            {
                var pick = interactions.Loadout;
                if (flippers) pick.Flippers = !pick.Flippers;
                else pick.Camera = !pick.Camera;
                interactions.SetLoadout(pick);
            });
            var label = button.GetComponentInChildren<TMP_Text>();
            loadoutLabels.Add(() =>
            {
                button.interactable = ItemCatalog.Count(GameState.inventory, item) > 0;
                bool selected = flippers ? interactions.Loadout.Flippers : interactions.Loadout.Camera;
                label.text = item + (selected ? "  [Equipped]" : button.interactable ? "" : "  •  Not owned");
            });
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

        public static Vector2 MinimapPosition(Vector2 offset, float range, float radius)
            => Vector2.ClampMagnitude(offset / Mathf.Max(.001f, range), 1f) * radius;

        private UnityEngine.UI.Image Image(string name, Sprite sprite, float size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.rectTransform.SetParent(minimapRoot, false);
            image.rectTransform.sizeDelta = Vector2.one * size;
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(string name, float size, Vector2 position)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.rectTransform.SetParent(minimapRoot, false);
            text.rectTransform.sizeDelta = new Vector2(180, 24);
            text.rectTransform.anchoredPosition = position;
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.outlineWidth = .15f;
            text.outlineColor = Color.black;
            return text;
        }

        private void Start()
        {
            var tint = Overlay("Low Hydrox Tint");
            tint.SetAsFirstSibling();
            lowGasTint = tint.GetComponent<UnityEngine.UI.Image>();
            lowGasTint.color = Color.clear;
            var warning = Panel("Hydrox Warning", Vector2.zero, new Vector2(560, 50));
            warning.anchorMin = warning.anchorMax = new Vector2(.5f, .5f);
            warning.anchoredPosition = new Vector2(0, 80);
            lowGasText = PanelText(warning, 22);
            lowGasText.alignment = TextAlignmentOptions.Center;
            warning.gameObject.SetActive(false);
            var weight = Panel("Weight", new Vector2(20, -46), new Vector2(240, 23));
            weight.anchorMin = weight.anchorMax = new Vector2(0, 1);
            weight.pivot = new Vector2(0, 1);
            weight.anchoredPosition = new Vector2(20, -46);
            weightFill = Panel("Weight Fill", Vector2.zero, Vector2.zero);
            weightFill.SetParent(weight, false);
            weightFill.anchorMin = Vector2.zero;
            weightFill.offsetMin = weightFill.offsetMax = Vector2.zero;
            weightColor = weightFill.GetComponent<UnityEngine.UI.Image>();
            weightLabel = PanelText(weight, 15);
            weightLabel.alignment = TextAlignmentOptions.Center;
            weightLabel.outlineWidth = .2f;
            weightLabel.rectTransform.offsetMin = weightLabel.rectTransform.offsetMax = Vector2.zero;
            tankLabel = PanelText(hydroxRoot, 15);
            tankLabel.alignment = TextAlignmentOptions.Center;
            tankLabel.outlineWidth = .2f;
            tankLabel.rectTransform.offsetMin = tankLabel.rectTransform.offsetMax = Vector2.zero;
            tutorial = interactions.GetComponent<OceanTutorial>();
            tutorialPanel = Panel("Excursion Tutorial", new Vector2(0, 115), new Vector2(700, 160));
            tutorialText = PanelText(tutorialPanel, 19);
            tutorialText.fontSizeMin = 16;
            tutorialText.fontSizeMax = 19;
            tutorialText.enableAutoSizing = true;
            hydroxHighlight = hydroxRoot.gameObject.AddComponent<UnityEngine.UI.Outline>();
            hydroxHighlight.effectColor = new Color(1f, .8f, .35f);
            hydroxHighlight.effectDistance = new Vector2(3f, 3f);
            hydroxHighlight.enabled = false;
            scanPanel = Panel("Prospecting Progress", new Vector2(0, 285), new Vector2(360, 42));
            scanFill = Panel("Scan Fill", Vector2.zero, Vector2.zero);
            scanFill.SetParent(scanPanel, false);
            scanFill.anchorMin = Vector2.zero;
            scanFill.anchorMax = new Vector2(0, 1);
            scanFill.offsetMin = scanFill.offsetMax = Vector2.zero;
            scanFill.GetComponent<UnityEngine.UI.Image>().color = new Color(.1f,.4f,.5f,.9f);
            scanText = PanelText(scanPanel, 18);
            scanText.alignment = TextAlignmentOptions.Center;
            float diameter = minimapRoot.rect.width;
            Image("Minimap Border", circleSprite, diameter, new Color(.4f, .65f, .7f, .85f));
            Image("Minimap Background", circleSprite, diameter - 4f, new Color(.015f, .04f, .055f, .96f));
            Label("Minimap North", 13, new Vector2(0, diameter * .5f + 13)).text = "N";
            submarineDistance = Label("Submarine Distance", 14, new Vector2(0, -diameter * .5f - 17));
            submarineDistance.color = new Color(.55f, .9f, 1f);
            AddMinimapMarkers();
            Image("Minimap Player", circleSprite, 6, Color.white);
            playerDirection = Image("Minimap Facing", arrowSprite, 7, Color.white).rectTransform;
            playerDirection.sizeDelta = new Vector2(6,9);
            BuildProspectMarkers();
            BuildReturnConfirmation();
            BuildLoadout();
            BuildSafetyOffer();
        }

        private void AddMinimapMarkers()
        {
            int start = markers?.Length ?? 0;
            System.Array.Resize(ref markers, interactions.targets.Length);
            for (int i = start; i < markers.Length; i++)
            {
                var target = interactions.targets[i];
                if (target == null) continue;
                if (target.kind == OceanTarget.TargetKind.Corpse || target.kind == OceanTarget.TargetKind.AbyssEye) continue;
                bool machine = target.kind == OceanTarget.TargetKind.MiningSpot;
                bool submarine = target.kind == OceanTarget.TargetKind.Submarine;
                markers[i] = Image(target.name + (machine ? " Machine" : "") + " Minimap Marker", submarine || machine ? squareSprite : circleSprite,
                    submarine ? 7 : machine ? 6 : 4, submarine ? new Color(.55f, .9f, 1f) : machine ? new Color(1f, .65f, .2f) : Color.white);
                if (submarine) markers[i].rectTransform.localRotation = Quaternion.Euler(0,0,45);
                if (machine) markers[i].gameObject.SetActive(target.HasMachine);
            }
            // Keep the home marker above overlapping item dots without changing its bearing.
            foreach (var marker in markers)
                if (marker != null && marker.name.StartsWith("Submarine")) marker.transform.SetAsLastSibling();
            if (playerDirection != null) playerDirection.SetAsLastSibling();
        }

        private void BuildProspectMarkers()
        {
            prospectOverlay = Overlay("Prospected Spots");
            prospectOverlay.pivot = Vector2.one * .5f;
            prospectOverlay.offsetMin = prospectOverlay.offsetMax = Vector2.zero;
            prospectOverlay.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            foreach (var target in interactions.targets)
            {
                if (target == null || target.kind != OceanTarget.TargetKind.MiningSpot) continue;
                var root = Panel("Prospected Spot", Vector2.zero, new Vector2(88, 50));
                root.SetParent(prospectOverlay, false);
                root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
                root.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                var arrow = Image("Direction", arrowSprite, 17, Color.white);
                arrow.rectTransform.SetParent(root, false);
                arrow.rectTransform.sizeDelta = new Vector2(13, 19);
                arrow.rectTransform.anchoredPosition = new Vector2(0, 15);
                var label = PanelText(root, 13);
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = new Vector2(0, -20);
                label.outlineWidth = .22f;
                label.outlineColor = Color.black;
                prospectMarkers.Add((target, root, arrow, label));
            }
            prospectOverlay.gameObject.SetActive(false);
        }

        private void UpdateProspectMarkers()
        {
            bool show = interactions.ProspectMarkersEnabled && !PauseManager.BlocksInput && Time.timeScale > 0f
                && !interactions.LoadoutOpen && !interactions.InsideSubmarine && !interactions.IsTransitioning
                && !SceneChanger.IsTransitioning && !interactions.Incapacitated;
            prospectOverlay.gameObject.SetActive(show);
            if (!show) return;
            Vector2 half = prospectOverlay.rect.size * .5f;
            Vector2 edge = Vector2.Max(half - new Vector2(47, 28), Vector2.one);
            Vector2 player = ((Vector2)interactions.oceanCamera.WorldToViewportPoint(interactions.transform.position) * 2f - Vector2.one) * half;
            var origin = new Vector2(Mathf.Clamp(player.x, -edge.x, edge.x), Mathf.Clamp(player.y, -edge.y, edge.y));
            occupiedMarkerCells.Clear();
            markerExclusions.Clear();
            var statusBox = status.rectTransform;
            Vector2 statusCorner = prospectOverlay.InverseTransformPoint(statusBox.TransformPoint(statusBox.rect.max));
            markerExclusions.Add(Rect.MinMaxRect(-half.x, statusCorner.y - Mathf.Max(statusBox.rect.height, status.preferredHeight),
                statusCorner.x, half.y));
            Vector2 mapCorner = prospectOverlay.InverseTransformPoint(minimapRoot.TransformPoint(minimapRoot.rect.min));
            markerExclusions.Add(Rect.MinMaxRect(mapCorner.x - 8, mapCorner.y - 32, half.x, half.y));
            if (tutorialPanel.gameObject.activeInHierarchy)
                markerExclusions.Add(HudBounds(tutorialPanel, tutorialPanel.rect.min, tutorialPanel.rect.max));
            if (!string.IsNullOrEmpty(prompt.text))
                markerExclusions.Add(HudBounds(prompt.rectTransform, prompt.textBounds.min, prompt.textBounds.max));
            foreach (var marker in prospectMarkers)
            {
                var target = marker.target;
                Vector3 viewport = interactions.oceanCamera.WorldToViewportPoint(target.transform.position);
                bool offscreen = viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1;
                bool visible = target.isActiveAndEnabled && target.IsProspected && offscreen;
                marker.root.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector2 direction = ((Vector2)viewport * 2f - Vector2.one) * half - origin;
                float x = direction.x == 0 ? float.PositiveInfinity : ((direction.x > 0 ? edge.x : -edge.x) - origin.x) / direction.x;
                float y = direction.y == 0 ? float.PositiveInfinity : ((direction.y > 0 ? edge.y : -edge.y) - origin.y) / direction.y;
                Vector2 desired = origin + direction * Mathf.Min(x, y);
                marker.root.anchoredPosition = PlaceProspectMarker(desired, edge, x <= y);
                bool bottom = y < x && direction.y < 0f;
                marker.arrow.rectTransform.anchoredPosition = new Vector2(0, bottom ? -15 : 15);
                marker.label.rectTransform.offsetMin = new Vector2(0, bottom ? 20 : 0);
                marker.label.rectTransform.offsetMax = new Vector2(0, bottom ? 0 : -20);
                Vector2 bearing = origin + direction - player;
                marker.arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(bearing.y, bearing.x) * Mathf.Rad2Deg - 90f);
                float distance = Vector2.Distance(interactions.transform.position, target.transform.position);
                marker.label.text = $"Tier {target.DisplayedTier}\n{distance:0} m";
                marker.arrow.color = marker.label.color = target.TierColor;
            }
        }

        private Rect HudBounds(RectTransform element, Vector2 min, Vector2 max)
        {
            Vector2 start = prospectOverlay.InverseTransformPoint(element.TransformPoint(min));
            Vector2 end = prospectOverlay.InverseTransformPoint(element.TransformPoint(max));
            return Rect.MinMaxRect(start.x, start.y, end.x, end.y);
        }

        private Vector2 PlaceProspectMarker(Vector2 desired, Vector2 edge, bool vertical)
        {
            var last = new Vector2Int(Mathf.Max(1, Mathf.FloorToInt(2f * edge.x / 88f)),
                Mathf.Max(1, Mathf.FloorToInt(2f * edge.y / 50f)));
            var spacing = new Vector2(2f * edge.x / last.x, 2f * edge.y / last.y);
            var first = Vector2Int.RoundToInt((desired + edge) / spacing);
            var along = vertical ? Vector2Int.up : Vector2Int.right;
            var inward = vertical ? (first.x == 0 ? Vector2Int.right : Vector2Int.left)
                : (first.y == 0 ? Vector2Int.up : Vector2Int.down);
            int length = vertical ? last.y : last.x;
            int depth = vertical ? last.x : last.y;
            for (int row = 0; row <= depth; row++)
            {
                for (int step = 0; step <= 2 * length; step++)
                {
                    int shift = step % 2 == 0 ? -step / 2 : (step + 1) / 2;
                    var cell = first + inward * row + along * shift;
                    if (cell.x < 0 || cell.y < 0 || cell.x > last.x || cell.y > last.y) continue;
                    Vector2 position = (Vector2)cell * spacing - edge;
                    var bounds = new Rect(position - new Vector2(44, 25), new Vector2(88, 50));
                    bool blocked = false;
                    foreach (var exclusion in markerExclusions)
                        if (bounds.Overlaps(exclusion)) { blocked = true; break; }
                    if (!blocked && occupiedMarkerCells.Add(cell)) return position;
                }
            }
            return desired;
        }

        private void LateUpdate()
        {
            if (markers == null) return;
            UpdateProspectMarkers();
            if (PauseManager.BlocksInput) return;
            bool showReturn = interactions.ReturnConfirmationOpen && !interactions.DepositOpen;
            if (returnOverlay.gameObject.activeSelf != showReturn)
            {
                returnOverlay.gameObject.SetActive(showReturn);
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
            if (showReturn)
            {
                int count = interactions.DeployedMachineCount;
                endExcursionButton.interactable = !interactions.TutorialBlocksReturn;
                returnWarning.text = "Dropped items will be washed away by the current."
                    + (count == 0 ? "" : $"\nDeployed mining machines: {count}. They will break if left out overnight.")
                    + (interactions.TutorialBlocksReturn ? "\nComplete the tutorial before ending the excursion." : "");
                returnWarning.color = new Color(1f, .75f, .4f);
            }
            if (interactions.IsTransitioning) return;
            bool showSafety = interactions.SafetyOfferOpen;
            if (safetyOverlay != null)
                safetyOverlay.gameObject.SetActive(showSafety);
            if (showSafety)
            {
                safetyText.text = "There are no mining machines in your inventory.\n\n"
                    + $"Auto-purchase {OceanInteractions.SafetyMachineCount} mining machines for {interactions.SafetyOrderPrice} credits?\n"
                    + $"Your new total credits will be {interactions.SafetyOrderBalance}."
                    + (interactions.SafetyOfferError == null ? "" : "\n" + interactions.SafetyOfferError);
            }
            bool showLoadout = interactions.LoadoutOpen && !showSafety;
            loadoutOverlay.gameObject.SetActive(showLoadout);
            if (showLoadout)
            {
                if (markers.Length < interactions.targets.Length) AddMinimapMarkers();
                foreach (var refresh in loadoutLabels) refresh();
                diveButton.interactable = interactions.LoadoutFits;
                loadoutWeight.text = interactions.LoadoutFits
                    ? $"Total weight  {interactions.LoadoutWeight} / {interactions.WeightCapacity}" : "Loadout exceeds inventory capacity";
            }
            bool lowGas = !interactions.LoadoutOpen && interactions.Hydrox.IsDepleted;
            lowGasTint.color = lowGas ? new Color(.68f, .015f, .025f, .22f) : Color.clear;
            lowGasText.transform.parent.gameObject.SetActive(lowGas);
            lowGasText.text = interactions.Incapacitated ? "INCAPACITATED"
                : $"OUT OF HYDROX  •  RETURN TO SUBMARINE  {Mathf.CeilToInt(interactions.BreathingTimeLeft)}s";
            int weight = interactions.CarriedWeight;
            int capacity = interactions.WeightCapacity;
            weightFill.anchorMax = new Vector2(Mathf.Clamp01((float)weight / capacity), 1);
            weightColor.color = weight < capacity * .5f ? new Color(.12f, .53f, .29f)
                : weight <= capacity ? new Color(.68f, .51f, .08f) : new Color(.72f, .14f, .16f);
            weightLabel.text = $"Weight  {weight} / {capacity}";
            tankLabel.text = ItemCatalog.Find(interactions.Loadout.Tank).Name;
            bool inside = interactions.InsideSubmarine;
            bool inventoryOpen = interactions.panel != null && interactions.panel.activeSelf;
            tutorialPanel.gameObject.SetActive(!inside && !inventoryOpen && !interactions.LoadoutOpen && !lowGas
                && tutorial != null && tutorial.IsVisible);
            if (tutorial != null) tutorialText.text = tutorial.Text;
            hydroxHighlight.enabled = tutorialPanel.gameObject.activeSelf && tutorial.HighlightHydrox;
            scanPanel.gameObject.SetActive(!inside && interactions.IsProspecting);
            scanFill.anchorMax = new Vector2(interactions.ScanProgress, 1);
            scanText.text = "PROSPECTING  " + (interactions.ScanProgress * interactions.prospectDuration).ToString("0.0") + " / " + interactions.prospectDuration.ToString("0") + "s";
            hydroxRoot.gameObject.SetActive(!inside && !inventoryOpen);
            weightFill.parent.gameObject.SetActive(!inventoryOpen);
            hydroxFill.anchorMax = new Vector2(interactions.Hydrox.NormalizedFill, 1f);
            interior.gameObject.SetActive(inside);
            minimapRoot.gameObject.SetActive(!inside && !inventoryOpen);
            status.gameObject.SetActive(!inventoryOpen);
            prompt.gameObject.SetActive(!inventoryOpen);
            message.gameObject.SetActive(!inventoryOpen);
            status.text = "OCEAN EXCURSION\nItems  " + interactions.ItemsCollected + "     Tellurium  " + interactions.Tellurium
                + "     Machines  " + interactions.availableMachines;
            if (interactions.DepositedTellurium > 0) status.text += $"\nDeposited tellurium  {interactions.DepositedTellurium}";
            message.text = interactions.Message;
            var target = interactions.Closest;
            prompt.text = inside ? "LEFT CLICK  Return to ocean" : target == null ? "" : target.kind == OceanTarget.TargetKind.Submarine
                ? "LEFT CLICK  Interact with submarine" : target.kind == OceanTarget.TargetKind.Item ? "LEFT CLICK  Pick up item"
                : target.kind == OceanTarget.TargetKind.Corpse
                    ? interactions.QuestTargetBlock(target.kind) ?? "LEFT CLICK  Collect fungus sample"
                : target.kind == OceanTarget.TargetKind.AbyssEye
                    ? interactions.QuestTargetBlock(target.kind) ?? "LEFT CLICK  Photograph the Eye"
                : target.HasWreck ? $"LEFT CLICK  Salvage wreck  |  {target.WreckOre} tellurium"
                : target.HasMachine ? "LEFT CLICK  Retrieve machine  |  " + target.YieldAt(Time.time) + " tellurium"
                : target.DisplayedTier == 0 ? "Depleted (last prospected)"
                : "Tier " + target.DisplayedTier + "  |  " + OceanTarget.Rate(target.DisplayedTier) + " / min (last prospected)  |  LEFT CLICK  Place machine";
            if (interactions.IsProspecting) prompt.text = "Hold E to finish prospecting • Release to cancel";
            if (!inside && !inventoryOpen && !interactions.LoadoutOpen &&
                !interactions.ReturnConfirmationOpen && !interactions.IsProspecting &&
                target?.kind == OceanTarget.TargetKind.AbyssEye)
                GameState.Playtest.Milestone("Eye of the Abyss encountered");
            if (!inside) status.text += "\nWASD  Swim    LEFT CLICK  Interact    TAB  Inventory\nSPACE  Toggle prospected spots\n"
                + (interactions.IsProspecting ? "SCANNING  Thrust disabled | Release E to cancel"
                    : "HOLD E  Prospect (3s)" + (interactions.CooldownRemaining > 0f
                        ? " | Cooldown " + interactions.CooldownRemaining.ToString("0.0") + "s" : " | Ready"));
            int stimulants = interactions.CarriedCount(ItemCatalog.Stimulant);
            if (interactions.Loadout.Stimulants > 0 || stimulants > 0 || interactions.StimulantTimeLeft > 0f)
                status.text += $"\nR  Stimulant  ×{stimulants}" + (interactions.StimulantTimeLeft > 0f
                    ? $"  |  {Mathf.CeilToInt(interactions.StimulantTimeLeft)}s cooldown" : "");
            if (inside) return;
            float radius = Mathf.Min(minimapRoot.rect.width, minimapRoot.rect.height) * .5f - 6;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null) continue;
                var tracked = interactions.targets[i];
                bool visible = tracked != null && tracked.isActiveAndEnabled
                    && (tracked.kind != OceanTarget.TargetKind.MiningSpot || tracked.HasMachine || tracked.HasWreck);
                markers[i].gameObject.SetActive(visible);
                if (!visible) continue;
                Vector2 offset = tracked.transform.position - interactions.transform.position;
                markers[i].rectTransform.anchoredPosition = MinimapPosition(offset, minimapRange, radius);
                var color = markers[i].color;
                color.a = offset.magnitude > minimapRange ? .65f : 1f;
                markers[i].color = color;
                if (tracked.kind == OceanTarget.TargetKind.Submarine)
                    submarineDistance.text = "Submarine  " + offset.magnitude.ToString("0") + "m";
            }
            Vector2 direction = interactions.GetComponent<FlashlightController>().AimDirection;
            playerDirection.anchoredPosition = direction * 10;
            playerDirection.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90);
        }
    }
}
