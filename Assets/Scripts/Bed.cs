using UnityEngine;
using Yarn.Unity;

public class Bed : MonoBehaviour, IClickable
{
    public string dialogueStartNode;
    public DialogueRunner runner;

    public void OnClicked()
    {
        if (!runner.IsDialogueRunning)
        {
            runner.StartDialogue(dialogueStartNode);
        }
    }
}