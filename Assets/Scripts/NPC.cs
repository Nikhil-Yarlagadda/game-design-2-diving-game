using UnityEngine;
using Yarn.Unity;

public class NPC : MonoBehaviour, IClickable
{
    public string npcId;
    public string dialogueStartNode;
    public Sprite portrait;
    

    public void OnClicked()
    {

        DialogueContext.nodeName = dialogueStartNode + GameState.days;

        if (portrait != null)
        {
            DialogueContext.portrait = portrait;
        }
        else
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            DialogueContext.portrait = sr != null ? sr.sprite : null;
        }

        DialogueContext.scene = SceneChanger.getScene();
        GameState.npcsTalks.Add(npcId);
        SceneChanger.ChangeScene("Dialogue");
    /*

        if (!runner.IsDialogueRunning)
        {
            runner.StartDialogue(dialogueStartNode);
        }*/
    }

    void Start()
    {
        if (npcId == "Apprentice Diver")
        {
            if(GameState.npcsTalks.Count < 2)
                gameObject.SetActive(false);
        }
    }
}
