using UnityEngine;
using UnityEngine.SceneManagement; // Required for switching scenes
using System.Collections;

public class SceneChanger : MonoBehaviour
{
    public static bool IsTransitioning { get; private set; }
    private static SceneChanger transition;
    private float previousTimeScale;
    private bool ownsTransition;

    public static bool ChangeSceneWithFade(string sceneName, System.Action beforeLoad = null)
    {
        if (IsTransitioning) return false;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Scene is unavailable: " + sceneName);
            return false;
        }
        var go = new GameObject("Scene Transition", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        DontDestroyOnLoad(go);
        transition = go.AddComponent<SceneChanger>();
        transition.ownsTransition = true;
        transition.previousTimeScale = Time.timeScale;
        IsTransitioning = true;
        Time.timeScale = 0f;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        transition.StartCoroutine(transition.FadeToScene(sceneName, image, beforeLoad));
        return true;
    }

    private IEnumerator FadeToScene(string sceneName, UnityEngine.UI.Image image, System.Action beforeLoad)
    {
        try
        {
            yield return Fade(image, 0f, 1f);
            beforeLoad?.Invoke();
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return null;
            yield return Fade(image, 1f, 0f);
        }
        finally { if (this != null) Destroy(gameObject); }
    }

    private static IEnumerator Fade(UnityEngine.UI.Image image, float from, float to)
    {
        const float duration = .35f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            image.color = new Color(0, 0, 0, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        image.color = new Color(0, 0, 0, to);
    }

    private void OnDestroy()
    {
        if (!ownsTransition || transition != this) return;
        Time.timeScale = previousTimeScale;
        IsTransitioning = false;
        transition = null;
    }

    // This function must be public so the UI Button can see it
    public static void ChangeScene(string sceneName)
    {
        if (IsTransitioning) return;
        Debug.Log("Change scene to " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    public static string getScene()
    {
        return SceneManager.GetActiveScene().name;
    }

    // Runs once per Play session, including when Domain Reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetWrapper()
    {
        if (transition != null)
        {
            transition.StopAllCoroutines();
            Destroy(transition.gameObject);
        }
        transition = null;
        IsTransitioning = false;
        GameState.reset();
        PauseManager.ResetState();
    }
}
