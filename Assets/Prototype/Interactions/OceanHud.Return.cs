using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

namespace DivingPrototype
{
    /// <summary>Confirmation shown when the diver interacts with the submarine (Figma "Seafloor UI - return to sub").</summary>
    public sealed partial class OceanHud
    {
        private RectTransform returnOverlay, returnMachineRow, returnDivider, returnDialog;
        private TMP_Text returnQuestion, returnMachineCount, returnMachinesLeft, returnNotice;
        private Button endExcursionButton;
        private CanvasGroup endExcursionGroup;

        private void BuildReturnConfirmation()
        {
            var dialog = returnDialog = BuildAlert("Return Confirmation", new Vector2(512, 356), true, out returnOverlay, .55f, 90f, 6f);
            returnQuestion = AlertText(dialog, 88, 24, 16, "Are you sure you want to return?");
            returnQuestion.textWrappingMode = TextWrappingModes.NoWrap;

            returnMachineRow = Theme.Node("Mining Machines Left", dialog).Place(
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -120), new Vector2(456, 58));
            var stillHave = Theme.Text("Still Have", returnMachineRow, font, 17, Theme.White,
                TextAlignmentOptions.MidlineRight, "You still have");
            var stillHaveRect = stillHave.rectTransform;
            stillHaveRect.anchorMin = Vector2.zero;
            stillHaveRect.anchorMax = new Vector2(.5f, 1);
            stillHaveRect.offsetMin = Vector2.zero;
            stillHaveRect.offsetMax = new Vector2(-40, 0);
            stillHave.textWrappingMode = TextWrappingModes.NoWrap;

            var countBox = Theme.Box("Machines", returnMachineRow, Color.clear, Theme.Cyan, 6f).rectTransform;
            countBox.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(58, 58));
            returnMachineCount = Theme.Text("Count", countBox, font, 36, Theme.Cyan, TextAlignmentOptions.Center);
            returnMachineCount.rectTransform.Fill();

            var machinesLeft = returnMachinesLeft = Theme.Text("Machines Left", returnMachineRow, font, 17, Theme.White,
                TextAlignmentOptions.MidlineLeft, "mining machines left");
            var machinesLeftRect = machinesLeft.rectTransform;
            machinesLeftRect.anchorMin = new Vector2(.5f, 0);
            machinesLeftRect.anchorMax = Vector2.one;
            machinesLeftRect.offsetMin = new Vector2(40, 0);
            machinesLeftRect.offsetMax = Vector2.zero;
            machinesLeft.textWrappingMode = TextWrappingModes.NoWrap;

            Button Stacked(string caption, float top, Theme.ButtonStyle style, UnityEngine.Events.UnityAction action)
            {
                var button = AlertButton(dialog, caption, top, new Vector2(200, 34), style, action);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.fontSize = 22;
                if (style == Theme.ButtonStyle.Secondary) label.color = Theme.Cyan;
                return button;
            }

            // Figma button tops are 195 / 244 / 293 px from the dialog top in both variants.
            Stacked("Continue Exploring", 193, Theme.ButtonStyle.Primary, () => interactions.CancelReturn());
            var inventory = FindAnyObjectByType<ExcursionInventory>();
            var deposit = Stacked("Deposit Items", 242, Theme.ButtonStyle.Secondary,
                () => FindAnyObjectByType<ExcursionInventory>()?.OpenDeposit());
            deposit.interactable = inventory != null;
            endExcursionButton = Stacked("End Excursion", 291, Theme.ButtonStyle.Secondary, () => interactions.ConfirmReturn());
            // The button tint alone leaves the caption bright, so a disabled End Excursion still looked clickable.
            endExcursionGroup = endExcursionButton.gameObject.AddComponent<CanvasGroup>();

            // Not in Figma: keeps the original return warnings below the buttons; the dialog grows to fit them.
            returnNotice = AlertText(dialog, ReturnNoticeTop, 22, 17);
            returnNotice.color = Theme.Hex("FFBF66");
            returnNotice.alignment = TextAlignmentOptions.Top;
            returnNotice.text = "Dropped items will be washed away by the current.";

            // Figma "return to sub - no machines left" draws a divider under the question.
            TutorialDivider(dialog, 166, 420);
            returnDivider = (RectTransform)dialog.Find("Divider");
        }

        private void UpdateReturnConfirmation()
        {
            bool showReturn = interactions.ReturnConfirmationOpen && !interactions.DepositOpen;
            if (returnOverlay.gameObject.activeSelf != showReturn)
            {
                returnOverlay.gameObject.SetActive(showReturn);
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
            if (!showReturn) return;
            int count = interactions.DeployedMachineCount;
            bool hasMachines = count > 0;
            returnQuestion.fontSize = hasMachines ? 16 : 28;
            returnQuestion.rectTransform.offsetMin = new Vector2(28, hasMachines ? -112 : -160);
            returnQuestion.rectTransform.offsetMax = new Vector2(-28, hasMachines ? -88 : -124);
            returnDivider.gameObject.SetActive(!hasMachines);
            returnMachineRow.gameObject.SetActive(hasMachines);
            if (hasMachines)
            {
                returnMachineCount.text = count.ToString();
                returnMachinesLeft.text = count == 1 ? "mining machine left" : "mining machines left";
            }
            bool blocked = interactions.TutorialBlocksReturn;
            returnNotice.text = "Dropped items will be washed away by the current."
                + (hasMachines ? "\nDeployed mining machines will break if left out overnight." : "")
                + (blocked ? "\nComplete the tutorial before ending the excursion." : "");
            int lines = 1 + (hasMachines ? 1 : 0) + (blocked ? 1 : 0);
            returnNotice.rectTransform.offsetMin = new Vector2(28, -ReturnNoticeTop - lines * ReturnNoticeLine);
            returnDialog.sizeDelta = new Vector2(returnDialog.sizeDelta.x, ReturnNoticeTop + lines * ReturnNoticeLine + 16);
            endExcursionButton.interactable = !blocked;
            endExcursionGroup.alpha = blocked ? .4f : 1f;
        }

        private const float ReturnNoticeTop = 340f, ReturnNoticeLine = 22f;
    }
}
