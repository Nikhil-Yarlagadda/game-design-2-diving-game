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
                if(GameState.days == 5 && !GameState.draftMessage)
                    runner.StartDialogue(dialogueStartNode + "draft");
                else if(GameState.days == 7 && !GameState.checkTerminal)
                    runner.StartDialogue(dialogueStartNode + "terminal");
                else
                    runner.StartDialogue(dialogueStartNode);
        }
    }
}