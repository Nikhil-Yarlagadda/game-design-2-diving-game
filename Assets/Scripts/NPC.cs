using UnityEngine;
using Yarn.Unity;

public class NPC : MonoBehaviour, IClickable
{
    public string npcId;
    public string dialogueStartNode;
    public Sprite portrait;
    [SerializeField] private DialogueRunner runner;

    private bool conversationInProgress;

    /// <summary>Starts this character’s dialogue in the current room.</summary>
    public async void OnClicked()
    {
        if (conversationInProgress || runner.IsDialogueRunning || !GameState.CanTalkToCrew(npcId)) return;

        conversationInProgress = true;
        try
        {
            await runner.StartDialogue(dialogueStartNode + GameState.days);
            GameState.npcsTalks.Add(npcId);
            await runner.DialogueTask;
        }
        finally
        {
            if (this != null) GameState.StartCrewConversationCooldown(npcId);
            conversationInProgress = false;
        }
    }

    /// <summary>Shows this NPC only in their assigned room while they are aboard.</summary>
    void Start()
    {
        string room = npcId switch
        {
            "Apprentice Diver" => GameState.days switch
            {
                15 or 19 => "Mineral Lab",
                17 => "Storage Closet",
                _ => "Moonpool"
            },
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
            (npcId != "Apprentice Diver" || GameState.days >= 15 || GameState.HasSpokenToOtherCrew()));
    }
}
