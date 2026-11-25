using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DivingPrototype
{
    /// <summary>
    /// Seafloor HUD (Figma "Seafloor UI", "TANK PROGRESSION", "Seafloor UI - low oxygen"):
    /// hydrox tank icon bottom-left; prospecting / stimulant slots beside it; weight bar and Tellurium / Mining Machine / Backpack slots bottom-right;
    /// red vignette and countdown during the 15 s after hydrox runs out.
    /// </summary>
    public sealed partial class OceanHud
    {
        private RectTransform tankRoot, slotRow, toolRow;
        private Image tankFillImage;
        private TMP_Text tankWarning;
        private Outline hydroxHighlight;
        private TMP_Text telluriumCount, machineCount, prospectCooldown, stimulantCount;
        private CanvasGroup prospectSlot, stimulantSlot;
        private UnityEngine.UI.Image stimulantCooldown, prospectCover;
        private Image weightBarFill;
        private Image lowGasTint, lowGasVignette;
        private TMP_Text lowGasText;
        private CanvasGroup hudGroup, minimapGroup;
        private float lowHydroxBlend;
        private readonly List<(Graphic graphic, Color normal, Color low)> lowHydroxTints = new();

        // Figma slot spacing and sizes, in HUD reference pixels.
        private const float SlotSize = 70f, SlotGap = 23f, ScreenMargin = 20f;

        private void BuildStatus()
        {
            status.gameObject.SetActive(false); // Replaced by the slots below; controls live in the inventory Help page.

            lowGasTint = OceanUiTheme.Node("Low Hydrox Tint", status.transform.parent).Fill().gameObject.AddComponent<UnityEngine.UI.Image>();
            lowGasTint.transform.SetAsFirstSibling();
            lowGasTint.raycastTarget = false;
            lowGasVignette = OceanUiTheme.Node("Low Hydrox Vignette", lowGasTint.transform).Fill(-160, -160, -160, -160).gameObject.AddComponent<UnityEngine.UI.Image>();
            lowGasVignette.sprite = OceanUiTheme.Vignette;
            lowGasVignette.raycastTarget = false;
            lowGasText = OceanUiTheme.Text("Hydrox Warning", status.transform.parent, font, 30, OceanUiTheme.Pink);
            lowGasText.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 80), new Vector2(760, 50));
            lowGasText.textWrappingMode = TextWrappingModes.NoWrap;
            lowGasText.gameObject.SetActive(false);

            var hud = OceanUiTheme.Node("Seafloor HUD", status.transform.parent).Fill();
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            hudGroup.interactable = hudGroup.blocksRaycasts = true;
            if (!minimapRoot.TryGetComponent(out minimapGroup)) minimapGroup = minimapRoot.gameObject.AddComponent<CanvasGroup>();
            minimapGroup.blocksRaycasts = false;
            BuildTank(hud);
            BuildSlots(hud);
        }

        /// <summary>Records a HUD graphic's normal color and its low-oxygen replacement.</summary>
        private void LowTint(Graphic graphic, Color low)
        {
            if (graphic != null) lowHydroxTints.Add((graphic, graphic.color, low));
        }

        private void ApplyLowHydroxTint(float amount)
        {
            foreach (var (graphic, normal, low) in lowHydroxTints)
                if (graphic != null) graphic.color = Color.Lerp(normal, low, amount);
        }

        /// <summary>Restyles the saved hydrox bar as the Figma tank: cyan shell, colored liquid, thirds markings.</summary>
        private void BuildTank(RectTransform hud)
        {
            tankRoot = OceanUiTheme.Node("Hydrox Tank", hud).Place(Vector2.zero, Vector2.zero, new Vector2(ScreenMargin, ScreenMargin + 6), new Vector2(76, 162));

            // Valve: neck, handle bar, and side nozzle with a dark joint.
            LowTint(Box(tankRoot, "Neck", OceanUiTheme.Cyan, 3f, new Vector2(18, 128), new Vector2(14, 22)), OceanUiTheme.LowOxygenShell);
            LowTint(Box(tankRoot, "Handle", OceanUiTheme.Cyan, 4f, new Vector2(0, 146), new Vector2(46, 12)), OceanUiTheme.LowOxygenShell);
            LowTint(Box(tankRoot, "Nozzle", OceanUiTheme.Cyan, 5f, new Vector2(44, 140), new Vector2(32, 9)), OceanUiTheme.LowOxygenShell);
            var joint = OceanUiTheme.Node("Joint", tankRoot).Place(Vector2.zero, Vector2.zero, new Vector2(35, 135), new Vector2(18, 18))
                .gameObject.AddComponent<UnityEngine.UI.Image>();
            joint.sprite = OceanUiTheme.Circle;
            joint.color = OceanUiTheme.Navy;
            joint.raycastTarget = false;

            // The saved "Hydrox Bar" becomes the shell and its "Hydrox Fill" the liquid, so existing references stay valid.
            hydroxRoot.SetParent(tankRoot, false);
            hydroxRoot.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(64, 132));
            foreach (Transform child in hydroxRoot)
                if (child != hydroxFill) child.gameObject.SetActive(false);
            var shell = hydroxRoot.GetComponent<Image>();
            OceanUiTheme.Rounded(shell, OceanUiTheme.RoundedRect, OceanUiTheme.Cyan, 12f);
            LowTint(shell, OceanUiTheme.LowOxygenShell);
            // The liquid fills an inset area, so anchorMax.y equals the remaining fraction and a nearly empty
            // tank still shows a visible red layer (Figma "TANK PROGRESSION" almost-empty stage).
            var liquidArea = OceanUiTheme.Node("Hydrox Liquid Area", hydroxRoot).Fill(5, 5, 5, 5);
            hydroxFill.SetParent(liquidArea, false);
            hydroxFill.anchorMin = Vector2.zero;
            hydroxFill.anchorMax = Vector2.one;
            hydroxFill.pivot = new Vector2(.5f, 0f);
            hydroxFill.offsetMin = Vector2.zero;
            hydroxFill.offsetMax = Vector2.zero;
            tankFillImage = hydroxFill.GetComponent<Image>();
            OceanUiTheme.Rounded(tankFillImage, OceanUiTheme.RoundedRect, OceanUiTheme.TankFull, 4f);
            for (int third = 1; third <= 2; third++)
            {
                var line = OceanUiTheme.Node("Hydrox divider", hydroxRoot).gameObject.AddComponent<UnityEngine.UI.Image>();
                line.rectTransform.anchorMin = new Vector2(0, third / 3f);
                line.rectTransform.anchorMax = new Vector2(1, third / 3f);
                line.rectTransform.offsetMin = new Vector2(5, -1);
                line.rectTransform.offsetMax = new Vector2(-5, 1);
                line.color = OceanUiTheme.White.WithAlpha(.85f);
                line.raycastTarget = false;
                LowTint(line, OceanUiTheme.LowOxygenText.WithAlpha(.85f));
            }
            tankWarning = OceanUiTheme.Text("Low Hydrox Mark", hydroxRoot, font, 72, OceanUiTheme.Pink, TextAlignmentOptions.Center, "!");
            tankWarning.rectTransform.Fill(0, 18, 0, 10);
            tankWarning.fontStyle = FontStyles.Bold;
            hydroxHighlight = hydroxRoot.gameObject.AddComponent<Outline>();
            hydroxHighlight.effectColor = new Color(1f, .8f, .35f);
            hydroxHighlight.effectDistance = new Vector2(3f, 3f);
            hydroxHighlight.enabled = false;
        }

        private static Image Box(RectTransform parent, string name, Color color, float radius, Vector2 position, Vector2 size)
        {
            var image = OceanUiTheme.Box(name, parent, color, null, radius);
            image.rectTransform.Place(Vector2.zero, Vector2.zero, position, size);
            return image;
        }

        private void BuildSlots(RectTransform hud)
        {
            slotRow = OceanUiTheme.Node("Item Slots", hud).Place(Vector2.right, Vector2.right,
                new Vector2(-ScreenMargin, ScreenMargin + 6), new Vector2(SlotSize * 3 + SlotGap * 2, SlotSize + 34));
            BuildWeightBar();
            telluriumCount = Slot(slotRow, 0, "Tellurium", "Tellurium").count;
            machineCount = Slot(slotRow, 1, "Mining Machine", "Machine").count;
            // Not clickable: a click here would also reach the seafloor Interact() handler (inventory opens with TAB).
            Slot(slotRow, 2, "Backpack", "Backpack", true);

            // Design doc HUD list: prospecting tool icon with its keybind, grayed out on cooldown; stimulant icon
            // with remaining count and keybind, only when canisters were equipped that day. Figma has no frame for
            // these, so they sit beside the tank, clear of the centred interaction prompt.
            toolRow = OceanUiTheme.Node("Tool Slots", hud).Place(Vector2.zero, Vector2.zero,
                new Vector2(ScreenMargin + 96, ScreenMargin + 6), new Vector2(SlotSize * 2 + SlotGap, SlotSize + 8));
            (prospectSlot, prospectCooldown) = Slot(toolRow, 0, "Prospecting Tool", "Prospector", key: "E");
            (stimulantSlot, stimulantCount) = Slot(toolRow, 1, "Stimulant", "Stimulant", key: "R");
            prospectCover = CooldownCover(prospectSlot, prospectCooldown);
            stimulantCooldown = CooldownCover(stimulantSlot, stimulantCount);
        }

        /// <summary>Cooldown wipe: a gray cover over the slot that clears from the top down as the cooldown runs.</summary>
        private static UnityEngine.UI.Image CooldownCover(CanvasGroup slot, TMP_Text count)
        {
            var cover = OceanUiTheme.Node("Cooldown", slot.transform).Fill(4, 4, 4, 4)
                .gameObject.AddComponent<UnityEngine.UI.Image>();
            cover.sprite = OceanUiTheme.Solid;
            cover.color = OceanUiTheme.Hex("2B3440").WithAlpha(.8f);
            cover.type = UnityEngine.UI.Image.Type.Filled;
            cover.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            cover.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
            cover.raycastTarget = false;
            cover.enabled = false;
            // Keep the count and key badge readable above the cover.
            count.transform.SetAsLastSibling();
            slot.transform.Find("Key")?.SetAsLastSibling();
            return cover;
        }

        /// <summary>Figma "Weight design": bag icon and a bar that fills and changes color with carried weight.</summary>
        private void BuildWeightBar()
        {
            var bar = OceanUiTheme.Node("Weight", slotRow).Place(Vector2.one, Vector2.one, Vector2.zero,
                new Vector2(SlotSize * 3 + SlotGap * 2, 26));
            // Figma weight icon, 21×22 at 1440 wide.
            var icon = OceanUiTheme.Node("Icon", bar).Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(7.5f, 0),
                new Vector2(15, 15.5f)).gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.sprite = OceanUiTheme.WeightIcon;
            icon.color = OceanUiTheme.Cyan;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            LowTint(icon, OceanUiTheme.LowOxygenShell);
            var track = OceanUiTheme.Box("Track", bar, OceanUiTheme.Navy.WithAlpha(.6f), OceanUiTheme.Cyan, 6f).rectTransform;
            track.anchorMin = new Vector2(0, .5f);
            track.anchorMax = new Vector2(1, .5f);
            track.offsetMin = new Vector2(21, -6.5f);
            track.offsetMax = new Vector2(0, 6.5f);
            LowTint(track.Find("Outline")?.GetComponent<Image>(), OceanUiTheme.LowOxygenShell);
            weightBarFill = OceanUiTheme.Box("Fill", track, OceanUiTheme.TankFull, null, 4.5f);
            weightBarFill.rectTransform.anchorMin = Vector2.zero;
            weightBarFill.rectTransform.offsetMin = new Vector2(2, 2);
            weightBarFill.rectTransform.offsetMax = new Vector2(-2, -2);
        }

        /// <summary>Builds one Figma HUD slot (slate rounded square, placeholder art, optional count and key badge), left to right in its row.</summary>
        private (CanvasGroup group, TMP_Text count) Slot(RectTransform row, int index, string name, string placeholder,
            bool ring = false, string key = null)
        {
            var slot = OceanUiTheme.Box(name + " Slot", row, OceanUiTheme.SlotFill, OceanUiTheme.Cyan, 9f);
            slot.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(index * (SlotSize + SlotGap), 0), Vector2.one * SlotSize);
            LowTint(slot, OceanUiTheme.LowOxygenFill);
            LowTint(slot.transform.Find("Outline")?.GetComponent<Image>(), OceanUiTheme.LowOxygenShell);
            var group = slot.gameObject.AddComponent<CanvasGroup>();
            if (ring)
            {
                var halo = OceanUiTheme.Box("Halo", slot.transform, Color.clear, OceanUiTheme.Cyan, 12f).rectTransform;
                halo.Fill(-5, -5, -5, -5);
                LowTint(halo.Find("Outline")?.GetComponent<Image>(), OceanUiTheme.LowOxygenShell);
            }
            // Placeholder art until item icons exist.
            var art = OceanUiTheme.Box("Icon", slot.transform, OceanUiTheme.Cyan, null, 2f).rectTransform;
            art.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * 46f);
            LowTint(art.GetComponent<Image>(), OceanUiTheme.LowOxygenArt);
            var caption = OceanUiTheme.Text("Placeholder", art, font, 13, OceanUiTheme.Navy, TextAlignmentOptions.Center, placeholder);
            caption.rectTransform.Fill(2, 2, 2, 2);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 7;
            caption.fontSizeMax = 13;
            var count = OceanUiTheme.Text("Count", slot.transform, font, 18, OceanUiTheme.White, TextAlignmentOptions.BottomRight);
            count.rectTransform.Fill(4, 3, 6, 4);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            LowTint(count, OceanUiTheme.LowOxygenText);
            if (key != null)
            {
                var badge = OceanUiTheme.Box("Key", slot.transform, OceanUiTheme.Navy, OceanUiTheme.Cyan, 5f).rectTransform;
                badge.Place(Vector2.up, Vector2.up, new Vector2(-6, 6), Vector2.one * 24f);
                LowTint(badge.Find("Outline")?.GetComponent<Image>(), OceanUiTheme.LowOxygenShell);
                var keyLabel = OceanUiTheme.Text("Label", badge, font, 18, OceanUiTheme.Cyan, TextAlignmentOptions.Center, key);
                keyLabel.rectTransform.Fill();
                LowTint(keyLabel, OceanUiTheme.LowOxygenShell);
            }
            return (group, count);
        }

        /// <summary>HUD rects that the off-screen prospect arrows avoid.</summary>
        private IEnumerable<RectTransform> MarkerExclusionZones()
        {
            yield return tankRoot;
            yield return slotRow;
            yield return toolRow;
        }

        private void UpdateStatus()
        {
            bool inside = interactions.InsideSubmarine;
            bool loadout = interactions.LoadoutOpen;
            bool depleted = !loadout && interactions.Hydrox.IsDepleted;

            // Emergency state: hydrox is out and the 15 s breathing grace period is counting down.
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f);
            // Figma "Seafloor UI - low oxygen": maroon tint and vignette once the tank is almost empty,
            // pulsing harder after it runs out.
            float fill = interactions.Hydrox.NormalizedFill;
            bool lowHydrox = !loadout && fill <= OceanUiTheme.LowHydroxFraction;
            lowHydroxBlend = Mathf.MoveTowards(lowHydroxBlend, lowHydrox ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            ApplyLowHydroxTint(lowHydroxBlend);
            lowGasTint.color = OceanUiTheme.FailureBackdrop.WithAlpha((depleted ? .35f : .22f) * lowHydroxBlend);
            lowGasVignette.color = OceanUiTheme.TankEmpty.WithAlpha((depleted ? .55f + .2f * pulse : .3f + .15f * pulse) * lowHydroxBlend);
            lowGasText.gameObject.SetActive(depleted);
            lowGasText.text = interactions.Incapacitated ? "INCAPACITATED"
                : $"OUT OF HYDROX  •  RETURN TO SUBMARINE  {Mathf.CeilToInt(interactions.BreathingTimeLeft)}s";
            hudGroup.alpha = minimapGroup.alpha = depleted ? .6f : 1f;

            hydroxFill.anchorMax = new Vector2(1f, fill);
            tankFillImage.color = OceanUiTheme.TankColor(fill);
            tankFillImage.enabled = fill > 0f;
            tankWarning.gameObject.SetActive(lowHydrox);
            tankWarning.color = OceanUiTheme.Pink.WithAlpha(depleted ? 1f : .55f + .45f * pulse);

            int weight = interactions.CarriedWeight;
            int capacity = interactions.WeightCapacity;
            weightBarFill.rectTransform.anchorMax = new Vector2(capacity > 0 ? Mathf.Clamp01((float)weight / capacity) : 1f, 1f);
            weightBarFill.color = Color.Lerp(OceanUiTheme.WeightColor(weight, capacity), OceanUiTheme.LowOxygenAlert, lowHydroxBlend);
            weightBarFill.enabled = weight > 0;
            telluriumCount.text = interactions.Tellurium.ToString();
            machineCount.text = interactions.availableMachines.ToString();

            float cooldown = interactions.CooldownRemaining;
            prospectSlot.alpha = interactions.IsProspecting ? .4f : 1f;
            prospectCooldown.text = "";
            prospectCover.fillAmount = interactions.prospectCooldown > 0f ? cooldown / interactions.prospectCooldown : 0f;
            prospectCover.enabled = !interactions.IsProspecting && cooldown > 0f;
            int stimulants = interactions.CarriedCount(ItemCatalog.Stimulant);
            stimulantSlot.gameObject.SetActive(interactions.Loadout.Stimulants > 0 || stimulants > 0 || interactions.StimulantTimeLeft > 0f);
            float stimulantCooldownLeft = interactions.StimulantCooldownFraction;
            stimulantCooldown.fillAmount = stimulantCooldownLeft;
            stimulantCooldown.enabled = stimulantCooldownLeft > 0f;
            stimulantSlot.alpha = stimulants > 0 || stimulantCooldownLeft > 0f ? 1f : .4f;
            stimulantCount.text = stimulants.ToString();

            bool hideForOverlay = InventoryOpen || loadout;
            tankRoot.gameObject.SetActive(!inside && !hideForOverlay);
            slotRow.gameObject.SetActive(!inside && !hideForOverlay);
            toolRow.gameObject.SetActive(!inside && !hideForOverlay);
            minimapRoot.gameObject.SetActive(!inside && !InventoryOpen);
        }
    }
}
