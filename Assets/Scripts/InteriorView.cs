using DivingPrototype;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public sealed class InteriorView : MonoBehaviour
{
    [SerializeField] private CanvasScaler[] widthScaledCanvases;
    private Camera roomCamera;

    /// <summary>Initializes the fixed room framing before the first input or canvas update.</summary>
    private void Awake()
    {
        roomCamera = GetComponent<Camera>();
        foreach (var scaler in widthScaledCanvases)
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        ApplyViewport();
    }

    /// <summary>Keeps the fitted viewport and canvas scale aligned after a display resize.</summary>
    private void Update() => ApplyViewport();

    /// <summary>Fits the room camera and keeps its authored width-based canvas scale inside that view.</summary>
    private void ApplyViewport()
    {
        roomCamera.rect = FixedOceanView.Viewport(Screen.width, Screen.height);
        roomCamera.aspect = 16f / 9f;
        foreach (var scaler in widthScaledCanvases)
            scaler.scaleFactor = Mathf.Max(1f, roomCamera.pixelWidth) / scaler.referenceResolution.x;
    }
}
