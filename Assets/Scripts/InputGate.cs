using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputGate : MonoBehaviour
{
    [SerializeField] private GameObject eventSystemObject;

    /// <summary>Enables the scene UI after the incoming click has been released.</summary>
    private IEnumerator Start()
    {
        while (Mouse.current != null && Mouse.current.leftButton.isPressed)
            yield return null;

        yield return null;
        eventSystemObject.SetActive(true);
    }
}
