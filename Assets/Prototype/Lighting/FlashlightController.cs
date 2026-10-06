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
        [SerializeField, Range(2f, 180f)] private float coneAngle = 60f;
        [SerializeField, Min(.001f)] private float halfStrengthDistance = 10f;
        [SerializeField, Min(0f), Tooltip("Fully bright circle radius in world units. Used directly, independent of player sprite size. Zero disables the circle.")]
        private float playerLightRadius = .1f;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private Transform map;
        [SerializeField] private BoxCollider2D[] blockers = new BoxCollider2D[0];

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

        private void OnEnable()
        {
            if (spriteMaterial == null || map == null || aimCamera == null || !ValidateBlockers()) return;
            swimmer = GetComponent<SwimmerController>();
            instance = new Material(spriteMaterial) { name = "Ocean Flashlight (Instance)", hideFlags = HideFlags.HideAndDontSave };
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
                properties.SetFloat(SelfId, self);
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
                    if (plane.Raycast(ray, out float distance)) candidate = (Vector2)(ray.GetPoint(distance) - transform.position);
                }
                direction = FlashlightMath.RetainDirection(candidate, direction);
            }
            instance.SetVector(OriginId, transform.position);
            instance.SetVector(DirectionId, new Vector4(direction.x, direction.y, 0f, 0f));
            instance.SetFloat(AmbientId, Mathf.Clamp01(ambientBrightness));
            instance.SetFloat(AngleId, Mathf.Clamp(coneAngle, 2f, 180f));
            instance.SetFloat(DistanceId, Mathf.Max(.001f, halfStrengthDistance));
            instance.SetFloat(RadiusId, Mathf.Max(0f, playerLightRadius));
            int count = Mathf.Min(blockers.Length, FlashlightMath.MaxBlockers);
            for (int i = 0; i < count; i++) FlashlightMath.PackBox(blockers[i], out rowsX[i], out rowsY[i]);
            instance.SetInt(CountId, count);
            instance.SetVectorArray(BoxesXId, rowsX);
            instance.SetVectorArray(BoxesYId, rowsY);
            if (aimCamera != null)
            {
                Color background = originalBackground;
                background.r *= ambientBrightness;
                background.g *= ambientBrightness;
                background.b *= ambientBrightness;
                aimCamera.backgroundColor = background;
            }
        }

        private void OnValidate()
        {
            ambientBrightness = Mathf.Clamp01(ambientBrightness);
            coneAngle = Mathf.Clamp(coneAngle, 2f, 180f);
            halfStrengthDistance = Mathf.Max(.001f, halfStrengthDistance);
            playerLightRadius = Mathf.Max(0f, playerLightRadius);
            ValidateBlockers();
        }

        private bool ValidateBlockers()
        {
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
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
            instance = null;
        }
    }
}
