using UnityEngine;

public class NPC : MonoBehaviour, IClickable
{
    public string npcId;
    public string dialogueStartNode; // for Yarn Spinner / Ink poetntially
    [TextArea(2, 5)]
    public string dialogueLine; //set text in inteface(placeholder for now)

    public void OnClicked()
    {
        if(! DialogueManager.Instance.dialoguePanel.activeSelf){
            DialogueManager.Instance.StartDialogue(dialogueLine);
        }
    }
}
