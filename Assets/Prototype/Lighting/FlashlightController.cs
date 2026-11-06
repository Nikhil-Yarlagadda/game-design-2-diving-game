using UnityEngine;
using UnityEngine.InputSystem;

namespace DivingPrototype
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(SwimmerController))]
    public sealed class FlashlightController : MonoBehaviour
    {
        public enum AimMode { Mouse, MovementKeys }

        [SerializeField] private AimMode aimMode = AimMode.Mouse;
        [SerializeField] private Camera aimCamera;
        [SerializeField, Range(0f, 1f)] private float ambientBrightness;
        [Header("Bright cone (total angles in degrees)")]
        [SerializeField, Range(0f, 1f)] private float beamIntensity = 1f;
        [SerializeField, Range(2f, 180f)] private float coneAngle = 125f;
        [SerializeField, Min(.001f)] private float halfStrengthDistance = 10f;
        [SerializeField, Range(2f, 180f)] private float coreAngle = 44f;
        [Header("Dim forward spill")]
        [SerializeField, Range(2f, 180f)] private float spillInnerAngle = 90f;
        [SerializeField, Range(2f, 180f)] private float spillAngle = 178f;
        [SerializeField, Range(0f, 1f)] private float spillIntensity = .18f;
        [SerializeField, Min(0f)] private float beamFadeStart = 24f;
        [SerializeField, Min(.01f)] private float beamRange = 36f;
        [SerializeField, Min(0f)] private float spillFadeStart = 22f;
        [SerializeField, Min(.01f)] private float spillRange = 34f;
        [Header("Player halo and water motion")]
        [SerializeField, Range(0f, 1f), Tooltip("Brightness of the player-centered glow. Zero disables the halo; one uses full strength.")]
        private float playerHaloStrength = 1f;
        [SerializeField, Min(.001f)] private float haloFeather = 2.5f;
        [SerializeField, Range(0f, 1f)] private float waterStrength = .55f;
        [SerializeField, Range(0f, 4f)] private float refractionTexels = 2f;
        [SerializeField, Min(0f)] private float waterSpeed = .6f;
        [SerializeField, Min(0f), Tooltip("Inner player circle radius in world units, with a soft outer fade. Brightness is controlled by Player Halo Strength. Used directly, independent of player sprite size. Zero disables the circle.")]
        private float playerLightRadius = .75f;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private Transform map;
        [SerializeField] private BoxCollider2D[] blockers = new BoxCollider2D[0];
        [SerializeField] private PolygonCollider2D[] polygonBlockers = new PolygonCollider2D[0];
        private const int MaxEdges = 256;
        private readonly Vector4[] edges = new Vector4[MaxEdges];
        private readonly float[] edgeOwners = new float[MaxEdges];
        private readonly System.Collections.Generic.List<Vector2> path = new System.Collections.Generic.List<Vector2>(MaxEdges);

        private static readonly int OriginId = Shader.PropertyToID("_FlashlightOrigin");
        private static readonly int DirectionId = Shader.PropertyToID("_FlashlightDirection");
        private static readonly int AmbientId = Shader.PropertyToID("_AmbientBrightness");
        private static readonly int AngleId = Shader.PropertyToID("_ConeAngle");
        private static readonly int DistanceId = Shader.PropertyToID("_HalfStrengthDistance");
        private static readonly int RadiusId = Shader.PropertyToID("_PlayerLightRadius");
        private static readonly int CountId = Shader.PropertyToID("_BlockerCount");
        private static readonly int BoxesXId = Shader.PropertyToID("_BlockerRowsX");
        private static readonly int BoxesYId = Shader.PropertyToID("_BlockerRowsY");
        private static readonly int SelfId = Shader.PropertyToID("_SelfBlocker");

        private readonly Vector4[] rowsX = new Vector4[FlashlightMath.MaxBlockers];
        private readonly Vector4[] rowsY = new Vector4[FlashlightMath.MaxBlockers];
        private SwimmerController swimmer;
        private Material instance;
        private SpriteRenderer[] sprites;
        private Material[] originalMaterials;
        private MaterialPropertyBlock[] originalProperties;
        private Color originalBackground;
        private Vector2 direction = Vector2.right;
        public Vector2 AimDirection => direction;
        public Camera AimCamera => aimCamera;
        private readonly System.Collections.Generic.List<Material> lightingMaterials = new System.Collections.Generic.List<Material>(4);

        public void RegisterLightingMaterial(Material material)
        {
            if (material != null && !lightingMaterials.Contains(material)) lightingMaterials.Add(material);
        }

        public void UnregisterLightingMaterial(Material material) => lightingMaterials.Remove(material);

        private void OnEnable()
        {
            if (spriteMaterial == null || map == null || aimCamera == null || !ValidateBlockers()) return;
            swimmer = GetComponent<SwimmerController>();
            instance = new Material(spriteMaterial) { name = "Ocean Flashlight (Instance)", hideFlags = HideFlags.HideAndDontSave };
            RegisterLightingMaterial(instance);
            var mapSprites = map.GetComponentsInChildren<SpriteRenderer>(true);
            var playerSprites = GetComponentsInChildren<SpriteRenderer>(true);
            sprites = new SpriteRenderer[mapSprites.Length + playerSprites.Length];
            mapSprites.CopyTo(sprites, 0);
            playerSprites.CopyTo(sprites, mapSprites.Length);
            originalMaterials = new Material[sprites.Length];
            originalProperties = new MaterialPropertyBlock[sprites.Length];
            var properties = new MaterialPropertyBlock();
            for (int i = 0; i < sprites.Length; i++)
            {
                originalMaterials[i] = sprites[i].sharedMaterial;
                originalProperties[i] = new MaterialPropertyBlock();
                sprites[i].GetPropertyBlock(originalProperties[i]);
                sprites[i].sharedMaterial = instance;
                sprites[i].GetPropertyBlock(properties);
                int self = -1;
                for (int b = 0; b < blockers.Length; b++)
                    if (blockers[b] != null && blockers[b].gameObject == sprites[i].gameObject) self = b;
                for (int b = 0; b < polygonBlockers.Length; b++)
                    if (polygonBlockers[b] != null && polygonBlockers[b].gameObject == sprites[i].gameObject) self = 32 + b;
                properties.SetFloat(SelfId, self);
                bool scenery = i < mapSprites.Length && !sprites[i].name.Contains("Highlight");
                properties.SetFloat("_SceneryEffects", scenery ? 1f : 0f);
                properties.SetFloat("_ObstacleEffects", scenery && self >= 0 ? 1f : 0f);
                var sprite = sprites[i].sprite;
                if (sprite != null)
                {
                    properties.SetVector("_SpriteUVRect", UnityEngine.Sprites.DataUtility.GetOuterUV(sprite));
                    var inverse = sprites[i].transform.worldToLocalMatrix;
                    float u = sprite.pixelsPerUnit / sprite.texture.width * (sprites[i].flipX ? -1f : 1f);
                    float v = sprite.pixelsPerUnit / sprite.texture.height * (sprites[i].flipY ? -1f : 1f);
                    properties.SetVector("_WorldToSpriteU", new Vector4(inverse.m00 * u, inverse.m01 * u, 0, 0));
                    properties.SetVector("_WorldToSpriteV", new Vector4(inverse.m10 * v, inverse.m11 * v, 0, 0));
                }
                sprites[i].SetPropertyBlock(properties);
            }
            originalBackground = aimCamera.backgroundColor;
            RefreshLighting();
        }

        private void LateUpdate() => RefreshLighting();

        public void RefreshLighting()
        {
            if (instance == null) return;
            if (Application.isPlaying)
            {
                Vector2 candidate = Vector2.zero;
                if (aimMode == AimMode.MovementKeys) candidate = swimmer.MovementInput;
                else if (Mouse.current != null && aimCamera != null)
                {
                    Vector2 screen = Mouse.current.position.ReadValue();
                    var ray = aimCamera.ScreenPointToRay(screen);
                    var plane = new Plane(Vector3.forward, transform.position);
                    if (aimCamera.pixelRect.Contains(screen) && plane.Raycast(ray, out float distance)) candidate = (Vector2)(ray.GetPoint(distance) - transform.position);
                }
                direction = FlashlightMath.RetainDirection(candidate, direction);
                // The diver artwork faces up in its source image; align the player sprite to the camera aim.
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            }
            int count = Mathf.Min(blockers.Length, FlashlightMath.MaxBlockers);
            for (int i = 0; i < count; i++) FlashlightMath.PackBox(blockers[i], out rowsX[i], out rowsY[i]);
            Vector2 viewMin = transform.position, viewMax = transform.position;
            var ground = new Plane(Vector3.forward, transform.position);
            bool boundedView = aimCamera != null;
            if (boundedView)
                for (int x = 0; x <= 1; x++)
                    for (int y = 0; y <= 1; y++)
                    {
                        var ray = aimCamera.ViewportPointToRay(new Vector3(x, y, 0));
                        if (!ground.Raycast(ray, out float depth)) { boundedView = false; continue; }
                        Vector2 corner = ray.GetPoint(depth);
                        viewMin = Vector2.Min(viewMin, corner);
                        viewMax = Vector2.Max(viewMax, corner);
                    }
            var rayBounds = Rect.MinMaxRect(viewMin.x - .01f, viewMin.y - .01f, viewMax.x + .01f, viewMax.y + .01f);
            int edgeCount = 0;
            for (int p = 0; p < polygonBlockers.Length; p++)
            {
                var polygon = polygonBlockers[p];
                if (polygon == null || !polygon.isActiveAndEnabled) continue;
                for (int part = 0; part < polygon.pathCount; part++)
                {
                    polygon.GetPath(part, path);
                    for (int e = 0; e < path.Count && edgeCount < MaxEdges; e++)
                    {
                        Vector2 a = polygon.transform.TransformPoint(path[e] + polygon.offset);
                        Vector2 b = polygon.transform.TransformPoint(path[(e + 1) % path.Count] + polygon.offset);
                        // Every ray to a visible fragment stays inside this rectangle, including its origin.
                        if (boundedView && !EdgeIntersectsView(a, b, rayBounds)) continue;
                        edges[edgeCount] = new Vector4(a.x, a.y, b.x, b.y);
                        edgeOwners[edgeCount++] = 32 + p;
                    }
                }
            }
            // Never place the terminal fade inside the camera rectangle, even after a zoom change.
            float viewReach = new Vector2(Mathf.Max(Mathf.Abs(viewMin.x - transform.position.x), Mathf.Abs(viewMax.x - transform.position.x)),
                Mathf.Max(Mathf.Abs(viewMin.y - transform.position.y), Mathf.Abs(viewMax.y - transform.position.y))).magnitude;
            foreach (var material in lightingMaterials) UploadLighting(material, count, edgeCount, viewReach);
            if (aimCamera != null)
            {
                Color background = originalBackground;
                background.r *= ambientBrightness;
                background.g *= ambientBrightness;
                background.b *= ambientBrightness;
                aimCamera.backgroundColor = background;
            }
        }

        private void UploadLighting(Material material, int count, int edgeCount, float viewReach)
        {
            material.SetVector(OriginId, transform.position);
            material.SetVector(DirectionId, new Vector4(direction.x, direction.y, 0f, 0f));
            material.SetFloat(AmbientId, Mathf.Clamp01(ambientBrightness));
            material.SetFloat(AngleId, coneAngle);
            material.SetFloat(DistanceId, halfStrengthDistance);
            material.SetFloat(RadiusId, playerLightRadius);
            material.SetFloat("_CoreAngle", coreAngle);
            material.SetFloat("_BeamIntensity", beamIntensity);
            material.SetFloat("_SpillInnerAngle", spillInnerAngle);
            material.SetFloat("_SpillAngle", spillAngle);
            material.SetFloat("_SpillIntensity", spillIntensity);
            material.SetFloat("_BeamFadeStart", Mathf.Max(beamFadeStart, viewReach * 1.1f));
            material.SetFloat("_BeamRange", Mathf.Max(beamRange, viewReach * 1.8f));
            material.SetFloat("_SpillFadeStart", Mathf.Max(spillFadeStart, viewReach * 1.05f));
            material.SetFloat("_SpillRange", Mathf.Max(spillRange, viewReach * 1.7f));
            material.SetFloat("_HaloFeather", haloFeather);
            material.SetFloat("_PlayerHaloStrength", Mathf.Clamp01(playerHaloStrength));
            material.SetFloat("_WaterStrength", waterStrength);
            material.SetFloat("_RefractionTexels", refractionTexels);
            material.SetFloat("_WaterSpeed", waterSpeed);
            material.SetFloat("_OceanTime", Application.isPlaying ? Time.time : 0f);
            material.SetInt(CountId, count);
            material.SetVectorArray(BoxesXId, rowsX);
            material.SetVectorArray(BoxesYId, rowsY);
            material.SetInt("_EdgeCount", edgeCount);
            material.SetVectorArray("_PolygonEdges", edges);
            material.SetFloatArray("_EdgeOwners", edgeOwners);
        }

        public static bool EdgeIntersectsView(Vector2 a, Vector2 b, Rect view)
            => Mathf.Max(a.x, b.x) >= view.xMin && Mathf.Min(a.x, b.x) <= view.xMax
                && Mathf.Max(a.y, b.y) >= view.yMin && Mathf.Min(a.y, b.y) <= view.yMax;

        private void OnValidate()
        {
            ambientBrightness = Mathf.Clamp01(ambientBrightness);
            coneAngle = Mathf.Clamp(coneAngle, 2f, 180f);
            halfStrengthDistance = Mathf.Max(.001f, halfStrengthDistance);
            playerLightRadius = Mathf.Max(0f, playerLightRadius);
            coreAngle = Mathf.Clamp(coreAngle, 1f, coneAngle - .01f);
            spillInnerAngle = Mathf.Clamp(spillInnerAngle, 2f, 179.99f);
            spillAngle = Mathf.Clamp(spillAngle, spillInnerAngle + .01f, 180f);
            beamFadeStart = Mathf.Max(0f, beamFadeStart);
            beamRange = Mathf.Max(beamFadeStart + .01f, beamRange);
            spillFadeStart = Mathf.Max(0f, spillFadeStart);
            spillRange = Mathf.Max(spillFadeStart + .01f, spillRange);
            haloFeather = Mathf.Max(.001f, haloFeather);
            playerHaloStrength = Mathf.Clamp01(playerHaloStrength);
            ValidateBlockers();
        }

        private bool ValidateBlockers()
        {
            int edgeCount = 0;
            foreach (var polygon in polygonBlockers)
                if (polygon != null) edgeCount += polygon.GetTotalPointCount();
            if (edgeCount > MaxEdges)
            {
                Debug.LogError("Flashlight supports at most 256 polygon edges.", this);
                return false;
            }
            if (blockers.Length > FlashlightMath.MaxBlockers)
            {
                Debug.LogError("Flashlight supports at most 32 blockers. Remove excess entries before enabling it.", this);
                return false;
            }
            for (int i = 0; i < blockers.Length; i++)
                if (blockers[i] != null && blockers[i].transform.IsChildOf(transform))
                {
                    Debug.LogError("The player cannot be a flashlight blocker.", this);
                    return false;
                }
            return true;
        }

        private void OnDisable()
        {
            if (instance == null) return;
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null)
                {
                    sprites[i].sharedMaterial = originalMaterials[i];
                    sprites[i].SetPropertyBlock(originalProperties[i]);
                }
            if (aimCamera != null) aimCamera.backgroundColor = originalBackground;
            UnregisterLightingMaterial(instance);
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
            instance = null;
        }
    }
}
