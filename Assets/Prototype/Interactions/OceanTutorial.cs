using UnityEngine;

namespace DivingPrototype
{
    [RequireComponent(typeof(OceanInteractions))]
    public sealed class OceanTutorial : MonoBehaviour
    {
        public enum TutorialMode { None, Mining, Delivery, Depletion }
        public TutorialMode Mode { get; private set; }
        public bool IsVisible => Mode == TutorialMode.Delivery || Mode == TutorialMode.Depletion || (Mode == TutorialMode.Mining && Stage >= 1 && Stage <= Messages.Length);
        public const string DeliveryMessage = "The Company has dropped your purchased items into the waters surrounding the submarine. Your radar will point to their locations.\n<b>Collect your items today, or else they will be swept away by the currents.</b>";
        public const string DepletionMessage = "Mining machines will slowly deplete their mining spots over the course of several days. You must re-prospect a spot to see if it has been depleted yet.\n<b>Prospect your most frequently used mining spots once in a while, or you may be mining less tellurium than you think.</b>";
        public int Stage { get; private set; }
        public bool IsMiningComplete => Mode == TutorialMode.Mining && Stage > Messages.Length;
        public bool HighlightHydrox => Mode == TutorialMode.Mining && Stage == 6;
        public float VisibleSeconds { get; private set; }
        private bool discovered, placed, produced, retrieved, replaced;
        private OceanInteractions ocean;
        private Vector2 startPosition;
        public string Text => Mode == TutorialMode.Delivery ? DeliveryMessage : Mode == TutorialMode.Depletion ? DepletionMessage
            : IsVisible ? Messages[Stage - 1] : "";
        private static readonly string[] Messages = {
            "Swim with WASD. Move your flashlight using the mouse.",
            "Hold E to use your prospecting tool.",
            "The prospecting tool will reveal nearby tellurium mining spots.\nMove near a prospected mining spot, then left click on it to place a mining machine.",
            "Mining machines will collect tellurium over time.",
            "Left click on a mining machine to retrieve it and the tellurium that it mined.",
            "Placed mining machines will continue to mine tellurium over time.\nKeep prospecting for mining spots and placing machines to gather as much tellurium as you can <b>before your tank of breathing gas runs out.</b>",
            "Mining machines cannot be left out overnight, or else they will permanently break.\n<b>Always retrieve all your mining machines before ending the excursion.</b>",
            "Prospected mining spots will stay visible forever, even on future excursions. If you find yourself waiting around for your machines, try exploring and prospecting further out to get ahead for next time.\nKeep an eye on your breathing gas, and return to the submarine whenever you’re ready to end the excursion.\n<b>Work hard. The Company is watching.</b>"
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
            if (next) { Stage++; VisibleSeconds = 0; }
        }
    }
}
