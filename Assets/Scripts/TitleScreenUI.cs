using DivingPrototype;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Theme = DivingPrototype.OceanUiTheme;

/// <summary>
/// Builds and drives the 1:1 Figma-styled Start Screen, 3-slot Load Game screen, and full-slot Overwrite screen
/// on "Title Screen" without modifying scene YAML (Figma frames 18:3, 232:1024, 232:1037, 232:1048, 232:1077).
/// </summary>
public sealed class TitleScreenUI : MonoBehaviour
{
    private enum View { Main, Load, Overwrite }

    // Sampled from Figma START SCREEN background gradient (node 18:4) and button tokens.
    private static readonly Color GradientTop = Theme.Hex("324858");
    private static readonly Color GradientMidHigh = Theme.Hex("283C4D");
    private static readonly Color GradientMid = Theme.Hex("1E2F41");
    private static readonly Color GradientMidLow = Theme.Hex("162438");
    private static readonly Color GradientBottom = Theme.Hex("152337");

    private static readonly Color BannerFill = Theme.Navy.WithAlpha(0.9f);
    // Figma uses rgba(164,221,219,0.2) in sRGB; 0.14f matches the visual contrast in Unity Linear space.
    private static readonly Color ButtonIdleFill = Theme.Cyan.WithAlpha(0.14f);
    private static readonly Color ButtonHoverFill = Theme.Cyan.WithAlpha(0.8f);
    private static readonly Color DisabledOverlayFill = Theme.Navy.WithAlpha(0.82f);

    private static Sprite backdropGradientSprite;

    private TMP_FontAsset font;
    private RectTransform mainView, slotsView, cardsContainer;
    private VerticalLayoutGroup cardsLayout;
    private Button loadGameButton;
    private GameObject loadDisabledOverlay;
    private TMP_Text slotsSubtitle, emptyText;
    private View currentView;

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
        if (!scene.isLoaded || scene.name != SaveManager.TitleScene) return;
        if (FindAnyObjectByType<TitleScreenUI>() != null) return;
        var host = new GameObject("Title Screen UI");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<TitleScreenUI>();
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        font = Theme.Font(null);
        HideLegacyCanvas();
        BuildUi();
        ShowView(View.Main);
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

    private static Sprite GetBackdropGradient()
    {
        if (backdropGradientSprite != null) return backdropGradientSprite;
        const int height = 256;
        var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color[height];
        for (int y = 0; y < height; y++)
        {
            // y = 0 is bottom of screen, y = height - 1 is top of screen.
            float t = y / (float)(height - 1);
            Color c;
            if (t <= 0.25f) c = Color.Lerp(GradientBottom, GradientMidLow, t / 0.25f);
            else if (t <= 0.50f) c = Color.Lerp(GradientMidLow, GradientMid, (t - 0.25f) / 0.25f);
            else if (t <= 0.75f) c = Color.Lerp(GradientMid, GradientMidHigh, (t - 0.50f) / 0.25f);
            else c = Color.Lerp(GradientMidHigh, GradientTop, (t - 0.75f) / 0.25f);
            pixels[y] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        backdropGradientSprite = Sprite.Create(tex, new Rect(0, 0, 1, height), Vector2.one * 0.5f, 100f);
        return backdropGradientSprite;
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("Start Screen Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1f;

        var root = canvasGo.GetComponent<RectTransform>();
        var bg = Theme.Node("Backdrop", root).Fill().gameObject.AddComponent<Image>();
        bg.sprite = GetBackdropGradient();
        bg.color = Color.white;
        bg.raycastTarget = true;

        BuildMainView(root);
        BuildSlotsView(root);
    }

    private void BuildMainView(RectTransform root)
    {
        mainView = Theme.Node("Main View", root).Fill();

        // Figma 232:925: 893x150, top=271, centered; rgba(21,35,55,0.9), top/bottom 3px Cyan border, radius 10.
        CreateTitleBanner(mainView, topFigma: 271f);

        // Figma 232:1013 / 232:931 / 232:1015: 466x88, top=512 / 631 / 750, centered; 48px Cyan text.
        var newGame = CreateMenuButton("NEW GAME", mainView, topFigma: 512f, OnNewGameClicked);
        newGame.name = "New Game Button";

        loadGameButton = CreateMenuButton("LOAD GAME", mainView, topFigma: 631f, () => ShowView(View.Load));
        loadGameButton.name = "Load Game Button";

        // Figma 232:1035: rgba(21,35,55,0.8) rounded overlay over LOAD GAME when there are no saves.
        var overlayImg = Theme.Box("Disabled Overlay", loadGameButton.transform, DisabledOverlayFill, outline: null, radius: Theme.Fig(10f));
        overlayImg.rectTransform.Fill();
        overlayImg.raycastTarget = false;
        loadDisabledOverlay = overlayImg.gameObject;

        var exit = CreateMenuButton("EXIT", mainView, topFigma: 750f, OnExitClicked);
        exit.name = "Exit Button";
    }

    private void BuildSlotsView(RectTransform root)
    {
        slotsView = Theme.Node("Slots View", root).Fill();

        // Figma 232:1050: 893x150, top=80, centered.
        CreateTitleBanner(slotsView, topFigma: 80f);

        // Figma 232:1073: 181x109, left=1221 (49px right of banner edge 1172), top=94; 64px White text.
        float backCenterFromScreenCenter = Theme.Fig((1221f + 181f * 0.5f) - 720f);
        var back = CreateFigmaButton("BACK", slotsView, Theme.Fig(181f, 109f), Theme.Fig(64f), Theme.White, () => ShowView(View.Main));
        back.name = "Back Button";
        ((RectTransform)back.transform).Place(
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(backCenterFromScreenCenter, -Theme.Fig(94f)),
            Theme.Fig(181f, 109f));

        slotsSubtitle = Theme.Text("Subtitle", slotsView, font, Theme.Fig(34f), Theme.Cyan, TextAlignmentOptions.Center, "");
        slotsSubtitle.rectTransform.Place(
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -Theme.Fig(248f)),
            Theme.Fig(893f, 44f));

        // Figma 232:1052 / 232:1066: width=893, top=323, gap=47.
        cardsContainer = Theme.Node("Cards", slotsView).Place(
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -Theme.Fig(323f)),
            Theme.Fig(893f, 660f));
        cardsLayout = cardsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        cardsLayout.spacing = Theme.Fig(47f);
        cardsLayout.childAlignment = TextAnchor.UpperCenter;
        cardsLayout.childControlWidth = true;
        cardsLayout.childControlHeight = false;
        cardsLayout.childForceExpandWidth = true;
        cardsLayout.childForceExpandHeight = false;

        // Figma 232:1094: "You have no saved games", 64px White, top=480, centered.
        emptyText = Theme.Text("No Saves", slotsView, font, Theme.Fig(64f), Theme.White, TextAlignmentOptions.Center, "You have no saved games");
        emptyText.rectTransform.Place(
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -Theme.Fig(480f)),
            Theme.Fig(893f, 80f));
    }

    private void CreateTitleBanner(RectTransform parent, float topFigma)
    {
        float radius = Theme.Fig(10f);
        var banner = Theme.Box("Title Banner", parent, BannerFill, outline: null, radius: radius);
        banner.rectTransform.Place(
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -Theme.Fig(topFigma)),
            Theme.Fig(893f, 150f));
        Theme.HorizontalRules(banner.rectTransform, Theme.Cyan, Theme.Fig(3f));
        float ruleInset = Theme.Fig(5f);
        foreach (var name in new[] { "Top Rule", "Bottom Rule" })
        {
            var rule = banner.transform.Find(name) as RectTransform;
            if (rule == null) continue;
            rule.offsetMin = new Vector2(ruleInset, rule.offsetMin.y);
            rule.offsetMax = new Vector2(-ruleInset, rule.offsetMax.y);
        }
        var titleText = Theme.Text("Title", banner.transform, font, Theme.Fig(96f), Theme.White, TextAlignmentOptions.Center, "THE SUNKEN CONTRACT");
        titleText.rectTransform.Fill(Theme.Fig(16f), 0f, Theme.Fig(16f), 0f);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
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
        float radius = Theme.Fig(10f);
        float border = Theme.Fig(5f);
        var image = Theme.Box(caption, parent, ButtonIdleFill, Theme.Cyan, radius: radius, thickness: border);
        image.raycastTarget = true;
        image.rectTransform.sizeDelta = size;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
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

    private void ShowView(View view)
    {
        currentView = view;
        mainView.gameObject.SetActive(view == View.Main);
        slotsView.gameObject.SetActive(view != View.Main);

        if (view == View.Main)
        {
            RefreshLoadButtonState();
            return;
        }
        RebuildSlotCards(overwriteMode: view == View.Overwrite);
    }

    private void RefreshLoadButtonState()
    {
        bool hasSaves = SaveManager.HasAnyValidSave();
        loadGameButton.interactable = hasSaves;
        var hover = loadGameButton.GetComponent<DivingPrototype.OceanHoverPalette>();
        if (hover != null) hover.enabled = hasSaves;

        var fill = loadGameButton.targetGraphic as Image;
        var border = loadGameButton.transform.Find("Outline")?.GetComponent<Image>();
        var label = loadGameButton.GetComponentInChildren<TMP_Text>(true);
        if (fill != null) fill.color = ButtonIdleFill;
        if (border != null) border.color = Theme.Cyan;
        if (label != null) label.color = Theme.Cyan;
        if (loadDisabledOverlay != null) loadDisabledOverlay.SetActive(!hasSaves);
    }

    private void RebuildSlotCards(bool overwriteMode)
    {
        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
        {
            var child = cardsContainer.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        SaveSlotSummary[] summaries = SaveManager.GetAllSummaries();
        slotsSubtitle.text = overwriteMode
            ? "All 3 save slots are full. Choose a slot to overwrite:"
            : "";
        slotsSubtitle.gameObject.SetActive(overwriteMode);

        int count = 0;
        foreach (var slot in summaries)
        {
            if (!overwriteMode && !slot.HasSave && !slot.IsCorrupted) continue;
            count++;
        }

        // Exact Figma geometry for 1-2 cards (top=323, height=226, gap=47); slightly tightened for 3 cards so all 3 fit on screen.
        bool compact = count >= 3 || overwriteMode;
        float topFigma = overwriteMode ? 306f : compact ? 276f : 323f;
        float cardHeightFigma = compact ? 196f : 226f;
        float gapFigma = overwriteMode ? 28f : compact ? 32f : 47f;
        cardsContainer.anchoredPosition = new Vector2(0f, -Theme.Fig(topFigma));
        cardsLayout.spacing = Theme.Fig(gapFigma);

        int shown = 0;
        foreach (var slot in summaries)
        {
            if (!overwriteMode && !slot.HasSave && !slot.IsCorrupted) continue;
            CreateSlotCard(slot, overwriteMode, cardHeightFigma);
            shown++;
        }

        emptyText.gameObject.SetActive(!overwriteMode && shown == 0);
        cardsContainer.gameObject.SetActive(shown > 0);
    }

    private void CreateSlotCard(SaveSlotSummary slot, bool overwriteMode, float cardHeightFigma)
    {
        float radius = Theme.Fig(10f);
        float border = Theme.Fig(5f);
        float cardHeight = Theme.Fig(cardHeightFigma);

        // Figma 232:1052: 893x226, rgba(164,221,219,0.2), 5px Cyan border, radius 10.
        var card = Theme.Box($"Slot {slot.SlotIndex}", cardsContainer, ButtonIdleFill, Theme.Cyan, radius: radius, thickness: border);
        card.rectTransform.sizeDelta = new Vector2(Theme.Fig(893f), cardHeight);
        var cardLayout = card.gameObject.AddComponent<LayoutElement>();
        cardLayout.minHeight = cardHeight;
        cardLayout.preferredHeight = cardHeight;

        // Figma 232:1061: slot number at x=52 from card left, 64px White.
        var indexText = Theme.Text("Slot Number", card.transform, font, Theme.Fig(64f), Theme.White, TextAlignmentOptions.Center, slot.SlotIndex.ToString());
        indexText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Theme.Fig(34f), 0f), Theme.Fig(68f, 90f));

        string info;
        if (slot.IsCorrupted)
        {
            info = "Corrupted Save";
        }
        else if (!slot.HasSave)
        {
            info = "Empty Slot";
        }
        else
        {
            string suffix = slot.IsCompleted ? " (Done)" : slot.IsPendingResult ? " (Results)" : "";
            info = $"Day: {slot.Days}{suffix}\nCredits: {slot.Credits}";
        }

        // Figma 232:1062: left=149 from card left (428-279), 64px White, two lines.
        var infoText = Theme.Text("Info", card.transform, font, Theme.Fig(64f), slot.IsCorrupted ? Theme.Pink : Theme.White,
            TextAlignmentOptions.MidlineLeft, info);
        infoText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Theme.Fig(149f), 0f), Theme.Fig(410f, cardHeightFigma - 24f));

        // Figma 232:1063: 283x110, right inset = 41 (893 - (569 + 283)), 48px White text.
        int slotIndex = slot.SlotIndex;
        string caption = overwriteMode ? "OVERWRITE" : "LOAD GAME";
        Vector2 btnSize = Theme.Fig(283f, 110f);
        var action = CreateFigmaButton(caption, card.transform, btnSize, Theme.Fig(48f), Theme.White, () =>
        {
            if (overwriteMode) StartNewGame(slotIndex);
            else SaveManager.LoadSlotAndTransition(slotIndex);
        });
        action.name = $"{caption} Slot {slotIndex}";
        ((RectTransform)action.transform).Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Theme.Fig(41f), 0f), btnSize);
        if (!overwriteMode && slot.IsCorrupted)
            action.interactable = false;
    }

    private void OnNewGameClicked()
    {
        if (SceneChanger.IsTransitioning) return;
        int emptySlot = SaveManager.FindFirstEmptySlot();
        if (emptySlot > 0) StartNewGame(emptySlot);
        else ShowView(View.Overwrite);
    }

    private void StartNewGame(int slot)
    {
        if (SceneChanger.IsTransitioning) return;
        if (!SaveManager.StartNewGameInSlot(slot))
        {
            Debug.LogError($"Unable to initialize save slot {slot}; staying on Title Screen.");
            RefreshLoadButtonState();
            return;
        }
        SceneChanger.ChangeSceneWithFade(SaveManager.IntroductionScene);
    }

    private static void OnExitClicked()
    {
        Debug.Log("Game is exiting...");
        Application.Quit();
    }
}
