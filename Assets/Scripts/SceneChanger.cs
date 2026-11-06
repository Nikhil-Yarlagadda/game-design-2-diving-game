using UnityEngine;
using UnityEngine.SceneManagement; // Required for switching scenes

public class SceneChanger : MonoBehaviour
{
    // This function must be public so the UI Button can see it
    public static void ChangeScene(string sceneName)
    {
        if (InventoryButton.IsOpen) return;
        Debug.Log("Change scene to " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    public static string getScene()
    {
        return SceneManager.GetActiveScene().name;
    }

    public static void ResetWrapper()
    {
        GameState.reset();
    }
}
