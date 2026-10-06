using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    private bool justOpened = false;

    void Awake()
    {
        Instance = this;
    }
    void Update()
    {
        if (dialoguePanel.activeSelf && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if(justOpened){
                justOpened = false;
                return;
            }
            CloseDialogue();
        }
    }

    public void StartDialogue(string text)
    {
        dialoguePanel.SetActive(true);
        dialogueText.text = text;
        justOpened = true;
    }

    public void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
    }
}