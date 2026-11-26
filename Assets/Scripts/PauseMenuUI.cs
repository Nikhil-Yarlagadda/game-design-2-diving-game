using DivingPrototype;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

/// <summary>
/// Renders a Figma-styled Pause menu matching the Start Screen design tokens (banner with top/bottom Cyan rules,
/// 5px Cyan-bordered buttons with 20%/80% Cyan fills), including a context-aware Save &amp; Main Menu confirmation modal.
/// </summary>
public sealed class PauseMenuUI : MonoBehaviour
{
    private static readonly Color BannerFill = Theme.Navy.WithAlpha(0.9f);
    private static readonly Color ButtonIdleFill = Theme.Cyan.WithAlpha(0.14f);
    private static readonly Color ButtonHoverFill = Theme.Cyan.WithAlpha(0.8f);

    private TMP_FontAsset font;
    private RectTransform menuView, confirmView;
    private Button resumeButton, cancelButton;
    private TMP_Text confirmBody;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAttach(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach(scene);

    private static void TryAttach(Scene scene)
    {
        if (scene.isLoaded && scene.name == "Pause Screen")
            AttachToScene(scene);
    }

    /// <summary>Replaces the legacy placeholder Pause Screen canvas with the themed pause UI.</summary>
    internal static void AttachToScene(Scene scene)
    {
        if (!scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<PauseMenuUI>() != null) return;
        }
        var host = new GameObject("Pause Menu UI");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<PauseMenuUI>();
    }

    private void Awake()
    {
        font = Theme.Font(null);
        HideLegacyCanvas();
        BuildUi();
        ShowConfirm(false);
    }

    private void Start()
    {
        if (resumeButton == null) return;
        var events = EventSystem.current;
        if (events != null)
        {
            events.firstSelectedGameObject = resumeButton.gameObject;
            events.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    private void HideLegacyCanvas()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root == gameObject) continue;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(false);
        }
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("Pause Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1f;

        var root = canvasGo.GetComponent<RectTransform>();
        var shade = Theme.Node("Backdrop", root).Fill().gameObject.AddComponent<Image>();
        shade.color = Theme.Navy.WithAlpha(0.86f);
        shade.raycastTarget = true;

        BuildMainMenu(root);
        BuildConfirmDialog(root);
    }

    private void BuildMainMenu(RectTransform root)
    {
        menuView = Theme.Node("Menu View", root).Fill();

        var titleBox = Theme.Box("Paused Banner", menuView, BannerFill, outline: null, radius: Theme.Fig(10f));
        titleBox.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Theme.Fig(190f)), Theme.Fig(640f, 140f));
        Theme.HorizontalRules(titleBox.rectTransform, Theme.Cyan, Theme.Fig(3f));
        float ruleInset = Theme.Fig(5f);
        foreach (var name in new[] { "Top Rule", "Bottom Rule" })
        {
            var rule = titleBox.transform.Find(name) as RectTransform;
            if (rule == null) continue;
            rule.offsetMin = new Vector2(ruleInset, rule.offsetMin.y);
            rule.offsetMax = new Vector2(-ruleInset, rule.offsetMax.y);
        }
        var title = Theme.Text("Title", titleBox.transform, font, Theme.Fig(88f), Theme.White, TextAlignmentOptions.Center, "PAUSED");
        title.rectTransform.Fill(Theme.Fig(16f), 0f, Theme.Fig(16f), 0f);

        string slotInfo = SaveManager.ActiveSlot > 0
            ? $"Slot {SaveManager.ActiveSlot}   •   Day {GameState.days}   •   {GameState.credits} Credits"
            : $"Day {GameState.days}   •   {GameState.credits} Credits";
        var subtitle = Theme.Text("Subtitle", menuView, font, Theme.Fig(34f), Theme.Cyan, TextAlignmentOptions.Center, slotInfo);
        subtitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Theme.Fig(356f)), Theme.Fig(720f, 44f));

        resumeButton = CreateMenuButton("RESUME", menuView, topFigma: 446f, PauseManager.Resume);
        resumeButton.name = "Resume Button";
        CreateMenuButton("PLAYTEST STATS", menuView, topFigma: 565f, PauseManager.OpenStats).name = "PlaytestStats";
        CreateMenuButton("SAVE & MAIN MENU", menuView, topFigma: 684f, OnMainMenuClicked).name = "Main Menu Button";
    }

    private void BuildConfirmDialog(RectTransform root)
    {
        confirmView = Theme.Node("Confirm View", root).Fill();

        var dialog = Theme.Box("Confirm Box", confirmView, Theme.Navy.WithAlpha(0.95f), Theme.Cyan, radius: Theme.Fig(10f), thickness: Theme.Fig(5f));
        dialog.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.one * 0.5f, Vector2.zero, Theme.Fig(920f, 440f));

        var heading = Theme.Text("Heading", dialog.transform, font, Theme.Fig(56f), Theme.White, TextAlignmentOptions.Center, "RETURN TO MAIN MENU?");
        heading.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Theme.Fig(64f)), Theme.Fig(840f, 68f));

        confirmBody = Theme.Text("Message", dialog.transform, font, Theme.Fig(38f), Theme.Cyan, TextAlignmentOptions.Center, "");
        confirmBody.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.one * 0.5f, new Vector2(0f, Theme.Fig(12f)), Theme.Fig(820f, 170f));

        Vector2 btnSize = Theme.Fig(310f, 84f);
        cancelButton = CreateFigmaButton("CANCEL", dialog.transform, btnSize, Theme.Fig(42f), Theme.Cyan, () => ShowConfirm(false));
        cancelButton.name = "Cancel Main Menu Button";
        ((RectTransform)cancelButton.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-Theme.Fig(180f), Theme.Fig(48f)), btnSize);

        var confirm = CreateFigmaButton("MAIN MENU", dialog.transform, btnSize, Theme.Fig(42f), Theme.White, OnConfirmMainMenu);
        confirm.name = "Confirm Main Menu Button";
        ((RectTransform)confirm.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(Theme.Fig(180f), Theme.Fig(48f)), btnSize);
    }

    private Button CreateMenuButton(string caption, Transform parent, float topFigma, UnityEngine.Events.UnityAction action)
    {
        Vector2 size = Theme.Fig(466f, 88f);
        var button = CreateFigmaButton(caption, parent, size, Theme.Fig(48f), Theme.Cyan, action);
        ((RectTransform)button.transform).Place(
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -Theme.Fig(topFigma)),
            size);
        return button;
    }

    private Button CreateFigmaButton(string caption, Transform parent, Vector2 size, float fontSize, Color idleTextColor, UnityEngine.Events.UnityAction action)
    {
        var image = Theme.Box(caption, parent, ButtonIdleFill, Theme.Cyan, radius: Theme.Fig(10f), thickness: Theme.Fig(5f));
        image.raycastTarget = true;
        image.rectTransform.sizeDelta = size;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        button.onClick.AddListener(action);

        var label = Theme.Text("Label", image.transform, font, fontSize, idleTextColor, TextAlignmentOptions.Center, caption);
        label.rectTransform.Fill(Theme.Fig(8f), 0f, Theme.Fig(8f), 0f);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        Theme.HoverPalette(
            button,
            ButtonIdleFill, Theme.Cyan, idleTextColor,
            ButtonHoverFill, Theme.White, Theme.Navy);
        return button;
    }

    private void OnMainMenuClicked()
    {
        if (SaveManager.IsInExcursionScene())
        {
            confirmBody.text =
                $"Current excursion progress will not be saved.\nLoading this save will return you to the Day {GameState.days} loadout screen before this dive.";
        }
        else if (SaveManager.IsInResultScene())
        {
            confirmBody.text =
                $"Your Day {GameState.days} results are already saved.\nLoading this save will return you to this results screen.";
        }
        else
        {
            confirmBody.text =
                $"In-day dialogue and actions are not saved.\nLoading this save will return you to the start of Day {GameState.days}.";
        }
        ShowConfirm(true);
    }

    private void ShowConfirm(bool visible)
    {
        menuView.gameObject.SetActive(!visible);
        confirmView.gameObject.SetActive(visible);
        var target = visible ? cancelButton : resumeButton;
        if (target != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    private static void OnConfirmMainMenu() => SaveManager.ReturnToTitleMenu();
}
