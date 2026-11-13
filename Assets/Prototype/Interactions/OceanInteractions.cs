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
        public int DeployedMachineCount
        {
            get { int count = 0; foreach (var target in targets) if (target != null && target.HasMachine) count++; return count; }
        }
        public bool TutorialBlocksReturn => GameState.days == 2 && Hydrox.NormalizedFill >= .15f
            && GetComponent<OceanTutorial>()?.IsMiningComplete != true;
        public HydroxTank Hydrox => hydrox != null ? hydrox : (hydrox = GetComponent<HydroxTank>());
        public int ItemsCollected { get; private set; }
        public int Tellurium => CarriedCount(ItemCatalog.Ore);
        public IReadOnlyDictionary<string, int> CarriedItems => carried;
        public GameState.DiveLoadout Loadout { get; private set; }
        public bool LoadoutOpen { get; private set; } = true;
        public bool Incapacitated => breathlessSeconds >= GraceSeconds;
        public float BreathingTimeLeft => Mathf.Max(0f, GraceSeconds - breathlessSeconds);
        public float StimulantTimeLeft => Mathf.Max(0f, stimulantEnds - Time.time);
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
        private float scanStarted;
        private float messageUntil, nextProspect;
        private bool transitioning, sceneUnavailable;
        private HydroxTank hydrox;
        private bool CanSwim => Time.timeScale > 0f && !PauseManager.BlocksInput && !SceneChanger.IsTransitioning
            && !InsideSubmarine && !transitioning && !ReturnConfirmationOpen && !LoadoutOpen && !Incapacitated;
        private bool CanActInOcean => CanSwim && !Hydrox.IsDepleted && !IsProspecting;
        private const float GraceSeconds = 15f, StimulantSeconds = 20f;
        private float breathlessSeconds, incapacitatedSeconds, stimulantEnds;
        private readonly Dictionary<string, int> carried = new();
        private int inventorySlots = 48;
        private float timeScaleBeforeReturn;
        private int resumeInputFrame = -1;
        private SwimmerController swimmer;
        private Rigidbody2D body;
        private readonly HashSet<OceanTarget> deliveries = new HashSet<OceanTarget>();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private readonly System.Collections.Generic.List<Vector2> spawnCells = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.HashSet<Vector2Int> reachable = new System.Collections.Generic.HashSet<Vector2Int>();
        private const float SpawnCellSize = 1f;
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
            body = GetComponent<Rigidbody2D>();
            var inventory = FindAnyObjectByType<ExcursionInventory>();
            if (inventory != null) inventorySlots = inventory.columns * inventory.rows;
            Loadout = GameState.lastLoadout;
            SetLoadout(Loadout);
            swimmer.SetMovementLocked(true);
            BuildSpawnArea();
            SpawnDeliveries();
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
            ItemCatalog.ChangeCount(carried, ItemCatalog.Ore, amount - Tellurium);
            if (!LoadoutOpen) UpdateSpeed();
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

        public bool BeginDive()
        {
            if (!LoadoutOpen || PauseManager.BlocksInput || SceneChanger.IsTransitioning) return false;
            SetLoadout(Loadout);
            if (!LoadoutFits) return false;
            if (swimmer == null) swimmer = GetComponent<SwimmerController>();
            ItemCatalog.ChangeCount(carried, ItemCatalog.Machine, Loadout.Machines);
            ItemCatalog.ChangeCount(carried, ItemCatalog.Stimulant, Loadout.Stimulants);
            if (Loadout.Flippers) carried.Add(ItemCatalog.Flippers, 1);
            if (Loadout.Camera) carried.Add(ItemCatalog.Camera, 1);
            Hydrox.Equip(ItemCatalog.Find(Loadout.Tank).TankSeconds);
            var flashlight = GetComponent<FlashlightController>();
            if (flashlight != null) flashlight.EquippedConeBonus = Loadout.Camera ? 10f : 0f;
            LoadoutOpen = false;
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
            Notify("Inventory full. Make room before collecting.");
            return false;
        }

        private void UpdateSpeed()
        {
            float weight = CarriedWeight / 100f;
            float speed = weight < .5f ? 1f : weight <= 1f ? 1.25f - .5f * weight : 1.75f - weight;
            swimmer.EquipmentSpeed = Mathf.Max(0f, speed + .5f * StimulantTimeLeft / StimulantSeconds + (Loadout.Flippers ? .3f : 0f));
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
            OceanTarget submarine = System.Array.Find(targets, t => t != null && t.kind == OceanTarget.TargetKind.Submarine);
            if (deliveryPrefab == null || deliveryPrefab.kind != OceanTarget.TargetKind.Item || submarine == null)
            {
                Debug.LogError("Purchased deliveries need an item prefab and a submarine. Orders remain pending.", this);
                return;
            }
            Vector2 center = submarine.transform.position;
            var all = new List<OceanTarget>(targets);
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
                    var item = Instantiate(deliveryPrefab, point, Quaternion.identity, oceanMap.transform);
                    item.itemName = definition.Id;
                    item.displayName = definition.Name;
                    item.name = "Purchased " + itemName + " " + (DeliveredItemCount + 1);
                    item.gameObject.SetActive(true);
                    deliveries.Add(item);
                    all.Add(item);
                    targets = all.ToArray();
                    DeliveredItemCount++;
                    RemainingDeliveryCount++;
                    GameState.itemsBought[itemName]--;
                }
            }
        }

        private bool TryFindDeliveryPosition(Vector2 center, out Vector2 point)
        {
            for (int attempt = 0; attempt < 300; attempt++)
            {
                point = center + Random.insideUnitCircle * deliveryRadius;
                if (CanSpawn(point, null)) return true;
            }
            foreach (var cell in spawnCells)
                if ((cell - center).sqrMagnitude <= deliveryRadius * deliveryRadius && CanSpawn(cell, null))
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
                if (other != null && other != item && other.gameObject.activeInHierarchy && Vector2.Distance(point, other.transform.position) < 1.5f) return false;
            return true;
        }

        private void Update()
        {
            if (Time.timeScale == 0f || PauseManager.BlocksInput || ReturnConfirmationOpen || Time.frameCount <= resumeInputFrame) return;
            if (panel != null && panel.activeSelf) { CancelProspect(); return; }
            if (Time.time > messageUntil) Message = "";
            if (transitioning || LoadoutOpen || InsideSubmarine) return;
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
                    if (!sceneUnavailable && (incapacitatedSeconds += Time.deltaTime) >= 1.5f)
                    {
                        if (GameState.days == 2) RetryTutorial();
                        else EndExcursion(true);
                    }
                    return;
                }
            }
            UpdateSpeed();
            RefreshClosest();
            var keyboard = Keyboard.current;
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
            if (now < nextProspect || !CanActInOcean) return false;
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
            int revealed = 0;
            foreach (var target in targets)
            {
                if (target == null || !target.isActiveAndEnabled || target.kind != OceanTarget.TargetKind.MiningSpot) continue;
                Vector3 viewport = oceanCamera.WorldToViewportPoint(target.transform.position);
                if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                { target.Reveal(now); revealed++; }
            }
            ScanCompleted?.Invoke(revealed);
            RefreshClosest();
        }

        private void OnOreProduced(OceanTarget target) => OreProduced?.Invoke(target);
        private void OnDisable()
        {
            CancelProspect();
            if (ReturnConfirmationOpen) CancelReturn();
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
                bool isDelivery = deliveries.Remove(Closest);
                ItemsCollected++;
                ItemCatalog.ChangeCount(carried, item.Id, Closest.itemCount);
                UpdateSpeed();
                Closest.gameObject.SetActive(false);
                if (isDelivery)
                {
                    RemainingDeliveryCount--;
                    DeliveryCollected?.Invoke(RemainingDeliveryCount);
                }
                Notify(item.Name + " collected.");
            }
            else if (Closest.HasWreck)
            {
                int ore = Closest.WreckOre;
                if (!CanCarry(ItemCatalog.Ore, ore)) return;
                Closest.ClearWreck();
                ItemCatalog.ChangeCount(carried, ItemCatalog.Ore, ore);
                UpdateSpeed();
                Notify($"Wreck salvaged. +{ore} tellurium.");
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot && Closest.HasMachine)
            {
                if (!CanCarry(ItemCatalog.Machine, 1, Closest.YieldAt(Time.time))) return;
                int amount = Closest.Retrieve(Time.time);
                ItemCatalog.ChangeCount(carried, ItemCatalog.Ore, amount);
                ItemCatalog.ChangeCount(carried, ItemCatalog.Machine, 1);
                UpdateSpeed();
                if (amount > 0) PositiveRetrieval?.Invoke();
                Notify("Machine retrieved. +" + amount + " tellurium.");
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot) PlaceMachine();
        }

        public void Notify(string message) { Message = message; messageUntil = Time.time + 3f; }

        public bool RequestEnterSubmarine()
        {
            if (!CanSwim || sceneUnavailable || IsProspecting) return false;
            RefreshClosest();
            if (Closest?.kind != OceanTarget.TargetKind.Submarine) return false;
            if (TutorialBlocksReturn)
            {
                Notify("Complete the tutorial before returning to the submarine.");
                return false;
            }
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
            ReturnConfirmationOpen = false;
            Time.timeScale = timeScaleBeforeReturn;
            swimmer.SetMovementLocked(false);
            // Consume the UI click before world interaction resumes.
            resumeInputFrame = Time.frameCount + 1;
        }

        public bool ConfirmReturn()
        {
            if (!ReturnConfirmationOpen) return false;
            CancelReturn();
            return EndExcursion(false);
        }

        private void RetryTutorial()
        {
            if (!SceneChanger.ChangeSceneWithFade(gameObject.scene.name, () =>
            {
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
            foreach (var target in targets)
                if (target != null) target.BreakOvernight(Time.time);
            ItemCatalog.ChangeCount(GameState.inventory, ItemCatalog.Machine, -Loadout.Machines);
            ItemCatalog.ChangeCount(GameState.inventory, ItemCatalog.Stimulant, -Loadout.Stimulants);
            if (Loadout.Flippers) ItemCatalog.ChangeCount(GameState.inventory, ItemCatalog.Flippers, -1);
            if (Loadout.Camera) ItemCatalog.ChangeCount(GameState.inventory, ItemCatalog.Camera, -1);
            foreach (var pair in carried)
                if (pair.Key != ItemCatalog.Ore) ItemCatalog.ChangeCount(GameState.inventory, pair.Key, pair.Value);
            if (CarriedCount(ItemCatalog.Medicine) > 0) GameState.medicineDelivered = true;
            GameState.lastLoadout = Loadout;
            foreach (var item in deliveries)
                if (item != null) item.gameObject.SetActive(false);
            deliveries.Clear();
            RemainingDeliveryCount = 0;
            Closest = null;
            ExcursionEnded?.Invoke();
            InsideSubmarine = true;
            GameState.excursionIncapacitated = incapacitated;
            GameState.oreLost = incapacitated ? Mathf.RoundToInt(Tellurium * Random.Range(.7f, .9f)) : 0;
            GameState.tellurium = Tellurium - GameState.oreLost;
            return true;
        }

        public bool DropItem(string itemName, int count = 1)
        {
            if (PauseManager.BlocksInput || transitioning || ReturnConfirmationOpen || SceneChanger.IsTransitioning
                || LoadoutOpen || Incapacitated || InsideSubmarine) return false;
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

            // create the pickup
            var drop = Instantiate(template, template.transform.parent);
            drop.kind = OceanTarget.TargetKind.Item;
            drop.itemName = item.Id;
            drop.itemCount = count;
            drop.displayName = item.Name;
            drop.name = "Dropped " + item.Name;
            drop.gameObject.SetActive(true);
            drop.transform.position = FindDropPoint(drop);

            var list = new System.Collections.Generic.List<OceanTarget>(targets);
            list.Add(drop);
            targets = list.ToArray();

            ItemCatalog.ChangeCount(carried, item.Id, -count);
            UpdateSpeed();
            Notify($"{count} × {item.Name} dropped.");
            return true;
        }

        private Vector2 FindDropPoint(OceanTarget drop)
        {
            Vector2 origin = transform.position;
            for (float radius = 0.8f; radius <= 1.4f; radius += 0.3f)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2f / 12f;
                    var point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (CanSpawn(point, drop)) return point;
                }
            }
            return origin; // nowhere free nearby, drop at the player's feet
        }

        private void StopOceanControls()
        {
            CancelProspect();
            body.linearVelocity = Vector2.zero;
            swimmer.enabled = false;
        }
    }
}
