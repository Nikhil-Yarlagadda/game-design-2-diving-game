using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yarn.Unity;

public class DialogueAdvancer : MonoBehaviour
{
    public DialogueRunner dialogueRunner;
    public LineAdvancer lineAdvancer;
    [SerializeField] private Canvas roomDialogueCanvas;

    private GraphicRaycaster roomDialogueRaycaster;

    public bool BlocksRoomInput => roomDialogueCanvas != null && roomDialogueCanvas.isActiveAndEnabled;

    /// <summary>Sets up a modal canvas over the room while Yarn is presenting dialogue.</summary>
    private void Awake()
    {
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
        dialogueRunner.onDialogueStart.AddListener(() => SetRoomDialogueVisible(true));
        dialogueRunner.onDialogueComplete.AddListener(() => SetRoomDialogueVisible(false));
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
        if (mouse == null || SceneChanger.IsTransitioning || !dialogueRunner.IsDialogueRunning) return;

        if (mouse.leftButton.wasPressedThisFrame)
            lineAdvancer.OnInputHurryUpLines();
        else if (mouse.rightButton.wasPressedThisFrame)
            dialogueRunner.RequestHurryUpLine();
    }
}
