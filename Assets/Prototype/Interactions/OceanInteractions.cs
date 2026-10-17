using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        [Min(0f)] public float prospectCooldown = 2f;
        [Min(1)] public int availableMachines = 3;
        [Min(1)] public int itemCount = 15;
        public string interiorScene = "Interior";
        public OceanTarget Closest { get; private set; }
        public bool InsideSubmarine { get; private set; }
        public bool IsTransitioning => transitioning;
        public HydroxTank Hydrox => hydrox != null ? hydrox : (hydrox = GetComponent<HydroxTank>());
        public int ItemsCollected { get; private set; }
        public int Tellurium { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, nextProspect - Time.time);
        public string Message { get; private set; }
        private float messageUntil, nextProspect;
        private bool transitioning, sceneUnavailable;
        private HydroxTank hydrox;
        private bool CanActInOcean => !InsideSubmarine && !transitioning && !Hydrox.IsDepleted;
        private SwimmerController swimmer;
        private Rigidbody2D body;
        private FlashlightController flashlight;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private readonly System.Collections.Generic.List<Vector2> spawnCells = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.HashSet<Vector2Int> reachable = new System.Collections.Generic.HashSet<Vector2Int>();
        private const float SpawnCellSize = 1f;

        private void Awake()
        {
            swimmer = GetComponent<SwimmerController>();
            body = GetComponent<Rigidbody2D>();
            flashlight = GetComponent<FlashlightController>();
            Hydrox.Refill();
            var items = new System.Collections.Generic.List<OceanTarget>();
            var all = new System.Collections.Generic.List<OceanTarget>();
            foreach (var target in targets)
                if (target.kind == OceanTarget.TargetKind.Item) items.Add(target); else all.Add(target);
            for (int i = 0; i < itemCount && items.Count > 0; i++)
            {
                var item = i < items.Count ? items[i] : Instantiate(items[0], items[0].transform.parent);
                item.name = "Item " + (i + 1);
                all.Add(item);
            }
            for (int i = itemCount; i < items.Count; i++) items[i].gameObject.SetActive(false);
            targets = all.ToArray();
            BuildSpawnArea();
            foreach (var target in targets)
            {
                if (target.kind != OceanTarget.TargetKind.Item) continue;
                bool placed = false;
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    var point = new Vector2(Random.Range(spawnMin.x, spawnMax.x), Random.Range(spawnMin.y, spawnMax.y));
                    if (!CanSpawn(point, target)) continue;
                    target.transform.position = point;
                    placed = true;
                    break;
                }
                if (!placed)
                    foreach (var point in spawnCells)
                        if (CanSpawn(point, target)) { target.transform.position = point; placed = true; break; }
                if (!placed)
                {
                    target.gameObject.SetActive(false);
                    Debug.LogError("No reachable free position for " + target.name + ". Pickup disabled.", target);
                }
            }
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
                if (other != item && Vector2.Distance(point, other.transform.position) < 1.5f) return false;
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
                if (pointerInside && mouse.leftButton.wasPressedThisFrame) StartCoroutine(ReturnToOcean());
                return;
            }
            Hydrox.Advance(Time.deltaTime);
            if (Hydrox.IsDepleted)
            {
                StopOceanControls();
                if (sceneUnavailable)
                {
                    Message = "Submarine scene is unavailable.";
                    messageUntil = float.PositiveInfinity;
                }
                RequestEnterSubmarine();
                return;
            }
            RefreshClosest();
            if (!pointerInside) return;
            if (mouse.rightButton.wasPressedThisFrame) Prospect(Time.time);
            if (mouse.leftButton.wasPressedThisFrame) Interact();
        }

        public void RefreshClosest()
        {
            Closest = null;
            float best = interactionRange;
            foreach (var target in targets)
            {
                if (target == null || !target.isActiveAndEnabled) continue;
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
            nextProspect = now + prospectCooldown;
            foreach (var target in targets)
            {
                if (target == null || !target.isActiveAndEnabled || target.kind != OceanTarget.TargetKind.MiningSpot) continue;
                Vector3 viewport = oceanCamera.WorldToViewportPoint(target.transform.position);
                if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f) target.Reveal(now);
            }
            return true;
        }

        public bool PlaceMachine()
        {
            if (!CanActInOcean) return false;
            RefreshClosest();
            if (Closest == null || Closest.kind != OceanTarget.TargetKind.MiningSpot || InsideSubmarine) return false;
            if (availableMachines <= 0) { Notify("No machines left. Left-click a deployed machine to retrieve it."); return false; }
            if (!Closest.Place(Time.time)) return false;
            availableMachines--;
            Notify("Mining started: " + Closest.level + " tellurium / minute.");
            return true;
        }

        public void Interact()
        {
            if (!CanActInOcean) return;
            RefreshClosest();
            if (Closest == null || InsideSubmarine || transitioning) return;
            if (Closest.kind == OceanTarget.TargetKind.Item)
            {
                ItemsCollected++;
                Notify("Item collected.");
                Closest.gameObject.SetActive(false);
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot && Closest.HasMachine)
            {
                int amount = Closest.Retrieve(Time.time);
                Tellurium += amount;
                availableMachines++;
                Notify("Machine retrieved. +" + amount + " tellurium.");
            }
            else if (Closest.kind == OceanTarget.TargetKind.MiningSpot) PlaceMachine();
            else if (Closest.kind == OceanTarget.TargetKind.Submarine) RequestEnterSubmarine();
        }

        public void Notify(string message) { Message = message; messageUntil = Time.time + 3f; }

        public bool RequestEnterSubmarine()
        {
            if (InsideSubmarine || transitioning || sceneUnavailable) return false;
            if (!Application.CanStreamedLevelBeLoaded(interiorScene))
            {
                sceneUnavailable = true;
                Notify("Submarine scene is unavailable.");
                if (Hydrox.IsDepleted) messageUntil = float.PositiveInfinity;
                Debug.LogError("Submarine scene is unavailable: " + interiorScene + ". Include it in the build scene list.", this);
                return false;
            }
            transitioning = true;
            StopOceanControls();
            StartCoroutine(EnterSubmarine());
            return true;
        }

        private void StopOceanControls()
        {
            body.linearVelocity = Vector2.zero;
            swimmer.enabled = false;
        }

        private IEnumerator EnterSubmarine()
        {
            var operation = SceneManager.LoadSceneAsync(interiorScene, LoadSceneMode.Additive);
            yield return operation;
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(interiorScene));
            InsideSubmarine = true;
            body.simulated = false;
            flashlight.enabled = false;
            oceanCamera.gameObject.SetActive(false);
            oceanMap.SetActive(false);
            GetComponent<SpriteRenderer>().enabled = false;
            transitioning = false;
        }

        private IEnumerator ReturnToOcean()
        {
            transitioning = true;
            SceneManager.SetActiveScene(gameObject.scene);
            yield return SceneManager.UnloadSceneAsync(interiorScene);
            oceanMap.SetActive(true);
            oceanCamera.gameObject.SetActive(true);
            GetComponent<SpriteRenderer>().enabled = true;
            flashlight.enabled = true;
            Hydrox.Refill();
            body.simulated = true;
            swimmer.enabled = true;
            InsideSubmarine = false;
            transitioning = false;
        }

    }
}
