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

        bool alreadyTalked = GameState.npcsTalks.Contains(npcId);
        conversationInProgress = true;
        try
        {
            await runner.StartDialogue(dialogueStartNode + GameState.days);
            GameState.npcsTalks.Add(npcId);
            if (alreadyTalked && runner.TryGetComponent<DialogueAdvancer>(out var dialogue))
                dialogue.ShowCrewSkip();
            await runner.DialogueTask;
        }
        finally
        {
            if (this != null) GameState.StartCrewConversationCooldown(npcId);
            conversationInProgress = false;
        }
    }

    /// <summary>Shows this NPC only in their assigned room while they are aboard.</summary>
    void Start() => gameObject.SetActive(GameState.GetAvailableCrewRoom(npcId) == SceneChanger.getScene());
}
