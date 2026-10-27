using UnityEngine;

namespace DivingPrototype
{
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(FlashlightController), typeof(Rigidbody2D))]
    public sealed class OceanVfx : MonoBehaviour
    {
        [SerializeField] private Shader particleShader;
        [SerializeField] private Shader waterOverlayShader;
        [SerializeField, Range(0f, 8f)] private float trailRate = 7f;
        [SerializeField, Min(.1f)] private float breathingInterval = 2.5f;
        [SerializeField, Range(0, 160)] private int suspendedCount = 120;
        public const int BubbleLimit = 96;
        public const int MoteLimit = 160;
        public ParticleSystem Bubbles => bubbles;
        public ParticleSystem Suspended => suspended;
        public MeshRenderer WaterOverlay => waterOverlay;

        private FlashlightController flashlight;
        private Rigidbody2D body;
        private OceanInteractions ocean;
        private ParticleSystem bubbles, suspended;
        private Material bubbleMaterial, moteMaterial, waterMaterial;
        private MeshRenderer waterOverlay;
        private Mesh waterMesh;
        private readonly ParticleSystem.Particle[] moteBuffer = new ParticleSystem.Particle[MoteLimit];
        private float trailAccumulator, breathTimer;
        private bool oceanActive;
        private float diverHalfLength = .5f;

        private void OnEnable()
        {
            flashlight = GetComponent<FlashlightController>();
            body = GetComponent<Rigidbody2D>();
            var artwork = GetComponent<SpriteRenderer>();
            if (artwork != null && artwork.sprite != null)
                diverHalfLength = Mathf.Max(.2f, artwork.sprite.bounds.extents.y * Mathf.Abs(transform.lossyScale.y));
            ocean = GetComponent<OceanInteractions>();
            if (particleShader == null) { enabled = false; return; }
            bubbleMaterial = NewMaterial(true);
            moteMaterial = NewMaterial(false);
            bubbles = NewSystem("Ocean Bubbles", BubbleLimit, bubbleMaterial);
            suspended = NewSystem("Suspended Water Particles", MoteLimit, moteMaterial);
            CreateWaterOverlay();
            oceanActive = ocean == null || !ocean.InsideSubmarine;
            trailAccumulator = 0f;
            breathTimer = 1f;
            if (ocean != null) ocean.ExcursionEnded += EndOcean;
            if (oceanActive) SeedSuspended();
            else EndOcean();
        }

        private void CreateWaterOverlay()
        {
            if (waterOverlayShader == null) return;
            waterMaterial = new Material(waterOverlayShader) { name = "Ocean Water Veil (Instance)", hideFlags = HideFlags.HideAndDontSave };
            waterMaterial.SetFloat("_SelfBlocker", -1f);
            flashlight.RegisterLightingMaterial(waterMaterial);
            var go = new GameObject("Water Ripple Overlay") { hideFlags = HideFlags.DontSave };
            waterMesh = new Mesh { name = "Ocean Water Quad", hideFlags = HideFlags.HideAndDontSave };
            waterMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            waterMesh.triangles = new[] { 0,1,2,0,2,3 };
            waterMesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            waterOverlay = go.AddComponent<MeshRenderer>();
            waterOverlay.sharedMaterial = waterMaterial;
            waterOverlay.sortingOrder = 25;
            waterOverlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waterOverlay.receiveShadows = false;
        }

        private void LateUpdate()
        {
            if (waterOverlay == null || flashlight.AimCamera == null) return;
            waterOverlay.enabled = oceanActive && flashlight.isActiveAndEnabled;
            var camera = flashlight.AimCamera;
            waterOverlay.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, transform.position.z);
            waterOverlay.transform.localScale = new Vector3(camera.orthographicSize * camera.aspect * 2.01f, camera.orthographicSize * 2.01f, 1f);
        }

        private Material NewMaterial(bool bubble)
        {
            var material = new Material(particleShader) { name = bubble ? "Ocean Bubbles (Instance)" : "Ocean Motes (Instance)", hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_BubbleStyle", bubble ? 1f : 0f);
            material.SetFloat("_SelfBlocker", -1f);
            flashlight.RegisterLightingMaterial(material);
            return material;
        }

        private ParticleSystem NewSystem(string label, int limit, Material material)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = limit;
            main.startSpeed = 0f;
            main.startLifetime = 4f;
            main.startSize = .08f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .65f), new Keyframe(.2f, 1f), new Keyframe(1f, 1.15f)));
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.65f, .12f), new GradientAlphaKey(.5f, .65f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = .06f;
            noise.frequency = .7f;
            noise.scrollSpeed = .12f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = 20;
            system.Play();
            return system;
        }

        private Vector2 ViewHalfSize()
        {
            var camera = flashlight.AimCamera;
            return camera != null ? new Vector2(camera.orthographicSize * camera.aspect + .5f, camera.orthographicSize + .5f) : new Vector2(10f, 6f);
        }

        private void SeedSuspended()
        {
            Vector2 extent = ViewHalfSize();
            for (int i = 0; i < Mathf.Clamp(suspendedCount, 0, MoteLimit); i++)
            {
                var particle = new ParticleSystem.EmitParams
                {
                    position = transform.position + new Vector3(Random.Range(-extent.x, extent.x), Random.Range(-extent.y, extent.y), 0f),
                    velocity = new Vector3(Random.Range(-.015f, .015f), Random.Range(.015f, .045f), 0f),
                    startSize = Random.Range(.025f, .07f),
                    startLifetime = 3600f,
                    startColor = new Color(.52f, .75f, .8f, Random.Range(.18f, .4f))
                };
                suspended.Emit(particle, 1);
            }
            // Motes persist and wrap around the camera; bubbles alone use the lifetime fade.
            var fade = suspended.colorOverLifetime; fade.enabled = false;
            var size = suspended.sizeOverLifetime; size.enabled = false;
        }

        private void Update()
        {
            if (!oceanActive || bubbles == null) return;
            if (!flashlight.isActiveAndEnabled || (ocean != null && ocean.InsideSubmarine)) { EndOcean(); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector2 velocity = body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed > .1f)
            {
                trailAccumulator += dt * trailRate * Mathf.Lerp(.72f, 1f, Mathf.Clamp01(speed / 3f));
                while (trailAccumulator >= 1f)
                {
                    EmitBubble((Vector2)transform.position - velocity.normalized * (diverHalfLength * .9f), velocity * -.06f);
                    trailAccumulator -= 1f;
                }
            }
            else trailAccumulator = 0f;
            breathTimer -= dt;
            if (breathTimer <= 0f)
            {
                Vector2 head = (Vector2)transform.position + flashlight.AimDirection * (diverHalfLength * .75f);
                EmitBubble(head, Vector2.zero);
                EmitBubble(head, Vector2.zero);
                breathTimer = breathingInterval + Random.Range(-.25f, .25f);
            }
            WrapSuspended();
        }

        private void EmitBubble(Vector2 position, Vector2 drift)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = position + Random.insideUnitCircle * .055f,
                velocity = drift + new Vector2(Random.Range(-.06f, .06f), Random.Range(.16f, .32f)),
                startSize = Random.Range(.06f, .13f),
                startLifetime = Random.Range(2f, 4f),
                startColor = new Color(.72f, .9f, .97f, .7f)
            };
            bubbles.Emit(particle, 1);
        }

        private void WrapSuspended()
        {
            Vector2 extent = ViewHalfSize();
            Vector3 center = flashlight.AimCamera != null ? flashlight.AimCamera.transform.position : transform.position;
            int count = suspended.GetParticles(moteBuffer);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = moteBuffer[i].position;
                p.x = center.x + Mathf.Repeat(p.x - center.x + extent.x, extent.x * 2f) - extent.x;
                p.y = center.y + Mathf.Repeat(p.y - center.y + extent.y, extent.y * 2f) - extent.y;
                p.z = transform.position.z;
                moteBuffer[i].position = p;
                // Keep long ocean sessions from silently losing their suspended particles.
                moteBuffer[i].remainingLifetime = 3600f;
            }
            suspended.SetParticles(moteBuffer, count);
        }

        private void EndOcean()
        {
            oceanActive = false;
            if (waterOverlay != null) waterOverlay.enabled = false;
            if (bubbles != null) bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (suspended != null) suspended.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            if (ocean != null) ocean.ExcursionEnded -= EndOcean;
            EndOcean();
            ReleaseMaterial(bubbleMaterial);
            ReleaseMaterial(moteMaterial);
            ReleaseMaterial(waterMaterial);
            if (waterOverlay != null) Destroy(waterOverlay.gameObject);
            if (waterMesh != null) Destroy(waterMesh);
            if (bubbles != null) Destroy(bubbles.gameObject);
            if (suspended != null) Destroy(suspended.gameObject);
        }

        private void ReleaseMaterial(Material material)
        {
            if (material == null) return;
            if (flashlight != null) flashlight.UnregisterLightingMaterial(material);
            Destroy(material);
        }
    }
}
