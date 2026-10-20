using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
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
        [Min(1)] public int availableMachines = 3;
        public OceanTarget deliveryPrefab;
        [Min(.1f)] public float deliveryRadius = 10f;
        [FormerlySerializedAs("interiorScene")] public string resultsScene = "Result";
        public int DeliveredItemCount { get; private set; }
        public int RemainingDeliveryCount { get; private set; }
        public event System.Action<int> DeliveryCollected;
        public OceanTarget Closest { get; private set; }
        public bool InsideSubmarine { get; private set; }
        public bool IsTransitioning => transitioning;
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
        private bool CanActInOcean => !InsideSubmarine && !transitioning && !Hydrox.IsDepleted && !IsProspecting;
        private SwimmerController swimmer;
        private Rigidbody2D body;
        private readonly HashSet<OceanTarget> deliveries = new HashSet<OceanTarget>();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private readonly System.Collections.Generic.List<Vector2> spawnCells = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.HashSet<Vector2Int> reachable = new System.Collections.Generic.HashSet<Vector2Int>();
        private const float SpawnCellSize = 1f;

        private void Awake()
        {
            swimmer = GetComponent<SwimmerController>();
            body = GetComponent<Rigidbody2D>();
            Hydrox.Refill();
            BuildSpawnArea();
            SpawnDeliveries();
            foreach (var target in targets)
                if (target != null) target.OreProduced += OnOreProduced;
        }

        private void SpawnDeliveries()
        {
            GameState.itemsBought.TryGetValue(GameState.MiningMachine, out int pending);
            if (pending <= 0) return;
            OceanTarget submarine = System.Array.Find(targets, t => t != null && t.kind == OceanTarget.TargetKind.Submarine);
            if (deliveryPrefab == null || deliveryPrefab.kind != OceanTarget.TargetKind.Item || submarine == null)
            {
                Debug.LogError("Purchased deliveries need an item prefab and a submarine. Orders remain pending.", this);
                return;
            }
            Vector2 center = submarine.transform.position;
            var all = new List<OceanTarget>(targets);
            for (int i = 0; i < pending; i++)
            {
                if (!TryFindDeliveryPosition(center, out Vector2 point))
                {
                    Debug.LogWarning("No safe delivery position within " + deliveryRadius + " units of the submarine. Remaining orders will be delivered on a later excursion.", this);
                    break;
                }
                var item = Instantiate(deliveryPrefab, point, Quaternion.identity, oceanMap.transform);
                item.name = "Purchased Mining Machine " + (DeliveredItemCount + 1);
                item.gameObject.SetActive(true);
                deliveries.Add(item);
                all.Add(item);
                targets = all.ToArray();
                DeliveredItemCount++;
                RemainingDeliveryCount++;
                GameState.itemsBought[GameState.MiningMachine]--;
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
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (swimmer == null) swimmer = GetComponent<SwimmerController>();
            if (SwimmerController.HasMovementKeysPressed() || body.linearVelocity.sqrMagnitude > .0001f)
            {
                Notify("Stop moving before prospecting");
                return false;
            }
            IsProspecting = true;
            scanStarted = now;
            swimmer.SetMovementLocked(true);
            return true;
        }

        public void CancelProspect()
        {
            IsProspecting = false;
            if (swimmer != null) swimmer.SetMovementLocked(false);
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
        private void OnDisable() => CancelProspect();
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
                if (!deliveries.Remove(Closest)) return;
                ItemsCollected++;
                availableMachines++;
                RemainingDeliveryCount--;
                Closest.gameObject.SetActive(false);
                DeliveryCollected?.Invoke(RemainingDeliveryCount);
                Notify("Purchased mining machine collected.");
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot && Closest.HasMachine)
            {
                int amount = Closest.Retrieve(Time.time);
                Tellurium += amount;
                availableMachines++;
                if (amount > 0) PositiveRetrieval?.Invoke();
                Notify("Machine retrieved. +" + amount + " tellurium.");
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot) PlaceMachine();
            else if (Closest.kind == OceanTarget.TargetKind.Submarine) RequestEnterSubmarine();
        }

        public void Notify(string message) { Message = message; messageUntil = Time.time + 3f; }

        public bool RequestEnterSubmarine()
        {
            if (InsideSubmarine || transitioning || sceneUnavailable) return false;
            if (!Application.CanStreamedLevelBeLoaded(resultsScene))
            {
                sceneUnavailable = true;
                Notify("Results scene is unavailable.");
                if (Hydrox.IsDepleted) messageUntil = float.PositiveInfinity;
                Debug.LogError("Results scene is unavailable: " + resultsScene + ". Include it in the build scene list.", this);
                return false;
            }
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
            SceneChanger.ChangeScene(resultsScene);
            return true;
        }

        private void StopOceanControls()
        {
            CancelProspect();
            body.linearVelocity = Vector2.zero;
            swimmer.enabled = false;
        }
    }
}
