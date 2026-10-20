using UnityEngine;
using Yarn.Unity;

public class DialogueRunnerAccessor : MonoBehaviour
{
    public static DialogueRunner Instance;
    private DialogueRunner runner;

    void Awake()
    {
        runner = GetComponent<DialogueRunner>();
        Instance = runner;
    }
}
