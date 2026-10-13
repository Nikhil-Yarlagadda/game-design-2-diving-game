using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

public class DialogueAdvancer : MonoBehaviour
{
    public DialogueRunner dialogueRunner;
    public LineAdvancer lineAdvancer;
    private bool wasRunningLastFrame = false;

    void Start()
    {

    }

    void Update()
    {
        bool running = dialogueRunner.IsDialogueRunning;

        if (running && wasRunningLastFrame && Mouse.current.leftButton.wasPressedThisFrame)
        {
            lineAdvancer.RequestNextLine();
        }

        wasRunningLastFrame = running;
    }
}
