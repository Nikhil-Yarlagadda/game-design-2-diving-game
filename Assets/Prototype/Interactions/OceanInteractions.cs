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
        [System.NonSerialized] public int availableMachines = GameState.excursionInventory[GameState.MiningMachine];
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
        public int Tellurium { get; private set; }
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
        private bool CanActInOcean => Time.timeScale > 0f && !PauseManager.BlocksInput && !InsideSubmarine && !transitioning && !ReturnConfirmationOpen && Time.frameCount > resumeInputFrame && !Hydrox.IsDepleted && !IsProspecting;
        private float timeScaleBeforeReturn;
        private int resumeInputFrame = -1;
        private SwimmerController swimmer;
        private Rigidbody2D body;
        private readonly HashSet<OceanTarget> deliveries = new HashSet<OceanTarget>();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private readonly System.Collections.Generic.List<Vector2> spawnCells = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.HashSet<Vector2Int> reachable = new System.Collections.Generic.HashSet<Vector2Int>();
        private const float SpawnCellSize = 1f;
        private ExcursionInventory inventoryUI;
        private static readonly System.Collections.Generic.HashSet<string> droppable =
            new System.Collections.Generic.HashSet<string> { "Mining Machine", "Stimulant Canister", "Tellurium" };
        private Dictionary<string, GameState.MiningSpotState> tutorialStartMining;
        private Dictionary<string, int> tutorialStartOrders;
        private Dictionary<string, int> tutorialStartInventory;
        private int tutorialStartTellurium;



        private void Awake()
        {
#if UNITY_EDITOR
            GameStateDebugger.CaptureFirstOceanEntry(gameObject.scene.name);
#endif
            inventoryUI = FindAnyObjectByType<ExcursionInventory>();
            if (GameState.days == 2)
            {
                tutorialStartMining = new Dictionary<string, GameState.MiningSpotState>(GameState.miningSpots);
                tutorialStartOrders = new Dictionary<string, int>(GameState.itemsBought);
                tutorialStartInventory = new Dictionary<string, int>(GameState.inventory);
                tutorialStartTellurium = GameState.tellurium;
            }
            swimmer = GetComponent<SwimmerController>();
            body = GetComponent<Rigidbody2D>();
            Hydrox.Refill();
            GameState.excursionInventory.TryGetValue(GameState.MiningMachine, out availableMachines);
            BuildSpawnArea();
            SpawnDeliveries();
            foreach (var target in targets)
                if (target != null) target.OreProduced += OnOreProduced;
        }

#if UNITY_EDITOR
        internal void SyncDebugInventory()
        {
            Tellurium = GameState.tellurium;
            GameState.inventory.TryGetValue(GameState.MiningMachine, out availableMachines);
        }
#endif

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
                int pending = GameState.itemsBought[itemName];
                for (int i = 0; i < pending; i++)
                {
                    if (!TryFindDeliveryPosition(center, out Vector2 point))
                    {
                        Debug.LogWarning("No safe delivery position. Remaining orders will be delivered on a later excursion.", this);
                        return;
                    }
                    var item = Instantiate(deliveryPrefab, point, Quaternion.identity, oceanMap.transform);
                    item.itemName = itemName;
                    item.displayName = itemName;
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

        public bool CanDrop(string itemName)
        {
            return droppable.Contains(itemName);
        }

        private void Update()
        {
            if (Time.timeScale == 0f || PauseManager.BlocksInput || ReturnConfirmationOpen || Time.frameCount <= resumeInputFrame) return;
            if (panel != null && panel.activeSelf) { CancelProspect(); return; }
            if (Time.time > messageUntil) Message = "";
            if (transitioning) return;
            var mouse = Mouse.current;
            bool pointerInside = mouse != null && (InsideSubmarine ? Camera.main : oceanCamera) != null
                && (InsideSubmarine ? Camera.main : oceanCamera).pixelRect.Contains(mouse.position.ReadValue());
            if (InsideSubmarine)
            {
                return;
            }
            Hydrox.Advance(Time.deltaTime);
            if (Hydrox.IsDepleted)
            {
                StopOceanControls();
                RequestEnterSubmarine();
                return;
            }
            RefreshClosest();
            var keyboard = Keyboard.current;
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
            availableMachines--;
            GameState.excursionInventory["Mining Machine"]--;
            Notify("Mining started: " + OceanTarget.Rate(Closest.DisplayedTier) + " tellurium / minute (last prospected).");
            MachinePlaced?.Invoke();
            return true;
        }

        public void Interact()
        {
            if (!CanActInOcean) return;
            RefreshClosest();
            if (Closest == null || InsideSubmarine || transitioning) return;
            if (Closest.kind == OceanTarget.TargetKind.Item)
            {
                if (inventoryUI != null && !inventoryUI.CanFit(Closest.itemName))
                {
                    Notify("Inventory full.");
                    return;
                }

                bool isDelivery = deliveries.Remove(Closest);
                string itemName = string.IsNullOrEmpty(Closest.itemName) ? GameState.MiningMachine : Closest.itemName;
                
                ItemsCollected++;
                if (itemName == "Tellurium")
                {
                    Tellurium++;
                    GameState.tellurium = Tellurium;
                }
                else
                {
                    if (GameState.excursionInventory.ContainsKey(itemName))
                        GameState.excursionInventory[itemName]++;
                    else
                        GameState.excursionInventory[itemName] = 1;

                    if (itemName == "Mining Machine")
                        availableMachines++;
                }
                Closest.gameObject.SetActive(false);
                if (isDelivery)
                {
                    RemainingDeliveryCount--;
                    DeliveryCollected?.Invoke(RemainingDeliveryCount);
                }
                Notify(itemName + " collected.");
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot && Closest.HasMachine)
            {
                int room = inventoryUI != null ? inventoryUI.RoomFor("Tellurium") : int.MaxValue;

                if (Closest.YieldAt(Time.time) > 0 && room <= 0)
                {
                    Notify("Inventory full.");
                    return;
                }

                int amount = Closest.Retrieve(Time.time, room);
                Tellurium += amount;
                GameState.tellurium = Tellurium;
                if (amount > 0) PositiveRetrieval?.Invoke();
                if (!Closest.HasMachine)   // fully retrieved, the machine comes back too
                {
                    availableMachines++;
                    GameState.excursionInventory[GameState.MiningMachine] = availableMachines;
                    Notify("Machine retrieved. +" + amount + " tellurium.");
                }
                else                       // partial, the machine stays deployed
                {
                    Notify("Inventory full. Took " + amount + " tellurium, the rest stays in the machine.");
                }
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot) PlaceMachine();
            else if (Closest.kind == OceanTarget.TargetKind.Submarine) RequestEnterSubmarine();
            else if (Closest.kind == OceanTarget.TargetKind.Corpse)
            {
                const string sample = "Full-grown Fungus Sample";

                if ((GameState.inventory.ContainsKey(sample) && GameState.inventory[sample] > 0)  || (GameState.excursionInventory.ContainsKey(sample) && GameState.inventory[sample] > 0))
                {
                    Notify("You've already collected the sample");
                    return;
                }

                if (!GameState.adminUnlocked)
                {
                    Notify("You find nothing of use.");
                    return;
                }

                if (inventoryUI != null && inventoryUI.CanFit(sample))
                {
                    Notify("Inventory full.");
                    return;
                }

                GameState.excursionInventory[sample] =
                    (GameState.excursionInventory.TryGetValue(sample, out int have) ? have : 0) + 1;

                if (GameState.dayFgfsCollected < 0)
                    GameState.dayFgfsCollected = GameState.days;   // the fungus day for Kirsten and Cole's dialogue

                Notify(sample + " collected.");
            }
        }

        public void Notify(string message) { Message = message; messageUntil = Time.time + 3f; }

        public bool RequestEnterSubmarine()
        {
            if (Time.timeScale == 0f || PauseManager.BlocksInput || InsideSubmarine || transitioning || sceneUnavailable || ReturnConfirmationOpen || Time.frameCount <= resumeInputFrame) return false;
            if (GameState.days == 2 && Hydrox.IsDepleted)
            {
                string scene = SceneChanger.getScene();
                if (!Application.CanStreamedLevelBeLoaded(scene))
                {
                    sceneUnavailable = true;
                    Debug.LogError("Tutorial scene is unavailable: " + scene, this);
                    return false;
                }
                transitioning = true;
                StopOceanControls();
                GameState.miningSpots = tutorialStartMining;
                GameState.itemsBought = tutorialStartOrders;
                GameState.inventory = tutorialStartInventory;
                GameState.tellurium = tutorialStartTellurium;
                SceneChanger.ChangeScene(scene);
                return true;
            }
            if (TutorialBlocksReturn)
            {
                Notify("Complete the tutorial before returning to the submarine.");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(resultsScene))
            {
                sceneUnavailable = true;
                Notify("Results scene is unavailable.");
                if (Hydrox.IsDepleted) messageUntil = float.PositiveInfinity;
                Debug.LogError("Results scene is unavailable: " + resultsScene + ". Include it in the build scene list.", this);
                return false;
            }
            if (!Hydrox.IsDepleted)
            {
                if (IsProspecting) return false;
                ReturnConfirmationOpen = true;
                timeScaleBeforeReturn = Time.timeScale;
                swimmer.SetMovementLocked(true);
                Time.timeScale = 0f;
                return true;
            }
            return EnterSubmarine();
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
            return EnterSubmarine();
        }

        private bool EnterSubmarine()
        {
            if (!SceneChanger.ChangeSceneWithFade(resultsScene)) return false;
            transitioning = true;
            StopOceanControls();
            foreach (var item in deliveries)
                if (item != null) item.gameObject.SetActive(false);
            deliveries.Clear();
            RemainingDeliveryCount = 0;
            Closest = null;
            ExcursionEnded?.Invoke();
            InsideSubmarine = true;
            GameState.tellurium = Tellurium;
            GameState.ReturnFromExcursion(); 
            SceneChanger.ChangeScene(resultsScene);
            return true;
        }

        public bool DropItem(string itemName)
        {
            if (PauseManager.BlocksInput || transitioning || ReturnConfirmationOpen || SceneChanger.IsTransitioning) return false;

            Debug.Log("DropItem called: " + itemName);
            
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

            // take it out of the inventory
            if (itemName == "Tellurium")
            {
                if (Tellurium <= 0) return false;
                Tellurium--;
                GameState.tellurium = Tellurium;
            }
            else if (GameState.excursionInventory.TryGetValue(itemName, out int count) && count > 0)
            {
                if (count <= 1) GameState.excursionInventory.Remove(itemName);
                else GameState.excursionInventory[itemName] = count - 1;

                if (itemName == "Mining Machine")
                    availableMachines = Mathf.Max(0, availableMachines - 1);
            }
            else return false;

            // create the pickup
            var drop = Instantiate(template, template.transform.parent);
            drop.kind = OceanTarget.TargetKind.Item;
            drop.itemName = itemName;
            drop.displayName = itemName;
            drop.name = "Dropped " + itemName;
            drop.gameObject.SetActive(true);
            drop.transform.position = FindDropPoint(drop);

            var list = new System.Collections.Generic.List<OceanTarget>(targets);
            list.Add(drop);
            targets = list.ToArray();

            Notify(itemName + " dropped.");
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
