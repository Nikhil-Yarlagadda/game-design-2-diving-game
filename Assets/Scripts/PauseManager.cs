using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity.LineAdvancerInput;

[DefaultExecutionOrder(-200)]
public class PauseManager : MonoBehaviour
{
    private const string MenuScene = "Pause Screen";
    private const string StatsScene = "Playtest Stats";
    private const string EndingScene = "Ending";

    public static bool isPaused { get; private set; }
    public static bool BlocksInput => isPaused || Time.frameCount == inputFrame;
    private static SuspendedUi gameplayUi, statsSourceUi;
    private static AsyncOperation sceneOperation;
    private static float previousTimeScale = 1f;
    private static int inputFrame = -1;

    /// <summary>Restores UI and clears pause state when a new game starts.</summary>
    public static void ResetState()
    {
        statsSourceUi?.Restore();
        gameplayUi?.Restore();
        statsSourceUi = gameplayUi = null;
        sceneOperation = null;
        previousTimeScale = 1f;
        inputFrame = -1;
        isPaused = false;
        Time.timeScale = 1f;
    }

    /// <summary>Restores a gameplay modal's prior time scale beneath any open pause menu.</summary>
    internal static void RestoreGameplayTimeScale(float scale)
    {
        if (isPaused) previousTimeScale = scale;
        else Time.timeScale = scale;
    }

    /// <summary>Configures the pause scene to render over the retained game.</summary>
    private void Awake()
    {
        if (gameObject.scene.name != MenuScene) return;
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

    /// <summary>Handles Escape from the gameplay scene or the active pause menu.</summary>
    private void Update()
    {
        if (sceneOperation != null || Time.frameCount == inputFrame ||
            Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (isPaused)
        {
            if (gameObject.scene.name != MenuScene) return;
            if (statsSourceUi != null) CloseStats();
            else Resume();
        }
        else if (gameObject.scene == SceneManager.GetActiveScene() && gameObject.scene.name != MenuScene)
        {
            if (statsSourceUi != null) CloseStats();
            else Pause();
        }
    }

    /// <summary>Pauses the current game without unloading its scene or modals.</summary>
    public static void Pause()
    {
        if (isPaused || statsSourceUi != null || sceneOperation != null || SceneChanger.IsTransitioning ||
            InventoryButton.IsLoading || !CanLoad(MenuScene)) return;
        inputFrame = Time.frameCount;
        previousTimeScale = Time.timeScale;
        isPaused = true;
        Time.timeScale = 0f;
        gameplayUi = new SuspendedUi();
        for (int index = 0; index < SceneManager.sceneCount; index++)
            gameplayUi.Suspend(SceneManager.GetSceneAt(index), hideCanvases: false);
        sceneOperation = SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Additive);
        if (sceneOperation == null) RestoreGameplay();
        else sceneOperation.completed += _ => sceneOperation = null;
    }

    /// <summary>Opens the stats overlay using the pause menu or ending screen's input system.</summary>
    public static void OpenStats()
    {
        if (sceneOperation != null || statsSourceUi != null || SceneChanger.IsTransitioning) return;
        Scene source = isPaused ? SceneManager.GetSceneByName(MenuScene) : SceneManager.GetActiveScene();
        if (!source.isLoaded || (!isPaused && source.name != EndingScene) || !CanLoad(StatsScene)) return;
        inputFrame = Time.frameCount;
        statsSourceUi = new SuspendedUi();
        EventSystem.current?.SetSelectedGameObject(null);
        statsSourceUi.Suspend(source, hideCanvases: true);
        sceneOperation = SceneManager.LoadSceneAsync(StatsScene, LoadSceneMode.Additive);
        if (sceneOperation == null) RestoreStatsSource();
        else sceneOperation.completed += _ => sceneOperation = null;
    }

    /// <summary>Closes stats and returns to the screen that opened it.</summary>
    public static void CloseStats()
    {
        if (sceneOperation != null || statsSourceUi == null) return;
        inputFrame = Time.frameCount;
        EventSystem.current?.SetSelectedGameObject(null);
        sceneOperation = SceneManager.UnloadSceneAsync(StatsScene);
        if (sceneOperation == null) RestoreStatsSource();
        else sceneOperation.completed += _ => RestoreStatsSource();
    }

    /// <summary>Unloads the pause menu and resumes the retained scene.</summary>
    public static void Resume()
    {
        if (!isPaused || sceneOperation != null || statsSourceUi != null) return;
        inputFrame = Time.frameCount;
        sceneOperation = SceneManager.UnloadSceneAsync(MenuScene);
        if (sceneOperation == null) RestoreGameplay();
        else sceneOperation.completed += _ => RestoreGameplay();
    }

    /// <summary>Checks that an overlay has been included in the build.</summary>
    private static bool CanLoad(string scene)
    {
        if (Application.CanStreamedLevelBeLoaded(scene)) return true;
        Debug.LogError($"Include {scene} in the build scene list.");
        return false;
    }

    /// <summary>Restores the source screen after the stats overlay closes.</summary>
    private static void RestoreStatsSource()
    {
        statsSourceUi.Restore();
        statsSourceUi = null;
        sceneOperation = null;
        inputFrame = Time.frameCount;
    }

    /// <summary>Restores the gameplay UI and its time scale after pausing.</summary>
    private static void RestoreGameplay()
    {
        gameplayUi.Restore();
        gameplayUi = null;
        sceneOperation = null;
        isPaused = false;
        Time.timeScale = previousTimeScale;
        inputFrame = Time.frameCount;
    }

    private sealed class SuspendedUi
    {
        private readonly List<Behaviour> disabled = new();
        private readonly EventSystem events = EventSystem.current;
        private readonly GameObject selected = EventSystem.current?.currentSelectedGameObject;

        /// <summary>Disables raycasters and either hides canvases or suspends the scene's event systems.</summary>
        internal void Suspend(Scene scene, bool hideCanvases)
        {
            if (!scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                Disable(root.GetComponentsInChildren<BaseRaycaster>());
                if (hideCanvases) Disable(root.GetComponentsInChildren<Canvas>());
                else
                {
                    Disable(root.GetComponentsInChildren<EventSystem>());
                    Disable(root.GetComponentsInChildren<BaseInputModule>());
                    Disable(root.GetComponentsInChildren<KeyCodes>());
                }
            }
        }

        /// <summary>Restores only components that were previously enabled.</summary>
        internal void Restore()
        {
            foreach (var component in disabled)
                if (component != null) component.enabled = true;
            if (events != null && events.isActiveAndEnabled && selected != null && selected.activeInHierarchy)
                events.SetSelectedGameObject(selected);
        }

        /// <summary>Remembers enabled components before disabling them.</summary>
        private void Disable(IEnumerable<Behaviour> components)
        {
            foreach (var component in components)
            {
                if (!component.enabled) continue;
                disabled.Add(component);
                component.enabled = false;
            }
        }
    }
}
