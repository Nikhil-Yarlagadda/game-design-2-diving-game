using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

namespace DivingPrototype
{
    /// <summary>
    /// Equip screen (Figma "EQUIP SCREEN - tiered / stackable object ex"), the safety mining-machine prompt that can
    /// precede it, and the non-blocking new-gear notice. Background: the sleeping quarters, where the player ended the previous day.
    /// </summary>
    public sealed partial class OceanHud
    {
        private enum GearCategory { Machine, Stimulant, Tank, Flippers, Camera }

        private static readonly (GearCategory kind, string label)[] GearTypes =
        {
            (GearCategory.Machine, "Mining Machine"), (GearCategory.Stimulant, "Canister"),
            (GearCategory.Tank, "Hydrox Tank"), (GearCategory.Flippers, "Flippers"), (GearCategory.Camera, "Camera")
        };
        private static readonly string[] Tanks = { ItemCatalog.LightTank, ItemCatalog.MediumTank, ItemCatalog.HeavyTank };

        private RectTransform loadoutOverlay, safetyOverlay, gearNotice;
        private RectTransform tankPane, countPane, togglePane, weightFill;
        private TMP_Text safetyCredits, safetyCost, safetyBalance, safetyError;
        private TMP_Text categoryTitle, countValue, countHint, toggleName, toggleWeight, toggleState;
        private TMP_Text describedName, describedText, describedWeight, loadoutWeight, totalLabel;
        private Image weightFillImage;
        private Button diveButton, toggleButton;
        private GearCategory category = GearCategory.Machine;
        private string describedTank;
        private readonly List<System.Action> loadoutRefresh = new();

        // Figma frames are laid out on a 1024x729 screenshot grid here, centered on the 1280x720 canvas.
        private const float LayoutWidth = 1024f, LayoutHeight = 720f, GridScale = 720f / 729f;

        /// <summary>Places a child by its top-left corner on the Figma screenshot grid.</summary>
        private static RectTransform At(RectTransform rect, float x, float y, float width, float height)
            => rect.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y * GridScale), new Vector2(width, height * GridScale));

        private void BuildLoadout()
        {
            loadoutOverlay = ThemedScreen("Excursion Loadout");
            var backdrop = loadoutOverlay.gameObject.AddComponent<Image>();
            backdrop.color = Theme.Navy;
            backdrop.raycastTarget = true;
            var room = Resources.Load<Sprite>("sleeping_quarters");
            if (room != null)
            {
                var background = Theme.Node("Room", loadoutOverlay).Fill().gameObject.AddComponent<AspectRatioFitter>();
                background.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                background.aspectRatio = room.rect.width / room.rect.height;
                var image = background.gameObject.AddComponent<Image>();
                image.sprite = room;
                image.raycastTarget = false;
                Theme.Node("Shade", loadoutOverlay).Fill().gameObject.AddComponent<Image>().color = Theme.Navy.WithAlpha(.88f);
            }
            var bottomBand = Theme.Node("Footer", loadoutOverlay).gameObject.AddComponent<Image>();
            bottomBand.rectTransform.anchorMin = Vector2.zero;
            bottomBand.rectTransform.anchorMax = new Vector2(1, 0);
            bottomBand.rectTransform.pivot = new Vector2(.5f, 0);
            bottomBand.rectTransform.sizeDelta = new Vector2(0, 106 * GridScale);
            bottomBand.color = Theme.Navy.WithAlpha(.7f);
            var rule = Theme.Node("Rule", bottomBand.transform).gameObject.AddComponent<Image>();
            rule.rectTransform.anchorMin = new Vector2(0, 1);
            rule.rectTransform.anchorMax = Vector2.one;
            rule.rectTransform.sizeDelta = new Vector2(0, 2);
            rule.color = Theme.Cyan;

            var layout = Theme.Node("Equip Layout", loadoutOverlay)
                .Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(LayoutWidth, LayoutHeight));

            // Header.
            Text(At(Theme.Frame("Title", layout, Theme.Navy, Theme.Cyan).rectTransform, 62, 28, 353, 92),
                "EQUIP EXCURSION", 42, Theme.White);
            var subtitle = At(Theme.Node("Subtitle", layout), 437, 40, 480, 68);
            var subtitleFill = subtitle.gameObject.AddComponent<Image>();
            subtitleFill.color = Theme.Navy;
            subtitleFill.raycastTarget = false;
            Theme.HorizontalRules(subtitle, Theme.Cyan);
            Text(subtitle, "Select your gear for the ocean excursion", 28, Theme.White).textWrappingMode = TextWrappingModes.NoWrap;

            BuildGearTypes(layout);
            categoryTitle = Section(layout, "Selected Gear", 280, 143, 458, 272, 272, 134, 166);
            categoryTitle.enableAutoSizing = true;
            categoryTitle.fontSizeMin = 18;
            categoryTitle.fontSizeMax = 28;
            categoryTitle.textWrappingMode = TextWrappingModes.NoWrap;
            tankPane = Pane(layout, "Tiered");
            countPane = Pane(layout, "Stackable");
            togglePane = Pane(layout, "Toggle");
            for (int i = 0; i < Tanks.Length; i++) BuildTankCard(Tanks[i], 290 + i * 151);
            BuildCountPane();
            BuildTogglePane();
            BuildDescription(layout);
            BuildCurrentLoadout(layout);

            diveButton = Theme.Button("Start Excursion", loadoutOverlay, font, 28, Theme.ButtonStyle.Secondary, () => interactions.BeginDive(), 2f);
            diveButton.name = "Begin dive";
            ((RectTransform)diveButton.transform).Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 30), new Vector2(242, 40));
            diveButton.GetComponent<Image>().color = Theme.Hex("5E8B86");
            var diveCaption = diveButton.GetComponentInChildren<TMP_Text>(true);
            diveCaption.color = Theme.Cyan;
            // Figma draws "→" after the caption; Jersey 20 has no arrow glyph, so the arrow is drawn like the failure icons.
            diveCaption.rectTransform.offsetMax = new Vector2(-40, diveCaption.rectTransform.offsetMax.y);
            ArrowIcon(Theme.Node("Icon", diveButton.transform).Place(new Vector2(1, .5f), new Vector2(.5f, .5f),
                new Vector2(-30, 0), Vector2.one * 28f), Theme.Cyan);
            SelectCategory(category);
        }

        private TMP_Text Text(RectTransform parent, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Theme.Text("Text", parent, font, size, color, alignment, value);
            text.rectTransform.Fill(8, 2, 8, 2);
            return text;
        }

        /// <summary>A framed panel with the Figma cyan tab label overlapping its top-left corner. Returns the tab text.</summary>
        private TMP_Text Section(RectTransform layout, string label, float x, float y, float width, float height,
            float tabX, float tabY, float tabWidth)
        {
            At(Theme.Frame(label + " Panel", layout, Theme.Navy.WithAlpha(.55f), Theme.Cyan).rectTransform, x, y, width, height);
            var tab = At(Theme.Frame(label + " Tab", layout, Theme.Cyan, Theme.Cyan).rectTransform, tabX, tabY, tabWidth, 40);
            return Text(tab, label, 28, Theme.Navy, TextAlignmentOptions.Center); // Figma 40 px; the wide tabs use 24 px (34 Figma).
        }

        private RectTransform Pane(RectTransform layout, string name)
            => At(Theme.Node(name + " Pane", layout), 280, 143, 458, 272);

        private void BuildGearTypes(RectTransform layout)
        {
            Section(layout, "ITEMS", 42, 145, 212, 452, 34, 136, 165).fontSize = 24;
            for (int i = 0; i < GearTypes.Length; i++)
            {
                var (kind, label) = GearTypes[i];
                var row = Theme.Frame(label, layout, Theme.Hex("2E4A5A"), Theme.Cyan);
                At(row.rectTransform, 55, 190 + i * 79, 180, 63);
                row.raycastTarget = true;
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = row;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => SelectCategory(kind));
                var icon = Theme.Node("Icon", row.transform).gameObject.AddComponent<Image>();
                icon.rectTransform.Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(42, 42));
                icon.raycastTarget = false;
                var iconLabel = Theme.Text("Placeholder", icon.transform, font, 10, Theme.Navy, TextAlignmentOptions.Center, "image");
                iconLabel.rectTransform.Fill();
                var text = Theme.Text("Label", row.transform, font, 18, Theme.White, TextAlignmentOptions.MidlineLeft, label);
                text.rectTransform.Fill(60, 2, 6, 2);
                loadoutRefresh.Add(() =>
                {
                    bool selected = category == kind;
                    row.color = selected ? Theme.Cyan : Theme.Hex("2E4A5A");
                    icon.color = selected ? Theme.Navy : Theme.Cyan;
                    iconLabel.color = selected ? Theme.Cyan : Theme.Navy;
                    text.color = selected ? Theme.Navy : Theme.White;
                });
            }
        }

        private void SelectCategory(GearCategory kind)
        {
            category = kind;
            describedTank = interactions.Loadout.Tank;
            categoryTitle.text = System.Array.Find(GearTypes, type => type.kind == kind).label;
            tankPane.gameObject.SetActive(kind == GearCategory.Tank);
            countPane.gameObject.SetActive(kind is GearCategory.Machine or GearCategory.Stimulant);
            togglePane.gameObject.SetActive(kind is GearCategory.Flippers or GearCategory.Camera);
        }

        /// <summary>The item described in the right-hand card for the current category.</summary>
        private ItemCatalog.Definition DescribedItem => ItemCatalog.Find(category switch
        {
            GearCategory.Machine => ItemCatalog.Machine,
            GearCategory.Stimulant => ItemCatalog.Stimulant,
            GearCategory.Flippers => ItemCatalog.Flippers,
            GearCategory.Camera => ItemCatalog.Camera,
            _ => describedTank ?? interactions.Loadout.Tank
        });

        /// <summary>Item art placeholder plus name and weight, shared by tank cards and the single-item panes.</summary>
        private (TMP_Text name, TMP_Text weight) ItemCard(RectTransform card, float artSize)
        {
            var art = Theme.Box("Art", card, Theme.Cyan, null, 2f).rectTransform;
            art.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(artSize, artSize * .94f));
            var name = Theme.Text("Name", card, font, 18, Theme.White, TextAlignmentOptions.Center);
            // Figma keeps the caption on one line even when it is wider than the art.
            name.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 46),
                new Vector2(Mathf.Max(card.rect.width, artSize + 28f), 44));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            var line = Theme.Node("Rule", card).gameObject.AddComponent<Image>();
            line.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(card.rect.width - 30, 1));
            line.color = Theme.Cyan.WithAlpha(.6f);
            var weight = Theme.WeightLabel(card, font, 18, Theme.Cyan, TextAlignmentOptions.Center);
            ((RectTransform)weight.transform.parent).Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(card.rect.width - 10, 28));
            return (name, weight);
        }

        private void BuildTankCard(string tank, float x)
        {
            var gear = ItemCatalog.Find(tank);
            var frame = Theme.Frame(gear.Name, tankPane, Theme.Navy, Theme.Cyan);
            var card = At(frame.rectTransform, x - 280, 188 - 143, 136, 212);
            frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                describedTank = tank;
                if (ItemCatalog.Count(GameState.inventory, tank) == 0) return;
                var pick = interactions.Loadout;
                pick.Tank = tank;
                interactions.SetLoadout(pick);
            });
            var (name, weight) = ItemCard(card, 108);
            name.text = gear.Name;
            // Figma 257:2754: two-line tank names above the rule (113x48 Figma px; 83x34 here so "Hydrox Tank" fits one line).
            name.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 42), new Vector2(83, 34.1f));
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true;
            name.fontSizeMin = 10;
            name.fontSizeMax = 16;
            var art = card.Find("Art").GetComponent<Image>();
            var rule = card.Find("Rule").GetComponent<Image>();
            var padlock = BuildTankPadlock(card);
            loadoutRefresh.Add(() =>
            {
                bool owned = ItemCatalog.Count(GameState.inventory, tank) > 0;
                bool equipped = interactions.Loadout.Tank == tank;
                frame.color = equipped ? Theme.Hex("2E5563") : Theme.Navy;
                Theme.SetFrameColor(frame, equipped ? Theme.White : owned ? Theme.Cyan : Theme.MutedLine);
                weight.text = $"{gear.Weight} lbs";
                weight.color = owned ? Theme.Cyan : Theme.MutedText;
                weight.transform.parent.GetChild(0).GetComponent<Image>().color = weight.color;
                name.color = owned ? Theme.White : Theme.MutedText;
                art.color = owned ? Theme.Cyan : Theme.PanelFill;
                rule.color = owned ? Theme.Cyan.WithAlpha(.6f) : Theme.MutedText.WithAlpha(.35f);
                padlock.gameObject.SetActive(!owned);
            });
        }

        /// <summary>Figma's padlock (exported from "Group 65") over an unowned tank card.</summary>
        private static RectTransform BuildTankPadlock(RectTransform card)
        {
            var root = Theme.Node("Padlock", card).Place(new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -86f), new Vector2(45.5f, 46.2f));
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = Theme.LockIcon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return root;
        }

        private void BuildCountPane()
        {
            var frame = At(Theme.Frame("Stackable Item", countPane, Theme.Navy, Theme.Cyan).rectTransform, 10, 45, 434, 212);
            var art = Theme.Node("Item", frame).Place(Vector2.up, Vector2.up, new Vector2(14, -14), new Vector2(108, 200));
            var (name, weight) = ItemCard(art, 108);
            Text(Theme.Node("Heading", frame).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -42), new Vector2(268, 46)),
                "Inventory Count", 34, Theme.Cyan);
            var controls = Theme.Frame("Controls", frame, Theme.Navy, Theme.Cyan).rectTransform
                .Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -96), new Vector2(268, 52));
            void Adjust(int delta)
            {
                var pick = interactions.Loadout;
                if (category == GearCategory.Machine) pick.Machines += delta;
                else pick.Stimulants += delta;
                interactions.SetLoadout(pick);
            }
            var minus = Theme.Button("−", controls, font, 42, Theme.ButtonStyle.Outline, () => Adjust(-1), 2f);
            ((RectTransform)minus.transform).Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(22, 0), new Vector2(50, 40));
            var plus = Theme.Button("+", controls, font, 42, Theme.ButtonStyle.Outline, () => Adjust(1), 2f);
            ((RectTransform)plus.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-22, 0), new Vector2(50, 40));
            var valueBox = Theme.Box("Value", controls, Theme.Hex("2E4A5A"), null, 2f).rectTransform;
            valueBox.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(104, 40));
            // "carried / owned", as the original loadout screen showed it.
            countValue = Text(valueBox, "0", 34, Theme.Cyan);
            countValue.enableAutoSizing = true;
            countValue.fontSizeMin = 18;
            countValue.fontSizeMax = 34;
            countValue.textWrappingMode = TextWrappingModes.NoWrap;
            countHint = Theme.Text("Hint", frame, font, 17, Theme.Cyan, TextAlignmentOptions.Center);
            countHint.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -152), new Vector2(268, 44));
            loadoutRefresh.Add(() =>
            {
                if (!countPane.gameObject.activeSelf) return;
                bool machine = category == GearCategory.Machine;
                var item = DescribedItem;
                int count = machine ? interactions.Loadout.Machines : interactions.Loadout.Stimulants;
                int owned = ItemCatalog.Count(GameState.inventory, item.Id);
                name.text = item.Name;
                weight.text = $"{item.Weight} lbs";
                countValue.text = $"{count} / {owned}";
                minus.interactable = count > 0;
                plus.interactable = count < owned;
                countHint.text = machine ? "minimum of 3 recommended" : "";
            });
        }

        private void BuildTogglePane()
        {
            var frame = At(Theme.Frame("Single Item", togglePane, Theme.Navy, Theme.Cyan).rectTransform, 10, 45, 434, 212);
            var art = Theme.Node("Item", frame).Place(Vector2.up, Vector2.up, new Vector2(14, -14), new Vector2(108, 200));
            (toggleName, toggleWeight) = ItemCard(art, 108);
            toggleState = Theme.Text("State", frame, font, 22, Theme.Cyan, TextAlignmentOptions.Center);
            toggleState.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -50), new Vector2(268, 40));
            toggleButton = Theme.Button("Equip", frame, font, 24, Theme.ButtonStyle.Primary, () =>
            {
                var pick = interactions.Loadout;
                if (category == GearCategory.Flippers) pick.Flippers = !pick.Flippers;
                else pick.Camera = !pick.Camera;
                interactions.SetLoadout(pick);
            });
            ((RectTransform)toggleButton.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(184, -104), new Vector2(200, 44));
            loadoutRefresh.Add(() =>
            {
                if (!togglePane.gameObject.activeSelf) return;
                var item = DescribedItem;
                bool owned = ItemCatalog.Count(GameState.inventory, item.Id) > 0;
                bool equipped = category == GearCategory.Flippers ? interactions.Loadout.Flippers : interactions.Loadout.Camera;
                toggleName.text = item.Name;
                toggleWeight.text = $"{item.Weight} lbs";
                toggleState.text = !owned ? "Not owned" : equipped ? "Equipped" : "Not equipped";
                toggleState.color = owned ? Theme.Cyan : Theme.Gray;
                toggleButton.interactable = owned;
                toggleButton.GetComponentInChildren<TMP_Text>(true).text = equipped ? "Unequip" : "Equip";
            });
        }

        private void BuildDescription(RectTransform layout)
        {
            var outer = At(Theme.Frame("Description", layout, Theme.Navy, Theme.Cyan).rectTransform, 762, 138, 233, 282);
            var card = Theme.Frame("Card", outer, Theme.DarkGray, Theme.Cyan).rectTransform.Fill(8, 8, 8, 8);
            var art = Theme.Box("Art", card, Theme.Navy, null, 1f).rectTransform;
            art.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -18), new Vector2(92, 92));
            Text(art, "image", 16, Theme.White);
            describedName = Theme.Text("Name", card, font, 18, Theme.White, TextAlignmentOptions.TopLeft);
            describedName.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -22), new Vector2(96, 92));
            describedName.enableAutoSizing = true;
            describedName.fontSizeMin = 13;
            describedName.fontSizeMax = 18;
            Rule(card, -118);
            describedText = Theme.Text("Description", card, font, 14, Theme.White, TextAlignmentOptions.TopLeft);
            describedText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -124), new Vector2(185, 100));
            describedText.enableAutoSizing = true;
            describedText.fontSizeMin = 9;
            describedText.fontSizeMax = 14;
            Rule(card, -228);
            describedWeight = Theme.WeightLabel(card, font, 24, Theme.White, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)describedWeight.transform.parent).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -232), new Vector2(185, 30));
            loadoutRefresh.Add(() =>
            {
                var item = DescribedItem;
                describedName.text = item.Name;
                describedText.text = item.Description;
                describedWeight.text = $"{item.Weight} lbs";
            });
        }

        private static void Rule(RectTransform card, float y)
        {
            var line = Theme.Node("Rule", card).gameObject.AddComponent<Image>();
            line.rectTransform.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y), new Vector2(185, 1));
            line.color = Theme.White.WithAlpha(.6f);
        }

        private void BuildCurrentLoadout(RectTransform layout)
        {
            Section(layout, "Current Loadout", 280, 441, 698, 154, 272, 436, 196).fontSize = 24;
            (string id, string label)[] slots =
            {
                (null, "Diver's Equipment"), (ItemCatalog.Machine, "Machine"), (ItemCatalog.Stimulant, "Canister"),
                (ItemCatalog.Flippers, "Flippers"), (ItemCatalog.Camera, "Camera")
            };
            for (int i = 0; i < slots.Length; i++)
            {
                var (id, label) = slots[i];
                var box = Theme.Frame(label + " Slot", layout, Theme.Navy, Theme.Cyan);
                At(box.rectTransform, 293 + i * 71, 492, 64, 64);
                var art = Theme.Box("Art", box.transform, Theme.Cyan, null, 1f);
                art.rectTransform.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(42, 40));
                var caption = Theme.Text("Placeholder", art.transform, font, 10, Theme.Navy, TextAlignmentOptions.Center, label);
                caption.rectTransform.Fill(1, 1, 1, 1);
                var count = Theme.Text("Count", box.transform, font, 20, Theme.White, TextAlignmentOptions.BottomRight);
                count.rectTransform.Fill(2, 0, 4, 2);
                loadoutRefresh.Add(() =>
                {
                    var pick = interactions.Loadout;
                    int amount = id == null ? 1 : id == ItemCatalog.Machine ? pick.Machines : id == ItemCatalog.Stimulant ? pick.Stimulants
                        : id == ItemCatalog.Flippers ? (pick.Flippers ? 1 : 0) : (pick.Camera ? 1 : 0);
                    art.gameObject.SetActive(amount > 0);
                    count.text = amount > 1 ? amount.ToString() : "";
                });
            }

            totalLabel = Text(At(Theme.Node("Total Label", layout), 666, 455, 292, 34), "TOTAL WEIGHT", 24, Theme.Cyan);
            totalLabel.enableAutoSizing = true;
            totalLabel.fontSizeMin = 13;
            totalLabel.fontSizeMax = 24;
            totalLabel.textWrappingMode = TextWrappingModes.NoWrap;
            loadoutWeight = Text(At(Theme.Node("Total Value", layout), 666, 490, 292, 52), "", 40, Theme.White);
            var track = Theme.Box("Weight Track", layout, Theme.Navy, Theme.Cyan, 12f).rectTransform;
            At(track, 666, 549, 292, 24);
            weightFill = Theme.Box("Weight Fill", track, Theme.Cyan, null, 10f).rectTransform;
            weightFill.anchorMin = Vector2.zero;
            weightFill.offsetMin = new Vector2(3, 3);
            weightFill.offsetMax = new Vector2(-3, -3);
            weightFillImage = weightFill.GetComponent<Image>();
            BuildGearNotice(layout);
        }

        /// <summary>Figma 257:2731: non-blocking new-gear notification in the bottom-left, left of Start Excursion.</summary>
        private void BuildGearNotice(RectTransform layout)
        {
            var box = Theme.Frame("New Gear Notice", layout, Theme.Navy, Theme.Cyan);
            At(box.rectTransform, 32, 636, 260, 81);
            gearNotice = box.rectTransform;
            gearNotice.gameObject.SetActive(false);
            Theme.WarningIcon(box.transform, font, 78f, Theme.Cyan, Theme.Navy)
                .Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, 0), new Vector2(92, 100));
            var text = Theme.Text("Text", box.transform, font, 25, Theme.White, TextAlignmentOptions.MidlineLeft,
                "You have new gear to equip!");
            text.rectTransform.Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(84, 0), new Vector2(168, 66));
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>Safety machines, the new-gear notice, then the equip screen itself.</summary>
        private void UpdatePreDivePrompts()
        {
            bool showSafety = interactions.SafetyOfferOpen;
            if (safetyOverlay != null) safetyOverlay.gameObject.SetActive(showSafety);
            if (safetyOverlay != null && showSafety)
            {
                safetyCredits.text = GameState.credits.ToString();
                safetyCost.text = interactions.SafetyOrderPrice.ToString();
                safetyBalance.text = interactions.SafetyOrderBalance.ToString();
                safetyError.text = interactions.SafetyOfferError ?? "";
            }
            bool showLoadout = interactions.LoadoutOpen;
            loadoutOverlay.gameObject.SetActive(showLoadout);
            if (showSafety) loadoutOverlay.SetAsLastSibling();
            if (safetyOverlay != null && showSafety) safetyOverlay.SetAsLastSibling();
            if (!showLoadout) return;
            if (markers.Length < interactions.targets.Length) AddMinimapMarkers();
            foreach (var refresh in loadoutRefresh) refresh();
            int weight = interactions.LoadoutWeight, capacity = interactions.WeightCapacity;
            loadoutWeight.text = $"{weight} / {capacity} lbs";
            weightFill.anchorMax = new Vector2(Mathf.Clamp01((float)weight / capacity), 1);
            weightFillImage.color = Theme.WeightColor(weight, capacity);
            bool fits = interactions.LoadoutFits;
            totalLabel.text = fits ? "TOTAL WEIGHT" : "Loadout exceeds inventory capacity";
            totalLabel.color = fits ? Theme.Cyan : Theme.Pink;
            gearNotice.gameObject.SetActive(interactions.GearReminderOpen);
            diveButton.interactable = fits && !showSafety;
        }

        /// <summary>Figma 255:2124: warning, credit rows, and SKIP/BUY for the auto-purchase offer.</summary>
        private void BuildSafetyOffer()
        {
            if (!interactions.SafetyOfferOpen) return;
            var dialog = BuildAlert("Safety Mining Machines", Theme.Fig(1030, 866), false, out safetyOverlay, .82f);
            var fill = dialog.GetComponent<Image>();
            if (fill != null)
            {
                Theme.Rounded(fill, Theme.RoundedRect, Theme.Hex("87B8BA").WithAlpha(.9f), 17f);
                fill.raycastTarget = true;
            }
            var border = dialog.Find("Outline");
            if (border != null)
                Theme.Rounded(border.GetComponent<Image>(), Theme.RoundedOutline(2f, 17f), Theme.Cyan, 17f);

            Theme.WarningIcon(dialog, font, 56f, Theme.Cyan, Theme.Navy)
                .Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -31), new Vector2(84, 58));
            var heading = AlertText(dialog, 125, 44, 42, "You have no mining machines equipped!");
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 30;
            heading.fontSizeMax = 42;

            var rule = Theme.Node("Rule", dialog).gameObject.AddComponent<Image>();
            rule.rectTransform.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -177), new Vector2(669, 2));
            rule.color = Theme.White;
            rule.raycastTarget = false;

            var question = AlertText(dialog, 199, 36, 31,
                $"Auto-purchase {OceanInteractions.SafetyMachineCount} mining machines?");
            question.textWrappingMode = TextWrappingModes.NoWrap;

            safetyCredits = SafetyRow(dialog, "Current Credit Total:", 264, 250);
            safetyCost = SafetyRow(dialog, "Mining Machine Cost:", 331, 319);
            safetyBalance = SafetyRow(dialog, "New Credit Total:", 433, 413);
            safetyError = AlertText(dialog, 478, 26, 17);
            safetyError.color = Theme.Pink;
            safetyError.textWrappingMode = TextWrappingModes.NoWrap;

            var buy = Theme.Button("BUY", dialog, font, 34, Theme.ButtonStyle.Primary,
                () => interactions.ResolveSafetyOffer(true), 8f);
            ((RectTransform)buy.transform).Place(new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(-9, -513), new Vector2(155, 64));
            var skip = Theme.Button("SKIP", dialog, font, 34, Theme.ButtonStyle.Outline,
                () => interactions.ResolveSafetyOffer(false), 8f);
            ((RectTransform)skip.transform).Place(new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(249, -513), new Vector2(159, 64));
            // Note 256:2228 (Frame 11): resting = #A4DDDB at 40% with #C4FDFB text and outline;
            // hovered = solid #A4DDDB with a white outline and navy text.
            var restFill = Theme.Hex("A4DDDB").WithAlpha(.4f);
            var restLine = Theme.Hex("C4FDFB");
            foreach (var choice in new[] { buy, skip })
                Theme.HoverPalette(choice, restFill, restLine, restLine, Theme.Hex("A4DDDB"), Theme.White, Theme.Navy);
        }

        /// <summary>One "label ... value" line in the safety-machine dialog.</summary>
        private TMP_Text SafetyRow(RectTransform dialog, string label, float labelTop, float boxTop)
        {
            var text = Theme.Text("Label", dialog, font, 30, Theme.White, TextAlignmentOptions.MidlineLeft, label);
            text.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(62, -labelTop), new Vector2(275, 34));
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var box = Theme.Frame("Value", dialog, Color.clear, Theme.Cyan, 2f);
            box.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(349, -boxTop), new Vector2(82, 60));
            var value = Theme.Text("Value", box.transform, font, 32, Theme.Cyan, TextAlignmentOptions.Center);
            value.rectTransform.Fill();
            value.enableAutoSizing = true;
            value.fontSizeMin = 16;
            value.fontSizeMax = 32;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            return value;
        }
    }
}
