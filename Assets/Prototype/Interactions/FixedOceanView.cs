using UnityEngine;
using UnityEngine.UI;

namespace DivingPrototype
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class FixedOceanView : MonoBehaviour
    {
        public OceanInteractions interactions;
        private Canvas canvas;

        public static Rect Viewport(float width, float height)
        {
            float aspect = width / Mathf.Max(1f, height);
            const float target = 16f / 9f;
            if (aspect > target)
            {
                float w = target / aspect;
                return new Rect((1f - w) * .5f, 0f, w, 1f);
            }
            float h = aspect / target;
            return new Rect(0f, (1f - h) * .5f, 1f, h);
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            // Expand uses the fitted viewport's scale, rather than scaling into the black bars.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Apply();
        }

        public void Apply()
        {
            var camera = interactions.InsideSubmarine ? Camera.main : interactions.oceanCamera;
            if (camera == null) return;
            camera.rect = Viewport(Screen.width, Screen.height);
            camera.aspect = 16f / 9f;
            canvas.worldCamera = camera;
        }

        private void Update() => Apply();
    }
}
