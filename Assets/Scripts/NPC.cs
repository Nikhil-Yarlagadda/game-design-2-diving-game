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
            if(GameState.npcsTalks.Count < 2 && GameState.days < 12)
                gameObject.SetActive(false);
            else if(GameState.npcsTalks.Count < 1 && GameState.days < 14)
                gameObject.SetActive(false);
            else if(GameState.days < 14 && SceneChanger.getScene() != "Airlock")
                gameObject.SetActive(false);
            else if(GameState.days == 17 && SceneChanger.getScene() != "Storage Closet")
                gameObject.SetActive(false);
            else if((GameState.days == 15 || GameState.days == 19) && SceneChanger.getScene() != "Mineral Lab")
                gameObject.SetActive(false);
        }

        if (npcId == "Medic")
        {
            if(GameState.days > 16 && GameState.dayOfKirstenCured < 0)
                gameObject.SetActive(false);
            else if((GameState.days == 1 || GameState.days == 9 || GameState.days == 15) && SceneChanger.getScene() != "Storage Closet")
                gameObject.SetActive(false);
            else if((GameState.days == 5 || GameState.days == 13 || GameState.days == 17) && SceneChanger.getScene() != "Mineral Lab")
                gameObject.SetActive(false);
            else if((GameState.days == 3 || GameState.days == 7 || GameState.days == 11 || GameState.days == 19) && SceneChanger.getScene() != "Shared Space")
                gameObject.SetActive(false);
        }

        if (npcId == "Mineral Processor")
        {
            if(GameState.days > 12)
                gameObject.SetActive(false);
            else if((GameState.days == 3 || GameState.days == 5 || GameState.days == 7) && SceneChanger.getScene() != "Storage Closet")
                gameObject.SetActive(false);
            else if((GameState.days == 1 || GameState.days == 9 || GameState.days == 11) && SceneChanger.getScene() != "Mineral Lab")
                gameObject.SetActive(false);
        }
    }
}
