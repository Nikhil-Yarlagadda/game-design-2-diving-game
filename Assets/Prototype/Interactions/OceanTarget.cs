using System;
using TMPro;
using UnityEngine;

namespace DivingPrototype
{
    public sealed class OceanTarget : MonoBehaviour
    {
        public enum TargetKind { Submarine, Item, MiningSpot, Corpse, AbyssEye }
        public TargetKind kind;
        public string corpseId;
        [SerializeField, Tooltip("Unique, stable ID for retaining this spot across excursions.")]
        private string persistentId;
        public string PersistentId => persistentId;
        [Min(1)] public int level = 1;
        public string displayName = "Item";
        public string itemName;
        public int itemCount = 1;
        public SpriteRenderer highlight;
        public GameObject machine;
        public bool HasMachine { get; private set; }
        public float PlacedAt { get; private set; }
        public bool IsProspected { get; private set; }
        public int DisplayedTier { get; private set; }
        public int ActualTier { get { InitializeMining(); return actualTier; } }
        public int StoredOre { get; private set; }
        public bool HasWreck { get; private set; }
        public int WreckOre { get; private set; }
        public event Action<OceanTarget> OreProduced;
        private int actualTier, tierOutput, previousDisplayedTier;
        private bool initialized;
        private float nextTick, changedUntil;
        private SpriteRenderer spot, fillDisc, outerRing, pillFill, pillOutline;
        private Sprite dashedOuterRingSprite;
        private static Sprite solidOuterRingSprite;
        private TMP_Text label;
        private MaterialPropertyBlock properties;
        private float markerDiameter = 1.35f, pillSpriteHeight = .64f, pillScale = .55f;
        private bool isClosest;
        private string lastLabelText;

        private static Sprite SolidOuterRingSprite
        {
            get
            {
                if (solidOuterRingSprite != null) return solidOuterRingSprite;
                const int size = 512;
                const float thickness = .085f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size - .5f;
                    float v = (y + .5f) / size - .5f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float outer = Mathf.Clamp01((.5f - radius) * size + .5f);
                    float inner = Mathf.Clamp01((radius - (.5f - thickness)) * size + .5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(outer, inner) * 255f));
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                solidOuterRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
                solidOuterRingSprite.hideFlags = HideFlags.DontSave;
                return solidOuterRingSprite;
            }
        }
        public static int Rate(int tier) => tier == 5 ? 24 : tier == 4 ? 12 : tier == 3 ? 6 : tier == 2 ? 4 : tier == 1 ? 2 : 0;
        public static float Interval(int tier) => tier >= 4 ? 5 : tier == 3 ? 10 : tier == 2 ? 15 : 30;
        private static int Allowance(int tier) => tier == 5 ? 60 : tier * 10;
        private void InitializeMining()
        {
            if (initialized) return;
            actualTier = Mathf.Clamp(level, 1, 5);
            if (CanPersist && GameState.miningSpots.TryGetValue(persistentId, out var saved))
            {
                actualTier = saved.ActualTier;
                tierOutput = saved.TierOutput;
                IsProspected = saved.IsProspected;
                DisplayedTier = saved.DisplayedTier;
                HasWreck = saved.HasWreck;
                WreckOre = saved.WreckOre;
            }
            initialized = true;
        }
        private bool CanPersist => Application.isPlaying && kind == TargetKind.MiningSpot && !string.IsNullOrEmpty(persistentId);
        private void SaveMiningState()
        {
            if (!CanPersist) return;
            GameState.miningSpots[persistentId] = new GameState.MiningSpotState {
                IsProspected = IsProspected, DisplayedTier = DisplayedTier,
                ActualTier = actualTier, TierOutput = tierOutput, HasWreck = HasWreck, WreckOre = WreckOre
            };
        }
        private SpriteRenderer CreateSpriteLayer(string name, Sprite sprite, Vector3 localPosition, Vector3 localScale,
            Material material, int sortingLayer, int sortingOrder, SpriteDrawMode drawMode = SpriteDrawMode.Simple)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = drawMode;
            if (material != null) renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }
        private void Awake()
        {
            if (kind != TargetKind.MiningSpot) return;
            InitializeMining();
            spot = GetComponent<SpriteRenderer>();
            if (HasWreck && machine != null) machine.SetActive(true);
            float spotDiameter = spot != null && spot.sprite != null ? spot.sprite.bounds.size.x : 1f;
            markerDiameter = highlight != null && highlight.sprite != null
                ? Mathf.Max(highlight.sprite.bounds.size.x * highlight.transform.localScale.x, spotDiameter * 1.35f)
                : spotDiameter * 1.35f;
            int sortingLayer = spot != null ? spot.sortingLayerID : highlight != null ? highlight.sortingLayerID : 0;
            int baseOrder = spot != null ? spot.sortingOrder : 15;
            int outerOrder = highlight != null ? highlight.sortingOrder : baseOrder + 5;
            Material baseMaterial = spot != null ? spot.sharedMaterial : highlight != null ? highlight.sharedMaterial : null;
            fillDisc = CreateSpriteLayer("Tier Disc", OceanUiTheme.Circle, Vector3.zero,
                new Vector3(markerDiameter, markerDiameter, 1f), baseMaterial, sortingLayer, baseOrder);
            if (spot != null)
            {
                spot.sprite = OceanUiTheme.DashedRing(10, .04f);
                spot.sortingOrder = baseOrder + 1;
                spot.enabled = false;
            }
            dashedOuterRingSprite = OceanUiTheme.DashedRing(12, .04f);
            if (highlight != null)
            {
                outerRing = highlight;
                outerRing.sprite = dashedOuterRingSprite;
                outerRing.transform.localScale = new Vector3(markerDiameter, markerDiameter, 1f);
                outerRing.sortingLayerID = sortingLayer;
                outerRing.sortingOrder = outerOrder;
                outerRing.enabled = false;
            }
            else
            {
                outerRing = CreateSpriteLayer("Tier Outer Ring", dashedOuterRingSprite, Vector3.zero,
                    new Vector3(markerDiameter, markerDiameter, 1f), baseMaterial, sortingLayer, outerOrder);
            }
            float pillHeight = markerDiameter * .26f;
            pillSpriteHeight = OceanUiTheme.RoundedRect.bounds.size.y;
            pillScale = pillHeight / pillSpriteHeight;
            var pillPosition = new Vector3(0f, -markerDiameter * .28f, 0f);
            var pillLocalScale = new Vector3(pillScale, pillScale, 1f);
            pillFill = CreateSpriteLayer("Tier Pill", OceanUiTheme.RoundedRect, pillPosition, pillLocalScale,
                baseMaterial, sortingLayer, outerOrder + 1, SpriteDrawMode.Sliced);
            pillOutline = CreateSpriteLayer("Tier Pill Outline", OceanUiTheme.RoundedOutline(2.5f, 10f), pillPosition,
                pillLocalScale, baseMaterial, sortingLayer, outerOrder + 2, SpriteDrawMode.Sliced);
            var go = new GameObject("Prospected Tier", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pillPosition;
            label = go.GetComponent<TextMeshPro>();
            const float labelScale = 10f;
            label.font = OceanUiTheme.Font(label.font);
            // High font size with an inverse transform keeps the pill the same on screen but samples the SDF much more sharply.
            label.fontSize = markerDiameter * 2.4f * labelScale;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(5f * labelScale, pillHeight * labelScale);
            go.transform.localScale = Vector3.one / labelScale;
            var labelRenderer = label.GetComponent<MeshRenderer>();
            labelRenderer.sortingLayerID = sortingLayer;
            labelRenderer.sortingOrder = outerOrder + 3;
            isClosest = false;
            RefreshPresentation(Time.time);
        }
        private void Update() => AdvanceMining(Time.time);
        private void LateUpdate() { if (kind == TargetKind.MiningSpot) RefreshPresentation(Time.time); }
        public int AdvanceMining(float now)
        {
            InitializeMining();
            int produced = 0;
            while (HasMachine && actualTier > 0 && now >= nextTick)
            {
                int amount = actualTier == 5 ? 2 : 1;
                StoredOre += amount;
                produced += amount;
                tierOutput += amount;
                if (tierOutput >= Allowance(actualTier)) { actualTier--; tierOutput = 0; }
                nextTick += Interval(actualTier);
            }
            if (produced > 0)
            {
                if (GameState.Playtest.ActiveDive is { } attempt) attempt.produced += produced;
                SaveMiningState();
                OreProduced?.Invoke(this);
            }
            return produced;
        }
        public int YieldAt(float now) { AdvanceMining(now); return HasMachine ? StoredOre : 0; }
        public float RevealAlpha(float now) => IsProspected ? 1f : 0f;
        public void Reveal(float now)
        {
            AdvanceMining(now);
            previousDisplayedTier = DisplayedTier;
            if (IsProspected && DisplayedTier != actualTier) changedUntil = now + 3f;
            DisplayedTier = actualTier;
            IsProspected = true;
            SaveMiningState();
            RefreshPresentation(now);
        }
        public bool Place(float now)
        {
            if (kind != TargetKind.MiningSpot || HasMachine || HasWreck || !IsProspected || DisplayedTier == 0) return false;
            InitializeMining();
            HasMachine = true;
            StoredOre = 0;
            PlacedAt = now;
            nextTick = now + Interval(actualTier);
            if (machine != null) machine.SetActive(true);
            return true;
        }
        public int Retrieve(float now)
        {
            if (!HasMachine) return 0;
            AdvanceMining(now);
            int amount = StoredOre;
            StoredOre = 0;
            HasMachine = false;
            if (machine != null) machine.SetActive(false);
            return amount;
        }

        public void BreakOvernight(float now)
        {
            if (!HasMachine) return;
            WreckOre = Mathf.FloorToInt(Retrieve(now) * UnityEngine.Random.Range(0f, .8f));
            HasWreck = true;
            actualTier = Mathf.Max(0, actualTier - 1);
            tierOutput = 0;
            SaveMiningState();
        }

        public void ClearWreck()
        {
            HasWreck = false;
            WreckOre = 0;
            if (machine != null) machine.SetActive(false);
            SaveMiningState();
        }
        public Color TierColor => OceanUiTheme.TierMain(DisplayedTier);
        private void RevealRenderer(SpriteRenderer renderer, Color color)
        {
            if (renderer == null) return;
            if (kind == TargetKind.MiningSpot && spot != null && spot.sharedMaterial != null && renderer.sharedMaterial != spot.sharedMaterial)
                renderer.sharedMaterial = spot.sharedMaterial;
            renderer.color = color;
            properties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Reveal", 1f);
            renderer.SetPropertyBlock(properties);
        }
        private void SetLayer(SpriteRenderer renderer, bool visible, Color color)
        {
            if (renderer == null) return;
            renderer.enabled = visible;
            if (visible) RevealRenderer(renderer, color);
        }
        private void ResizePill(string text)
        {
            if (pillFill == null && pillOutline == null) return;
            float textWidth = label != null ? label.GetPreferredValues(text).x * label.transform.localScale.x : 0f;
            float pillWidth = Mathf.Max(markerDiameter * .72f, textWidth + markerDiameter * .22f);
            var size = new Vector2(Mathf.Max(pillSpriteHeight, pillWidth / pillScale), pillSpriteHeight);
            if (pillFill != null) pillFill.size = size;
            if (pillOutline != null) pillOutline.size = size;
        }
        private void RefreshPresentation(float now)
        {
            if (spot == null) spot = GetComponent<SpriteRenderer>();
            if (outerRing == null) outerRing = highlight;
            if (outerRing != null)
            {
                Sprite desiredOuter = isClosest ? SolidOuterRingSprite : (dashedOuterRingSprite ??= OceanUiTheme.DashedRing(12, .04f));
                if (outerRing.sprite != desiredOuter) outerRing.sprite = desiredOuter;
            }
            Color main = TierColor;
            Color small = OceanUiTheme.TierSmall(DisplayedTier);
            bool changed = now < changedUntil;
            Color labelColor = changed ? Color.Lerp(main, new Color(1f, .65f, .1f), .5f + .5f * Mathf.Sin(now * 10f)) : main;
            // Tier 5 is purple on a dark purple disc; lift the pill text toward white so it reads as clearly as Tier 2.
            if (DisplayedTier == 5) labelColor = Color.Lerp(labelColor, Color.white, .6f);
            Color discColor = small.WithAlpha(isClosest ? .85f : .50f);
            Color innerColor = (isClosest ? Color.Lerp(small, main, .25f) : small).WithAlpha(isClosest ? 1f : .95f);
            Color outerColor = isClosest ? Color.Lerp(main, Color.white, .4f).WithAlpha(1f) : main.WithAlpha(.85f);
            Color pillFillColor = Color.clear; // Figma "TIER DESIGN": the label pill is outline-only over the disc.
            SetLayer(fillDisc, IsProspected, discColor);
            SetLayer(spot, IsProspected, innerColor);
            SetLayer(outerRing, IsProspected, outerColor);
            SetLayer(pillFill, IsProspected, pillFillColor);
            SetLayer(pillOutline, IsProspected, labelColor);
            if (label == null) return;
            label.gameObject.SetActive(IsProspected);
            if (!IsProspected) return;
            label.color = labelColor;
            string tier = DisplayedTier == 0 ? "Depleted" : "Tier " + DisplayedTier;
            string next = DisplayedTier == 0 ? "Depleted" : DisplayedTier.ToString();
            string text = HasWreck ? tier + " • Wreck"
                : changed ? "Tier " + previousDisplayedTier + " > " + next
                : tier;
            if (text != lastLabelText)
            {
                lastLabelText = text;
                label.text = text;
                ResizePill(text);
            }
        }
        public void ShowHighlight(bool closest, float now)
        {
            if (kind != TargetKind.MiningSpot)
            {
                if (highlight == null) return;
                highlight.enabled = closest;
                if (highlight.enabled) RevealRenderer(highlight, Color.yellow);
                return;
            }
            isClosest = closest;
            RefreshPresentation(now);
        }
    }
}
