using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private Button inventoryButtonStyle;

    private GraphicRaycaster roomDialogueRaycaster;
    private readonly List<RaycastResult> pointerHits = new();
    private int dialogueStartFrame = -1, dialogueEndFrame = -1;
    private Button crewSkip;

    public bool BlocksRoomInput => roomDialogueCanvas != null
        && (roomDialogueCanvas.isActiveAndEnabled || Time.frameCount == dialogueEndFrame);

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
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueCompleted);
        if (inventoryButtonStyle != null) BuildCrewSkip();
    }

    /// <summary>Builds a top-right dialogue button using the room inventory button's visual styling.</summary>
    private void BuildCrewSkip()
    {
        crewSkip = Instantiate(inventoryButtonStyle, roomDialogueCanvas.transform, false);
        crewSkip.name = "Skip conversation";
        Destroy(crewSkip.GetComponent<InventoryButton>());
        crewSkip.onClick = new Button.ButtonClickedEvent();
        crewSkip.onClick.AddListener(SkipConversation);
        crewSkip.GetComponentInChildren<TMP_Text>().text = "Skip";
        var rect = (RectTransform)crewSkip.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-25, -25);
        crewSkip.gameObject.SetActive(false);
    }

    /// <summary>Shows Skip for a running crewmate conversation that was already visited today.</summary>
    internal void ShowCrewSkip()
    {
        if (crewSkip != null && dialogueRunner.IsDialogueRunning) crewSkip.gameObject.SetActive(true);
    }

    /// <summary>Cancels the current conversation through Yarn without selecting an option.</summary>
    private async void SkipConversation()
    {
        if (!crewSkip.gameObject.activeSelf || !dialogueRunner.IsDialogueRunning || PauseManager.BlocksInput) return;
        crewSkip.gameObject.SetActive(false);
        await dialogueRunner.Stop();
    }

    /// <summary>Closes the modal and consumes any click on the frame that ended the dialogue.</summary>
    private void OnDialogueCompleted()
    {
        dialogueEndFrame = Time.frameCount;
        if (crewSkip != null) crewSkip.gameObject.SetActive(false);
        SetRoomDialogueVisible(false);
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
