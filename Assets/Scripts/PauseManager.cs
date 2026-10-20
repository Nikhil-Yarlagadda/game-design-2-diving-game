using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    private static string resumeScene;
    public static bool isPaused = false;

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    public static void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;
        resumeScene = SceneChanger.getScene();
        SceneChanger.ChangeScene("Pause Screen");
    }

    public static void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        SceneChanger.ChangeScene(resumeScene);
    }
}