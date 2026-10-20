using System;
using TMPro;
using UnityEngine;

namespace DivingPrototype
{
    public sealed class OceanTarget : MonoBehaviour
    {
        public enum TargetKind { Submarine, Item, MiningSpot }
        public TargetKind kind;
        [Min(1)] public int level = 1;
        public string displayName = "Item";
        public SpriteRenderer highlight;
        public GameObject machine;
        public bool HasMachine { get; private set; }
        public float PlacedAt { get; private set; }
        public bool IsProspected { get; private set; }
        public int DisplayedTier { get; private set; }
        public int ActualTier { get { InitializeMining(); return actualTier; } }
        public int StoredOre { get; private set; }
        public event Action<OceanTarget> OreProduced;
        private int actualTier, tierOutput, previousDisplayedTier;
        private bool initialized;
        private float nextTick, changedUntil;
        private SpriteRenderer spot;
        private TMP_Text label;
        private MaterialPropertyBlock properties;
        public static int Rate(int tier) => tier == 5 ? 24 : tier == 4 ? 12 : tier == 3 ? 6 : tier == 2 ? 4 : tier == 1 ? 2 : 0;
        public static float Interval(int tier) => tier >= 4 ? 5 : tier == 3 ? 10 : tier == 2 ? 15 : 30;
        private static int Allowance(int tier) => tier == 5 ? 60 : tier * 10;
        private void InitializeMining()
        {
            if (initialized) return;
            actualTier = Mathf.Clamp(level, 1, 5);
            initialized = true;
        }
        private void Awake()
        {
            if (kind != TargetKind.MiningSpot) return;
            InitializeMining();
            spot = GetComponent<SpriteRenderer>();
            if (spot != null) spot.enabled = false;
            if (highlight != null) highlight.enabled = false;
            var go = new GameObject("Prospected Tier", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, .85f, -.1f);
            label = go.GetComponent<TextMeshPro>();
            label.fontSize = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(5, 1);
            label.GetComponent<MeshRenderer>().sortingOrder = 30;
            label.gameObject.SetActive(false);
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
            if (produced > 0) OreProduced?.Invoke(this);
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
            RefreshPresentation(now);
        }
        public bool Place(float now)
        {
            if (kind != TargetKind.MiningSpot || HasMachine || !IsProspected || DisplayedTier == 0) return false;
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
        private Color TierColor => DisplayedTier == 5 ? new Color(.85f,.5f,1) : DisplayedTier == 4 ? new Color(1,.65f,.25f)
            : DisplayedTier == 3 ? new Color(.3f,.8f,1) : DisplayedTier == 2 ? new Color(.4f,1,.6f)
            : DisplayedTier == 1 ? new Color(.85f,.85f,.6f) : Color.gray;
        private void RevealRenderer(SpriteRenderer renderer, Color color)
        {
            if (renderer == null) return;
            renderer.color = color;
            properties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Reveal", 1f);
            renderer.SetPropertyBlock(properties);
        }
        private void RefreshPresentation(float now)
        {
            if (spot == null) spot = GetComponent<SpriteRenderer>();
            if (spot != null) { spot.enabled = IsProspected; if (IsProspected) RevealRenderer(spot, TierColor); }
            if (label == null) return;
            label.gameObject.SetActive(IsProspected);
            bool changed = now < changedUntil;
            label.color = changed ? Color.Lerp(TierColor, new Color(1,.65f,.1f), .5f + .5f * Mathf.Sin(now * 10)) : TierColor;
            string tier = DisplayedTier == 0 ? "Depleted" : "Tier " + DisplayedTier;
            label.text = changed ? "Tier " + previousDisplayedTier + " → " + tier : tier;
        }
        public void ShowHighlight(bool closest, float now)
        {
            if (highlight == null) return;
            bool mine = kind == TargetKind.MiningSpot;
            highlight.enabled = (!mine || IsProspected) && (closest || mine);
            if (highlight.enabled) RevealRenderer(highlight, closest ? Color.yellow : TierColor);
        }
    }
}
