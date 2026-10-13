using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class DialogueSceneController : MonoBehaviour
{
    public Image characterImage; // drag CharacterImage here
    public DialogueRunner runner;

    void Start()
    {
        characterImage.sprite = DialogueContext.portrait;
        characterImage.preserveAspect = true;

        runner.onDialogueComplete.AddListener(CloseScene);
        runner.StartDialogue(DialogueContext.nodeName);
    }

    void CloseScene()
    {
        SceneChanger.ChangeScene(DialogueContext.scene);
    }
}