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
        private float revealedUntil;
        private const float RevealDuration = 1.5f;
        private MaterialPropertyBlock properties;
        public int YieldAt(float now) => HasMachine ? Mathf.FloorToInt(Mathf.Max(0f, now - PlacedAt) * level / 60f) : 0;
        public float RevealAlpha(float now) => Mathf.Clamp01((revealedUntil - now) / RevealDuration);
        public void Reveal(float now) => revealedUntil = now + RevealDuration;

        public bool Place(float now)
        {
            if (kind != TargetKind.MiningSpot || HasMachine) return false;
            HasMachine = true;
            PlacedAt = now;
            if (machine != null) machine.SetActive(true);
            return true;
        }

        public int Retrieve(float now)
        {
            int amount = YieldAt(now);
            HasMachine = false;
            if (machine != null) machine.SetActive(false);
            return amount;
        }

        public void ShowHighlight(bool closest, float now)
        {
            if (highlight == null) return;
            properties ??= new MaterialPropertyBlock();
            float alpha = Mathf.Max(closest ? 1f : 0f, RevealAlpha(now));
            highlight.enabled = alpha > 0f;
            highlight.color = new Color(closest ? 1f : .2f, 1f, closest ? .45f : 1f, alpha);
            highlight.GetPropertyBlock(properties);
            properties.SetFloat("_Reveal", 1f);
            highlight.SetPropertyBlock(properties);
        }
    }
}
