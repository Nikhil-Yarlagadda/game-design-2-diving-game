using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Displays and exports the current run from the pause menu or ending.</summary>
public sealed class PlaytestStatsScreen : MonoBehaviour
{
    private static readonly Color Background = new(.04f, .09f, .13f);
    private static readonly Color Panel = new(.08f, .16f, .21f);
    private static readonly Color Accent = new(.12f, .39f, .43f);
    private static readonly Color Foreground = new(.91f, .97f, .97f);
    private static readonly Color Muted = new(.68f, .79f, .81f);
    private readonly HashSet<int> expandedDays = new();
    private readonly List<Button> tabs = new();
    private RectTransform content;
    private ScrollRect scroll;
#if UNITY_WEBGL && !UNITY_EDITOR
    private TMP_Text status;
#endif
    private int view;

    /// <summary>Builds the overlay using the source screen's EventSystem.</summary>
    private void Awake()
    {
        var canvas = Rect(transform, "Stats Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var overlay = canvas.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 1100;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 640);
        scaler.matchWidthOrHeight = 1;

        var page = Rect(canvas, "Page", typeof(Image), typeof(VerticalLayoutGroup));
        Stretch(page);
        page.GetComponent<Image>().color = Background;
        var layout = page.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 22, 18);
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var header = Row(page, "Header", 44);
        var title = Label(header, "Playtest Stats", 32, Foreground);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
#if UNITY_WEBGL && !UNITY_EDITOR
        ActionButton(header, "Export Report", Export, 148);
#endif
        ActionButton(header, "Back", PauseManager.CloseStats, 76);

        var navigation = Row(page, "Views", 34);
        string[] views = { "Summary", "Day Log", "Quest & Choices" };
        for (int index = 0; index < views.Length; index++)
        {
            int tab = index;
            tabs.Add(ActionButton(navigation, views[index], () => ShowView(tab)));
        }

        var scrollArea = Rect(page, "Scroll view", typeof(ScrollRect), typeof(LayoutElement));
        scrollArea.GetComponent<LayoutElement>().flexibleHeight = 1;
        var viewport = Rect(scrollArea, "Viewport", typeof(Image), typeof(RectMask2D));
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-14, 0);
        viewport.GetComponent<Image>().color = Panel;
        scroll = scrollArea.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        scroll.verticalScrollbar = ScrollbarFor(scrollArea);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        content = Rect(viewport, "Report", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero;
        var entries = content.GetComponent<VerticalLayoutGroup>();
        entries.padding = new RectOffset(16, 16, 14, 14);
        entries.spacing = 10;
        entries.childControlWidth = true;
        entries.childControlHeight = true;
        entries.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

#if UNITY_WEBGL && !UNITY_EDITOR
        status = Label(page, "Export a JSON copy before quitting or starting a new game.", 15, Muted);
        status.gameObject.AddComponent<LayoutElement>().minHeight = 42;
#endif
        ShowView(0);
        EventSystem.current?.SetSelectedGameObject(tabs[0].gameObject);
    }

    /// <summary>Shows a report tab and optionally retains its scroll position.</summary>
    private void ShowView(int index, bool keepPosition = false)
    {
        float position = keepPosition ? scroll.verticalNormalizedPosition : 1f;
        view = index;
        for (int tab = 0; tab < tabs.Count; tab++)
            tabs[tab].GetComponent<Image>().color = tab == index ? Accent : Panel;

        for (int child = content.childCount - 1; child >= 0; child--)
        {
            var entry = content.GetChild(child).gameObject;
            entry.SetActive(false);
            Destroy(entry);
        }

        PlaytestReport report = GameState.Playtest;
        report.RefreshBalance(GameState.credits);
        switch (view)
        {
            case 0: ShowSummary(report); break;
            case 1: ShowDays(report); break;
            case 2: ShowQuest(report); break;
        }

        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = position;
    }

    /// <summary>Shows totals and the most recent recorded quest events.</summary>
    private void ShowSummary(PlaytestReport report)
    {
        var dives = report.days.SelectMany(day => day.attempts).ToArray();
        var transactions = report.days.SelectMany(day => day.transactions).ToArray();
        Section("Run");
        Entry($"Day {report.currentDay} reached   •   {(report.completed ? "Completed" : "In progress")}   •   {GameState.credits} credits");
        if (report.debuggerUsed) Entry("This run was modified with the editor debugger.");
        Section("Excursions");
        Entry($"{dives.Count(dive => dive.outcome == "Returned")} returned   •   " +
            $"{dives.Count(dive => dive.outcome == "Incapacitated")} incapacitated   •   " +
            $"{dives.Count(dive => dive.outcome == "Tutorial retry")} tutorial retries   •   " +
            $"{dives.Count(dive => dive.outcome == "In progress")} in progress");
        Entry($"{dives.Sum(dive => dive.produced)} produced   •   {dives.Sum(dive => dive.retrieved)} retrieved   •   " +
            $"{dives.Sum(dive => dive.salvaged)} salvaged   •   {dives.Sum(dive => dive.lost)} lost   •   " +
            $"{dives.Sum(dive => dive.credited)} credited");
        Section("Credits");
        Entry($"{transactions.Where(item => item.kind == "Excursion mining income").Sum(item => item.amount)} from excursions   •   " +
            $"{transactions.Where(item => item.kind == "Cole's mining income").Sum(item => item.amount)} from Cole");
        Entry($"{transactions.Where(item => item.kind == "Purchase").Sum(item => item.amount):+#;-#;0} net credits from purchases   •   " +
            $"{-transactions.Where(item => item.kind == "Daily expenses").Sum(item => item.amount)} in daily expenses");
        int bonus = transactions.Where(item => item.kind == "Credit bonus").Sum(item => item.amount);
        if (bonus > 0) Entry($"{bonus} bonus credits");
        Section("Recent quest activity");
        if (report.quest.Count == 0) Entry("No quest activity recorded yet.");
        foreach (var entry in report.quest.AsEnumerable().Reverse().Take(4)) Entry(QuestLine(entry));
    }

    /// <summary>Shows expandable day rows with attempts and credit transactions.</summary>
    private void ShowDays(PlaytestReport report)
    {
        for (int index = 0; index < report.days.Count; index++)
        {
            int dayIndex = index;
            var day = report.days[index];
            bool current = index == report.days.Count - 1 && !report.completed;
            string state = day.settled ? "Settled" : current ? "Current day" : "Unfinished";
            bool expanded = expandedDays.Contains(dayIndex);
            var button = ActionButton(content, $"{(expanded ? "−" : "+")}   Day {day.number}    •    {state}    •    {day.balance} credits", () =>
            {
                if (!expandedDays.Remove(dayIndex)) expandedDays.Add(dayIndex);
                ShowView(view, keepPosition: true);
            });
            button.GetComponent<LayoutElement>().minHeight = 38;
            if (!expanded) continue;

            Entry($"Opening balance {day.openingBalance}   •   {(day.settled ? "Closing" : "Recorded")} balance {day.balance}");
            foreach (var item in day.transactions)
            {
                string detail = item.kind == "Purchase" ? ItemCatalog.Find(item.item)?.Name ?? item.item : item.kind;
                if (item.samples > 0) detail += $" ({item.samples} samples)";
                Entry($"{detail}    {item.amount:+#;-#;0} credits");
            }

            for (int attempt = 0; attempt < day.attempts.Count; attempt++)
            {
                var dive = day.attempts[attempt];
                Section($"Attempt {attempt + 1}   •   {dive.outcome}");
                Entry($"{ItemCatalog.Find(dive.tank)?.Name ?? dive.tank}   •   {dive.machinesPacked} machines   •   " +
                    $"{dive.stimulantsPacked} stimulants   •   Camera {(dive.camera ? "yes" : "no")}   •   " +
                    $"Flippers {(dive.flippers ? "yes" : "no")}   •   {TimeSpan.FromSeconds(dive.activeSeconds):mm\\:ss} active");
                Entry($"{dive.produced} produced   •   {dive.retrieved} retrieved   •   {dive.salvaged} salvaged   •   " +
                    $"{dive.carriedNow} carried   •   {dive.deposited} deposited   •   {dive.lost} lost   •   {dive.credited} credited");
                Entry($"{dive.scans} scans   •   {dive.spotsRevealed} new spots   •   {dive.machinesDeployed} machines deployed   •   " +
                    $"{dive.machinesAbandoned} abandoned   •   {dive.deliveriesCollected} packages collected   •   {dive.deliveriesMissed} missed");
            }
        }
    }

    /// <summary>Shows quest milestones and choices in the order they occurred.</summary>
    private void ShowQuest(PlaytestReport report)
    {
        if (report.quest.Count == 0) Entry("No quest activity recorded yet.");
        foreach (var entry in report.quest) Entry(QuestLine(entry));
    }

    /// <summary>Formats a recorded event, omitting details when none were recorded.</summary>
    private static string QuestLine(PlaytestReport.QuestEvent entry) =>
        $"Day {entry.day}    •    {entry.kind}" +
        (string.IsNullOrEmpty(entry.subject) ? "" : $"    •    {entry.subject}") +
        (string.IsNullOrEmpty(entry.detail) ? "" : $"    •    {entry.detail}");

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>Requests a browser download of the supplied JSON report.</summary>
    [DllImport("__Internal")]
    private static extern int DownloadPlaytestReport(string json, string filename);

    /// <summary>Downloads the snapshot shown in the scene as a JSON file.</summary>
    private void Export()
    {
        PlaytestReport report = GameState.Playtest;
        report.RefreshBalance(GameState.credits);
        string filename = $"playtest-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{report.runId}.json";
        int requested = DownloadPlaytestReport(JsonUtility.ToJson(report, true), filename);
        status.text = requested != 0
            ? $"Download requested for {filename}"
            : "The browser download could not start. Try exporting again.";
    }
#endif

    /// <summary>Adds a report section heading.</summary>
    private void Section(string heading) => Label(content, heading, 23, Foreground);

    /// <summary>Adds a line of report data.</summary>
    private void Entry(string value) => Label(content, value, 18, Muted);

    /// <summary>Creates a fixed-height row with horizontally arranged controls.</summary>
    private static RectTransform Row(Transform parent, string name, float height)
    {
        var row = Rect(parent, name, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        var size = row.GetComponent<LayoutElement>();
        size.minHeight = height;
        size.preferredHeight = height;
        size.flexibleHeight = 0;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        return row;
    }

    /// <summary>Creates a text button sized by its parent layout.</summary>
    private static Button ActionButton(Transform parent, string text, Action action, float width = 0)
    {
        var rect = Rect(parent, text, typeof(Image), typeof(Button), typeof(LayoutElement));
        var element = rect.GetComponent<LayoutElement>();
        element.minWidth = width;
        element.flexibleWidth = width == 0 ? 1 : 0;
        var image = rect.GetComponent<Image>();
        image.color = Panel;
        var button = rect.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action());
        var label = Label(rect, text, 18, Foreground);
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform);
        return button;
    }

    /// <summary>Creates the report's vertical scroll control.</summary>
    private static Scrollbar ScrollbarFor(Transform parent)
    {
        var track = Rect(parent, "Scrollbar", typeof(Image), typeof(Scrollbar));
        track.anchorMin = new Vector2(1, 0);
        track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(-8, 0);
        track.offsetMax = Vector2.zero;
        track.GetComponent<Image>().color = Panel;
        var handleArea = Rect(track, "Sliding area");
        Stretch(handleArea);
        var handle = Rect(handleArea, "Handle", typeof(Image));
        Stretch(handle);
        handle.GetComponent<Image>().color = Accent;
        var scrollbar = track.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        return scrollbar;
    }

    /// <summary>Creates a non-interactive label for layout.</summary>
    private static TMP_Text Label(Transform parent, string value, int size, Color color)
    {
        var label = Rect(parent, "Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.richText = false;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>Creates a UI rect with the requested components and a local parent.</summary>
    private static RectTransform Rect(Transform parent, string name, params Type[] components)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        foreach (var component in components) rect.gameObject.AddComponent(component);
        return rect;
    }

    /// <summary>Fits a UI rect to all sides of its parent.</summary>
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
