using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DivingPrototype
{
    /// <summary>Shared palette, font, generated sprites, and widget builders for the excursion UI (Figma "UI-UX Prototypes").</summary>
    public static class OceanUiTheme
    {
        /// <summary>Figma frames are 1440x1024; the HUD canvas reference is 1280x720. Scaling by the height ratio keeps full-screen layouts on screen.</summary>
        public const float FigmaScale = 720f / 1024f;
        public static float Fig(float figmaPixels) => figmaPixels * FigmaScale;
        public static Vector2 Fig(float x, float y) => new Vector2(x, y) * FigmaScale;

        // Figma "COLOR PALETTE" frame.
        public static readonly Color Navy = Hex("152337");
        public static readonly Color Cyan = Hex("A4DDDB");
        public static readonly Color White = Color.white;
        public static readonly Color Slate = Hex("A8AEC8");
        public static readonly Color DeepTeal = Hex("042825");
        public static readonly Color Red = Hex("EE2121");
        public static readonly Color Pink = Hex("FE8D8D");
        public static readonly Color Gray = Hex("B3B0B0");
        public static readonly Color DarkGray = Hex("7B7B7B");
        // Equip screen locked-tank state.
        public static readonly Color MutedLine = Hex("405060");
        public static readonly Color MutedText = Hex("8E97A2");
        // Figma "Seafloor UI - low oxygen" HUD tint.
        public static readonly Color LowOxygenShell = Hex("B79A9F");
        public static readonly Color LowOxygenFill = Hex("7A4E5A");
        public static readonly Color LowOxygenArt = Hex("A58B8B");
        public static readonly Color LowOxygenText = Hex("EFB3B3");
        public static readonly Color LowOxygenAlert = Hex("E45B5B");

        // Surfaces sampled from the Figma frames.
        public static readonly Color PanelFill = Hex("1A2A3E");
        public static readonly Color PopupFill = Hex("5E7F87");
        public static readonly Color SlotFill = Hex("5E7F87");
        public static readonly Color MutedButton = Hex("7FA9AC");
        public static readonly Color DisabledText = new Color(1f, 1f, 1f, .45f);
        public static readonly Color FailureBackdrop = Hex("4A1C2B");

        // Figma "TANK PROGRESSION" frame (full, 2/3, 1/3, almost empty).
        public static readonly Color TankFull = Hex("589996");
        public static readonly Color TankTwoThirds = Hex("7A9958");
        public static readonly Color TankOneThird = Hex("999058");
        public static readonly Color TankEmpty = Hex("D25050");

        /// <summary>Figma "TIER DESIGN" frame, read top to bottom as tier 5 down to tier 1.</summary>
        public static Color TierMain(int tier) => tier switch
        {
            5 => Hex("AC42C7"), 4 => Hex("A4DDDB"), 3 => Hex("D3F6AE"), 2 => Hex("DBB539"), 1 => Hex("FFFFFF"), _ => DarkGray
        };

        /// <summary>Darker companion color for the inner circle of a tier marker.</summary>
        public static Color TierSmall(int tier) => tier switch
        {
            5 => Hex("732D85"), 4 => Hex("6D9997"), 3 => Hex("7A9959"), 2 => Hex("997F29"), 1 => Hex("AFAEAE"), _ => Hex("4A4A4A")
        };

        /// <summary>
        /// Figma "Weight design": the four tank colors keyed to the sample bars (about 25% teal, 50% green,
        /// 75% olive, full red), blended in between as the design note allows.
        /// </summary>
        public static Color WeightColor(int weight, int capacity)
        {
            if (capacity <= 0 || weight >= capacity) return TankEmpty;
            float load = Mathf.Clamp01((float)weight / capacity);
            if (load <= .25f) return TankFull;
            if (load <= .5f) return Color.Lerp(TankFull, TankTwoThirds, (load - .25f) * 4f);
            if (load <= .75f) return Color.Lerp(TankTwoThirds, TankOneThird, (load - .5f) * 4f);
            return Color.Lerp(TankOneThird, TankEmpty, (load - .75f) * 4f);
        }

        /// <summary>Blends the four Figma tank stages continuously from full to empty.</summary>
        public static Color TankColor(float fill)
        {
            fill = Mathf.Clamp01(fill);
            if (fill >= 2f / 3f) return Color.Lerp(TankTwoThirds, TankFull, (fill - 2f / 3f) * 3f);
            if (fill >= 1f / 3f) return Color.Lerp(TankOneThird, TankTwoThirds, (fill - 1f / 3f) * 3f);
            return Color.Lerp(TankEmpty, TankOneThird, Mathf.InverseLerp(LowHydroxFraction, 1f / 3f, fill));
        }

        /// <summary>Remaining fraction at which the tank switches to the "almost empty" warning.</summary>
        public const float LowHydroxFraction = .15f;

        public static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }

        public static Color WithAlpha(this Color color, float alpha) { color.a = alpha; return color; }

        private static TMP_FontAsset font;
        private static bool fontLoaded;

        /// <summary>Jersey 20 (OFL) from Resources, or the scene font when the asset is missing.</summary>
        public static TMP_FontAsset Font(TMP_FontAsset fallback)
        {
            if (!fontLoaded)
            {
                font = Resources.Load<TMP_FontAsset>("Fonts/Jersey20 SDF");
                fontLoaded = true;
            }
            return font != null ? font : fallback;
        }

        // ---------- Generated sprites ----------

        private static readonly Dictionary<string, Sprite> sprites = new();
        private const int SliceTexture = 64;

        /// <summary>A 9-sliced rounded rectangle; Image.pixelsPerUnitMultiplier scales its corner radius.</summary>
        public static Sprite RoundedRect => Cached("rounded", () => SlicedSprite((x, y) => RoundedBox(x, y, 0f)));

        /// <summary>
        /// The border ring of <see cref="RoundedRect"/>. The ring is generated per thickness/radius ratio so that,
        /// once Image.pixelsPerUnitMultiplier scales the corner to <c>radius</c>, the line is <c>thickness</c> pixels wide.
        /// </summary>
        public static Sprite RoundedOutline(float thickness, float radius)
        {
            float hollow = Mathf.Clamp(Mathf.Round(24f * thickness / Mathf.Max(.5f, radius) * 2f) / 2f, 1f, 28f);
            return Cached($"rounded-outline-{hollow}", () => SlicedSprite((x, y) => RoundedBox(x, y, hollow)));
        }

        public static Sprite Circle => Cached("circle", () => ShapeSprite(256, (u, v) => Edge(.5f - Mathf.Sqrt(u * u + v * v), 256)));

        /// <summary>A triangle pointing up with rounded corners (Figma "ARROW NOTES").</summary>
        public static Sprite Triangle => Cached("triangle", () => ShapeSprite(256, RoundedTriangle));

        /// <summary>A dashed ring, used for tier markers.</summary>
        public static Sprite DashedRing(int dashes, float thickness) => Cached($"dash-{dashes}-{thickness}", () => ShapeSprite(512, (u, v) =>
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float ring = Mathf.Min(Edge(.5f - radius, 512), Edge(radius - (.5f - thickness), 512));
            float angle = (Mathf.Atan2(v, u) / (2f * Mathf.PI) + 1f) * dashes % 1f;
            float dash = Mathf.Min(Edge(angle - .12f, dashes * 2f), Edge(.88f - angle, dashes * 2f));
            return Mathf.Min(ring, dash);
        }));

        /// <summary>A ring open at the top right, for the Figma "retry" icon.</summary>
        public static Sprite OpenRing => Cached("open-ring", () => ShapeSprite(128, (u, v) =>
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float ring = Mathf.Min(Edge(.5f - radius, 128), Edge(radius - .36f, 128));
            float angle = Mathf.Atan2(v, u) * Mathf.Rad2Deg; // 0 = right, 90 = up
            return angle > 20f && angle < 80f ? 0f : ring;
        }));

        /// <summary>Transparent center fading to opaque edges for full-screen tints.</summary>
        public static Sprite Vignette => Cached("vignette", () => ShapeSprite(128, (u, v) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Sqrt(u * u + v * v) - .12f) / .62f))));

        /// <summary>Figma weight icon (exported from the "Weight design" bar), white so callers can tint it.</summary>
        public static Sprite WeightIcon => Cached("weight-icon", () => Resources.Load<Sprite>("OceanUi/WeightIcon"));
        /// <summary>Figma padlock (equip screen "Group 65") drawn over gear the player does not own.</summary>
        public static Sprite LockIcon => Cached("lock-icon", () => Resources.Load<Sprite>("OceanUi/LockIcon"));
        /// <summary>Plain white square, needed by Filled images (cooldown wipes).</summary>
        public static Sprite Solid => Cached("solid", () => ShapeSprite(4, (u, v) => 1f));

        /// <summary>The Figma weight icon followed by an "N lbs" caption, centred as one group.</summary>
        public static TMP_Text WeightLabel(Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var row = Node("Weight", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = size * .35f;
            layout.childAlignment = alignment == TextAlignmentOptions.MidlineLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var icon = Node("Icon", row).gameObject.AddComponent<Image>();
            icon.sprite = WeightIcon;
            icon.color = color;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconSize = icon.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = size * .8f;
            iconSize.preferredHeight = size * .83f;
            var text = Text("Value", row, font, size, color, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static Sprite Cached(string key, System.Func<Sprite> create)
        {
            if (!sprites.TryGetValue(key, out var sprite) || sprite == null) sprites[key] = sprite = create();
            return sprite;
        }

        private static float Edge(float signedDistance, float resolution) => Mathf.Clamp01(signedDistance * resolution + .5f);

        private static float RoundedBox(float x, float y, float hollow)
        {
            const float radius = 24f;
            // Signed distance to a rounded square filling the texture (negative inside).
            float half = SliceTexture * .5f;
            float qx = Mathf.Abs(x - half) - (half - radius);
            float qy = Mathf.Abs(y - half) - (half - radius);
            float dx = Mathf.Max(qx, 0f), dy = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(dx * dx + dy * dy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            float shape = Mathf.Clamp01(.5f - outside);
            return hollow <= 0f ? shape : Mathf.Min(shape, Mathf.Clamp01(outside + hollow + .5f));
        }

        private static float RoundedTriangle(float u, float v)
        {
            // Signed distance to an equilateral triangle, shrunk and inflated to round its corners.
            const float rounding = .07f;
            float k = Mathf.Sqrt(3f);
            // The centroid sits at the origin; nudge down so the bounding box (apex to base) is centered.
            float px = Mathf.Abs(u), py = v + .07f;
            float size = .36f - rounding;
            px -= size; py += size / k;
            if (px + k * py > 0f) { float nx = (px - k * py) * .5f, ny = (-k * px - py) * .5f; px = nx; py = ny; }
            px -= Mathf.Clamp(px, -2f * size, 0f);
            float distance = -Mathf.Sqrt(px * px + py * py) * Mathf.Sign(py) - rounding;
            return Edge(-distance, 256);
        }

        private static Sprite SlicedSprite(System.Func<float, float, float> alpha)
        {
            var texture = NewTexture(SliceTexture);
            var pixels = new Color32[SliceTexture * SliceTexture];
            for (int y = 0; y < SliceTexture; y++)
            for (int x = 0; x < SliceTexture; x++)
                pixels[y * SliceTexture + x] = new Color32(255, 255, 255, (byte)(alpha(x + .5f, y + .5f) * 255));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, SliceTexture, SliceTexture), Vector2.one * .5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(28, 28, 28, 28));
        }

        private static Sprite ShapeSprite(int size, System.Func<float, float, float> alpha)
        {
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha((x + .5f) / size - .5f, (y + .5f) / size - .5f) * 255));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }

        private static Texture2D NewTexture(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
        };

        // ---------- Widget builders ----------

        public static RectTransform Node(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Positions a rect by anchor, pivot, offset, and size in HUD reference pixels.</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Stretches a rect over its parent with optional insets (left, bottom, right, top).</summary>
        public static RectTransform Fill(this RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>A rounded box with an optional outline. Corner radius is in reference pixels.</summary>
        public static Image Box(string name, Transform parent, Color fill, Color? outline = null, float radius = 8f, float thickness = 2f)
        {
            var rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            Rounded(image, RoundedRect, fill, radius);
            if (outline is Color line)
            {
                var border = Node("Outline", rect).Fill().gameObject.AddComponent<Image>();
                Rounded(border, RoundedOutline(thickness, radius), line, radius);
            }
            return image;
        }

        /// <summary>A square-cornered framed box as used by the Figma equip and inventory panels. Recolor with <see cref="SetFrameColor"/>.</summary>
        public static Image Frame(string name, Transform parent, Color fill, Color outline, float thickness = 2f)
        {
            var rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = fill;
            image.raycastTarget = false;
            var border = Node("Border", rect).Fill();
            Edge(border, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, thickness), outline);
            Edge(border, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, thickness), outline);
            Edge(border, new Vector2(0, 0), new Vector2(0, 1), new Vector2(thickness, 0), outline);
            Edge(border, new Vector2(1, 0), new Vector2(1, 1), new Vector2(thickness, 0), outline);
            return image;
        }

        /// <summary>Changes the border color of a <see cref="Frame"/>.</summary>
        public static void SetFrameColor(Image frame, Color outline)
        {
            var border = frame.transform.Find("Border");
            if (border == null) return;
            foreach (var edge in border.GetComponentsInChildren<Image>()) edge.color = outline;
        }

        /// <summary>Adds only top and bottom horizontal rules, as used by Figma title and instruction banners.</summary>
        public static void HorizontalRules(RectTransform rect, Color color, float thickness = 2f)
        {
            var top = Node("Top Rule", rect).gameObject.AddComponent<Image>();
            top.rectTransform.anchorMin = new Vector2(0, 1);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.pivot = new Vector2(.5f, 1);
            top.rectTransform.anchoredPosition = Vector2.zero;
            top.rectTransform.sizeDelta = new Vector2(0, thickness);
            top.color = color;
            top.raycastTarget = false;
            var bottom = Node("Bottom Rule", rect).gameObject.AddComponent<Image>();
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(.5f, 0);
            bottom.rectTransform.anchoredPosition = Vector2.zero;
            bottom.rectTransform.sizeDelta = new Vector2(0, thickness);
            bottom.color = color;
            bottom.raycastTarget = false;
        }

        private static void Edge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
        {
            var edge = Node("Edge", parent).gameObject.AddComponent<Image>();
            edge.rectTransform.anchorMin = anchorMin;
            edge.rectTransform.anchorMax = anchorMax;
            edge.rectTransform.pivot = anchorMin;
            edge.rectTransform.sizeDelta = size;
            edge.rectTransform.anchoredPosition = Vector2.zero;
            edge.color = color;
            edge.raycastTarget = false;
        }

        public static void Rounded(Image image, Sprite sprite, Color color, float radius)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            image.pixelsPerUnitMultiplier = 24f / Mathf.Max(1f, radius);
        }

        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, string value = "")
        {
            var text = Node(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = value;
            return text;
        }

        public enum ButtonStyle { Primary, Secondary, Outline, Danger }

        /// <summary>A rounded button styled after the Figma dialogs. The caption is the first TMP_Text child.</summary>
        public static Button Button(string caption, Transform parent, TMP_FontAsset font, float fontSize, ButtonStyle style,
            UnityEngine.Events.UnityAction action, float radius = 4f)
        {
            Color fill = style switch
            {
                ButtonStyle.Primary => Cyan,
                ButtonStyle.Secondary => MutedButton,
                ButtonStyle.Danger => Hex("4E5866"),
                _ => Navy.WithAlpha(.6f)
            };
            Color label = style == ButtonStyle.Primary ? Navy : style == ButtonStyle.Danger ? Pink : style == ButtonStyle.Outline ? Cyan : White;
            var image = Box(caption, parent, fill, style == ButtonStyle.Primary ? White : Cyan, radius);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(.85f, .85f, .85f);
            colors.disabledColor = new Color(.6f, .6f, .6f, .55f);
            button.colors = colors;
            button.onClick.AddListener(action);
            var text = Text("Label", image.transform, font, fontSize, label, TextAlignmentOptions.Center, caption);
            text.rectTransform.Fill(6, 2, 6, 2);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        /// <summary>The "!" warning triangle drawn at the top of Figma alerts.</summary>
        public static RectTransform WarningIcon(Transform parent, TMP_FontAsset font, float size, Color fill, Color mark)
        {
            var root = Node("Warning", parent);
            root.sizeDelta = Vector2.one * size;
            var outline = Node("Border", root).Fill(-size * .06f, -size * .06f, -size * .06f, -size * .06f).gameObject.AddComponent<Image>();
            outline.sprite = Triangle;
            outline.color = White;
            outline.raycastTarget = false;
            var body = Node("Body", root).Fill().gameObject.AddComponent<Image>();
            body.sprite = Triangle;
            body.color = fill;
            body.raycastTarget = false;
            var bang = Text("Mark", root, font, size * .55f, mark, TextAlignmentOptions.Center, "!");
            bang.rectTransform.Fill(0, size * .04f, 0, size * .16f);
            bang.fontStyle = FontStyles.Bold;
            return root;
        }

        /// <summary>Swaps a rounded button between its resting and hovered Figma colors (fill, outline, caption).</summary>
        public static void HoverPalette(Button button, Color idleFill, Color idleOutline, Color idleText,
            Color hoverFill, Color hoverOutline, Color hoverText)
        {
            button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<OceanHoverPalette>()
                .Init(button, new[] { idleFill, idleOutline, idleText }, new[] { hoverFill, hoverOutline, hoverText });
        }
    }

    /// <summary>Runtime helper for <see cref="OceanUiTheme.HoverPalette"/>.</summary>
    public sealed class OceanHoverPalette : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image fill, outline;
        private TMP_Text label;
        private Color[] idle, hover;
        private bool hovered;

        public void Init(Button button, Color[] idleColors, Color[] hoverColors)
        {
            fill = button.targetGraphic as Image;
            var border = button.transform.Find("Outline");
            outline = border != null ? border.GetComponent<Image>() : null;
            label = button.GetComponentInChildren<TMP_Text>(true);
            idle = idleColors;
            hover = hoverColors;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; Apply(); }
        private void OnDisable() { hovered = false; Apply(); }

        private void Apply()
        {
            if (idle == null) return;
            var colors = hovered ? hover : idle;
            if (fill != null) fill.color = colors[0];
            if (outline != null) outline.color = colors[1];
            if (label != null) label.color = colors[2];
        }
    }
}
