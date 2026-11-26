using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    /// <summary>
    /// Experimental "game feel" layer for excursions. It only listens to gameplay events and adds
    /// world feedback (camera shake and thud, metal drop, sediment/bubbles, prospect punch, a weak
    /// photo flash, and an out-of-hydrox heartbeat), so it never changes gameplay rules.
    /// Press F8 to toggle it for before/after comparisons.
    ///
    /// Pattern for adding new effects:
    /// 1. Add a [Serializable] settings class with a public `enabled` bool and Inspector tuning fields.
    /// 2. Add a [SerializeField] instance of that settings class on OceanGameFeel.
    /// 3. Gate the event handler/update on `effectsEnabled && setting.enabled` and clean up active
    ///    visuals when either switch turns off.
    /// </summary>
    // After CenteredCamera (0) follows the diver, before FlashlightController (100) and OceanVfx (110),
    // which size the lighting and water overlay from the camera.
    [DefaultExecutionOrder(90)]
    [RequireComponent(typeof(OceanInteractions))]
    public sealed class OceanGameFeel : MonoBehaviour
    {
        [Serializable]
        public sealed class CameraShakeSettings
        {
            [Tooltip("Enable trauma-based camera shake across all effects.")]
            public bool enabled = true;

            [Min(0f), Tooltip("Maximum world-space camera position offset at trauma = 1.")]
            public float maxShakeOffset = 0.35f;

            [Range(0f, 15f), Tooltip("Maximum camera roll angle (degrees) at trauma = 1; keep 0 because the water overlay quad does not rotate.")]
            public float maxShakeAngle = 0f;

            [Min(0.01f), Tooltip("Linear trauma decay rate per second.")]
            public float traumaDecay = 1.2f;

            [Min(0.1f), Tooltip("Perlin noise sampling frequency for camera shake.")]
            public float shakeFrequency = 18f;
        }

        [Serializable]
        public sealed class PlaceMachineSettings
        {
            [Tooltip("Enable the ease-in drop, sediment ring, bubble burst, and directional camera thud on machine placement.")]
            public bool enabled = true;

            [Header("Drop")]
            [Min(0f), Tooltip("Duration of the ease-in drop onto the mining spot (seconds).")]
            public float fallTime = 0.16f;

            [Min(0f), Tooltip("Starting height above the spot before dropping (world units).")]
            public float fallHeight = 1.2f;

            [Header("Sediment ring")]
            [Range(0, 80), Tooltip("Number of sand particles kicked out sideways on impact.")]
            public int sedimentCount = 28;

            [Min(0f), Tooltip("Initial radial offset from the spot center.")]
            public float sedimentRadius = 0.25f;

            [Range(0.1f, 1f), Tooltip("Vertical flattening of the ring so it reads as seabed floor dust.")]
            public float sedimentFlattenY = 0.45f;

            [Min(0f), Tooltip("Minimum outward speed of sediment particles.")]
            public float sedimentSpeedMin = 1.6f;

            [Min(0f), Tooltip("Maximum outward speed of sediment particles.")]
            public float sedimentSpeedMax = 2.4f;

            [Min(0.01f), Tooltip("Minimum sediment particle size.")]
            public float sedimentSizeMin = 0.08f;

            [Min(0.01f), Tooltip("Maximum sediment particle size.")]
            public float sedimentSizeMax = 0.16f;

            [Min(0.05f), Tooltip("Minimum sediment particle lifetime (seconds).")]
            public float sedimentLifetimeMin = 0.5f;

            [Min(0.05f), Tooltip("Maximum sediment particle lifetime (seconds).")]
            public float sedimentLifetimeMax = 0.9f;

            [Tooltip("Color of the seabed sediment puff.")]
            public Color sedimentColor = new Color(0.78f, 0.72f, 0.6f, 0.85f);

            [Header("Bubbles")]
            [Range(0, 64), Tooltip("Number of bubbles released when the machine hits the seabed.")]
            public int bubbleCount = 18;

            [Header("Camera thud")]
            [Min(0f), Tooltip("Downward world-space camera offset at the moment of impact.")]
            public float thudOffset = 0.15f;

            [Min(0.01f), Tooltip("Time for the camera to return monotonically to its rest position (seconds).")]
            public float thudDuration = 0.22f;
        }

        [Serializable]
        public sealed class RetrieveSettings
        {
            [Tooltip("Enable the bubble burst and slight camera trauma when retrieving a machine.")]
            public bool enabled = true;

            [Header("Bubbles & camera")]
            [Range(0, 64), Tooltip("Minimum bubbles emitted when retrieving positive tellurium.")]
            public int minBubbleCount = 6;

            [Range(0, 64), Tooltip("Maximum bubbles emitted when retrieving positive tellurium.")]
            public int maxBubbleCount = 24;

            [Min(0), Tooltip("Extra bubbles emitted per unit of tellurium retrieved.")]
            public int bubblesPerTellurium = 1;

            [Range(0f, 1f), Tooltip("Camera trauma added when retrieving positive tellurium.")]
            public float trauma = 0.18f;
        }

        [Serializable]
        public sealed class OutOfHydroxSettings
        {
            [Tooltip("Enable heartbeat zoom and panic tremor during the breathless grace period.")]
            public bool enabled = true;

            [Header("Zoom")]
            [Range(0f, 0.25f), Tooltip("Base orthographic zoom pulse fraction on each heartbeat.")]
            public float heartbeatZoom = 0.03f;

            [Min(0f), Tooltip("Zoom multiplier when gas first runs out (urgency = 0).")]
            public float zoomScaleMin = 0.6f;

            [Min(0f), Tooltip("Zoom multiplier just before blackout (urgency = 1).")]
            public float zoomScaleMax = 1.5f;

            [Header("Rate & tremor")]
            [Range(20f, 240f), Tooltip("Heartbeat rate (BPM) when gas first runs out.")]
            public float bpmMin = 70f;

            [Range(20f, 300f), Tooltip("Heartbeat rate (BPM) just before blackout.")]
            public float bpmMax = 160f;

            [Range(0f, 1f), Tooltip("Minimum camera trauma floor when gas first runs out.")]
            public float traumaMin = 0.05f;

            [Range(0f, 1f), Tooltip("Maximum camera trauma floor just before blackout; shake = trauma².")]
            public float traumaMax = 0.35f;

            [Header("Heartbeat pulse")]
            [Range(0.05f, 0.4f), Tooltip("Phase offset of the second ('dub') beat within one heartbeat cycle.")]
            public float secondBeatOffset = 0.18f;

            [Range(0f, 1f), Tooltip("Relative amplitude of the second ('dub') beat.")]
            public float secondBeatStrength = 0.6f;

            [Range(0.03f, 0.3f), Tooltip("Duration of each pulse lobe as a fraction of the heartbeat cycle.")]
            public float pulseWidth = 0.14f;
        }

        [Serializable]
        public sealed class SpotRevealSettings
        {
            [Tooltip("Enable the scale punch and bubble ring when the prospect sweep reveals a spot.")]
            public bool enabled = true;

            [Header("Scale punch")]
            [Min(0f), Tooltip("Extra scale at DisplayedTier 0; higher tiers add scalePunchPerTier on top.")]
            public float scalePunch = 0.12f;

            [Min(0f), Tooltip("Additional scale punch per DisplayedTier above 0 (1-5 reads as stronger info).")]
            public float scalePunchPerTier = 0.025f;

            [Min(0.05f), Tooltip("Total duration of the scale punch (seconds).")]
            public float punchDuration = 0.28f;

            [Range(0.05f, 0.9f), Tooltip("Normalized fraction of duration spent growing to the peak before settling back.")]
            public float punchRiseFraction = 0.35f;

            [Header("Bubble ring")]
            [Range(0, 64), Tooltip("Ring bubbles at DisplayedTier 0; higher tiers add bubbleCountPerTier.")]
            public int bubbleCount = 4;

            [Range(0, 16), Tooltip("Additional ring bubbles per DisplayedTier above 0.")]
            public int bubbleCountPerTier = 1;

            [Min(0f), Tooltip("Ring radius at DisplayedTier 0 (world units).")]
            public float bubbleRadius = 0.3f;

            [Min(0f), Tooltip("Additional ring radius per DisplayedTier above 0.")]
            public float bubbleRadiusPerTier = 0.025f;

            [Tooltip("Tint ring bubbles with the spot tier color so the revealed tier reads at a glance.")]
            public bool tintBubblesByTier = true;
        }

        [Serializable]
        public sealed class ItemPickupSettings
        {
            [Tooltip("Enable a small bubble puff when picking up an item or order.")]
            public bool enabled = true;

            [Header("Bubbles")]
            [Range(0, 32), Tooltip("Bubbles emitted at the pickup position.")]
            public int bubbleCount = 5;
        }

        [Serializable]
        public sealed class QuestItemSettings
        {
            [Tooltip("Enable the Eye photo flash. Fungus pickups have no game-feel overlay.")]
            public bool enabled = true;

            [Header("Eye photo flash")]
            [Range(0f, 1f), Tooltip("Peak opacity of the world-space camera flash.")]
            public float flashAlpha = 0.3f;

            [Min(0.01f), Tooltip("Duration of the flash (seconds, unscaled).")]
            public float flashDuration = 0.12f;

            [Tooltip("Tint of the camera flash overlay.")]
            public Color flashColor = Color.white;
        }

        [Serializable]
        public sealed class OreProductionSettings
        {
            [Tooltip("Enable the machine settle and silver metal sparkles when a machine in view produces ore.")]
            public bool enabled = true;

            [Min(0.1f), Tooltip("Minimum seconds between production feedback, so many machines stay subtle.")]
            public float minInterval = 2.5f;

            [Header("Machine settle")]
            [Min(0f), Tooltip("Small downward offset of the machine sprite before it returns to rest (world units).")]
            public float settleDistance = 0.05f;

            [Min(0.01f), Tooltip("Total duration of the machine settle (seconds).")]
            public float settleDuration = 0.18f;

            [Range(0.05f, 0.9f), Tooltip("Normalized fraction of settleDuration spent moving down before returning.")]
            public float settleDownFraction = 0.35f;

            [Header("Metal sparkles")]
            [Range(0, 32), Tooltip("Number of silver-white sparkle sprites emitted per production puff.")]
            public int sparkleCount = 6;

            [Tooltip("Silver-white tint of the metal sparkles.")]
            public Color sparkleColor = new Color(0.95f, 0.98f, 1f, 1f);

            [Min(0.005f), Tooltip("Minimum sparkle sprite size.")]
            public float sparkleSizeMin = 0.08f;

            [Min(0.005f), Tooltip("Maximum sparkle sprite size.")]
            public float sparkleSizeMax = 0.13f;

            [Min(0.05f), Tooltip("Minimum sparkle lifetime (seconds).")]
            public float sparkleLifetimeMin = 0.35f;

            [Min(0.05f), Tooltip("Maximum sparkle lifetime (seconds).")]
            public float sparkleLifetimeMax = 0.6f;

            [Min(0f), Tooltip("Minimum upward drift speed of sparkles.")]
            public float sparkleRiseSpeedMin = 0.35f;

            [Min(0f), Tooltip("Maximum upward drift speed of sparkles.")]
            public float sparkleRiseSpeedMax = 0.7f;

            [Min(0f), Tooltip("Maximum horizontal drift speed of sparkles.")]
            public float sparkleDriftSpeed = 0.15f;

            [Min(0f), Tooltip("Radial spread of sparkle spawn positions around the machine.")]
            public float sparkleSpread = 0.4f;

            [Min(0f), Tooltip("Initial upward offset from the machine center so sparkles read against the sprite.")]
            public float sparkleSpawnHeight = 0.55f;

            [Tooltip("World sprite sorting order; keep below the HUD canvas (100).")]
            public int sparkleSortingOrder = 90;
        }

        [Serializable]
        public sealed class BubbleBurstStyle
        {
            [Min(0f), Tooltip("Minimum radial spawn offset from the event position.")]
            public float radiusMin = 0.05f;

            [Min(0f), Tooltip("Maximum radial spawn offset from the event position.")]
            public float radiusMax = 0.3f;

            [Min(0f), Tooltip("Minimum outward radial velocity.")]
            public float radialSpeedMin = 0.3f;

            [Min(0f), Tooltip("Maximum outward radial velocity.")]
            public float radialSpeedMax = 0.9f;

            [Min(0f), Tooltip("Minimum upward velocity boost.")]
            public float riseSpeedMin = 0.2f;

            [Min(0f), Tooltip("Maximum upward velocity boost.")]
            public float riseSpeedMax = 0.5f;

            [Min(0.01f), Tooltip("Minimum bubble particle size.")]
            public float sizeMin = 0.05f;

            [Min(0.01f), Tooltip("Maximum bubble particle size.")]
            public float sizeMax = 0.14f;

            [Min(0.05f), Tooltip("Minimum bubble particle lifetime (seconds).")]
            public float lifetimeMin = 0.8f;

            [Min(0.05f), Tooltip("Maximum bubble particle lifetime (seconds).")]
            public float lifetimeMax = 1.8f;

            [Tooltip("Tint of impact/retrieval bubbles.")]
            public Color color = new Color(0.72f, 0.9f, 0.97f, 0.75f);
        }

        [Header("Master")]
        [SerializeField, Tooltip("Master switch for all game feel effects (toggle in play mode with F8).")]
        private bool effectsEnabled = true;

        [Header("Effects")]
        [SerializeField] private CameraShakeSettings cameraShake = new();
        [SerializeField] private PlaceMachineSettings placeMachine = new();
        [SerializeField] private RetrieveSettings retrieve = new();
        [SerializeField] private OutOfHydroxSettings outOfHydrox = new();
        [SerializeField] private SpotRevealSettings spotReveal = new();
        [SerializeField] private ItemPickupSettings itemPickup = new();
        [SerializeField] private QuestItemSettings questItem = new();
        [SerializeField] private OreProductionSettings oreProduction = new();

        [Header("Shared particle style")]
        [SerializeField] private BubbleBurstStyle bubbleBurst = new();

        private const float ToggleBannerSeconds = 1.5f;

        public bool EffectsEnabled
        {
            get => effectsEnabled;
            set
            {
                if (effectsEnabled == value) return;
                effectsEnabled = value;
                if (!effectsEnabled) CleanupAllEffects();
            }
        }

        private OceanInteractions ocean;
        private OceanVfx vfx;
        private Camera view;
        private bool followsTarget;
        private float baseSize;
        private Quaternion baseRotation;
        private Vector3 lastOffset;
        private float trauma, beatTime, toggleShownUntil;
        private bool cameraDirty;
        private float thudElapsed = float.PositiveInfinity;
        private readonly Dictionary<Transform, (Vector3 position, Coroutine run)> machineDrops = new();
        private readonly Dictionary<Transform, (Vector3 scale, Coroutine run)> revealPunches = new();
        private readonly Dictionary<Transform, (Vector3 position, Coroutine run)> oreSettles = new();
        private readonly List<(GameObject go, Coroutine run)> sparkles = new();
        private readonly System.Random presentationRandom = new();
        private float photoFlashStart = -1f;
        private float nextOreFeedbackTime;
        private UnityEngine.UI.Image photoFlash;
        private Sprite whiteSprite;
        private Texture2D whiteTexture;

        private bool CameraThudActive => placeMachine.enabled && placeMachine.thudOffset > 0f
            && placeMachine.thudDuration > 0f && thudElapsed < placeMachine.thudDuration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
            Attach(default, default);
        }

        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            var ocean = FindAnyObjectByType<OceanInteractions>(FindObjectsInactive.Include);
            if (ocean != null && !ocean.TryGetComponent<OceanGameFeel>(out _))
                ocean.gameObject.AddComponent<OceanGameFeel>();
        }

        private void Awake()
        {
            ocean = GetComponent<OceanInteractions>();
            vfx = GetComponent<OceanVfx>();
        }

        private void OnEnable()
        {
            if (ocean == null) return;
            ocean.MachinePlaced += OnMachinePlaced;
            ocean.MachineRetrieved += OnMachineRetrieved;
            ocean.SpotRevealed += OnSpotRevealed;
            ocean.ItemCollected += OnItemCollected;
            ocean.QuestItemCollected += OnQuestItemCollected;
            ocean.OreProduced += OnOreProduced;
        }

        private void OnDisable()
        {
            if (ocean != null)
            {
                ocean.MachinePlaced -= OnMachinePlaced;
                ocean.MachineRetrieved -= OnMachineRetrieved;
                ocean.SpotRevealed -= OnSpotRevealed;
                ocean.ItemCollected -= OnItemCollected;
                ocean.QuestItemCollected -= OnQuestItemCollected;
                ocean.OreProduced -= OnOreProduced;
            }
            CleanupAllEffects();
        }

        private void OnDestroy()
        {
            CleanupOreProduction();
            if (photoFlash != null) Destroy(photoFlash.gameObject);
            if (whiteSprite != null) Destroy(whiteSprite);
            if (whiteTexture != null) Destroy(whiteTexture);
        }

        private void OnValidate()
        {
            if (Application.isPlaying) SyncEnabledStates();
        }

        private void Start()
        {
            view = ocean != null ? ocean.oceanCamera : null;
            if (view == null) { enabled = false; return; }
            followsTarget = view.TryGetComponent<CenteredCamera>(out _);
            baseSize = view.orthographicSize;
            baseRotation = view.transform.rotation;
        }

        /// <summary>Adds camera trauma; the visible shake is trauma squared, so small hits stay subtle.</summary>
        public void AddTrauma(float amount)
        {
            if (!effectsEnabled || !cameraShake.enabled || amount <= 0f) return;
            trauma = Mathf.Clamp01(trauma + amount);
        }

        private void SyncEnabledStates()
        {
            if (!effectsEnabled)
            {
                CleanupAllEffects();
                return;
            }
            if (!placeMachine.enabled && (machineDrops.Count > 0 || CameraThudActive)) CleanupPlaceMachine();
            if (!spotReveal.enabled && revealPunches.Count > 0) CleanupSpotReveal();
            if (!questItem.enabled && photoFlashStart >= 0f) CleanupPhotoFlash();
            if (!oreProduction.enabled)
            {
                if (oreSettles.Count > 0 || sparkles.Count > 0) CleanupOreProduction();
                nextOreFeedbackTime = 0f;
            }
            if (!outOfHydrox.enabled) beatTime = 0f;
            if (!cameraShake.enabled) trauma = 0f;
            if (!cameraShake.enabled && !outOfHydrox.enabled && !CameraThudActive) ResetCamera();
        }

        private void CleanupAllEffects()
        {
            CleanupPlaceMachine();
            CleanupSpotReveal();
            CleanupOreProduction();
            CleanupPhotoFlash();
            beatTime = 0f;
            trauma = 0f;
            ResetCamera();
        }

        private void CleanupPlaceMachine()
        {
            foreach (var pair in machineDrops)
            {
                if (pair.Value.run != null) StopCoroutine(pair.Value.run);
                if (pair.Key != null) pair.Key.localPosition = pair.Value.position;
            }
            machineDrops.Clear();
            thudElapsed = float.PositiveInfinity;
            ResetCamera();
        }

        private void CleanupSpotReveal()
        {
            foreach (var pair in revealPunches)
            {
                if (pair.Value.run != null) StopCoroutine(pair.Value.run);
                if (pair.Key != null) pair.Key.localScale = pair.Value.scale;
            }
            revealPunches.Clear();
        }

        private void CleanupOreProduction()
        {
            foreach (var pair in oreSettles)
            {
                if (pair.Value.run != null) StopCoroutine(pair.Value.run);
                if (pair.Key != null) pair.Key.localPosition = pair.Value.position;
            }
            oreSettles.Clear();
            foreach (var entry in sparkles)
            {
                if (entry.run != null) StopCoroutine(entry.run);
                if (entry.go != null) Destroy(entry.go);
            }
            sparkles.Clear();
            nextOreFeedbackTime = 0f;
        }

        private void CleanupPhotoFlash()
        {
            photoFlashStart = -1f;
            if (photoFlash != null) photoFlash.enabled = false;
        }

        // ---------------------------------------------------------------- 1. Placing a machine

        private void OnMachinePlaced()
        {
            if (!effectsEnabled || !placeMachine.enabled || ocean == null) return;
            var spot = ocean.Closest;
            if (spot == null) return;
            if (spot.machine != null) DropIn(spot.machine.transform, spot.transform.position);
            else LandingImpact(spot.transform.position);
        }

        private void DropIn(Transform target, Vector3 spotPosition)
        {
            // Remember the true rest position so re-triggers never leave the machine offset.
            bool hadDrop = machineDrops.TryGetValue(target, out var running);
            var restPosition = hadDrop ? running.position : target.localPosition;
            if (hadDrop && running.run != null) StopCoroutine(running.run);
            if (oreSettles.TryGetValue(target, out var settling))
            {
                if (!hadDrop) restPosition = settling.position;
                if (settling.run != null) StopCoroutine(settling.run);
                oreSettles.Remove(target);
            }
            // Register first: a zero-duration drop can finish synchronously and remove itself.
            machineDrops[target] = (restPosition, null);
            var run = StartCoroutine(DropMachine(target, restPosition, spotPosition));
            if (machineDrops.ContainsKey(target)) machineDrops[target] = (restPosition, run);
        }

        /// <summary>Ease-in metal drop onto the seabed; the machine never scales or squashes.</summary>
        private IEnumerator DropMachine(Transform target, Vector3 restPosition, Vector3 spotPosition)
        {
            float fallTime = placeMachine.fallTime;
            float fallHeight = placeMachine.fallHeight;
            if (fallTime > 0f && fallHeight > 0f)
            {
                for (float t = 0f; t < fallTime && target != null; t += Time.deltaTime)
                {
                    float k = Mathf.Clamp01(t / fallTime);
                    float height = fallHeight * (1f - k * k); // ease-in: accelerates like it's heavy
                    target.localPosition = restPosition + Vector3.up * height;
                    yield return null;
                }
            }
            if (target == null) { machineDrops.Remove(target); yield break; }

            target.localPosition = restPosition;
            LandingImpact(spotPosition);
            machineDrops.Remove(target);
        }

        private void LandingImpact(Vector3 position)
        {
            SedimentRing(position);
            BubbleBurst(position, placeMachine.bubbleCount);
            StartCameraThud();
        }

        private void StartCameraThud()
        {
            if (!effectsEnabled || !placeMachine.enabled || placeMachine.thudOffset <= 0f
                || placeMachine.thudDuration <= 0f) return;
            thudElapsed = 0f;
        }

        /// <summary>Single downward camera bump that returns monotonically to rest with ease-out.</summary>
        private float CameraThudOffset()
        {
            if (!CameraThudActive) return 0f;
            float k = Mathf.Clamp01(thudElapsed / Mathf.Max(0.0001f, placeMachine.thudDuration));
            return -placeMachine.thudOffset * Mathf.Pow(1f - k, 3f);
        }

        /// <summary>A flat ring of sediment kicked out sideways by the impact.</summary>
        private void SedimentRing(Vector3 position) => SedimentPuff(position,
            placeMachine.sedimentCount, placeMachine.sedimentRadius, placeMachine.sedimentFlattenY,
            placeMachine.sedimentSpeedMin, placeMachine.sedimentSpeedMax,
            placeMachine.sedimentSizeMin, placeMachine.sedimentSizeMax,
            placeMachine.sedimentLifetimeMin, placeMachine.sedimentLifetimeMax, placeMachine.sedimentColor);

        /// <summary>A flat ring of sand particles emitted with the supplied tuning.</summary>
        private void SedimentPuff(Vector3 position, int count, float radius, float flattenY,
            float speedMin, float speedMax, float sizeMin, float sizeMax,
            float lifetimeMin, float lifetimeMax, Color color)
        {
            if (vfx == null || vfx.Bubbles == null || count <= 0) return;
            float minSpeed = Mathf.Min(speedMin, speedMax);
            float maxSpeed = Mathf.Max(speedMin, speedMax);
            float minSize = Mathf.Min(sizeMin, sizeMax);
            float maxSize = Mathf.Max(sizeMin, sizeMax);
            float minLife = Mathf.Min(lifetimeMin, lifetimeMax);
            float maxLife = Mathf.Max(lifetimeMin, lifetimeMax);

            for (int i = 0; i < count; i++)
            {
                float angle = (i + PresentationValue() * 0.5f) / count * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * flattenY);
                vfx.Bubbles.Emit(new ParticleSystem.EmitParams
                {
                    position = (Vector2)position + dir * radius,
                    velocity = dir * PresentationRange(minSpeed, maxSpeed),
                    startSize = PresentationRange(minSize, maxSize),
                    startLifetime = PresentationRange(minLife, maxLife),
                    startColor = color
                }, 1);
            }
        }

        private float PresentationValue() => (float)presentationRandom.NextDouble();
        private float PresentationRange(float min, float max) => min + PresentationValue() * (max - min);

        private Vector2 PresentationDirection()
        {
            double angle = presentationRandom.NextDouble() * Math.PI * 2.0;
            return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        // ---------------------------------------------------------------- 2. Retrieving tellurium

        private void OnMachineRetrieved(OceanTarget spot, int amount)
        {
            if (!effectsEnabled || !retrieve.enabled || spot == null || amount <= 0) return;
            int minBubbles = Mathf.Min(retrieve.minBubbleCount, retrieve.maxBubbleCount);
            int maxBubbles = Mathf.Max(retrieve.minBubbleCount, retrieve.maxBubbleCount);
            int count = Mathf.Clamp(minBubbles + amount * retrieve.bubblesPerTellurium, minBubbles, maxBubbles);
            BubbleBurst(spot.transform.position, count);
            AddTrauma(retrieve.trauma);
        }

        private void BubbleBurst(Vector3 position, int count)
        {
            if (vfx == null || vfx.Bubbles == null || count <= 0) return;
            float radiusMin = Mathf.Min(bubbleBurst.radiusMin, bubbleBurst.radiusMax);
            float radiusMax = Mathf.Max(bubbleBurst.radiusMin, bubbleBurst.radiusMax);
            float speedMin = Mathf.Min(bubbleBurst.radialSpeedMin, bubbleBurst.radialSpeedMax);
            float speedMax = Mathf.Max(bubbleBurst.radialSpeedMin, bubbleBurst.radialSpeedMax);
            float riseMin = Mathf.Min(bubbleBurst.riseSpeedMin, bubbleBurst.riseSpeedMax);
            float riseMax = Mathf.Max(bubbleBurst.riseSpeedMin, bubbleBurst.riseSpeedMax);
            float sizeMin = Mathf.Min(bubbleBurst.sizeMin, bubbleBurst.sizeMax);
            float sizeMax = Mathf.Max(bubbleBurst.sizeMin, bubbleBurst.sizeMax);
            float lifeMin = Mathf.Min(bubbleBurst.lifetimeMin, bubbleBurst.lifetimeMax);
            float lifeMax = Mathf.Max(bubbleBurst.lifetimeMin, bubbleBurst.lifetimeMax);

            for (int i = 0; i < count; i++)
            {
                Vector2 dir = PresentationDirection();
                vfx.Bubbles.Emit(new ParticleSystem.EmitParams
                {
                    position = (Vector2)position + dir * PresentationRange(radiusMin, radiusMax),
                    velocity = dir * PresentationRange(speedMin, speedMax)
                        + Vector2.up * PresentationRange(riseMin, riseMax),
                    startSize = PresentationRange(sizeMin, sizeMax),
                    startLifetime = PresentationRange(lifeMin, lifeMax),
                    startColor = bubbleBurst.color
                }, 1);
            }
        }

        // ---------------------------------------------------------------- 3. Prospect reveal

        private void OnSpotRevealed(OceanTarget spot)
        {
            if (!effectsEnabled || !spotReveal.enabled || spot == null) return;
            int tier = Mathf.Clamp(spot.DisplayedTier, 0, 5);
            float peak = 1f + Mathf.Max(0f, spotReveal.scalePunch + spotReveal.scalePunchPerTier * tier);
            float duration = Mathf.Max(0.05f, spotReveal.punchDuration);
            float riseFraction = Mathf.Clamp(spotReveal.punchRiseFraction, 0.05f, 0.9f);

            // Scale only visual children; the root keeps its position and any collider/Closest behavior.
            foreach (var visual in spot.GetComponentsInChildren<Renderer>(true))
            {
                if (visual == null) continue;
                var target = visual.transform;
                if (target == spot.transform) continue;
                // The machine has its own placement and production feedback; leave it alone.
                if (spot.machine != null && target == spot.machine.transform) continue;
                if (target.GetComponent<Collider2D>() != null || target.GetComponent<Collider>() != null) continue;
                StartScalePunch(target, peak, duration, riseFraction);
            }

            Color color = spotReveal.tintBubblesByTier ? spot.TierColor : bubbleBurst.color;
            color.a = bubbleBurst.color.a;
            int count = Mathf.Max(0, spotReveal.bubbleCount + spotReveal.bubbleCountPerTier * tier);
            float radius = Mathf.Max(0f, spotReveal.bubbleRadius + spotReveal.bubbleRadiusPerTier * tier);
            BubbleRing(spot.transform.position, count, radius, color);
        }

        private void StartScalePunch(Transform target, float peak, float duration, float riseFraction)
        {
            // Keep the true rest pose so repeated reveals never leave a distorted visual.
            var rest = target.localScale;
            if (revealPunches.TryGetValue(target, out var running))
            {
                rest = running.scale;
                if (running.run != null) StopCoroutine(running.run);
            }
            // Register first: a zero-duration punch can finish synchronously and remove itself.
            revealPunches[target] = (rest, null);
            var run = StartCoroutine(ScalePunch(target, rest, peak, duration, riseFraction));
            if (revealPunches.ContainsKey(target)) revealPunches[target] = (rest, run);
        }

        private IEnumerator ScalePunch(Transform target, Vector3 rest, float peak, float duration, float riseFraction)
        {
            float riseTime = duration * Mathf.Clamp(riseFraction, 0.01f, 0.99f);
            for (float t = 0f; t < duration && target != null; t += Time.deltaTime)
            {
                float scale;
                if (t < riseTime)
                {
                    float k = t / riseTime;
                    scale = Mathf.LerpUnclamped(1f, peak, 1f - Mathf.Pow(1f - k, 3f));
                }
                else
                {
                    float k = Mathf.Clamp01((t - riseTime) / Mathf.Max(0.0001f, duration - riseTime));
                    scale = Mathf.LerpUnclamped(peak, 1f, 1f - Mathf.Pow(1f - k, 3f));
                }
                target.localScale = rest * scale;
                yield return null;
            }
            if (target != null) target.localScale = rest;
            revealPunches.Remove(target);
        }

        private void BubbleRing(Vector3 position, int count, float radius, Color color)
        {
            if (vfx == null || vfx.Bubbles == null || count <= 0) return;
            float speedMin = Mathf.Min(bubbleBurst.radialSpeedMin, bubbleBurst.radialSpeedMax);
            float speedMax = Mathf.Max(bubbleBurst.radialSpeedMin, bubbleBurst.radialSpeedMax);
            float riseMin = Mathf.Min(bubbleBurst.riseSpeedMin, bubbleBurst.riseSpeedMax);
            float riseMax = Mathf.Max(bubbleBurst.riseSpeedMin, bubbleBurst.riseSpeedMax);
            float sizeMin = Mathf.Min(bubbleBurst.sizeMin, bubbleBurst.sizeMax);
            float sizeMax = Mathf.Max(bubbleBurst.sizeMin, bubbleBurst.sizeMax);
            float lifeMin = Mathf.Min(bubbleBurst.lifetimeMin, bubbleBurst.lifetimeMax);
            float lifeMax = Mathf.Max(bubbleBurst.lifetimeMin, bubbleBurst.lifetimeMax);

            for (int i = 0; i < count; i++)
            {
                float angle = (i + PresentationValue() * 0.5f) / count * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vfx.Bubbles.Emit(new ParticleSystem.EmitParams
                {
                    position = (Vector2)position + dir * radius,
                    velocity = dir * PresentationRange(speedMin, speedMax)
                        + Vector2.up * PresentationRange(riseMin, riseMax),
                    startSize = PresentationRange(sizeMin, sizeMax),
                    startLifetime = PresentationRange(lifeMin, lifeMax),
                    startColor = color
                }, 1);
            }
        }

        // ---------------------------------------------------------------- 4. Item pickup

        private void OnItemCollected(Vector3 position, int _count, string _itemName, bool _isDelivery)
        {
            // Counts, names, and order state belong to the HUD; only the world bubble remains here.
            if (!effectsEnabled || !itemPickup.enabled) return;
            BubbleBurst(position, itemPickup.bubbleCount);
        }

        // ---------------------------------------------------------------- 5. Eye photo flash

        private void OnQuestItemCollected(OceanTarget _spot, string _itemName, bool isEyePhoto)
        {
            if (!effectsEnabled || !questItem.enabled || !isEyePhoto) return;
            EnsureWhiteSprite();
            EnsurePhotoFlash();
            if (photoFlash == null) return;
            photoFlashStart = Time.unscaledTime;
            photoFlash.enabled = true;
            UpdatePhotoFlash();
        }

        private void EnsureWhiteSprite()
        {
            if (whiteSprite != null) return;
            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Juice White Pixel",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            whiteSprite.name = "Juice White Sprite";
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private void EnsurePhotoFlash()
        {
            if (photoFlash != null || whiteSprite == null) return;
            var hud = FindAnyObjectByType<OceanHud>();
            var canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;
            var go = new GameObject("Juice Photo Flash", typeof(RectTransform), typeof(UnityEngine.UI.Image))
            {
                hideFlags = HideFlags.DontSave
            };
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            photoFlash = go.GetComponent<UnityEngine.UI.Image>();
            photoFlash.sprite = whiteSprite;
            photoFlash.color = Color.clear;
            photoFlash.raycastTarget = false;
            photoFlash.enabled = false;
        }

        /// <summary>
        /// Updates a full-viewport flash image. It is the first child of the HUD canvas, so it covers the
        /// world viewport but renders below every HUD element.
        /// </summary>
        private void UpdatePhotoFlash()
        {
            if (photoFlashStart < 0f || photoFlash == null) return;
            float duration = Mathf.Max(0.01f, questItem.flashDuration);
            float elapsed = Time.unscaledTime - photoFlashStart;
            if (elapsed < 0f || elapsed >= duration)
            {
                CleanupPhotoFlash();
                return;
            }

            float fade = 1f - elapsed / duration;
            var tint = questItem.flashColor;
            photoFlash.color = new Color(tint.r, tint.g, tint.b, tint.a * questItem.flashAlpha * fade * fade);
            photoFlash.enabled = true;
        }

        // ---------------------------------------------------------------- 6. Ore production

        private void OnOreProduced(OceanTarget spot)
        {
            if (!effectsEnabled || !oreProduction.enabled || spot == null || view == null) return;
            if (Time.time < nextOreFeedbackTime) return;
            Vector3 viewport = view.WorldToViewportPoint(spot.transform.position);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return;
            nextOreFeedbackTime = Time.time + Mathf.Max(0.1f, oreProduction.minInterval);
            StartOreSettle(spot);
            EmitMetalSparkles(spot);
        }

        private void StartOreSettle(OceanTarget spot)
        {
            if (oreProduction.settleDistance <= 0f || oreProduction.settleDuration <= 0f) return;
            var machine = spot.machine;
            if (machine == null) return;
            var target = machine.transform;
            if (machineDrops.ContainsKey(target)) return;

            var restPosition = target.localPosition;
            if (oreSettles.TryGetValue(target, out var running))
            {
                restPosition = running.position;
                if (running.run != null) StopCoroutine(running.run);
            }
            // Register first: a zero-duration settle can finish synchronously and remove itself.
            oreSettles[target] = (restPosition, null);
            var run = StartCoroutine(SettleMachine(target, restPosition, oreProduction.settleDistance,
                oreProduction.settleDuration, oreProduction.settleDownFraction));
            if (oreSettles.ContainsKey(target)) oreSettles[target] = (restPosition, run);
        }

        /// <summary>Small downward dip and monotonic return; the machine never scales or overshoots.</summary>
        private IEnumerator SettleMachine(Transform target, Vector3 restPosition, float distance, float duration,
            float downFraction)
        {
            duration = Mathf.Max(0.01f, duration);
            float downTime = duration * Mathf.Clamp(downFraction, 0.05f, 0.9f);
            for (float t = 0f; t < duration && target != null; t += Time.deltaTime)
            {
                float offset;
                if (t < downTime)
                {
                    float k = t / downTime;
                    offset = -distance * k * k;
                }
                else
                {
                    float k = Mathf.Clamp01((t - downTime) / Mathf.Max(0.0001f, duration - downTime));
                    offset = -distance * Mathf.Pow(1f - k, 3f);
                }
                target.localPosition = restPosition + Vector3.up * offset;
                yield return null;
            }
            if (target != null) target.localPosition = restPosition;
            oreSettles.Remove(target);
        }

        private void EmitMetalSparkles(OceanTarget spot)
        {
            if (oreProduction.sparkleCount <= 0) return;
            EnsureWhiteSprite();
            if (whiteSprite == null) return;
            Vector3 center = spot.machine != null ? spot.machine.transform.position : spot.transform.position;
            float minSize = Mathf.Min(oreProduction.sparkleSizeMin, oreProduction.sparkleSizeMax);
            float maxSize = Mathf.Max(oreProduction.sparkleSizeMin, oreProduction.sparkleSizeMax);
            float minLife = Mathf.Min(oreProduction.sparkleLifetimeMin, oreProduction.sparkleLifetimeMax);
            float maxLife = Mathf.Max(oreProduction.sparkleLifetimeMin, oreProduction.sparkleLifetimeMax);
            float minRise = Mathf.Min(oreProduction.sparkleRiseSpeedMin, oreProduction.sparkleRiseSpeedMax);
            float maxRise = Mathf.Max(oreProduction.sparkleRiseSpeedMin, oreProduction.sparkleRiseSpeedMax);

            for (int i = 0; i < oreProduction.sparkleCount; i++)
            {
                var go = new GameObject("Juice Metal Sparkle", typeof(SpriteRenderer)) { hideFlags = HideFlags.DontSave };
                var sprite = go.GetComponent<SpriteRenderer>();
                sprite.sprite = whiteSprite;
                sprite.sortingOrder = oreProduction.sparkleSortingOrder;
                sprite.color = oreProduction.sparkleColor;

                float angle = PresentationValue() * Mathf.PI * 2f;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * PresentationRange(0f, oreProduction.sparkleSpread);
                go.transform.position = center + Vector3.up * oreProduction.sparkleSpawnHeight + (Vector3)offset;
                go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                float size = PresentationRange(minSize, maxSize);
                go.transform.localScale = Vector3.one * size;

                var velocity = new Vector2(PresentationRange(-oreProduction.sparkleDriftSpeed, oreProduction.sparkleDriftSpeed),
                    PresentationRange(minRise, maxRise));
                float lifetime = PresentationRange(minLife, maxLife);
                // Register first: a zero-duration sparkle can finish synchronously and remove itself.
                sparkles.Add((go, null));
                var run = StartCoroutine(AnimateSparkle(go, sprite, velocity, size, lifetime, oreProduction.sparkleColor));
                for (int j = sparkles.Count - 1; j >= 0; j--)
                    if (sparkles[j].go == go) { sparkles[j] = (go, run); break; }
            }
        }

        private IEnumerator AnimateSparkle(GameObject go, SpriteRenderer sprite, Vector2 velocity, float size,
            float lifetime, Color color)
        {
            float duration = Mathf.Max(0.01f, lifetime);
            for (float t = 0f; t < duration && go != null; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                go.transform.position += (Vector3)(velocity * Time.deltaTime);
                sprite.color = new Color(color.r, color.g, color.b, color.a * (1f - k) * (1f - k));
                go.transform.localScale = Vector3.one * (size * Mathf.Lerp(1f, 0.55f, k));
                yield return null;
            }
            for (int i = sparkles.Count - 1; i >= 0; i--)
                if (sparkles[i].go == go) sparkles.RemoveAt(i);
            if (go != null) Destroy(go);
        }

        // ---------------------------------------------------------------- 7. Out-of-hydrox heartbeat

        /// <summary>
        /// Runs only during the breathless grace period after the tank is empty: the heartbeat speeds up
        /// and the screen trembles harder as time runs out, then everything stops when the diver blacks out.
        /// </summary>
        private float HeartbeatZoom(float dt)
        {
            if (!outOfHydrox.enabled || ocean == null) { beatTime = 0f; return 0f; }
            var tank = ocean.Hydrox;
            float grace = ocean.BreathlessGraceSeconds;
            if (tank == null || !tank.IsDepleted || ocean.Incapacitated || grace <= 0f)
            {
                beatTime = 0f;
                return 0f;
            }

            float urgency = Mathf.Clamp01(1f - ocean.BreathingTimeLeft / grace); // 0 at "out of gas", 1 at blackout
            if (cameraShake.enabled)
                trauma = Mathf.Max(trauma, Mathf.Lerp(outOfHydrox.traumaMin, outOfHydrox.traumaMax, urgency * urgency));

            float bpm = Mathf.Lerp(outOfHydrox.bpmMin, outOfHydrox.bpmMax, urgency);
            beatTime += dt * bpm / 60f;
            float phase = beatTime % 1f;
            // "Lub-dub": two quick pulses at the start of each beat.
            float pulse = Pulse(phase, 0f, outOfHydrox.pulseWidth)
                + outOfHydrox.secondBeatStrength * Pulse(phase, outOfHydrox.secondBeatOffset, outOfHydrox.pulseWidth);
            float zoomScale = Mathf.Lerp(outOfHydrox.zoomScaleMin, outOfHydrox.zoomScaleMax, urgency);
            return outOfHydrox.heartbeatZoom * zoomScale * pulse;
        }

        private static float Pulse(float phase, float start, float width)
        {
            if (width <= 0f) return 0f;
            float x = (phase - start) / width;
            return x < 0f || x > 1f ? 0f : Mathf.Sin(x * Mathf.PI);
        }

        // ---------------------------------------------------------------- Camera & input

        private void Update()
        {
            SyncEnabledStates();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                EffectsEnabled = !EffectsEnabled;
                toggleShownUntil = Time.unscaledTime + ToggleBannerSeconds;
            }
        }

        private void LateUpdate()
        {
            if (view == null) return;
            SyncEnabledStates();
            UpdatePhotoFlash();
            bool active = effectsEnabled && Time.timeScale > 0f && ocean != null && !ocean.InsideSubmarine && !ocean.LoadoutOpen;
            if (!active) { trauma = 0f; beatTime = 0f; ResetCamera(); return; }

            float dt = Time.deltaTime;
            float zoom = HeartbeatZoom(dt);
            trauma = cameraShake.enabled ? Mathf.Max(0f, trauma - cameraShake.traumaDecay * dt) : 0f;

            bool thudActive = CameraThudActive;
            // Idle: leave the camera alone so other code (and the editor play checks) own it.
            if (trauma <= 0f && zoom <= 0f && !thudActive) { ResetCamera(); return; }
            cameraDirty = true;
            float shake = cameraShake.enabled ? trauma * trauma : 0f;
            float time = Time.time * cameraShake.shakeFrequency;
            var offset = new Vector3(Noise(time, 0f), Noise(time, 10f), 0f) * (cameraShake.maxShakeOffset * shake);
            if (thudActive) offset += Vector3.up * CameraThudOffset();
            float angle = Noise(time, 20f) * cameraShake.maxShakeAngle * shake;

            // CenteredCamera rewrites the position every frame; otherwise remove last frame's offset.
            if (!followsTarget) view.transform.position -= lastOffset;
            view.transform.position += offset;
            lastOffset = offset;
            view.transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
            view.orthographicSize = baseSize * (1f - zoom);

            if (thudActive) thudElapsed += dt;
        }

        /// <summary>Smooth random value in [-1, 1]; Perlin noise keeps the shake from looking jittery.</summary>
        private static float Noise(float time, float seed) => Mathf.PerlinNoise(time, seed) * 2f - 1f;

        /// <summary>Restores the camera once, when an effect ends; does nothing while idle.</summary>
        private void ResetCamera()
        {
            thudElapsed = float.PositiveInfinity;
            if (view == null || !cameraDirty) return;
            cameraDirty = false;
            if (!followsTarget) view.transform.position -= lastOffset;
            lastOffset = Vector3.zero;
            view.transform.rotation = baseRotation;
            view.orthographicSize = baseSize;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > toggleShownUntil) return;
            GUI.Label(new Rect(12, 12, 240, 24), effectsEnabled ? "Game Feel: ON  (F8)" : "Game Feel: OFF  (F8)");
        }
    }
}
