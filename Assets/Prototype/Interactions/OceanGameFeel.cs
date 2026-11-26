using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    /// <summary>
    /// Experimental "game feel" layer for excursions. It only listens to gameplay events and adds
    /// feedback (camera shake, drop/squash, floating numbers, prospect/item/wreck flourishes, a
    /// photo flash, an out-of-hydrox heartbeat), so it never changes gameplay rules. Press F8 to
    /// toggle it for before/after comparisons.
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
            [Tooltip("Enable heavy drop, impact squash, sediment ring, bubble burst, and camera trauma on machine placement.")]
            public bool enabled = true;

            [Header("Drop")]
            [Min(0f), Tooltip("Duration of the ease-in drop onto the mining spot (seconds).")]
            public float fallTime = 0.16f;

            [Min(0f), Tooltip("Starting height above the spot before dropping (world units).")]
            public float fallHeight = 1.2f;

            [Range(0f, 0.4f), Tooltip("Optional vertical stretch while falling (0 = rigid heavy metal).")]
            public float fallStretch = 0f;

            [Header("Impact squash")]
            [Range(0f, 0.4f), Tooltip("Single landing squash magnitude; recovers monotonically with ease-out and never overshoots.")]
            public float squashAmount = 0.1f;

            [Min(0f), Tooltip("Time to settle monotonically from impact squash back to rest scale (seconds).")]
            public float squashDuration = 0.2f;

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

            [Header("Bubbles & camera")]
            [Range(0, 64), Tooltip("Number of bubbles released when the machine hits the seabed.")]
            public int bubbleCount = 18;

            [Range(0f, 1f), Tooltip("Camera trauma added on landing impact.")]
            public float trauma = 0.5f;
        }

        [Serializable]
        public sealed class RetrieveSettings
        {
            [Tooltip("Enable floating tellurium text, bubble burst, and camera trauma on machine retrieval.")]
            public bool enabled = true;

            [Header("Floating text")]
            [Min(0.05f), Tooltip("Lifetime of the floating text (seconds).")]
            public float textDuration = 1.1f;

            [Tooltip("Initial vertical offset above the mining spot.")]
            public float textStartY = 0.9f;

            [Min(0f), Tooltip("Upward travel distance over the text lifetime.")]
            public float textRiseDistance = 1.2f;

            [Range(1f, 3f), Tooltip("Initial pop-in scale multiplier before settling to 1.")]
            public float textPopScale = 1.6f;

            [Range(0.01f, 0.5f), Tooltip("Normalized fraction of duration spent popping from textPopScale down to 1.")]
            public float textPopFraction = 0.15f;

            [Range(1f, 15f), Tooltip("TextMeshPro font size.")]
            public float textFontSize = 5f;

            [Header("Layout")]
            public FloatingTextLayout textLayout = new();

            [Tooltip("Text color when tellurium > 0 is retrieved.")]
            public Color positiveColor = new Color(0.55f, 0.95f, 1f, 1f);

            [Tooltip("Text color when the retrieved machine yielded 0 tellurium.")]
            public Color emptyColor = new Color(0.7f, 0.7f, 0.7f, 1f);

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
        public sealed class FloatingTextLayout
        {
            [Tooltip("World-space Z offset so floating text stays in front of the target.")]
            public float zOffset = -0.2f;

            [Tooltip("Sorting order of the floating text renderer.")]
            public int sortingOrder = 40;

            [Tooltip("Local size of the text box before TextMeshPro renders the string.")]
            public Vector2 boxSize = new Vector2(5f, 1f);
        }

        [Serializable]
        public sealed class FloatingTextTuning
        {
            [Min(0.05f), Tooltip("Lifetime of the floating text (seconds).")]
            public float duration = 1f;

            [Tooltip("Initial vertical offset above the event position.")]
            public float startY = 0.9f;

            [Min(0f), Tooltip("Upward travel distance over the text lifetime.")]
            public float riseDistance = 1f;

            [Range(1f, 3f), Tooltip("Initial pop-in scale multiplier before settling to 1.")]
            public float popScale = 1.5f;

            [Range(0.01f, 0.5f), Tooltip("Normalized fraction of duration spent popping from popScale down to 1.")]
            public float popFraction = 0.15f;

            [Range(1f, 15f), Tooltip("TextMeshPro font size.")]
            public float fontSize = 4.5f;

            [Header("Layout")]
            public FloatingTextLayout layout = new();
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
        public sealed class WreckSalvageSettings
        {
            [Tooltip("Enable floating tellurium text, sediment dust, and camera trauma when salvaging a wreck.")]
            public bool enabled = true;

            [Header("Floating text")]
            public FloatingTextTuning text = new();

            [Tooltip("Color of the recovered-tellurium text.")]
            public Color textColor = new Color(0.55f, 0.95f, 1f, 1f);

            [Header("Sediment")]
            [Range(0, 80), Tooltip("Number of sand particles kicked out by the salvage.")]
            public int sedimentCount = 18;

            [Min(0f), Tooltip("Initial radial offset from the wreck center.")]
            public float sedimentRadius = 0.2f;

            [Range(0.1f, 1f), Tooltip("Vertical flattening of the ring so it reads as seabed floor dust.")]
            public float sedimentFlattenY = 0.5f;

            [Min(0f), Tooltip("Minimum outward speed of sediment particles.")]
            public float sedimentSpeedMin = 1.2f;

            [Min(0f), Tooltip("Maximum outward speed of sediment particles.")]
            public float sedimentSpeedMax = 1.8f;

            [Min(0.01f), Tooltip("Minimum sediment particle size.")]
            public float sedimentSizeMin = 0.07f;

            [Min(0.01f), Tooltip("Maximum sediment particle size.")]
            public float sedimentSizeMax = 0.13f;

            [Min(0.05f), Tooltip("Minimum sediment particle lifetime (seconds).")]
            public float sedimentLifetimeMin = 0.4f;

            [Min(0.05f), Tooltip("Maximum sediment particle lifetime (seconds).")]
            public float sedimentLifetimeMax = 0.8f;

            [Tooltip("Color of the salvage sediment puff.")]
            public Color sedimentColor = new Color(0.78f, 0.72f, 0.6f, 0.85f);

            [Header("Camera")]
            [Range(0f, 1f), Tooltip("Camera trauma added on a successful salvage.")]
            public float trauma = 0.12f;
        }

        [Serializable]
        public sealed class ItemCollectSettings
        {
            [Tooltip("Enable floating item text and a small bubble puff when picking up an item or order.")]
            public bool enabled = true;

            [Header("Floating text")]
            public FloatingTextTuning text = new();

            [Tooltip("Color of regular pickup text.")]
            public Color textColor = new Color(0.85f, 1f, 0.85f, 1f);

            [Tooltip("Color used when the pickup is a purchased order.")]
            public Color deliveryTextColor = new Color(1f, 0.84f, 0.35f, 1f);

            [Header("Bubbles")]
            [Range(0, 32), Tooltip("Small bubbles emitted at the pickup position.")]
            public int bubbleCount = 5;
        }

        [Serializable]
        public sealed class QuestItemSettings
        {
            [Tooltip("Enable the Eye camera flash and the Fungus pickup text.")]
            public bool enabled = true;

            [Header("Fungus text")]
            public FloatingTextTuning text = new();

            [Tooltip("Color of the Fungus sample text.")]
            public Color fungusTextColor = new Color(0.6f, 1f, 0.65f, 1f);

            [Header("Eye photo flash")]
            [Range(0f, 1f), Tooltip("Peak opacity of the full-screen camera flash.")]
            public float flashAlpha = 0.85f;

            [Min(0.05f), Tooltip("Duration of the full-screen flash (seconds, unscaled).")]
            public float flashDuration = 0.22f;

            [Tooltip("Tint of the camera flash overlay.")]
            public Color flashColor = Color.white;
        }

        [Serializable]
        public sealed class OreProductionSettings
        {
            [Tooltip("Enable a small bubble puff when a machine in view produces ore.")]
            public bool enabled = true;

            [Min(0.1f), Tooltip("Minimum seconds between production puffs, so many machines stay subtle.")]
            public float minInterval = 2.5f;

            [Range(0, 32), Tooltip("Bubbles emitted per production puff.")]
            public int bubbleCount = 3;
        }

        [Serializable]
        public sealed class CarryBlockedSettings
        {
            [Tooltip("Enable the red 'Inventory full' text when a pickup is rejected.")]
            public bool enabled = true;

            [Header("Floating text")]
            public FloatingTextTuning text = new();

            [Tooltip("Color of the rejected-pickup text.")]
            public Color textColor = new Color(1f, 0.35f, 0.3f, 1f);

            [Header("Horizontal shake")]
            [Min(0f), Tooltip("Maximum horizontal text offset in world units.")]
            public float shakeAmplitude = 0.12f;

            [Range(0f, 20f), Tooltip("Horizontal shake frequency in cycles per second.")]
            public float shakeFrequency = 14f;
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
        [SerializeField] private WreckSalvageSettings wreckSalvage = new();
        [SerializeField] private ItemCollectSettings itemCollect = new();
        [SerializeField] private QuestItemSettings questItem = new();
        [SerializeField] private OreProductionSettings oreProduction = new();
        [SerializeField] private CarryBlockedSettings carryBlocked = new();

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
        private readonly Dictionary<Transform, (Vector3 scale, Vector3 position, Coroutine run)> squashing = new();
        private readonly Dictionary<Transform, (Vector3 scale, Coroutine run)> revealPunches = new();
        private readonly List<(GameObject go, Coroutine run)> floatingTexts = new();
        private readonly List<(GameObject go, Coroutine run)> wreckTexts = new();
        private readonly List<(GameObject go, Coroutine run)> itemTexts = new();
        private readonly List<(GameObject go, Coroutine run)> questTexts = new();
        private readonly List<(GameObject go, Coroutine run)> carryTexts = new();
        private readonly System.Random presentationRandom = new();
        private float photoFlashStart = -1f;
        private float nextOreBubbleTime;

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
            ocean.WreckSalvaged += OnWreckSalvaged;
            ocean.ItemCollected += OnItemCollected;
            ocean.QuestItemCollected += OnQuestItemCollected;
            ocean.OreProduced += OnOreProduced;
            ocean.CarryBlocked += OnCarryBlocked;
        }

        private void OnDisable()
        {
            if (ocean != null)
            {
                ocean.MachinePlaced -= OnMachinePlaced;
                ocean.MachineRetrieved -= OnMachineRetrieved;
                ocean.SpotRevealed -= OnSpotRevealed;
                ocean.WreckSalvaged -= OnWreckSalvaged;
                ocean.ItemCollected -= OnItemCollected;
                ocean.QuestItemCollected -= OnQuestItemCollected;
                ocean.OreProduced -= OnOreProduced;
                ocean.CarryBlocked -= OnCarryBlocked;
            }
            CleanupAllEffects();
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
            if (!placeMachine.enabled && squashing.Count > 0) CleanupPlaceMachine();
            if (!retrieve.enabled && floatingTexts.Count > 0) CleanupRetrieve();
            if (!spotReveal.enabled && revealPunches.Count > 0) CleanupSpotReveal();
            if (!wreckSalvage.enabled && wreckTexts.Count > 0) CleanupWreckSalvage();
            if (!itemCollect.enabled && itemTexts.Count > 0) CleanupItemCollect();
            if (!questItem.enabled && (questTexts.Count > 0 || photoFlashStart >= 0f)) CleanupQuestItem();
            if (!oreProduction.enabled) nextOreBubbleTime = 0f;
            if (!carryBlocked.enabled && carryTexts.Count > 0) CleanupCarryBlocked();
            if (!outOfHydrox.enabled) beatTime = 0f;
            if (!cameraShake.enabled) trauma = 0f;
            if (!cameraShake.enabled && !outOfHydrox.enabled) ResetCamera();
        }

        private void CleanupAllEffects()
        {
            CleanupPlaceMachine();
            CleanupRetrieve();
            CleanupSpotReveal();
            CleanupWreckSalvage();
            CleanupItemCollect();
            CleanupQuestItem();
            CleanupCarryBlocked();
            nextOreBubbleTime = 0f;
            beatTime = 0f;
            trauma = 0f;
            ResetCamera();
        }

        private void CleanupPlaceMachine()
        {
            foreach (var pair in squashing)
            {
                if (pair.Value.run != null) StopCoroutine(pair.Value.run);
                if (pair.Key != null)
                {
                    pair.Key.localScale = pair.Value.scale;
                    pair.Key.localPosition = pair.Value.position;
                }
            }
            squashing.Clear();
        }

        private void CleanupRetrieve() => CleanupTexts(floatingTexts);

        private void CleanupSpotReveal()
        {
            foreach (var pair in revealPunches)
            {
                if (pair.Value.run != null) StopCoroutine(pair.Value.run);
                if (pair.Key != null) pair.Key.localScale = pair.Value.scale;
            }
            revealPunches.Clear();
        }

        private void CleanupWreckSalvage() => CleanupTexts(wreckTexts);

        private void CleanupItemCollect() => CleanupTexts(itemTexts);

        private void CleanupQuestItem()
        {
            CleanupTexts(questTexts);
            photoFlashStart = -1f;
        }

        private void CleanupCarryBlocked() => CleanupTexts(carryTexts);

        private void CleanupTexts(List<(GameObject go, Coroutine run)> group)
        {
            foreach (var entry in group)
            {
                if (entry.run != null) StopCoroutine(entry.run);
                if (entry.go != null) Destroy(entry.go);
            }
            group.Clear();
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
            // Remember the true rest pose so a re-trigger mid-animation never leaves the machine distorted.
            var rest = (target.localScale, target.localPosition);
            if (squashing.TryGetValue(target, out var running))
            {
                rest = (running.scale, running.position);
                if (running.run != null) StopCoroutine(running.run);
            }
            // Register first: a zero-duration fall/squash can finish synchronously and remove itself.
            squashing[target] = (rest.Item1, rest.Item2, null);
            var run = StartCoroutine(DropAndSquash(target, rest.Item1, rest.Item2, spotPosition));
            if (squashing.ContainsKey(target)) squashing[target] = (rest.Item1, rest.Item2, run);
        }

        /// <summary>
        /// Heavy drop -> landing impact -> monotonic settle: falls with ease-in acceleration, lands with
        /// sediment, bubbles, and trauma, then settles monotonically from a single squash back to rest.
        /// </summary>
        private IEnumerator DropAndSquash(Transform target, Vector3 restScale, Vector3 restPosition, Vector3 spotPosition)
        {
            float fallTime = placeMachine.fallTime;
            float fallHeight = placeMachine.fallHeight;
            float fallStretch = placeMachine.fallStretch;
            if (fallTime > 0f && fallHeight > 0f)
            {
                for (float t = 0f; t < fallTime && target != null; t += Time.deltaTime)
                {
                    float k = Mathf.Clamp01(t / fallTime);
                    float height = fallHeight * (1f - k * k); // ease-in: accelerates like it's heavy
                    float stretch = fallStretch * k;
                    target.localPosition = restPosition + Vector3.up * height;
                    target.localScale = new Vector3(restScale.x * (1f - stretch), restScale.y * (1f + stretch), restScale.z);
                    yield return null;
                }
            }
            if (target == null) { squashing.Remove(target); yield break; }

            target.localPosition = restPosition;
            LandingImpact(spotPosition);

            float squashAmount = placeMachine.squashAmount;
            float squashDuration = placeMachine.squashDuration;
            if (squashAmount > 0f && squashDuration > 0f)
            {
                for (float t = 0f; t < squashDuration && target != null; t += Time.deltaTime)
                {
                    float k = Mathf.Clamp01(t / squashDuration);
                    // Monotonic ease-out recovery from impact squash back to rest (never overshoots or wobbles).
                    float easeOut = 1f - Mathf.Pow(1f - k, 3f);
                    float squash = squashAmount * (1f - easeOut);
                    target.localScale = new Vector3(restScale.x * (1f + squash), restScale.y * (1f - squash), restScale.z);
                    yield return null;
                }
            }
            if (target != null) target.localScale = restScale;
            squashing.Remove(target);
        }

        private void LandingImpact(Vector3 position)
        {
            SedimentRing(position);
            BubbleBurst(position, placeMachine.bubbleCount);
            AddTrauma(placeMachine.trauma);
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
            if (!effectsEnabled || !retrieve.enabled || spot == null) return;
            string text = amount > 0 ? $"+{amount} Te" : "Empty";
            Color color = amount > 0 ? retrieve.positiveColor : retrieve.emptyColor;
            StartFloatingText(spot.transform.position, text, color, RetrieveStyle(), floatingTexts);
            if (amount <= 0) return;

            int minBubbles = Mathf.Min(retrieve.minBubbleCount, retrieve.maxBubbleCount);
            int maxBubbles = Mathf.Max(retrieve.minBubbleCount, retrieve.maxBubbleCount);
            int count = Mathf.Clamp(minBubbles + amount * retrieve.bubblesPerTellurium, minBubbles, maxBubbles);
            BubbleBurst(spot.transform.position, count);
            AddTrauma(retrieve.trauma);
        }

        private FloatingTextStyle RetrieveStyle() => new(
            retrieve.textDuration, retrieve.textStartY, retrieve.textRiseDistance,
            retrieve.textPopScale, retrieve.textPopFraction, retrieve.textFontSize, retrieve.textLayout);

        private static FloatingTextStyle Style(FloatingTextTuning tuning, float shakeAmplitude = 0f, float shakeFrequency = 0f) => new(
            tuning.duration, tuning.startY, tuning.riseDistance,
            tuning.popScale, tuning.popFraction, tuning.fontSize, tuning.layout, shakeAmplitude, shakeFrequency);

        private void StartFloatingText(Vector3 origin, string text, Color color, FloatingTextStyle style,
            List<(GameObject go, Coroutine run)> group)
        {
            var go = new GameObject("Juice Floating Text", typeof(TextMeshPro));
            // Register first: a zero-duration text can finish synchronously and remove itself.
            group.Add((go, null));
            var run = StartCoroutine(FloatingText(go, origin, text, color, style, group));
            for (int i = group.Count - 1; i >= 0; i--)
                if (group[i].go == go) { group[i] = (go, run); break; }
        }

        /// <summary>Pops in larger than normal, then rises with ease-out and fades away.</summary>
        private IEnumerator FloatingText(GameObject go, Vector3 origin, string text, Color color, FloatingTextStyle style,
            List<(GameObject go, Coroutine run)> group)
        {
            var label = go.GetComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = style.FontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = style.BoxSize;
            label.GetComponent<MeshRenderer>().sortingOrder = style.SortingOrder;

            float duration = Mathf.Max(0.05f, style.Duration);
            float popFraction = Mathf.Clamp(style.PopFraction, 0.01f, 0.99f);
            for (float t = 0f; t < duration && go != null; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                float rise = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
                float pop = k < popFraction ? Mathf.Lerp(style.PopScale, 1f, k / popFraction) : 1f;
                // Only the text shakes, and only horizontally, so the camera stays steady.
                float shake = style.ShakeAmplitude * (1f - k) * Mathf.Sin(t * style.ShakeFrequency * Mathf.PI * 2f);
                go.transform.position = origin + new Vector3(shake, style.StartY + style.RiseDistance * rise, style.ZOffset);
                go.transform.localScale = Vector3.one * pop;
                label.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                yield return null;
            }
            for (int i = group.Count - 1; i >= 0; i--)
                if (group[i].go == go) group.RemoveAt(i);
            if (go != null) Destroy(go);
        }

        private readonly struct FloatingTextStyle
        {
            public readonly float Duration, StartY, RiseDistance, PopScale, PopFraction, FontSize, ShakeAmplitude, ShakeFrequency;
            public readonly float ZOffset;
            public readonly int SortingOrder;
            public readonly Vector2 BoxSize;

            public FloatingTextStyle(float duration, float startY, float riseDistance, float popScale, float popFraction,
                float fontSize, FloatingTextLayout layout, float shakeAmplitude = 0f, float shakeFrequency = 0f)
            {
                Duration = duration;
                StartY = startY;
                RiseDistance = riseDistance;
                PopScale = popScale;
                PopFraction = popFraction;
                FontSize = fontSize;
                ShakeAmplitude = shakeAmplitude;
                ShakeFrequency = shakeFrequency;
                ZOffset = layout.zOffset;
                SortingOrder = layout.sortingOrder;
                BoxSize = layout.boxSize;
            }
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

        // ---------------------------------------------------------------- 4. Prospect reveal

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
                // The machine has its own drop/squash effect; leave it alone to avoid fighting over scale.
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

        // ---------------------------------------------------------------- 5. Wreck salvage

        private void OnWreckSalvaged(OceanTarget spot, int ore)
        {
            if (!effectsEnabled || !wreckSalvage.enabled || spot == null) return;
            StartFloatingText(spot.transform.position, $"+{ore} Te", wreckSalvage.textColor,
                Style(wreckSalvage.text), wreckTexts);
            SedimentPuff(spot.transform.position, wreckSalvage.sedimentCount, wreckSalvage.sedimentRadius,
                wreckSalvage.sedimentFlattenY, wreckSalvage.sedimentSpeedMin, wreckSalvage.sedimentSpeedMax,
                wreckSalvage.sedimentSizeMin, wreckSalvage.sedimentSizeMax,
                wreckSalvage.sedimentLifetimeMin, wreckSalvage.sedimentLifetimeMax, wreckSalvage.sedimentColor);
            AddTrauma(wreckSalvage.trauma);
        }

        // ---------------------------------------------------------------- 6. Item pickup

        private void OnItemCollected(Vector3 position, int count, string itemName, bool isDelivery)
        {
            if (!effectsEnabled || !itemCollect.enabled) return;
            string text = $"+{count} {itemName}";
            Color color = isDelivery ? itemCollect.deliveryTextColor : itemCollect.textColor;
            StartFloatingText(position, text, color, Style(itemCollect.text), itemTexts);
            BubbleBurst(position, itemCollect.bubbleCount);
        }

        // ---------------------------------------------------------------- 7. Quest items

        private void OnQuestItemCollected(OceanTarget spot, string itemName, bool isEyePhoto)
        {
            if (!effectsEnabled || !questItem.enabled) return;
            if (isEyePhoto)
            {
                photoFlashStart = Time.unscaledTime;
                return;
            }
            if (spot == null) return;
            StartFloatingText(spot.transform.position, "+1 " + itemName, questItem.fungusTextColor,
                Style(questItem.text), questTexts);
        }

        // ---------------------------------------------------------------- 8. Ore production

        private void OnOreProduced(OceanTarget spot)
        {
            if (!effectsEnabled || !oreProduction.enabled || spot == null || view == null) return;
            if (Time.time < nextOreBubbleTime) return;
            Vector3 viewport = view.WorldToViewportPoint(spot.transform.position);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return;
            nextOreBubbleTime = Time.time + Mathf.Max(0.1f, oreProduction.minInterval);
            BubbleBurst(spot.transform.position, oreProduction.bubbleCount);
        }

        // ---------------------------------------------------------------- 9. Inventory full

        private void OnCarryBlocked(Vector3 position)
        {
            if (!effectsEnabled || !carryBlocked.enabled) return;
            var style = Style(carryBlocked.text, carryBlocked.shakeAmplitude, carryBlocked.shakeFrequency);
            StartFloatingText(position, "Inventory full", carryBlocked.textColor, style, carryTexts);
        }

        // ---------------------------------------------------------------- 10. Out-of-hydrox heartbeat

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
            bool active = effectsEnabled && Time.timeScale > 0f && ocean != null && !ocean.InsideSubmarine && !ocean.LoadoutOpen;
            if (!active) { trauma = 0f; beatTime = 0f; ResetCamera(); return; }

            float dt = Time.deltaTime;
            float zoom = HeartbeatZoom(dt);
            trauma = cameraShake.enabled ? Mathf.Max(0f, trauma - cameraShake.traumaDecay * dt) : 0f;

            // Idle: leave the camera alone so other code (and the editor play checks) own it.
            if (trauma <= 0f && zoom <= 0f) { ResetCamera(); return; }
            cameraDirty = true;
            float shake = cameraShake.enabled ? trauma * trauma : 0f;
            float time = Time.time * cameraShake.shakeFrequency;
            var offset = new Vector3(Noise(time, 0f), Noise(time, 10f), 0f) * (cameraShake.maxShakeOffset * shake);
            float angle = Noise(time, 20f) * cameraShake.maxShakeAngle * shake;

            // CenteredCamera rewrites the position every frame; otherwise remove last frame's offset.
            if (!followsTarget) view.transform.position -= lastOffset;
            view.transform.position += offset;
            lastOffset = offset;
            view.transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
            view.orthographicSize = baseSize * (1f - zoom);
        }

        /// <summary>Smooth random value in [-1, 1]; Perlin noise keeps the shake from looking jittery.</summary>
        private static float Noise(float time, float seed) => Mathf.PerlinNoise(time, seed) * 2f - 1f;

        /// <summary>Restores the camera once, when an effect ends; does nothing while idle.</summary>
        private void ResetCamera()
        {
            if (view == null || !cameraDirty) return;
            cameraDirty = false;
            if (!followsTarget) view.transform.position -= lastOffset;
            lastOffset = Vector3.zero;
            view.transform.rotation = baseRotation;
            view.orthographicSize = baseSize;
        }

        private void OnGUI()
        {
            DrawPhotoFlash();
            if (Time.unscaledTime > toggleShownUntil) return;
            GUI.Label(new Rect(12, 12, 240, 24), effectsEnabled ? "Game Feel: ON  (F8)" : "Game Feel: OFF  (F8)");
        }

        /// <summary>A simple full-screen white flash over the game view; no HUD or scene objects are touched.</summary>
        private void DrawPhotoFlash()
        {
            if (!effectsEnabled || !questItem.enabled || photoFlashStart < 0f) return;
            float duration = Mathf.Max(0.01f, questItem.flashDuration);
            float elapsed = Time.unscaledTime - photoFlashStart;
            if (elapsed < 0f || elapsed >= duration)
            {
                photoFlashStart = -1f;
                return;
            }
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            float fade = 1f - elapsed / duration;
            var tint = questItem.flashColor;
            var previous = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, tint.a * questItem.flashAlpha * fade * fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
