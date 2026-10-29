using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    private static string resumeScene;
    public static bool isPaused = false;
    public static bool BlocksInput => isPaused || Time.frameCount == inputFrame;
    private static bool oceanRetained, unloadingMenu;
    private static GameObject oceanEvents;
    private static float previousTimeScale = 1f;
    private static int inputFrame = -1;

    public static void ResetState()
    {
        if (oceanEvents != null) oceanEvents.SetActive(true);
        oceanEvents = null;
        oceanRetained = unloadingMenu = false;
        previousTimeScale = 1f;
        inputFrame = -1;
        resumeScene = null;
        isPaused = false;
        Time.timeScale = 1f;
    }

    void Awake()
    {
        if (!oceanRetained || gameObject.scene.name != "Pause Screen") return;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
            }
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var clicks in root.GetComponentsInChildren<ClickHandler>(true)) clicks.enabled = false;
        }
    }

    void Update()
    {
        if (unloadingMenu || Time.frameCount == inputFrame) return;
        if (Time.timeScale == 0f && !isPaused) return;
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
        if (isPaused || Time.timeScale == 0f) return;
        if (!Application.CanStreamedLevelBeLoaded("Pause Screen"))
        {
            Debug.LogError("Pause Screen is unavailable. Include it in the build scene list.");
            return;
        }
        inputFrame = Time.frameCount;
        previousTimeScale = Time.timeScale;
        isPaused = true;
        Time.timeScale = 0f;
        resumeScene = SceneChanger.getScene();
        oceanRetained = Object.FindAnyObjectByType<DivingPrototype.OceanInteractions>() != null;
        if (oceanRetained)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            oceanEvents = events != null ? events.gameObject : null;
            if (oceanEvents != null) oceanEvents.SetActive(false);
            SceneManager.LoadScene("Pause Screen", LoadSceneMode.Additive);
        }
        else SceneChanger.ChangeScene("Pause Screen");
    }

    public static void Resume()
    {
        if (!isPaused || unloadingMenu) return;
        inputFrame = Time.frameCount;
        if (oceanRetained)
        {
            unloadingMenu = true;
            SceneManager.UnloadSceneAsync("Pause Screen").completed += _ =>
            {
                if (oceanEvents != null) oceanEvents.SetActive(true);
                oceanEvents = null;
                oceanRetained = unloadingMenu = false;
                isPaused = false;
                Time.timeScale = previousTimeScale;
                inputFrame = Time.frameCount;
            };
            return;
        }
        isPaused = false;
        Time.timeScale = previousTimeScale;
        SceneChanger.ChangeScene(resumeScene);
    }
}
