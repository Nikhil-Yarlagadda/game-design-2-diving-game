using TMPro;
using UnityEngine;
using Theme = DivingPrototype.OceanUiTheme;

namespace DivingPrototype
{
    /// <summary>
    /// Figma "Seafloor UI - too heavy": non-blocking "Inventory Full" box above the HUD, shown while the
    /// existing inventory-full notification is active (same trigger and duration as before).
    /// </summary>
    public sealed partial class OceanHud
    {
        // Figma wording, kept until the team settles the final copy.
        private const string InventoryFullTitle = "Inventory Full";
        private const string InventoryFullBody = "You cannot pick this up.\nCannot exceed weight limit.\nTry dropping some items.";

        private RectTransform inventoryFullBox;

        private void BuildInventoryFull()
        {
            // Figma 723×295 box, bottom edge 149 px above the screen bottom (1440×1024 frame).
            var box = Theme.Box("Inventory Full", status.transform.parent, Theme.PopupFill, Theme.Cyan, 10f);
            inventoryFullBox = box.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 149 * Theme.FigmaScale), new Vector2(723, 295) * Theme.FigmaScale);
            TutorialTitleText(inventoryFullBox, 22, 36, 34).text = InventoryFullTitle;
            TutorialDivider(inventoryFullBox, 66, 592 * Theme.FigmaScale);
            var body = TutorialBody(inventoryFullBox, 82, 112);
            body.text = InventoryFullBody;
            body.enableAutoSizing = false;
            body.fontSize = 32; // Figma sets the body at the same size as the title.
            inventoryFullBox.gameObject.SetActive(false);
        }

        private void UpdateInventoryFull()
        {
            bool show = interactions.Message == OceanInteractions.InventoryFullMessage && !interactions.InsideSubmarine
                && !InventoryOpen && !interactions.LoadoutOpen && !interactions.ReturnConfirmationOpen;
            inventoryFullBox.gameObject.SetActive(show);
            if (!show) return;
            message.gameObject.SetActive(false); // The box replaces the plain message line.
            tutorialPanel.gameObject.SetActive(false); // Both boxes share the same spot above the HUD.
        }
    }
}
