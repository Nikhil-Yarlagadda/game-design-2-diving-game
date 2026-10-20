using UnityEngine;

namespace DivingPrototype
{
    [RequireComponent(typeof(OceanInteractions))]
    public sealed class OceanTutorial : MonoBehaviour
    {
        public int Stage { get; private set; }
        public float VisibleSeconds { get; private set; }
        private bool discovered, placed, produced, retrieved, replaced;
        private OceanInteractions ocean;
        public string Text => Stage >= 1 && Stage <= 7 ? Messages[Stage - 1] : "";
        private static readonly string[] Messages = {
            "Stop moving, then <b>hold E for 3 seconds</b> to use your prospecting tool.",
            "The prospecting tool will reveal nearby tellurium mining spots.\nMove near a prospected mining spot, then left-click while it is highlighted to place a mining machine.",
            "Mining machines will collect tellurium over time. They will gradually deplete the mining spot.\n<b>You must re-prospect a spot to see if it has been depleted yet. Prospect your mining spots frequently, or you may be mining less tellurium than you think.</b>",
            "Move near a mining machine, then left-click while it is highlighted to retrieve it and the tellurium it mined.",
            "Placed mining machines will continue to mine tellurium over time as long as their spot isn’t fully depleted.\nPlace a machine again. Keep prospecting for mining spots and placing machines to gather as much tellurium as you can <b>before your tank of breathing gas runs out.</b>",
            "Mining machines cannot be left out overnight, or they will permanently break.\n<b>Always retrieve all your mining machines before ending the excursion.</b>",
            "Keep an eye on your breathing gas, and return to the submarine whenever you’re ready to end the excursion.\n<b>Work hard. The Company is watching.</b>"
        };
        private void Awake() => ocean = GetComponent<OceanInteractions>();
        private void OnEnable()
        {
            ocean.ScanCompleted += OnScan;
            ocean.MachinePlaced += OnPlaced;
            ocean.OreProduced += OnProduced;
            ocean.PositiveRetrieval += OnRetrieved;
            ocean.ExcursionDeparted += BeginExcursion;
            ocean.ExcursionEnded += EndExcursion;
        }
        private void Start() => BeginExcursion();
        private void OnDisable()
        {
            ocean.ScanCompleted -= OnScan;
            ocean.MachinePlaced -= OnPlaced;
            ocean.OreProduced -= OnProduced;
            ocean.PositiveRetrieval -= OnRetrieved;
            ocean.ExcursionDeparted -= BeginExcursion;
            ocean.ExcursionEnded -= EndExcursion;
        }
        public void BeginExcursion()
        {
            discovered = placed = produced = retrieved = replaced = false;
            Stage = 1;
            VisibleSeconds = 0;
        }
        public void EndExcursion() { Stage = 0; VisibleSeconds = 0; }
        public void OnScan(int count) { if (Stage > 0 && count > 0) discovered = true; }
        public void OnPlaced() { if (Stage <= 0) return; placed = true; if (retrieved) replaced = true; }
        public void OnProduced(OceanTarget target) { if (Stage > 0) produced = true; }
        public void OnRetrieved() { if (Stage > 0) retrieved = true; }
        private void Update()
        {
            if (ocean.InsideSubmarine || ocean.IsTransitioning) return;
            // A machine deployed on an earlier excursion can already contain ore.
            foreach (var target in ocean.targets)
                if (target != null && target.HasMachine && target.StoredOre > 0) produced = true;
            Advance(Time.deltaTime);
        }
        public void Advance(float seconds)
        {
            if (Stage < 1 || Stage >= 8 || seconds < 0) return;
            VisibleSeconds += seconds;
            bool next = Stage == 1 ? discovered
                : Stage == 2 ? placed : Stage == 3 ? produced : Stage == 4 ? retrieved
                : Stage == 5 ? replaced : VisibleSeconds >= 30f;
            if (next && (Stage == 1 || VisibleSeconds >= 5f)) { Stage++; VisibleSeconds = 0; }
        }
    }
}
