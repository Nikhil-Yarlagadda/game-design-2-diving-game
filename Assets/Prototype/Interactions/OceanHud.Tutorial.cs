using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

namespace DivingPrototype
{
    /// <summary>
    /// Tutorial boxes (Figma "Tutorial Pop Up" = blocking prompt, "Tutorial Pop Up 2" = passive box),
    /// the contextual interaction prompt, the transient message line, and the prospecting progress bar.
    /// </summary>
    public sealed partial class OceanHud
    {
        private TMP_Text tutorialText, tutorialPromptText, scanText, tutorialPanelTitle, tutorialPromptTitle;
        private RectTransform tutorialPanel, tutorialOverlay, scanPanel, scanFill, promptPill;

        private void BuildTutorial()
        {
            var panel = Theme.Box("Excursion Tutorial", status.transform.parent, Theme.PopupFill, Theme.Cyan, 10f);
            tutorialPanel = panel.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 122), new Vector2(512, 130));
            // Body first so GetComponentInChildren<TMP_Text>() still finds the copy, not the heading.
            tutorialText = TutorialBody(tutorialPanel, 78, 44);
            tutorialPanelTitle = TutorialTitleText(tutorialPanel, 20, 28, 24);
            TutorialDivider(tutorialPanel, 52, 440);

            scanPanel = Theme.Box("Prospecting Progress", status.transform.parent, Theme.Navy.WithAlpha(.9f),
                Theme.Cyan, 10f).rectTransform;
            scanPanel.Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 285), new Vector2(360, 42));
            scanFill = Theme.Box("Scan Fill", scanPanel, Theme.Cyan, null, 8f).rectTransform;
            scanFill.anchorMin = Vector2.zero;
            scanFill.anchorMax = new Vector2(0, 1);
            scanFill.offsetMin = new Vector2(5, 5);
            scanFill.offsetMax = new Vector2(-5, -5);
            scanText = Theme.Text("Label", scanPanel, font, 18, Theme.White, TextAlignmentOptions.Center);
            scanText.rectTransform.Fill(8, 2, 8, 2);

            prompt.font = font;
            prompt.color = Theme.White;
            prompt.fontSize = 22;
            prompt.alignment = TextAlignmentOptions.Center;
            message.font = font;
            message.color = Theme.Pink;
            message.fontSize = 20;
            message.alignment = TextAlignmentOptions.Center;

            var promptRect = prompt.rectTransform;
            promptPill = Theme.Box("Prompt Pill", promptRect.parent, Theme.Navy.WithAlpha(.75f), null, 17f).rectTransform;
            promptPill.SetSiblingIndex(promptRect.GetSiblingIndex());
            promptPill.gameObject.SetActive(false);
        }

        /// <summary>Builds the blocking tutorial prompt shown over the visible excursion HUD.</summary>
        private void BuildTutorialPrompt()
        {
            if (tutorial == null) return;
            var dialog = BuildAlert("Excursion Paused", new Vector2(512, 337), true, out tutorialOverlay, .62f, 90f, 8f);
            tutorialPromptTitle = TutorialTitleText(dialog, 88, 28, 24);
            TutorialDivider(dialog, 120, 440);
            tutorialPromptText = TutorialBody(dialog, 141, 106);
            // Figma "Tutorial Pop Up": small rounded "continue" button.
            var button = Theme.Button("continue", dialog, font, 22f, Theme.ButtonStyle.Secondary, tutorial.Acknowledge, 12f);
            ((RectTransform)button.transform).Place(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -259), new Vector2(112, 44));
        }

        private void UpdateTutorial()
        {
            bool inside = interactions.InsideSubmarine;
            bool showTutorial = !inside && !InventoryOpen && !interactions.LoadoutOpen && !interactions.Hydrox.IsDepleted
                && !interactions.ReturnConfirmationOpen && tutorial != null && tutorial.IsVisible;
            bool showTutorialPrompt = showTutorial && tutorial.AwaitingAcknowledgement;
            tutorialPanel.gameObject.SetActive(showTutorial && !showTutorialPrompt);
            if (tutorial != null)
            {
                string title = TutorialHeading;
                tutorialText.text = tutorial.Text;
                tutorialPanelTitle.text = title;
                tutorialOverlay.gameObject.SetActive(showTutorialPrompt);
                if (showTutorialPrompt)
                {
                    tutorialPromptText.text = tutorial.Text;
                    tutorialPromptTitle.text = title;
                }
            }
            hydroxHighlight.enabled = tutorialPanel.gameObject.activeSelf && tutorial != null && tutorial.HighlightHydrox;
            scanPanel.gameObject.SetActive(!inside && interactions.IsProspecting);
            scanFill.anchorMax = new Vector2(interactions.ScanProgress, 1);
            scanFill.gameObject.SetActive(interactions.ScanProgress > .03f);
            scanText.text = "PROSPECTING  " + (interactions.ScanProgress * interactions.prospectDuration).ToString("0.0") + " / " + interactions.prospectDuration.ToString("0") + "s";
        }

        /// <summary>Updates the contextual "LEFT CLICK ..." hint and the transient message line.</summary>
        private void UpdatePrompt()
        {
            bool inside = interactions.InsideSubmarine;
            interior.gameObject.SetActive(inside);
            prompt.gameObject.SetActive(!InventoryOpen);
            message.gameObject.SetActive(!InventoryOpen);
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

            bool showPill = prompt.gameObject.activeSelf && !string.IsNullOrEmpty(prompt.text);
            promptPill.gameObject.SetActive(showPill);
            if (showPill)
            {
                var targetRect = prompt.rectTransform;
                promptPill.anchorMin = targetRect.anchorMin;
                promptPill.anchorMax = targetRect.anchorMax;
                promptPill.pivot = targetRect.pivot;
                promptPill.anchoredPosition = targetRect.anchoredPosition;
                promptPill.sizeDelta = new Vector2(Mathf.Max(40f, prompt.preferredWidth + 32f),
                    Mathf.Max(34f, prompt.preferredHeight + 12f));
            }

            if (!inside && !InventoryOpen && !interactions.LoadoutOpen &&
                !interactions.ReturnConfirmationOpen && !interactions.IsProspecting &&
                target?.kind == OceanTarget.TargetKind.AbyssEye)
                GameState.Playtest.Milestone("Eye of the Abyss encountered");
        }

        private string TutorialHeading => tutorial != null && tutorial.Mode == OceanTutorial.TutorialMode.Mining
            ? $"TUTORIAL {tutorial.Stage}/{OceanTutorial.MiningMessageCount}"
            : "TUTORIAL";

        private TMP_Text TutorialBody(RectTransform parent, float top, float height)
        {
            // Figma tutorial boxes start the copy right under the divider.
            var body = TutorialTextBlock("Text", parent, top, height, 20, 22);
            body.alignment = TextAlignmentOptions.Top;
            body.enableAutoSizing = true;
            body.fontSizeMin = 14;
            body.fontSizeMax = 22;
            body.fontSize = 22;
            body.richText = true;
            return body;
        }

        private TMP_Text TutorialTitleText(RectTransform parent, float top, float height, float size)
        {
            var title = TutorialTextBlock("Title", parent, top, height, 20, size);
            title.enableAutoSizing = false;
            return title;
        }

        private TMP_Text TutorialTextBlock(string name, RectTransform parent, float top, float height, float inset, float size)
        {
            var text = Theme.Text(name, parent, font, size, Theme.White, TextAlignmentOptions.Center);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(inset, -top - height);
            rect.offsetMax = new Vector2(-inset, -top);
            text.richText = true;
            return text;
        }

        private static void TutorialDivider(RectTransform parent, float top, float width)
        {
            var line = Theme.Node("Divider", parent).gameObject.AddComponent<Image>();
            line.rectTransform.Place(new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -top), new Vector2(width, 1.5f));
            line.color = Theme.White.WithAlpha(.85f);
            line.raycastTarget = false;
        }
    }
}
