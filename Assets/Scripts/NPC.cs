using UnityEngine;

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
    }

    /// <summary>Shows this NPC only in their assigned room while they are aboard.</summary>
    void Start()
    {
        string room = npcId switch
        {
            "Apprentice Diver" => "Airlock",
            "Medic" => GameState.days switch
            {
                5 or 13 or 17 => "Mineral Lab",
                3 or 7 or 11 or 19 => "Shared Space",
                _ => "Storage Closet"
            },
            "Mineral Processor" => GameState.days is 3 or 5 or 7 ? "Storage Closet" : "Mineral Lab",
            _ => SceneChanger.getScene()
        };

        gameObject.SetActive(GameState.IsCrewAboard(npcId) && SceneChanger.getScene() == room &&
            (npcId != "Apprentice Diver" || GameState.HasSpokenToOtherCrew()));
    }
}
