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

        private RectTransform Panel(string name, Vector2 position, Vector2 size)
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
        private TMP_Text PanelText(RectTransform panel, float size)
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
            tutorial = interactions.GetComponent<OceanTutorial>();
            tutorialPanel = Panel("Excursion Tutorial", new Vector2(0, 115), new Vector2(700, 160));
            tutorialText = PanelText(tutorialPanel, 19);
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
            markers = new UnityEngine.UI.Image[interactions.targets.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                var target = interactions.targets[i];
                if (target.kind == OceanTarget.TargetKind.MiningSpot) continue;
                bool submarine = target.kind == OceanTarget.TargetKind.Submarine;
                markers[i] = Image(target.name + " Minimap Marker", submarine ? squareSprite : circleSprite,
                    submarine ? 7 : 4, submarine ? new Color(.55f, .9f, 1f) : Color.white);
                if (submarine) markers[i].rectTransform.localRotation = Quaternion.Euler(0,0,45);
            }
            // Keep the home marker above overlapping item dots without changing its bearing.
            foreach (var marker in markers)
                if (marker != null && marker.name.StartsWith("Submarine")) marker.transform.SetAsLastSibling();
            Image("Minimap Player", circleSprite, 6, Color.white);
            playerDirection = Image("Minimap Facing", arrowSprite, 7, Color.white).rectTransform;
            playerDirection.sizeDelta = new Vector2(6,9);
        }

        private void LateUpdate()
        {
            if (markers == null) return;
            bool inside = interactions.InsideSubmarine;
            tutorialPanel.gameObject.SetActive(!inside && !interactions.IsTransitioning && tutorial != null && tutorial.Stage > 0 && tutorial.Stage < 8);
            if (tutorial != null) tutorialText.text = tutorial.Text;
            scanPanel.gameObject.SetActive(!inside && interactions.IsProspecting);
            scanFill.anchorMax = new Vector2(interactions.ScanProgress, 1);
            scanText.text = "PROSPECTING  " + (interactions.ScanProgress * interactions.prospectDuration).ToString("0.0") + " / " + interactions.prospectDuration.ToString("0") + "s";
            hydroxRoot.gameObject.SetActive(!inside);
            hydroxFill.anchorMax = new Vector2(interactions.Hydrox.NormalizedFill, 1f);
            interior.gameObject.SetActive(inside);
            minimapRoot.gameObject.SetActive(!inside);
            status.text = "OCEAN EXCURSION\nItems  " + interactions.ItemsCollected + "     Tellurium  " + interactions.Tellurium
                + "     Machines  " + interactions.availableMachines;
            message.text = interactions.Message;
            var target = interactions.Closest;
            prompt.text = inside ? "LEFT CLICK  Return to ocean" : target == null ? "" : target.kind == OceanTarget.TargetKind.Submarine
                ? "LEFT CLICK  Enter submarine" : target.kind == OceanTarget.TargetKind.Item ? "LEFT CLICK  Pick up item"
                : target.HasMachine ? "LEFT CLICK  Retrieve machine  |  " + target.YieldAt(Time.time) + " tellurium"
                : target.DisplayedTier == 0 ? "Depleted (last prospected)"
                : "Tier " + target.DisplayedTier + "  |  " + OceanTarget.Rate(target.DisplayedTier) + " / min (last prospected)  |  LEFT CLICK  Place machine";
            if (interactions.IsProspecting) prompt.text = "Hold E to finish prospecting • Release to cancel";
            if (!inside) status.text += "\nWASD  Swim    LEFT CLICK  Interact\nSTOP + HOLD E  Prospect (3s)"
                + (interactions.CooldownRemaining > 0f ? " (" + interactions.CooldownRemaining.ToString("0.0") + "s)" : " (ready)");
            if (inside) return;
            float radius = Mathf.Min(minimapRoot.rect.width, minimapRoot.rect.height) * .5f - 6;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null) continue;
                var tracked = interactions.targets[i];
                bool visible = tracked != null && tracked.isActiveAndEnabled;
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
