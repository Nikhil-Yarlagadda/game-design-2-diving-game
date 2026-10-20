using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

public class DialogueAdvancer : MonoBehaviour
{
    public DialogueRunner dialogueRunner;
    public LineAdvancer lineAdvancer;
    void Start()
    {
        
    }

    void Update()
    {

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
          lineAdvancer.RequestLineHurryUp();            
        }
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
          lineAdvancer.RequestNextLine();            
        }
    }
}
