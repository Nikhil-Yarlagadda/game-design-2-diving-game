using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(SwimmerController))]
    public sealed class OceanInteractions : MonoBehaviour
    {
        public Camera oceanCamera;
        public GameObject oceanMap;
        public OceanTarget[] targets;
        public Collider2D[] obstacles;
        public Vector2 spawnMin = new Vector2(-21f, -18f);
        public Vector2 spawnMax = new Vector2(21f, 18f);
        [Min(.1f)] public float interactionRange = 1.8f;
        [Min(0f)] public float holdSeconds = .35f;
        [Min(0f)] public float prospectCooldown = 2f;
        [Min(1)] public int availableMachines = 3;
        public string interiorScene = "Interior";
        public OceanTarget Closest { get; private set; }
        public bool RadarActive { get; private set; }
        public bool InsideSubmarine { get; private set; }
        public int ItemsCollected { get; private set; }
        public int Tellurium { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, nextProspect - Time.time);
        public string Message { get; private set; }
        private float messageUntil, pressedAt, nextProspect;
        private bool zHeld, transitioning;
        private SwimmerController swimmer;
        private Rigidbody2D body;
        private FlashlightController flashlight;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

        private void Awake()
        {
            swimmer = GetComponent<SwimmerController>();
            body = GetComponent<Rigidbody2D>();
            flashlight = GetComponent<FlashlightController>();
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
                if (!placed) Debug.LogWarning("No free random position for " + target.name + "; retaining its authored fallback.", target);
            }
        }

        public bool CanSpawn(Vector2 point, OceanTarget item)
        {
            if (point.x < spawnMin.x || point.x > spawnMax.x || point.y < spawnMin.y || point.y > spawnMax.y) return false;
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
            var keyboard = Keyboard.current;
            if (InsideSubmarine)
            {
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) StartCoroutine(ReturnToOcean());
                return;
            }
            RefreshClosest();
            HandleProspectInput(keyboard != null && keyboard.zKey.wasPressedThisFrame,
                keyboard != null && keyboard.zKey.isPressed, keyboard != null && keyboard.zKey.wasReleasedThisFrame, Time.time);
            if (keyboard == null) return;
            if (keyboard.xKey.wasPressedThisFrame) PlaceMachine();
            if (keyboard.spaceKey.wasPressedThisFrame) Interact();
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

        public void HandleProspectInput(bool pressed, bool held, bool released, float now)
        {
            if (pressed) { zHeld = true; pressedAt = now; }
            if (zHeld && held && now - pressedAt >= holdSeconds) RadarActive = true;
            if (zHeld && released)
            {
                if (!RadarActive && now - pressedAt < holdSeconds) Prospect(now);
                zHeld = false;
                RadarActive = false;
            }
            if (!held && !released) { zHeld = false; RadarActive = false; }
        }

        public bool Prospect(float now)
        {
            if (now < nextProspect || InsideSubmarine) return false;
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
            RefreshClosest();
            if (Closest == null || Closest.kind != OceanTarget.TargetKind.MiningSpot || InsideSubmarine) return false;
            if (availableMachines <= 0) { Notify("No machines left. Retrieve one with SPACE."); return false; }
            if (!Closest.Place(Time.time)) return false;
            availableMachines--;
            Notify("Mining started: " + Closest.level + " tellurium / minute.");
            return true;
        }

        public void Interact()
        {
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
            else if (Closest.kind == OceanTarget.TargetKind.Submarine) StartCoroutine(EnterSubmarine());
        }

        public void Notify(string message) { Message = message; messageUntil = Time.time + 3f; }

        private IEnumerator EnterSubmarine()
        {
            if (!Application.CanStreamedLevelBeLoaded(interiorScene)) { Notify("Submarine scene is unavailable."); yield break; }
            transitioning = true;
            zHeld = RadarActive = false;
            body.linearVelocity = Vector2.zero;
            swimmer.enabled = false;
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
            body.simulated = true;
            swimmer.enabled = true;
            InsideSubmarine = false;
            transitioning = false;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) { zHeld = false; RadarActive = false; } }
        private void OnDisable() { zHeld = false; RadarActive = false; }
    }
}
