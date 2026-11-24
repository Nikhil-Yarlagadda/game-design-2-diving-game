using UnityEngine;

namespace DivingPrototype
{
    [RequireComponent(typeof(OceanInteractions))]
    public sealed class OceanTutorial : MonoBehaviour
    {
        public enum TutorialMode { None, Mining, Delivery, Depletion, Markers, Inventory, Deposit }
        public TutorialMode Mode { get; private set; }
        public bool IsVisible => Text.Length > 0;
        public bool AwaitingAcknowledgement { get; private set; }
        internal bool BlocksGameplayInput => AwaitingAcknowledgement || Time.frameCount <= acknowledgedFrame;
        public const string DeliveryMessage = "The Company has dropped your purchased items into the waters surrounding the submarine. Your radar will point to their locations.\n<b>Collect your items today, or else they will be swept away by the currents.</b>";
        public const string DepletionMessage = "Mining machines will slowly deplete their mining spots over the course of several days. You must re-prospect a spot to see if it has been depleted yet.\n<b>Prospect your most frequently used mining spots once in a while, or you may be mining less tellurium than you think.</b>";
        private const string MarkersMessage = "Press SPACE to toggle markers that will guide you to all the mining spots you’ve prospected. Press SPACE again to toggle them off.";
        private const string InventoryMessage = "Carrying many items will slow you down, but weight capacity upgrades are available for purchase.\n\nAccess your inventory with TAB. From there, you can drop certain items on the ground. <b>You can only retrieve items on the same day you dropped them. They will be washed away by the current if you leave them out overnight.</b>";
        private const string DepositMessage = "You can also deposit items and tellurium at the submarine whenever you want to. Deposited items will enter the submarine’s storage <b>and cannot be re-equipped until the dive is over</b>.";
        private const float ReadingSeconds = 30f, AfterInventorySeconds = 10f;
        public int Stage { get; private set; }
        public bool IsMiningComplete => Mode == TutorialMode.Mining && Stage > Messages.Length;
        public bool HighlightHydrox => Mode == TutorialMode.Mining && Stage == 6;
        public float VisibleSeconds { get; private set; }
        private bool discovered, placed, produced, retrieved, placedAgain;
        private bool markersWereEnabled, inventoryOpened;
        private OceanInteractions ocean;
        private Vector2 startPosition;
        private float? timeScaleBeforePrompt;
        private int acknowledgedFrame = -1;
        public string Text => Mode switch
        {
            TutorialMode.Mining when Stage >= 1 && Stage <= Messages.Length => Messages[Stage - 1],
            TutorialMode.Delivery => DeliveryMessage,
            TutorialMode.Markers => MarkersMessage,
            TutorialMode.Depletion => DepletionMessage,
            TutorialMode.Inventory => InventoryMessage,
            TutorialMode.Deposit => DepositMessage,
            _ => ""
        };
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
        private void OnDisable()
        {
            ocean.ScanCompleted -= OnScan;
            ocean.MachinePlaced -= OnPlaced;
            ocean.OreProduced -= OnProduced;
            ocean.PositiveRetrieval -= OnRetrieved;
            ocean.DeliveryCollected -= OnDeliveryCollected;
            ocean.ExcursionEnded -= EndExcursion;
            EndExcursion();
        }
        public void BeginExcursion()
        {
            if (ocean == null) ocean = GetComponent<OceanInteractions>();
            EndExcursion();
            discovered = placed = produced = retrieved = placedAgain = false;
            inventoryOpened = false;
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
            else if (GameState.days == 4) BeginDayFour();
            PauseForNewBlock();
        }

        /// <summary>Clears tutorial progress and releases any pause owned by the current block.</summary>
        public void EndExcursion()
        {
            Mode = TutorialMode.None;
            Stage = 0;
            VisibleSeconds = 0;
            AwaitingAcknowledgement = false;
            acknowledgedFrame = -1;
            ReleasePromptPause();
        }

        /// <summary>Confirms the visible block and consumes the input for the rest of the frame.</summary>
        public void Acknowledge()
        {
            if (!AwaitingAcknowledgement || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return;
            AwaitingAcknowledgement = false;
            acknowledgedFrame = Time.frameCount;
            ReleasePromptPause();
        }

        /// <summary>Pauses simulation while the player reads a newly visible tutorial block.</summary>
        private void PauseForNewBlock()
        {
            if (!IsVisible) return;
            AwaitingAcknowledgement = true;
            if (!Application.isPlaying) return;
            timeScaleBeforePrompt = Time.timeScale;
            Time.timeScale = 0f;
        }

        /// <summary>Restores the simulation scale, including when the Escape menu is above the prompt.</summary>
        private void ReleasePromptPause()
        {
            if (timeScaleBeforePrompt is not float previousScale) return;
            timeScaleBeforePrompt = null;
            PauseManager.RestoreGameplayTimeScale(previousScale);
        }
        public void OnDeliveryCollected(int remaining)
        {
            if (Mode != TutorialMode.Delivery || remaining != 0) return;
            if (GameState.days == 4)
            {
                BeginDayFour();
                PauseForNewBlock();
            }
            else EndExcursion();
        }

        /// <summary>Starts day-four guidance with the current marker setting as its baseline.</summary>
        private void BeginDayFour()
        {
            Mode = TutorialMode.Markers;
            VisibleSeconds = 0;
            markersWereEnabled = ocean.ProspectMarkersEnabled;
        }

        public void OnScan(int count) { if (Stage > 0 && count > 0) discovered = true; }
        public void OnPlaced()
        {
            if (Stage <= 0) return;
            if (placed && (retrieved || Stage >= 6)) placedAgain = true;
            placed = true;
        }
        public void OnProduced(OceanTarget target) { if (Stage > 0) produced = true; }
        public void OnRetrieved() { if (Stage > 0) retrieved = true; }
        private void Update()
        {
            if (!IsVisible || BlocksGameplayInput || PauseManager.BlocksInput || SceneChanger.IsTransitioning
                || ocean.LoadoutOpen || ocean.InsideSubmarine || ocean.IsTransitioning || ocean.ReturnConfirmationOpen
                || ocean.Hydrox.IsDepleted) return;
            if (Mode == TutorialMode.Mining)
                // A machine deployed on an earlier excursion can already contain ore.
                foreach (var target in ocean.targets)
                    if (target != null && target.HasMachine && target.StoredOre > 0) produced = true;
            Advance(Time.deltaTime);
        }
        public void Advance(float seconds)
        {
            if (seconds < 0 || !IsVisible || AwaitingAcknowledgement || Mode == TutorialMode.Delivery) return;
            if (ocean.panel != null && ocean.panel.activeSelf)
            {
                if (Mode == TutorialMode.Inventory)
                {
                    inventoryOpened = true;
                    VisibleSeconds = 0;
                }
                return;
            }

            VisibleSeconds += seconds;
            if (Mode != TutorialMode.Mining)
            {
                AdvanceDayFour();
                return;
            }

            bool next = Stage == 1 ? Vector2.Distance(startPosition, transform.position) >= 3f
                : Stage == 2 ? discovered : Stage == 3 ? placed : Stage == 4 ? produced
                : Stage == 5 ? retrieved || VisibleSeconds >= ReadingSeconds
                : Stage == 6 ? placedAgain || VisibleSeconds >= ReadingSeconds : VisibleSeconds >= ReadingSeconds;
            if (next)
            {
                Stage++;
                VisibleSeconds = 0;
                PauseForNewBlock();
            }
        }

        /// <summary>Advances day-four guidance using elapsed reading time and the actual marker and inventory states.</summary>
        private void AdvanceDayFour()
        {
            TutorialMode next = Mode;
            switch (Mode)
            {
                case TutorialMode.Markers:
                    bool markersEnabled = ocean.ProspectMarkersEnabled;
                    if ((markersWereEnabled && !markersEnabled) || VisibleSeconds >= ReadingSeconds)
                        next = TutorialMode.Depletion;
                    markersWereEnabled = markersEnabled;
                    break;
                case TutorialMode.Depletion when VisibleSeconds >= ReadingSeconds:
                    next = TutorialMode.Inventory;
                    break;
                case TutorialMode.Inventory when inventoryOpened && VisibleSeconds >= AfterInventorySeconds:
                    next = TutorialMode.Deposit;
                    break;
                case TutorialMode.Deposit when VisibleSeconds >= ReadingSeconds:
                    next = TutorialMode.None;
                    break;
            }

            if (next == Mode) return;
            Mode = next;
            VisibleSeconds = 0;
            PauseForNewBlock();
        }
    }
}
