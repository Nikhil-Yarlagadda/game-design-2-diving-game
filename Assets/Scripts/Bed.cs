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
            if(GameState.npcsTalks.Count < 3)
                runner.StartDialogue(dialogueStartNode + "early");
            else
                runner.StartDialogue(dialogueStartNode);
        }
    }
}