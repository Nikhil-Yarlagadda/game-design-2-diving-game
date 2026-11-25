using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DivingPrototype
{
    /// <summary>Radar minimap and the SPACE-toggled arrows that point at off-screen prospected spots.</summary>
    public sealed partial class OceanHud
    {
        private UnityEngine.UI.Image[] markers;
        private RectTransform playerDirection;
        private RectTransform minimapSweep, minimapPlayer;
        private UnityEngine.UI.Image minimapDisc, minimapBorder, minimapSweepImage;
        private TMP_Text minimapNorth, submarineDistance;
        private RectTransform prospectOverlay;
        private readonly List<(OceanTarget target, RectTransform root, UnityEngine.UI.Image arrow, TMP_Text label)> prospectMarkers = new();
        private readonly HashSet<Vector2Int> occupiedMarkerCells = new();
        private readonly List<Rect> markerExclusions = new();
        private static Sprite minimapRingSprite, minimapSweepSprite;
        private const float MinimapSweepSpeed = 45f;

        private bool MinimapBuilt => markers != null;

        public static Vector2 MinimapPosition(Vector2 offset, float range, float radius)
            => Vector2.ClampMagnitude(offset / Mathf.Max(.001f, range), 1f) * radius;

        private static Sprite CreateMinimapSprite(int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha((x + .5f) / size - .5f, (y + .5f) / size - .5f) * 255f));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Sprite MinimapRingSprite => minimapRingSprite != null ? minimapRingSprite : (minimapRingSprite = CreateMinimapSprite(512, (u, v) =>
        {
            const float thickness = .017f;
            float radius = Mathf.Sqrt(u * u + v * v);
            float outer = Mathf.Clamp01((.5f - radius) * 512f + .5f);
            float inner = Mathf.Clamp01((radius - (.5f - thickness)) * 512f + .5f);
            return Mathf.Min(outer, inner);
        }));

        private static Sprite MinimapSweepSprite => minimapSweepSprite != null ? minimapSweepSprite : (minimapSweepSprite = CreateMinimapSprite(256, (u, v) =>
        {
            // Figma "Seafloor UI": a narrow solid wedge (about 25 degrees) with anti-aliased straight edges.
            const float wedgeRadians = 25f * Mathf.Deg2Rad;
            float radius = Mathf.Sqrt(u * u + v * v);
            float circle = Mathf.Clamp01((.5f - radius) * 256f + .5f);
            float leadEdge = Mathf.Clamp01(-u * 256f + .5f);
            float baseEdge = Mathf.Clamp01(v * 256f + .5f);
            float trail = Mathf.Atan2(-u, v);
            if (trail <= -Mathf.PI * .5f) return 0f;
            float trailEdge = Mathf.Clamp01((wedgeRadians - trail) * radius * 256f + .5f);
            return circle * leadEdge * baseEdge * trailEdge;
        }));

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

        private void BuildMinimap()
        {
            // Figma renders the minimap at ~210 px in the 1440 frame (about 150 px on the 1280 HUD).
            const float TargetMinimapSize = 150f;
            float mapWidth = minimapRoot.rect.width;
            if (mapWidth > 0f)
            {
                // Keep the existing top-right corner insets while scaling the map and all of its labels down.
                float rightInset = -minimapRoot.anchoredPosition.x - mapWidth * .5f;
                float topInset = -minimapRoot.anchoredPosition.y - minimapRoot.rect.height * .5f;
                minimapRoot.localScale = Vector3.one * (TargetMinimapSize / mapWidth);
                minimapRoot.anchoredPosition = new Vector2(-(rightInset + TargetMinimapSize * .5f),
                    -(topInset + TargetMinimapSize * .5f));
            }
            float diameter = minimapRoot.rect.width;
            minimapDisc = Image("Minimap Background", OceanUiTheme.Circle, diameter - 2f, OceanUiTheme.SlotFill.WithAlpha(.8f));
            minimapSweep = Image("Minimap Sweep", MinimapSweepSprite, diameter - 5f, OceanUiTheme.Cyan.WithAlpha(.5f)).rectTransform;
            minimapSweepImage = minimapSweep.GetComponent<UnityEngine.UI.Image>();
            minimapBorder = Image("Minimap Border", MinimapRingSprite, diameter, OceanUiTheme.Cyan);
            minimapNorth = Label("Minimap North", 15, new Vector2(0, diameter * .5f + 13));
            minimapNorth.text = "N";
            minimapNorth.color = OceanUiTheme.Cyan;
            submarineDistance = Label("Submarine Distance", 16, new Vector2(0, -diameter * .5f - 17));
            submarineDistance.color = OceanUiTheme.Cyan;
            LowTint(minimapDisc, OceanUiTheme.LowOxygenFill.WithAlpha(.8f));
            LowTint(minimapSweepImage, OceanUiTheme.LowOxygenShell.WithAlpha(.5f));
            LowTint(minimapBorder, OceanUiTheme.LowOxygenShell);
            LowTint(minimapNorth, OceanUiTheme.LowOxygenShell);
            LowTint(submarineDistance, OceanUiTheme.LowOxygenShell);
            AddMinimapMarkers();
            minimapPlayer = Image("Minimap Player", OceanUiTheme.Circle, 6, OceanUiTheme.White).rectTransform;
            playerDirection = Image("Minimap Facing", OceanUiTheme.Triangle, 7, OceanUiTheme.White).rectTransform;
            playerDirection.sizeDelta = new Vector2(6, 9);
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
                markers[i] = Image(target.name + (machine ? " Machine" : "") + " Minimap Marker", submarine || machine ? squareSprite : OceanUiTheme.Circle,
                    submarine ? 7 : machine ? 6 : 4, submarine ? OceanUiTheme.Cyan : machine ? new Color(1f, .65f, .2f) : OceanUiTheme.White);
                if (submarine) markers[i].rectTransform.localRotation = Quaternion.Euler(0,0,45);
                if (machine) markers[i].gameObject.SetActive(target.HasMachine);
            }
            // Keep the home marker above overlapping item dots without changing its bearing.
            foreach (var marker in markers)
                if (marker != null && marker.name.StartsWith("Submarine")) marker.transform.SetAsLastSibling();
            if (minimapPlayer != null) minimapPlayer.SetAsLastSibling();
            if (playerDirection != null) playerDirection.SetAsLastSibling();
        }

        private void UpdateMinimap()
        {
            if (minimapSweep != null)
                minimapSweep.localRotation = Quaternion.Euler(0f, 0f, -(Time.unscaledTime * MinimapSweepSpeed) % 360f);
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

        private static readonly Vector2 ProspectMarkerSize = new Vector2(116f, 84f);
        private const float ProspectArrowSize = 48f;
        private const float ProspectEdgePadding = 8f;

        private void BuildProspectMarkers()
        {
            prospectOverlay = Overlay("Prospected Spots");
            prospectOverlay.pivot = Vector2.one * .5f;
            prospectOverlay.offsetMin = prospectOverlay.offsetMax = Vector2.zero;
            prospectOverlay.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            foreach (var target in interactions.targets)
            {
                if (target == null || target.kind != OceanTarget.TargetKind.MiningSpot) continue;
                var root = Panel("Prospected Spot", Vector2.zero, ProspectMarkerSize);
                root.SetParent(prospectOverlay, false);
                root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
                root.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                var arrow = OceanUiTheme.Node("Direction", root).gameObject.AddComponent<UnityEngine.UI.Image>();
                arrow.sprite = OceanUiTheme.Triangle;
                arrow.color = Color.white;
                arrow.raycastTarget = false;
                arrow.rectTransform.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * ProspectArrowSize);
                var label = OceanUiTheme.Text("Distance", root, font, 30f, Color.white, TextAlignmentOptions.Center);
                label.rectTransform.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(96f, 32f));
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.outlineWidth = .16f;
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
            Vector2 edge = Vector2.Max(half - (ProspectMarkerSize * .5f + Vector2.one * ProspectEdgePadding), Vector2.one);
            Vector2 player = ((Vector2)interactions.oceanCamera.WorldToViewportPoint(interactions.transform.position) * 2f - Vector2.one) * half;
            var origin = new Vector2(Mathf.Clamp(player.x, -edge.x, edge.x), Mathf.Clamp(player.y, -edge.y, edge.y));
            occupiedMarkerCells.Clear();
            markerExclusions.Clear();
            foreach (var hudElement in MarkerExclusionZones())
                if (hudElement != null && hudElement.gameObject.activeInHierarchy)
                    markerExclusions.Add(HudBounds(hudElement, hudElement.rect.min, hudElement.rect.max));
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
                bool vertical = x <= y;
                marker.root.anchoredPosition = PlaceProspectMarker(desired, edge, vertical);
                if (vertical)
                {
                    float outward = direction.x > 0f ? 1f : -1f;
                    marker.arrow.rectTransform.anchoredPosition = new Vector2(outward * 30f, 0f);
                    marker.label.rectTransform.anchoredPosition = new Vector2(-outward * 22f, 0f);
                    marker.label.rectTransform.sizeDelta = new Vector2(64f, 32f);
                    marker.label.alignment = direction.x > 0f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                }
                else
                {
                    float outward = direction.y > 0f ? 1f : -1f;
                    marker.arrow.rectTransform.anchoredPosition = new Vector2(0f, outward * 16f);
                    marker.label.rectTransform.anchoredPosition = new Vector2(0f, -outward * 24f);
                    marker.label.rectTransform.sizeDelta = new Vector2(96f, 32f);
                    marker.label.alignment = TextAlignmentOptions.Center;
                }
                Vector2 bearing = origin + direction - player;
                marker.arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(bearing.y, bearing.x) * Mathf.Rad2Deg - 90f);
                float distance = Vector2.Distance(interactions.transform.position, target.transform.position);
                marker.label.text = $"{distance:0}m";
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
            var last = new Vector2Int(Mathf.Max(1, Mathf.FloorToInt(2f * edge.x / ProspectMarkerSize.x)),
                Mathf.Max(1, Mathf.FloorToInt(2f * edge.y / ProspectMarkerSize.y)));
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
                    var bounds = new Rect(position - ProspectMarkerSize * .5f, ProspectMarkerSize);
                    bool blocked = false;
                    foreach (var exclusion in markerExclusions)
                        if (bounds.Overlaps(exclusion)) { blocked = true; break; }
                    if (!blocked && occupiedMarkerCells.Add(cell)) return position;
                }
            }
            return desired;
        }
    }
}
