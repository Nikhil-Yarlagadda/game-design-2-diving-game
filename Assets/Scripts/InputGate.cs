using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputGate : MonoBehaviour   // put on the EventSystem object
{
    public GameObject eventSystemObject;   // the EventSystem, set inactive in the scene

    IEnumerator Start()
    {
        // wait for the click that changed the scene to be released
        while (Mouse.current != null && Mouse.current.leftButton.isPressed)
            yield return null;

        yield return null;
        eventSystemObject.SetActive(true);
    }
}

public static class ClickGuard
{
    private static bool waitingForRelease;
    private static float minUntil;

    public static void Block(float minSeconds = 0.1f)
    {
        waitingForRelease = true;
        minUntil = Time.unscaledTime + minSeconds;
    }

    public static bool Blocked
    {
        get
        {
            if (waitingForRelease && Mouse.current != null && !Mouse.current.leftButton.isPressed)
                waitingForRelease = false;

            return waitingForRelease || Time.unscaledTime < minUntil;
        }
    }
}