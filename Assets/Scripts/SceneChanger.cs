using UnityEngine;
using UnityEngine.SceneManagement; // Required for switching scenes

public class SceneChanger : MonoBehaviour
{
    // This function must be public so the UI Button can see it
    public void ChangeScene(string sceneName)
    {
        Debug.Log("Change scene to " + sceneName);
        SceneManager.LoadScene(sceneName);
    }
}
