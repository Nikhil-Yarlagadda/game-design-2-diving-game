using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

namespace DivingPrototype
{
    /// <summary>
    /// Incapacitation screen (Figma "Day 2 Failure" / "Regular Failure"). Day 2 is the tutorial dive, so it offers a retry;
    /// every other day continues to the results. MAIN MENU is shown but intentionally does nothing yet (no save system).
    /// </summary>
    public sealed partial class OceanHud
    {
        private RectTransform failureOverlay;
        private TMP_Text failureAction, failureReason;
        private RectTransform continueIcon, retryIcon, failureActionRect, failureMenuRect, failureReasonRect;

        private void BuildFailure()
        {
            failureOverlay = ThemedScreen("Excursion Failed");
            var backdrop = failureOverlay.gameObject.AddComponent<Image>();
            backdrop.color = Theme.FailureBackdrop;
            backdrop.raycastTarget = true;
            var glow = Theme.Node("Vignette", failureOverlay).Fill(-200, -200, -200, -200).gameObject.AddComponent<Image>();
            glow.sprite = Theme.Vignette;
            glow.color = Theme.Hex("5A2236").WithAlpha(.9f);
            glow.raycastTarget = false;

            var layout = Theme.Node("Layout", failureOverlay)
                .Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(LayoutWidth, LayoutHeight));
            Theme.WarningIcon(layout, font, 145f, Theme.Cyan, Theme.Navy)
                .Place(new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -12 * GridScale), new Vector2(160, 160));
            var title = Theme.Node("Title", layout);
            At(title, 254, 201, 516, 105);
            var titleFill = title.gameObject.AddComponent<Image>();
            titleFill.color = Theme.Navy;
            titleFill.raycastTarget = false;
            Theme.HorizontalRules(title, Theme.Cyan, 3f);
            Text(title, "EXCURSION FAILED", 62, Theme.White);
            failureReason = Text(At(Theme.Node("Reason", layout), 132, 338, 760, 80), "You ran out of oxygen.", 32, Theme.White);
            failureReasonRect = (RectTransform)failureReason.transform.parent;
            failureReason.textWrappingMode = TextWrappingModes.NoWrap;

            var action = Theme.Button("CONTINUE", layout, font, 26, Theme.ButtonStyle.Primary, ResolveFailure, 2f);
            action.name = "Failure Action";
            At((RectTransform)action.transform, 347, 450, 330, 60);
            failureActionRect = (RectTransform)action.transform;
            failureAction = action.GetComponentInChildren<TMP_Text>(true);
            // Figma button icons: arrow for CONTINUE, circular arrow for RETRY, list for MAIN MENU.
            continueIcon = ArrowIcon(ButtonIcon(action.transform, failureAction), Theme.Navy);
            retryIcon = RetryIcon(ButtonIcon(action.transform, failureAction), Theme.Navy);
            var menu = Theme.Button("MAIN MENU", layout, font, 26, Theme.ButtonStyle.Outline, ReturnToMainMenu, 2f);
            menu.name = "Main Menu";
            At((RectTransform)menu.transform, 347, 538, 330, 62);
            failureMenuRect = (RectTransform)menu.transform;
            MenuIcon(ButtonIcon(menu.transform, menu.GetComponentInChildren<TMP_Text>(true)), Theme.Cyan);
        }

        /// <summary>A 28 px icon slot at the button's left; the caption shifts right as in Figma.</summary>
        private static RectTransform ButtonIcon(Transform button, TMP_Text caption)
        {
            caption.rectTransform.offsetMin = new Vector2(56, caption.rectTransform.offsetMin.y);
            caption.rectTransform.offsetMax = new Vector2(-24, caption.rectTransform.offsetMax.y);
            return Theme.Node("Icon", button).Place(new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(46, 0), Vector2.one * 28f);
        }

        private static Image IconPart(RectTransform icon, string name, Sprite sprite, Vector2 position, Vector2 size, float angle, Color color)
        {
            var part = Theme.Node(name, icon).Place(Vector2.one * .5f, Vector2.one * .5f, position, size).gameObject.AddComponent<Image>();
            part.sprite = sprite;
            part.color = color;
            part.raycastTarget = false;
            part.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            return part;
        }

        private static RectTransform ArrowIcon(RectTransform icon, Color color)
        {
            IconPart(icon, "Shaft", null, Vector2.zero, new Vector2(26, 3), 0, color);
            IconPart(icon, "Head Up", null, new Vector2(8.5f, 4), new Vector2(13, 3), -45, color);
            IconPart(icon, "Head Down", null, new Vector2(8.5f, -4), new Vector2(13, 3), 45, color);
            return icon;
        }

        private static RectTransform RetryIcon(RectTransform icon, Color color)
        {
            IconPart(icon, "Arc", Theme.OpenRing, Vector2.zero, Vector2.one * 24f, 0, color);
            IconPart(icon, "Head", Theme.Triangle, new Vector2(3, 10), Vector2.one * 10f, -90, color);
            return icon;
        }

        private static RectTransform MenuIcon(RectTransform icon, Color color)
        {
            for (int row = -1; row <= 1; row++)
            {
                IconPart(icon, "Dot", Theme.Circle, new Vector2(-11, row * 8), Vector2.one * 4f, 0, color);
                IconPart(icon, "Line", null, new Vector2(3, row * 8), new Vector2(20, 3), 0, color);
            }
            return icon;
        }

        private void ResolveFailure() => interactions.ResolveFailure();
        private void ReturnToMainMenu() => interactions.ReturnToMainMenuFromFailure();

        private void UpdateFailure()
        {
            bool show = interactions.FailureScreenOpen;
            if (failureOverlay.gameObject.activeSelf != show)
            {
                failureOverlay.gameObject.SetActive(show);
                failureOverlay.SetAsLastSibling();
            }
            if (!show) return;
            bool retry = interactions.FailureOffersRetry;
            failureAction.text = retry ? "RETRY EXCURSION" : "CONTINUE";
            failureReason.text = retry
                ? "You ran out of oxygen."
                : "You ran out of oxygen.\nSome items from your inventory have been lost at sea";
            failureReason.fontSize = retry ? 30 : 32;
            // The retry frame fits one line; the CONTINUE frame gives the two-line reason room.
            At(failureReasonRect, 132, retry ? 330 : 338, 760, retry ? 48 : 80);
            At(failureActionRect, 347, retry ? 421 : 450, 330, 60);
            At(failureMenuRect, 347, retry ? 508 : 538, 330, 62);
            retryIcon.gameObject.SetActive(retry);
            continueIcon.gameObject.SetActive(!retry);
        }
    }
}
