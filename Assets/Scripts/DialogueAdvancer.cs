using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yarn.Unity;

public class DialogueAdvancer : MonoBehaviour
{
    public DialogueRunner dialogueRunner;
    public LineAdvancer lineAdvancer;
    [SerializeField] private Canvas roomDialogueCanvas;

    private GraphicRaycaster roomDialogueRaycaster;
    private readonly List<RaycastResult> pointerHits = new();
    private int dialogueStartFrame = -1;

    public bool BlocksRoomInput => roomDialogueCanvas != null && roomDialogueCanvas.isActiveAndEnabled;

    /// <summary>Sets up a modal canvas over the room while Yarn is presenting dialogue.</summary>
    private void Awake()
    {
        dialogueRunner.onDialogueStart.AddListener(OnDialogueStarted);
        if (roomDialogueCanvas == null) return;

        var blocker = new GameObject("Dialogue Input Blocker", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)blocker.transform;
        rect.SetParent(roomDialogueCanvas.transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = Color.clear;

        roomDialogueRaycaster = roomDialogueCanvas.GetComponent<GraphicRaycaster>();
        SetRoomDialogueVisible(false);
        dialogueRunner.onDialogueComplete.AddListener(() => SetRoomDialogueVisible(false));
    }

    /// <summary>Opens the room dialogue without forwarding the click that started it to Yarn.</summary>
    private void OnDialogueStarted()
    {
        dialogueStartFrame = Time.frameCount;
        if (roomDialogueCanvas != null) SetRoomDialogueVisible(true);
    }

    /// <summary>Enables the dialogue overlay and its input blocker together.</summary>
    private void SetRoomDialogueVisible(bool visible)
    {
        roomDialogueCanvas.enabled = visible;
        roomDialogueRaycaster.enabled = visible;
    }

    /// <summary>Lets Yarn reveal or advance the current line after room clicks have been processed.</summary>
    private void LateUpdate()
    {
        var mouse = Mouse.current;
        if (mouse == null || PauseManager.BlocksInput || SceneChanger.IsTransitioning || !dialogueRunner.IsDialogueRunning
            || Time.frameCount == dialogueStartFrame) return;
        Vector2 pointer = mouse.position.ReadValue();
        var camera = Camera.main;
        if (camera != null && !camera.pixelRect.Contains(pointer)) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (!ClickHandler.IsOverUiControl(pointer, pointerHits)) lineAdvancer.OnInputHurryUpLines();
        }
        else if (mouse.rightButton.wasPressedThisFrame)
            dialogueRunner.RequestHurryUpLine();
    }
}
