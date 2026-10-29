using UnityEngine;

namespace DivingPrototype
{
    [RequireComponent(typeof(OceanInteractions))]
    public sealed class OceanTutorial : MonoBehaviour
    {
        public enum TutorialMode { None, Mining, Delivery, Depletion }
        public TutorialMode Mode { get; private set; }
        public bool IsVisible => Mode == TutorialMode.Delivery || Mode == TutorialMode.Depletion || (Mode == TutorialMode.Mining && Stage >= 1 && Stage <= Messages.Length);
        public const string DeliveryMessage = "The Company has dropped your purchased items near the submarine. White dots on the minimap show their locations.\nMove near a package and <b>left-click while it is highlighted</b> to collect it. Collect every package today, or the currents will sweep it away.";
        public const string DepletionMessage = "Mining slowly depletes a spot and reduces its output. The displayed tier is only its <b>last prospected tier</b>.\n<b>Hold E for 3 seconds</b> near your mining spots to check their current tiers. Re-prospect frequently used spots, or you may be collecting less tellurium than you think.";
        public int Stage { get; private set; }
        public bool IsMiningComplete => Mode == TutorialMode.Mining && Stage > Messages.Length;
        public float VisibleSeconds { get; private set; }
        private bool discovered, placed, produced, retrieved, replaced;
        private OceanInteractions ocean;
        private Vector2 startPosition;
        public string Text => Mode == TutorialMode.Delivery ? DeliveryMessage : Mode == TutorialMode.Depletion ? DepletionMessage : IsVisible
            ? Messages[Stage - 1] + (Stage == Messages.Length ? "\nReturn unlocks in " + Mathf.CeilToInt(Mathf.Max(0, 30f - VisibleSeconds)) + "s." : "") : "";
        private static readonly string[] Messages = {
            "<b>Swim with WASD. Aim your flashlight with the mouse.</b>\nSwim away from the submarine to begin. Left-click interacts with the nearest highlighted object. Complete this training before returning to the submarine.",
            "<b>Hold E for 3 seconds, even while swimming,</b> to prospect. Scanning automatically stops you and locks movement. <b>Release E to cancel</b> and swim again.\nFind at least one mining spot. If none appear, swim to a different area and try again after the 5-second cooldown.",
            "Prospecting reveals tellurium mining spots in your current view and their tiers. Higher tiers produce more tellurium.\nMove near a revealed spot, then <b>left-click while it is highlighted</b> to place a mining machine.",
            "Your machine is collecting tellurium. <b>Wait for it to collect some ore.</b>\nThe nearby prompt shows the amount collected. <b>Orange squares on the minimap mark deployed machines.</b> You can keep exploring while they mine.",
            "Move near your mining machine, then <b>left-click while it is highlighted</b> to retrieve both the machine and its collected tellurium.\nA machine can be reused after you collect it.",
            "<b>Place a machine again</b> on a revealed mining spot. Keep prospecting and placing machines to gather tellurium before your breathing gas runs out.\nWatch the Hydrox meter at the bottom of the screen.",
            "<b>Retrieve all your mining machines before ending the excursion.</b>\nMachines left out overnight will break. Return to each deployed machine and left-click while it is highlighted to collect it.",
            "Watch your Hydrox meter and leave enough gas to swim home. The <b>cyan diamond</b> on the minimap points to the submarine; distance is shown below.\nMove near it, <b>left-click while highlighted</b>, then choose <b>End excursion</b> in the confirmation."
        };
        private void Awake() => ocean = GetComponent<OceanInteractions>();
        private void OnEnable()
        {
            ocean.ScanCompleted += OnScan;
            ocean.MachinePlaced += OnPlaced;
            ocean.OreProduced += OnProduced;
            ocean.PositiveRetrieval += OnRetrieved;
            ocean.DeliveryCollected += OnDeliveryCollected;
            ocean.ExcursionEnded += EndExcursion;
        }
        private void Start() => BeginExcursion();
        private void OnDisable()
        {
            ocean.ScanCompleted -= OnScan;
            ocean.MachinePlaced -= OnPlaced;
            ocean.OreProduced -= OnProduced;
            ocean.PositiveRetrieval -= OnRetrieved;
            ocean.DeliveryCollected -= OnDeliveryCollected;
            ocean.ExcursionEnded -= EndExcursion;
        }
        public void BeginExcursion()
        {
            if (ocean == null) ocean = GetComponent<OceanInteractions>();
            discovered = placed = produced = retrieved = replaced = false;
            Stage = 0;
            Mode = TutorialMode.None;
            VisibleSeconds = 0;
            startPosition = transform.position;
            if (GameState.days == 2)
            {
                Mode = TutorialMode.Mining;
                Stage = 1;
            }
            else if (GameState.days > 2 && !GameState.deliveryTutorialShown && ocean.RemainingDeliveryCount > 0)
            {
                Mode = TutorialMode.Delivery;
                GameState.deliveryTutorialShown = true;
            }
            else if (GameState.days == 4) Mode = TutorialMode.Depletion;
        }
        public void EndExcursion() { Mode = TutorialMode.None; Stage = 0; VisibleSeconds = 0; }
        public void OnDeliveryCollected(int remaining)
        {
            if (Mode != TutorialMode.Delivery || remaining != 0) return;
            if (GameState.days == 4) { Mode = TutorialMode.Depletion; VisibleSeconds = 0; }
            else EndExcursion();
        }
        public void OnScan(int count) { if (Stage > 0 && count > 0) discovered = true; }
        public void OnPlaced() { if (Stage <= 0) return; placed = true; if (retrieved) replaced = true; }
        public void OnProduced(OceanTarget target) { if (Stage > 0) produced = true; }
        public void OnRetrieved() { if (Stage > 0) retrieved = true; }
        private void Update()
        {
            if (!IsVisible || ocean.InsideSubmarine || ocean.IsTransitioning) return;
            if (Mode == TutorialMode.Mining)
                // A machine deployed on an earlier excursion can already contain ore.
                foreach (var target in ocean.targets)
                    if (target != null && target.HasMachine && target.StoredOre > 0) produced = true;
            Advance(Time.deltaTime);
        }
        public void Advance(float seconds)
        {
            if (seconds < 0) return;
            if (Mode == TutorialMode.Depletion)
            {
                VisibleSeconds += seconds;
                if (VisibleSeconds >= 30f) EndExcursion();
                return;
            }
            if (Mode != TutorialMode.Mining || Stage < 1 || Stage > Messages.Length) return;
            VisibleSeconds += seconds;
            bool next = Stage == 1 ? Vector2.Distance(startPosition, transform.position) >= 1f
                : Stage == 2 ? discovered : Stage == 3 ? placed : Stage == 4 ? produced : Stage == 5 ? retrieved
                : Stage == 6 ? replaced : VisibleSeconds >= 30f;
            if (next && (Stage <= 2 || VisibleSeconds >= 5f)) { Stage++; VisibleSeconds = 0; }
        }
    }
}
