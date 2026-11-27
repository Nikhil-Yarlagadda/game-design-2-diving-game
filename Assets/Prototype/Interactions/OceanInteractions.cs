using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;

namespace DivingPrototype
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(SwimmerController), typeof(HydroxTank))]
    public sealed class OceanInteractions : MonoBehaviour
    {
        public Camera oceanCamera;
        public GameObject oceanMap;
        public OceanTarget[] targets;
        public Collider2D[] obstacles;
        public Vector2 spawnMin = new Vector2(-21f, -18f);
        public Vector2 spawnMax = new Vector2(21f, 18f);
        [Min(.1f)] public float interactionRange = 1.8f;
        [Min(0f)] public float prospectCooldown = 5f;
        [Min(.1f)] public float prospectDuration = 3f;
        [SerializeField] private LineRenderer prospectRing;
        public int availableMachines => CarriedCount(ItemCatalog.Machine);
        public OceanTarget deliveryPrefab;
        public GameObject panel;
        public OceanTarget dropPrefab;
        [Min(.1f)] public float deliveryRadius = 10f;
        [FormerlySerializedAs("interiorScene")] public string resultsScene = "Result";
        public int DeliveredItemCount { get; private set; }
        public int RemainingDeliveryCount { get; private set; }
        public event System.Action<int> DeliveryCollected;
        public OceanTarget Closest { get; private set; }
        public bool InsideSubmarine { get; private set; }
        public bool IsTransitioning => transitioning;
        public bool ReturnConfirmationOpen { get; private set; }
        public bool DepositOpen { get; private set; }
        public bool SafetyOfferOpen { get; private set; }
        public bool GearReminderOpen => LoadoutOpen && GameState.pendingGearReminder.Count > 0;
        internal bool TutorialBlocksInput => tutorial != null && tutorial.BlocksGameplayInput;
        public const int SafetyMachineCount = 3;
        public int SafetyOrderPrice => SafetyMachineCount * ItemCatalog.Find(ItemCatalog.Machine).Price;
        public int SafetyOrderBalance => Mathf.Max(GameState.MinimumCredits, GameState.credits - SafetyOrderPrice);
        public string SafetyOfferError { get; private set; }
        public bool ProspectMarkersEnabled { get; private set; }
        public int DeployedMachineCount
        {
            get { int count = 0; foreach (var target in targets) if (target != null && target.HasMachine) count++; return count; }
        }
        public bool TutorialBlocksReturn => GameState.days == 2 && Hydrox.NormalizedFill >= .15f
            && GetComponent<OceanTutorial>()?.IsMiningComplete != true;
        public HydroxTank Hydrox => hydrox != null ? hydrox : (hydrox = GetComponent<HydroxTank>());
        public int ItemsCollected { get; private set; }
        public int Tellurium => CarriedCount(ItemCatalog.Ore);
        public int DepositedTellurium => ItemCatalog.Count(deposited, ItemCatalog.Ore);
        public IReadOnlyDictionary<string, int> CarriedItems => carried;
        public GameState.DiveLoadout Loadout { get; private set; }
        public bool LoadoutOpen { get; private set; } = true;
        public bool Incapacitated => breathlessSeconds >= GraceSeconds;
        public float BreathingTimeLeft => Mathf.Max(0f, GraceSeconds - breathlessSeconds);
        /// <summary>True once an incapacitated diver is shown the "Excursion Failed" screen.</summary>
        public bool FailureScreenOpen { get; private set; }
        /// <summary>The day-2 tutorial dive restarts instead of ending the day.</summary>
        public bool FailureOffersRetry => GameState.days == 2;
        private float timeScaleBeforeFailure = 1f;
        public float BreathlessGraceSeconds => GraceSeconds;
        public float StimulantTimeLeft => Mathf.Max(0f, stimulantEnds - Time.time);
        /// <summary>Remaining share of the stimulant effect (1 just after use, 0 when it can be used again).</summary>
        public float StimulantCooldownFraction => StimulantTimeLeft / StimulantSeconds;
        public int WeightCapacity => ItemCatalog.Find(Loadout.Tank).WeightCapacity;
        public int CarriedWeight => LoadoutOpen ? LoadoutWeight : ItemCatalog.Find(Loadout.Tank).Weight
            + carried.Sum(pair => ItemCatalog.Find(pair.Key).Weight * pair.Value);
        public int LoadoutWeight => ItemCatalog.Find(Loadout.Tank).Weight + Loadout.Machines * ItemCatalog.Find(ItemCatalog.Machine).Weight
            + Loadout.Stimulants * ItemCatalog.Find(ItemCatalog.Stimulant).Weight
            + (Loadout.Flippers ? ItemCatalog.Find(ItemCatalog.Flippers).Weight : 0)
            + (Loadout.Camera ? ItemCatalog.Find(ItemCatalog.Camera).Weight : 0);
        public float CooldownRemaining => Mathf.Max(0f, nextProspect - Time.time);
        public string Message { get; private set; }
        public bool IsProspecting { get; private set; }
        public float ScanProgress => IsProspecting ? Mathf.Clamp01((Time.time - scanStarted) / prospectDuration) : 0f;
        public event System.Action<int> ScanCompleted;
        public event System.Action MachinePlaced, PositiveRetrieval, ExcursionEnded;
        public event System.Action<OceanTarget> OreProduced;
        /// <summary>Raised after a machine is picked up, with its spot and the tellurium it yielded.</summary>
        public event System.Action<OceanTarget, int> MachineRetrieved;
        /// <summary>Raised when the prospect sweep reveals a mining spot.</summary>
        public event System.Action<OceanTarget> SpotRevealed;
        /// <summary>Raised after a collected item is fully processed, with its pose and pickup snapshot.</summary>
        public event System.Action<Vector3, int, string, bool> ItemCollected;
        /// <summary>Raised after a quest sample is collected; the flag is true for the Eye photograph.</summary>
        public event System.Action<OceanTarget, string, bool> QuestItemCollected;
        private float scanStarted;
        private const float ProspectSweepDuration = .7f;
        private readonly List<(OceanTarget target, float distance)> pendingScanHits = new();
        private MaterialPropertyBlock prospectRingProperties;
        private static readonly int RingColor = Shader.PropertyToID("_Color");
        private float sweepStarted, sweepRange;
        private int sweepHits;
        private float messageUntil, nextProspect;
        private bool transitioning, sceneUnavailable;
        private HydroxTank hydrox;
        private bool CanSwim => Time.timeScale > 0f && !PauseManager.BlocksInput && !SceneChanger.IsTransitioning
            && !InsideSubmarine && !transitioning && !ReturnConfirmationOpen && !LoadoutOpen && !Incapacitated && !TutorialBlocksInput;
        private bool CanActInOcean => CanSwim && !Hydrox.IsDepleted && !IsProspecting;
        private const float GraceSeconds = 15f, StimulantSeconds = 20f;
        private float breathlessSeconds, incapacitatedSeconds, stimulantEnds;
        private readonly Dictionary<string, int> carried = new();
        private readonly Dictionary<string, int> deposited = new();
        private readonly Dictionary<string, int> collectedPurchasedGear = new();
        private int inventorySlots = 48;
        private float timeScaleBeforeReturn;
        private int resumeInputFrame = -1;
        private SwimmerController swimmer;
        private OceanTutorial tutorial;
        private FlashlightController flashlight;
        private bool EquippedCameraCarried => Loadout.Camera && CarriedCount(ItemCatalog.Camera) > 0;
        private Rigidbody2D body;
        private readonly HashSet<OceanTarget> deliveries = new HashSet<OceanTarget>();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private readonly System.Collections.Generic.List<Vector2> spawnCells = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.HashSet<Vector2Int> reachable = new System.Collections.Generic.HashSet<Vector2Int>();
        private const float SpawnCellSize = 1f;
        private const float TargetClearance = 1.5f;
        private Dictionary<string, GameState.MiningSpotState> tutorialStartMining;
        private Dictionary<string, int> tutorialStartOrders;

        private void Awake()
        {
#if UNITY_EDITOR
            GameStateDebugger.CaptureFirstOceanEntry(gameObject.scene.name);
#endif
            if (GameState.days == 2)
            {
                tutorialStartMining = new Dictionary<string, GameState.MiningSpotState>(GameState.miningSpots);
                tutorialStartOrders = new Dictionary<string, int>(GameState.itemsBought);
            }
            swimmer = GetComponent<SwimmerController>();
            tutorial = GetComponent<OceanTutorial>();
            body = GetComponent<Rigidbody2D>();
            flashlight = GetComponent<FlashlightController>();
            var inventory = FindAnyObjectByType<ExcursionInventory>();
            if (inventory != null) inventorySlots = inventory.columns * inventory.rows;
            Loadout = GameState.lastLoadout;
            SetLoadout(Loadout);
            swimmer.SetMovementLocked(true);
            BuildSpawnArea();
            SpawnDeliveries();
            SafetyOfferOpen = ItemCatalog.Count(GameState.inventory, ItemCatalog.Machine) == 0
                && !deliveries.Any(item => item.isActiveAndEnabled && item.itemName == ItemCatalog.Machine);
            foreach (var target in targets)
                if (target != null) target.OreProduced += OnOreProduced;
        }

#if UNITY_EDITOR
        internal void SyncDebugInventory()
        {
            if (LoadoutOpen) SetLoadout(Loadout);
        }

        internal void SetDebugTellurium(int amount)
        {
            if (LoadoutOpen) GameState.tellurium = amount;
            else SetDebugCarriedItem(ItemCatalog.Ore, amount);
        }

        /// <summary>Sets the live bag count and reapplies the effects of the carried inventory.</summary>
        internal void SetDebugCarriedItem(string id, int amount)
        {
            ItemCatalog.ChangeCount(carried, id, amount - CarriedCount(id));
            RecordCarriedOre();
            UpdateSpeed();
        }

        /// <summary>Preserves the post-loadout bag before the debugger unloads the excursion scene.</summary>
        internal void KeepDebugInventoryForDayJump()
        {
            if (LoadoutOpen || transitioning || InsideSubmarine) return;
            StoreCarriedItems();
            GameState.tellurium = Tellurium + DepositedTellurium;
        }
#endif

        public int CarriedCount(string item) => ItemCatalog.Count(carried, item);

        public void SetLoadout(GameState.DiveLoadout selection)
        {
            if (!LoadoutOpen) return;
            var tank = ItemCatalog.Find(selection.Tank);
            if (tank != null && tank.TankSeconds > 0 && ItemCatalog.Count(GameState.inventory, tank.Id) > 0)
                selection.Tank = tank.Id;
            else selection.Tank = ItemCatalog.LightTank;
            selection.Machines = Mathf.Clamp(selection.Machines, 0, ItemCatalog.Count(GameState.inventory, ItemCatalog.Machine));
            selection.Stimulants = Mathf.Clamp(selection.Stimulants, 0, ItemCatalog.Count(GameState.inventory, ItemCatalog.Stimulant));
            selection.Flippers &= ItemCatalog.Count(GameState.inventory, ItemCatalog.Flippers) > 0;
            selection.Camera &= ItemCatalog.Count(GameState.inventory, ItemCatalog.Camera) > 0;
            Loadout = selection;
        }

        public bool LoadoutFits => Slots(ItemCatalog.Machine, Loadout.Machines)
            + Slots(ItemCatalog.Stimulant, Loadout.Stimulants) + (Loadout.Flippers ? 1 : 0) + (Loadout.Camera ? 1 : 0) <= inventorySlots;

        /// <summary>Dismisses the pending gear reminder and optionally packs the items it lists.</summary>
        public void ResolveGearReminder(bool equip)
        {
            if (!GearReminderOpen || SafetyOfferOpen || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return;
            if (equip)
            {
                var pick = Loadout;
                if (GameState.pendingGearReminder.ContainsKey(ItemCatalog.Machine))
                    pick.Machines = ItemCatalog.Count(GameState.inventory, ItemCatalog.Machine);
                if (GameState.pendingGearReminder.ContainsKey(ItemCatalog.Stimulant))
                    pick.Stimulants = ItemCatalog.Count(GameState.inventory, ItemCatalog.Stimulant);
                pick.Flippers |= GameState.pendingGearReminder.ContainsKey(ItemCatalog.Flippers);
                pick.Camera |= GameState.pendingGearReminder.ContainsKey(ItemCatalog.Camera);
                var newestTank = GameState.pendingGearReminder.Keys.Select(ItemCatalog.Find)
                    .Where(item => item.TankSeconds > 0 && ItemCatalog.Count(GameState.inventory, item.Id) > 0)
                    .OrderByDescending(item => item.TankSeconds).FirstOrDefault();
                if (newestTank != null) pick.Tank = newestTank.Id;
                SetLoadout(pick);
            }
            GameState.pendingGearReminder.Clear();
        }

        public bool BeginDive()
        {
            if (!LoadoutOpen || SafetyOfferOpen || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return false;
            SetLoadout(Loadout);
            if (!LoadoutFits) return false;
            if (swimmer == null) swimmer = GetComponent<SwimmerController>();
            ItemCatalog.ChangeCount(carried, ItemCatalog.Machine, Loadout.Machines);
            ItemCatalog.ChangeCount(carried, ItemCatalog.Stimulant, Loadout.Stimulants);
            if (Loadout.Flippers) carried.Add(ItemCatalog.Flippers, 1);
            if (Loadout.Camera) carried.Add(ItemCatalog.Camera, 1);
#if UNITY_EDITOR
            ItemCatalog.ChangeCount(carried, ItemCatalog.Ore, GameState.tellurium);
#endif
            Hydrox.Equip(ItemCatalog.Find(Loadout.Tank).TankSeconds);
            LoadoutOpen = false;
            GameState.pendingGearReminder.Clear();
            GameState.Playtest.BeginDive(Loadout);
            tutorial?.BeginExcursion();
            RecordCarriedOre();
            swimmer.SetMovementLocked(false);
            UpdateSpeed();
            return true;
        }

        private static int Slots(string id, int count)
        {
            int stack = ItemCatalog.Find(id).Stack;
            return count / stack + (count % stack == 0 ? 0 : 1);
        }

        private bool CanCarry(string first, int count, int ore = 0)
        {
            int used = carried.Sum(pair => Slots(pair.Key, pair.Value));
            used += Slots(first, CarriedCount(first) + count) - Slots(first, CarriedCount(first));
            if (ore > 0) used += Slots(ItemCatalog.Ore, Tellurium + ore) - Slots(ItemCatalog.Ore, Tellurium);
            if (used <= inventorySlots) return true;
            Notify(InventoryFullMessage);
            return false;
        }

        private void UpdateSpeed()
        {
            float weight = (float)CarriedWeight / WeightCapacity;
            float speed = weight < .5f ? 1f : weight <= 1f ? 1.25f - .5f * weight : 1.75f - weight;
            bool flippersCarried = Loadout.Flippers && CarriedCount(ItemCatalog.Flippers) > 0;
            swimmer.EquipmentSpeed = Mathf.Max(0f, speed + StimulantTimeLeft / StimulantSeconds + (flippersCarried ? .3f : 0f));
            if (flashlight != null) flashlight.EquippedConeBonus = EquippedCameraCarried ? 10f : 0f;
            swimmer.BreathingSpeed = BreathingTimeLeft / GraceSeconds;
        }

        public bool UseStimulant()
        {
            if (!CanSwim || IsProspecting || StimulantTimeLeft > 0f || CarriedCount(ItemCatalog.Stimulant) <= 0) return false;
            ItemCatalog.ChangeCount(carried, ItemCatalog.Stimulant, -1);
            stimulantEnds = Time.time + StimulantSeconds;
            UpdateSpeed();
            return true;
        }

        private void SpawnDeliveries()
        {
            if (GameState.itemsBought.Values.All(count => count <= 0)) return;
            if (!TryGetDeliveryCenter(out Vector2 center))
            {
                Debug.LogError("Purchased deliveries need an item prefab and a submarine. Orders remain pending.", this);
                return;
            }
            foreach (string itemName in new List<string>(GameState.itemsBought.Keys))
            {
                var definition = ItemCatalog.Find(itemName);
                if (definition == null || definition.Stock == 0) continue;
                int pending = GameState.itemsBought[itemName];
                for (int i = 0; i < pending; i++)
                {
                    if (!TryFindDeliveryPosition(center, out Vector2 point))
                    {
                        Debug.LogWarning("No safe delivery position. Remaining orders will be delivered on a later excursion.", this);
                        return;
                    }
                    SpawnDelivery(definition, point);
                    GameState.itemsBought[itemName]--;
                }
            }
        }

        private bool TryGetDeliveryCenter(out Vector2 center)
        {
            var submarine = System.Array.Find(targets, target => target != null && target.kind == OceanTarget.TargetKind.Submarine);
            center = submarine != null ? (Vector2)submarine.transform.position : Vector2.zero;
            return deliveryPrefab != null && deliveryPrefab.kind == OceanTarget.TargetKind.Item && submarine != null && oceanMap != null;
        }

        private void SpawnDelivery(ItemCatalog.Definition definition, Vector2 point)
        {
            var item = Instantiate(deliveryPrefab, point, Quaternion.identity, oceanMap.transform);
            item.itemName = definition.Id;
            item.displayName = definition.Name;
            item.name = "Purchased " + definition.Id + " " + (DeliveredItemCount + 1);
            item.gameObject.SetActive(true);
            deliveries.Add(item);
            System.Array.Resize(ref targets, targets.Length + 1);
            targets[targets.Length - 1] = item;
            DeliveredItemCount++;
            RemainingDeliveryCount++;
        }

        public bool ResolveSafetyOffer(bool purchase)
        {
            if (!SafetyOfferOpen || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return false;
            if (purchase)
            {
                var positions = new List<Vector2>(SafetyMachineCount);
                if (TryGetDeliveryCenter(out Vector2 center))
                {
                    for (int i = 0; i < SafetyMachineCount && TryFindDeliveryPosition(center, out Vector2 point, positions); i++)
                        positions.Add(point);
                }
                if (positions.Count != SafetyMachineCount)
                {
                    SafetyOfferError = "No safe delivery space. Your credits have not changed.";
                    return false;
                }
                var machine = ItemCatalog.Find(ItemCatalog.Machine);
                foreach (var point in positions) SpawnDelivery(machine, point);
                int balance = SafetyOrderBalance;
                GameState.Playtest.Purchase(machine.Id, GameState.credits - balance, balance);
                GameState.credits = balance;
                // A tutorial retry respawns paid deliveries without charging again.
                if (tutorialStartOrders != null) ItemCatalog.ChangeCount(tutorialStartOrders, machine.Id, SafetyMachineCount);
            }
            SafetyOfferOpen = false;
            return true;
        }

        private bool TryFindDeliveryPosition(Vector2 center, out Vector2 point, List<Vector2> reserved = null)
        {
            bool Clear(Vector2 candidate) => CanSpawn(candidate, null) && (reserved == null
                || reserved.All(position => (position - candidate).sqrMagnitude >= TargetClearance * TargetClearance));
            for (int attempt = 0; attempt < 300; attempt++)
            {
                point = center + Random.insideUnitCircle * deliveryRadius;
                if (Clear(point)) return true;
            }
            foreach (var cell in spawnCells)
                if ((cell - center).sqrMagnitude <= deliveryRadius * deliveryRadius && Clear(cell))
                { point = cell; return true; }
            point = default;
            return false;
        }

        public void BuildSpawnArea()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            reachable.Clear();
            spawnCells.Clear();
            var seed = Cell(transform.position);
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            var visited = new System.Collections.Generic.HashSet<Vector2Int>();
            queue.Enqueue(seed);
            visited.Add(seed);
            var steps = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var point = Point(cell);
                if (!FreePoint(point)) continue;
                reachable.Add(cell);
                spawnCells.Add(point);
                foreach (var step in steps)
                {
                    var next = cell + step;
                    if (visited.Contains(next) || !FreePoint(Point(next)) || !FreeSegment(point, Point(next))) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        private Vector2Int Cell(Vector2 point) => new Vector2Int(Mathf.RoundToInt((point.x - spawnMin.x) / SpawnCellSize), Mathf.RoundToInt((point.y - spawnMin.y) / SpawnCellSize));
        private Vector2 Point(Vector2Int cell) => spawnMin + (Vector2)cell * SpawnCellSize;
        private bool FreePoint(Vector2 point)
        {
            if (point.x < spawnMin.x || point.x > spawnMax.x || point.y < spawnMin.y || point.y > spawnMax.y) return false;
            foreach (var obstacle in obstacles)
                if (obstacle != null && obstacle.isActiveAndEnabled && !obstacle.isTrigger && Vector2.Distance(point, obstacle.ClosestPoint(point)) < .7f) return false;
            return true;
        }
        private bool FreeSegment(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            int count = Physics2D.CircleCast(from, .5f, delta.normalized, new ContactFilter2D { useTriggers = false }, hits, delta.magnitude);
            for (int i = 0; i < count; i++)
                if (hits[i].collider.attachedRigidbody != body) return false;
            return true;
        }

        public bool IsReachable(Vector2 point) => reachable.Contains(Cell(point)) && FreeSegment(Point(Cell(point)), point);

        public bool CanSpawn(Vector2 point, OceanTarget item)
        {
            if (point.x < spawnMin.x || point.x > spawnMax.x || point.y < spawnMin.y || point.y > spawnMax.y) return false;
            if (!IsReachable(point)) return false;
            foreach (var obstacle in obstacles)
                if (obstacle != null && obstacle.isActiveAndEnabled && Vector2.Distance(point, obstacle.ClosestPoint(point)) < .7f) return false;
            foreach (var other in targets)
                if (other != null && other != item && other.gameObject.activeInHierarchy && Vector2.Distance(point, other.transform.position) < TargetClearance) return false;
            return true;
        }

        private void Update()
        {
            if (Time.timeScale == 0f || PauseManager.BlocksInput || ReturnConfirmationOpen || TutorialBlocksInput
                || Time.frameCount <= resumeInputFrame) return;
            if (panel != null && panel.activeSelf) { CancelProspect(); return; }
            if (Time.time > messageUntil) Message = "";
            if (transitioning || LoadoutOpen || InsideSubmarine) return;
            if (prospectRing.enabled) AdvanceProspectSweep(Time.time);
            if (GameState.Playtest.ActiveDive is { } attempt)
                attempt.activeSeconds += Time.deltaTime;
            var mouse = Mouse.current;
            bool pointerInside = mouse != null && oceanCamera != null && oceanCamera.pixelRect.Contains(mouse.position.ReadValue());
            float secondsOfGas = Hydrox.CurrentCapacity / Hydrox.ConsumptionRate;
            Hydrox.Advance(Time.deltaTime);
            if (Hydrox.IsDepleted)
            {
                CancelProspect();
                breathlessSeconds = Mathf.Min(GraceSeconds, breathlessSeconds + Mathf.Max(0f, Time.deltaTime - secondsOfGas));
                if (Incapacitated)
                {
                    swimmer.SetMovementLocked(true);
                    if (!sceneUnavailable && !FailureScreenOpen && (incapacitatedSeconds += Time.deltaTime) >= 1.5f)
                        OpenFailureScreen();
                    return;
                }
            }
            UpdateSpeed();
            RefreshClosest();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                ProspectMarkersEnabled = !ProspectMarkersEnabled;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) UseStimulant();
            if (Hydrox.IsDepleted)
            {
                if (pointerInside && mouse.leftButton.wasPressedThisFrame && Closest?.kind == OceanTarget.TargetKind.Submarine)
                    RequestEnterSubmarine();
                return;
            }
            if (IsProspecting)
            {
                if (keyboard == null || !keyboard.eKey.isPressed) CancelProspect();
                else if (Time.time - scanStarted >= prospectDuration) CompleteProspect(Time.time);
                return;
            }
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) Prospect(Time.time);
            if (IsProspecting || !pointerInside) return;
            if (mouse.leftButton.wasPressedThisFrame) Interact();
        }

        public void RefreshClosest()
        {
            Closest = null;
            float best = interactionRange;
            foreach (var target in targets)
            {
                if (target == null || !target.isActiveAndEnabled) continue;
                if (Hydrox.IsDepleted && target.kind != OceanTarget.TargetKind.Submarine) continue;
                if (target.kind == OceanTarget.TargetKind.MiningSpot && !target.IsProspected) continue;
                float distance = Vector2.Distance(transform.position, target.transform.position);
                if (distance <= best && HasClearPath(target.transform.position)) { best = distance; Closest = target; }
            }
            foreach (var target in targets)
                if (target != null && target.isActiveAndEnabled) target.ShowHighlight(target == Closest, Time.time);
        }

        private bool HasClearPath(Vector2 destination)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            Vector2 delta = destination - (Vector2)transform.position;
            int count = Physics2D.Raycast(transform.position, delta.normalized, filter, hits, delta.magnitude);
            for (int i = 0; i < count; i++)
                if (hits[i].collider.attachedRigidbody != body) return false;
            return true;
        }

        public bool Prospect(float now)
        {
            if (now < nextProspect || prospectRing.enabled || !CanActInOcean) return false;
            if (swimmer == null) swimmer = GetComponent<SwimmerController>();
            IsProspecting = true;
            scanStarted = now;
            swimmer.PropulsionLocked = true;
            return true;
        }

        public void CancelProspect()
        {
            IsProspecting = false;
            if (swimmer != null) swimmer.PropulsionLocked = false;
        }

        public void CompleteProspect(float now)
        {
            if (!IsProspecting || now - scanStarted < prospectDuration) return;
            CancelProspect();
            nextProspect = now + prospectCooldown;
            if (GameState.Playtest.ActiveDive is { } attempt) attempt.scans++;
            BeginProspectSweep(now);
        }

        /// <summary>Launches a sweep from the diver and captures the mining spots in view at launch.</summary>
        private void BeginProspectSweep(float now)
        {
            Vector3 center = transform.position;
            prospectRing.transform.position = center;
            Vector3 viewportCenter = oceanCamera.WorldToViewportPoint(center);
            Vector3 farCorner = oceanCamera.ViewportToWorldPoint(new Vector3(
                viewportCenter.x < .5f ? 1f : 0f, viewportCenter.y < .5f ? 1f : 0f, viewportCenter.z));
            sweepRange = Vector2.Distance(center, farCorner);
            sweepStarted = now;
            sweepHits = 0;
            pendingScanHits.Clear();
            foreach (var target in targets)
            {
                if (target == null || !target.isActiveAndEnabled || target.kind != OceanTarget.TargetKind.MiningSpot) continue;
                Vector3 viewport = oceanCamera.WorldToViewportPoint(target.transform.position);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                float distance = Vector2.Distance(center, target.transform.position);
                pendingScanHits.Add((target, distance));
                sweepRange = Mathf.Max(sweepRange, distance);
            }
            prospectRing.enabled = true;
            AdvanceProspectSweep(now);
        }

        /// <summary>Draws the wavefront and reveals each spot when the ring reaches its center.</summary>
        private void AdvanceProspectSweep(float now)
        {
            float progress = Mathf.Clamp01((now - sweepStarted) / ProspectSweepDuration);
            float radius = sweepRange * progress;
            prospectRing.transform.localScale = new Vector3(radius, radius, 1f);
            prospectRingProperties ??= new MaterialPropertyBlock();
            prospectRingProperties.SetColor(RingColor, new Color(1f, 1f, 1f, Mathf.Lerp(1f, .25f, progress)));
            prospectRing.SetPropertyBlock(prospectRingProperties);

            for (int i = pendingScanHits.Count - 1; i >= 0; i--)
            {
                var (target, distance) = pendingScanHits[i];
                if (distance > radius) continue;
                pendingScanHits.RemoveAt(i);
                if (target == null || !target.isActiveAndEnabled) continue;
                if (!target.IsProspected && GameState.Playtest.ActiveDive is { } attempt) attempt.spotsRevealed++;
                target.Reveal(now);
                sweepHits++;
                SpotRevealed?.Invoke(target);
            }

            if (progress < 1f) return;
            StopProspectSweep();
            ScanCompleted?.Invoke(sweepHits);
        }

        /// <summary>Discards any pending hits and hides the ring when the sweep or excursion ends.</summary>
        private void StopProspectSweep()
        {
            pendingScanHits.Clear();
            if (prospectRing != null) prospectRing.enabled = false;
        }

        private void OnOreProduced(OceanTarget target) => OreProduced?.Invoke(target);
        private void OnDisable()
        {
            CancelProspect();
            StopProspectSweep();
            if (ReturnConfirmationOpen) CancelReturn();
            if (FailureScreenOpen && !transitioning)
            {
                // Leaving the scene another way must not keep the whole game frozen.
                FailureScreenOpen = false;
                PauseManager.RestoreGameplayTimeScale(timeScaleBeforeFailure); // Keeps an open pause menu's saved scale correct.
            }
        }
        private void OnDestroy()
        {
            foreach (var target in targets)
                if (target != null) target.OreProduced -= OnOreProduced;
        }

        public bool PlaceMachine()
        {
            if (!CanActInOcean) return false;
            RefreshClosest();
            if (Closest == null || Closest.kind != OceanTarget.TargetKind.MiningSpot || InsideSubmarine) return false;
            if (availableMachines <= 0) { Notify("No machines left. Left-click a deployed machine to retrieve it."); return false; }
            if (!Closest.Place(Time.time)) return false;
            if (GameState.Playtest.ActiveDive is { } attempt) attempt.machinesDeployed++;
            ItemCatalog.ChangeCount(carried, ItemCatalog.Machine, -1);
            UpdateSpeed();
            Notify("Mining started: " + OceanTarget.Rate(Closest.DisplayedTier) + " tellurium / minute (last prospected).");
            MachinePlaced?.Invoke();
            return true;
        }

        public void Interact()
        {
            if (!CanSwim || IsProspecting) return;
            RefreshClosest();
            if (Closest == null) return;
            if (Closest.kind == OceanTarget.TargetKind.Submarine)
            {
                RequestEnterSubmarine();
                return;
            }
            if (Hydrox.IsDepleted) return;
            if (Closest.kind == OceanTarget.TargetKind.Item)
            {
                var item = ItemCatalog.Find(Closest.itemName);
                if (item == null || Closest.itemCount < 1 || (item.InteriorOnly && item.Id != ItemCatalog.Medicine)) return;
                if (!CanCarry(item.Id, Closest.itemCount)) return;
                Vector3 position = Closest.transform.position;
                int count = Closest.itemCount;
                bool isDelivery = deliveries.Remove(Closest);
                ItemsCollected++;
                ItemCatalog.ChangeCount(carried, item.Id, count);
                if (item.Id == ItemCatalog.Medicine) GameState.Playtest.Milestone("Medicine picked up");
                UpdateSpeed();
                Closest.gameObject.SetActive(false);
                if (isDelivery)
                {
                    if (GameState.Playtest.ActiveDive is { } attempt) attempt.deliveriesCollected++;
                    RemainingDeliveryCount--;
                    if (item.TankSeconds > 0 || item.Id is ItemCatalog.Machine or ItemCatalog.Stimulant or ItemCatalog.Flippers or ItemCatalog.Camera)
                        ItemCatalog.ChangeCount(collectedPurchasedGear, item.Id, Closest.itemCount);
                    DeliveryCollected?.Invoke(RemainingDeliveryCount);
                }
                Notify(item.Name + " collected.");
                ItemCollected?.Invoke(position, count, item.Name, isDelivery);
            }
            else if (Closest.HasWreck)
            {
                int ore = Closest.WreckOre;
                if (!CanCarry(ItemCatalog.Ore, ore)) return;
                Closest.ClearWreck();
                ItemCatalog.ChangeCount(carried, ItemCatalog.Ore, ore);
                if (GameState.Playtest.ActiveDive is { } attempt) attempt.salvaged += ore;
                UpdateSpeed();
                Notify($"Wreck salvaged. +{ore} tellurium.");
            }
            else if (Closest.kind is OceanTarget.TargetKind.Corpse or OceanTarget.TargetKind.AbyssEye)
            {
                string id = Closest.kind == OceanTarget.TargetKind.Corpse ? ItemCatalog.Fungus : ItemCatalog.EyePhoto;
                string blocked = QuestTargetBlock(Closest.kind);
                if (blocked != null)
                {
                    Notify(blocked);
                    return;
                }
                if (!CanCarry(id, 1)) return;
                ItemCatalog.ChangeCount(carried, id, 1);
                GameState.Playtest.Milestone(id == ItemCatalog.Fungus ? "Fungus picked up" : "Eye photo captured");
                UpdateSpeed();
                Notify(id == ItemCatalog.Fungus ? "Fungus sample collected." : "Photograph taken.");
                QuestItemCollected?.Invoke(Closest, ItemCatalog.Find(id).Name, id == ItemCatalog.EyePhoto);
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot && Closest.HasMachine)
            {
                if (!CanCarry(ItemCatalog.Machine, 1, Closest.YieldAt(Time.time))) return;
                int amount = Closest.Retrieve(Time.time);
                ItemCatalog.ChangeCount(carried, ItemCatalog.Ore, amount);
                if (GameState.Playtest.ActiveDive is { } attempt) attempt.retrieved += amount;
                ItemCatalog.ChangeCount(carried, ItemCatalog.Machine, 1);
                UpdateSpeed();
                if (amount > 0) PositiveRetrieval?.Invoke();
                Notify("Machine retrieved. +" + amount + " tellurium.");
                MachineRetrieved?.Invoke(Closest, amount);
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot) PlaceMachine();
            RecordCarriedOre();
        }

        /// <summary>Returns the reason a quest interaction is unavailable, or null when it can proceed.</summary>
        internal string QuestTargetBlock(OceanTarget.TargetKind kind)
        {
            bool corpse = kind == OceanTarget.TargetKind.Corpse;
            if (corpse && !GameState.adminUnlocked) return "You find nothing of use.";
            if (!corpse && !GameState.passkeyHintSeen) return "You see something in the abyss.";
            if (!corpse && !EquippedCameraCarried) return "Equip a camera module to photograph the Eye.";
            string id = corpse ? ItemCatalog.Fungus : ItemCatalog.EyePhoto;
            if (RecoveredCount(id) > 0 || ItemCatalog.Count(GameState.inventory, id) > 0)
                return "You already collected this.";
            return null;
        }

        public const string InventoryFullMessage = "Inventory full. Make room before collecting.";
        public void Notify(string message) { Message = message; messageUntil = Time.time + 3f; }

        public bool RequestEnterSubmarine()
        {
            if (!CanSwim || sceneUnavailable || IsProspecting) return false;
            RefreshClosest();
            if (Closest?.kind != OceanTarget.TargetKind.Submarine) return false;
            if (!Application.CanStreamedLevelBeLoaded(resultsScene))
            {
                sceneUnavailable = true;
                Notify("Results scene is unavailable.");
                Debug.LogError("Results scene is unavailable: " + resultsScene + ". Include it in the build scene list.", this);
                return false;
            }
            ReturnConfirmationOpen = true;
            timeScaleBeforeReturn = Time.timeScale;
            swimmer.SetMovementLocked(true);
            Time.timeScale = 0f;
            return true;
        }

        public void CancelReturn()
        {
            if (!ReturnConfirmationOpen) return;
            DepositOpen = false;
            ReturnConfirmationOpen = false;
            Time.timeScale = timeScaleBeforeReturn;
            swimmer.SetMovementLocked(false);
            // Consume the UI click before world interaction resumes.
            resumeInputFrame = Time.frameCount + 1;
        }

        public bool ConfirmReturn()
        {
            if (!ReturnConfirmationOpen || DepositOpen || TutorialBlocksReturn || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return false;
            CancelReturn();
            return EndExcursion(false);
        }

        /// <summary>Freezes the dive behind the "Excursion Failed" screen until the player picks an option.</summary>
        private void OpenFailureScreen()
        {
            FailureScreenOpen = true;
            timeScaleBeforeFailure = Time.timeScale;
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Leaves the failure screen: day 2 (the tutorial dive) restarts the excursion; other days settle the
        /// incapacitation penalty and continue to the results. Returns false if the transition could not start.
        /// </summary>
        public bool ResolveFailure()
        {
            if (!FailureScreenOpen || transitioning || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return false;
            // SceneChanger restores the scale captured when the fade starts, so resume normal time first.
            Time.timeScale = timeScaleBeforeFailure;
            if (FailureOffersRetry) RetryTutorial();
            else EndExcursion(true);
            if (transitioning) return true;
            Time.timeScale = 0f;
            return false;
        }

        private void RetryTutorial()
        {
            if (!SceneChanger.ChangeSceneWithFade(gameObject.scene.name, () =>
            {
                GameState.Playtest.FinishDive("Tutorial retry", Tellurium, 0, DeployedMachineCount, RemainingDeliveryCount);
                GameState.miningSpots = tutorialStartMining;
                GameState.itemsBought = tutorialStartOrders;
            }))
            {
                sceneUnavailable = true;
                return;
            }
            transitioning = true;
            StopOceanControls();
        }

        private bool EndExcursion(bool incapacitated)
        {
            if (transitioning) return false;
            if (!SceneChanger.ChangeSceneWithFade(resultsScene))
            {
                if (incapacitated) sceneUnavailable = true;
                return false;
            }
            transitioning = true;
            StopOceanControls();
            int abandoned = DeployedMachineCount;
            int missed = RemainingDeliveryCount;
            foreach (var target in targets)
                if (target != null) target.BreakOvernight(Time.time);
            bool medicAboard = GameState.IsCrewAboard("Medic");
            GameState.oreLost = incapacitated
                ? medicAboard ? Mathf.RoundToInt(Tellurium * Random.Range(.7f, .9f)) : Tellurium
                : 0;
            if (incapacitated && !medicAboard)
            {
                ItemCatalog.ChangeCount(carried, ItemCatalog.Machine,
                    -Mathf.RoundToInt(CarriedCount(ItemCatalog.Machine) * Random.Range(.2f, .5f)));
                ItemCatalog.ChangeCount(carried, ItemCatalog.Stimulant,
                    -Mathf.RoundToInt(CarriedCount(ItemCatalog.Stimulant) * Random.Range(.2f, .5f)));
            }
            bool recoveredFungus = GameState.dayFgfsCollected < 0 && RecoveredCount(ItemCatalog.Fungus) > 0;
            StoreCarriedItems();
            foreach (var pair in collectedPurchasedGear)
                ItemCatalog.ChangeCount(GameState.pendingGearReminder, pair.Key, pair.Value);
            if (recoveredFungus) GameState.Playtest.Milestone("Fungus recovered");
            foreach (var item in deliveries)
                if (item != null) item.gameObject.SetActive(false);
            deliveries.Clear();
            RemainingDeliveryCount = 0;
            Closest = null;
            ExcursionEnded?.Invoke();
            InsideSubmarine = true;
            GameState.excursionIncapacitated = incapacitated;
            GameState.tellurium = Tellurium - GameState.oreLost + DepositedTellurium;
            GameState.Playtest.FinishDive(incapacitated ? "Incapacitated" : "Returned",
                Tellurium, GameState.oreLost, abandoned, missed);
            return true;
        }

        /// <summary>Reconciles the selected equipment with the non-ore items remaining in the excursion bag.</summary>
        private void StoreCarriedItems()
        {
            var storedItems = carried.Keys.Concat(deposited.Keys)
                .Concat(new[] { ItemCatalog.Machine, ItemCatalog.Stimulant, ItemCatalog.Flippers, ItemCatalog.Camera })
                .Distinct();
            foreach (string itemId in storedItems)
            {
                if (itemId == ItemCatalog.Ore) continue;
                int packed = itemId switch
                {
                    ItemCatalog.Machine => Loadout.Machines,
                    ItemCatalog.Stimulant => Loadout.Stimulants,
                    ItemCatalog.Flippers => Loadout.Flippers ? 1 : 0,
                    ItemCatalog.Camera => Loadout.Camera ? 1 : 0,
                    _ => 0
                };
                ItemCatalog.ChangeCount(GameState.inventory, itemId, RecoveredCount(itemId) - packed);
            }
            if (RecoveredCount(ItemCatalog.Fungus) > 0 && GameState.dayFgfsCollected < 0)
                GameState.dayFgfsCollected = GameState.days;
            if (RecoveredCount(ItemCatalog.Medicine) > 0) GameState.medicineDelivered = true;
            GameState.lastLoadout = Loadout;
        }

        /// <summary>Counts the units of a catalog item still carried or secured in the submarine.</summary>
        private int RecoveredCount(string id) => CarriedCount(id) + ItemCatalog.Count(deposited, id);

        /// <summary>Captures the live ore count after the excursion bag changes.</summary>
        private void RecordCarriedOre()
        {
            if (GameState.Playtest.ActiveDive is { } attempt) attempt.carriedNow = Tellurium;
        }

        public bool BeginDeposit()
        {
            if (!ReturnConfirmationOpen || DepositOpen || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return false;
            DepositOpen = true;
            return true;
        }

        /// <summary>Returns how many carried units can be deposited, reserving any equipped unit.</summary>
        public int DepositableCount(ItemCatalog.Definition item)
        {
            if (item == null) return 0;
            // The active tank is represented by the loadout, outside the carried bag.
            bool reserveCarriedUnit = (item.Id == ItemCatalog.Flippers && Loadout.Flippers)
                || (item.Id == ItemCatalog.Camera && Loadout.Camera);
            return Mathf.Max(0, CarriedCount(item.Id) - (reserveCarriedUnit ? 1 : 0));
        }

        public bool DepositItem(string itemName, int count)
        {
            if (!DepositOpen || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return false;
            var item = ItemCatalog.Find(itemName);
            if (count < 1 || count > DepositableCount(item)) return false;
            ItemCatalog.ChangeCount(carried, item.Id, -count);
            ItemCatalog.ChangeCount(deposited, item.Id, count);
            RecordCarriedOre();
            if (GameState.Playtest.ActiveDive is { } attempt) attempt.deposited = DepositedTellurium;
            UpdateSpeed();
            return true;
        }

        public bool DropItem(string itemName, int count = 1)
        {
            if (PauseManager.BlocksInput || transitioning || ReturnConfirmationOpen || SceneChanger.IsTransitioning
                || LoadoutOpen || Incapacitated || InsideSubmarine || TutorialBlocksInput) return false;
            var item = ItemCatalog.Find(itemName);
            if (item == null || !item.Droppable || count < 1 || count > CarriedCount(item.Id)) return false;
            
            // find something to clone
            var template = dropPrefab;
            if (template == null)
                foreach (var t in targets)
                    if (t != null && t.kind == OceanTarget.TargetKind.Item) { template = t; break; }

            if (template == null)
            {
                Notify("Can't drop that here.");
                return false;
            }

            if (!TryFindDropPoint(out var point))
            {
                Notify("Move away from the submarine before dropping items.");
                return false;
            }

            // create the pickup
            var drop = Instantiate(template, template.transform.parent);
            drop.kind = OceanTarget.TargetKind.Item;
            drop.itemName = item.Id;
            drop.itemCount = count;
            drop.displayName = item.Name;
            drop.name = "Dropped " + item.Name;
            drop.transform.position = point;
            drop.gameObject.SetActive(true);

            var list = new System.Collections.Generic.List<OceanTarget>(targets);
            list.Add(drop);
            targets = list.ToArray();

            ItemCatalog.ChangeCount(carried, item.Id, -count);
            RecordCarriedOre();
            UpdateSpeed();
            Notify($"{count} × {item.Name} dropped.");
            return true;
        }

        private bool TryFindDropPoint(out Vector2 point)
        {
            Vector2 origin = transform.position;
            float outerRadius = Mathf.Min(1.7f, interactionRange);
            for (int ring = 0; ring < 4; ring++)
            {
                float radius = Mathf.Lerp(Mathf.Min(.8f, outerRadius), outerRadius, ring / 3f);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2f / 12f;
                    point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (CanSpawn(point, null) && HasClearPath(point)) return true;
                }
            }
            // Keep the at-feet fallback for shedding weight away from the return point.
            point = origin;
            return !targets.Any(target => target != null && target.isActiveAndEnabled
                && target.kind == OceanTarget.TargetKind.Submarine
                && Vector2.Distance(origin, target.transform.position) < TargetClearance);
        }

        private void StopOceanControls()
        {
            CancelProspect();
            StopProspectSweep();
            body.linearVelocity = Vector2.zero;
            swimmer.enabled = false;
        }
    }
}
