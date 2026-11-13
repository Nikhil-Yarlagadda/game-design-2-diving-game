#if UNITY_EDITOR
using System.Collections.Generic;
using DivingPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class GameStateDebugger : MonoBehaviour
{
    private const string InteriorScene = "Interior";
    private const string OceanScene = "Ocean Interactables";
    private static GameStateDebugger instance;
    private DayStart checkpoint;

    [SerializeField] private bool enableNextDayShortcut;
    [SerializeField] private bool enableResetDayShortcut;

    public const int LastDay = 20;
    public string CheckpointScene => checkpoint?.Day == GameState.days ? checkpoint.Scene : null;
    public string Status { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        instance = new GameObject("GameState Debugger").AddComponent<GameStateDebugger>();
        DontDestroyOnLoad(instance.gameObject);
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    internal static void ClearDayStart()
    {
        if (instance == null) return;
        instance.checkpoint = null;
        instance.Status = null;
    }

    internal static void CaptureDayStart(string scene)
    {
        if (instance != null) instance.checkpoint = new DayStart(scene);
    }

    internal static void CaptureFirstOceanEntry(string scene)
    {
        if (instance != null && instance.checkpoint == null && scene == OceanScene)
            CaptureDayStart(OceanScene);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (checkpoint == null && mode == LoadSceneMode.Single && IsInteriorScene(scene.name))
            CaptureDayStart(InteriorScene);
    }

    private static bool IsInteriorScene(string scene) => scene is InteriorScene or "Shared Space" or
        "Airlock" or "Storage Closet" or "Sleeping Quarters" or "Mineral Lab" or "Terminal" or "Shop";

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (enableResetDayShortcut && keyboard.leftBracketKey.wasPressedThisFrame)
            ResetCurrentDay();
        else if (enableNextDayShortcut && keyboard.rightBracketKey.wasPressedThisFrame)
            GoToDay(GameState.days + 1);
    }

    public bool GoToDay(int day)
    {
        if (!CanChangeDay()) return false;
        if (day < 1 || day > LastDay) return Reject($"Choose a day from 1 to {LastDay}.");
        if (day == GameState.days) return Reject("That day is already active.");

        string scene = day % 2 == 0 ? OceanScene : InteriorScene;
        return LoadDay(scene, () =>
        {
            GameState.days = day;
            GameState.tellurium = 0;
            GameState.npcsTalks.Clear();
            CaptureDayStart(scene);
        });
    }

    public bool ResetCurrentDay()
    {
        if (!CanChangeDay()) return false;
        if (CheckpointScene == null) return Reject("No start-of-day checkpoint for the current day.");
        var start = checkpoint;
        return LoadDay(start.Scene, start.Restore);
    }

    private bool CanChangeDay()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (!Application.isPlaying || SceneChanger.IsTransitioning || Time.timeScale <= 0f ||
            PauseManager.BlocksInput || InventoryButton.IsOpen ||
            (!IsInteriorScene(scene) && scene is not (OceanScene or "Dialogue" or "Result")))
            return Reject("Day commands are unavailable on this screen or while the game is paused.");

        var ocean = FindAnyObjectByType<OceanInteractions>();
        if (ocean != null && (ocean.ReturnConfirmationOpen || ocean.IsTransitioning))
            return Reject("Finish the excursion transition before changing the day.");
        return true;
    }

    private bool LoadDay(string scene, System.Action beforeLoad)
    {
        if (!SceneChanger.ChangeSceneWithFade(scene, beforeLoad))
            return Reject($"Could not load {scene}.");
        Status = null;
        return true;
    }

    private bool Reject(string message)
    {
        Status = message;
        return false;
    }

    public void SetCredits(int value)
    {
        if (SceneChanger.IsTransitioning) return;
        GameState.credits = value;
        RefreshResults();
        Status = null;
    }

    public void SetTellurium(int value)
    {
        if (SceneChanger.IsTransitioning) return;
        if (value < 0) { Reject("Tellurium must be nonnegative."); return; }
        var ocean = FindAnyObjectByType<OceanInteractions>();
        if (ocean != null) ocean.SetDebugTellurium(value);
        else GameState.tellurium = value;
        RefreshExcursionInventory();
        RefreshResults();
        Status = null;
    }

    public void SetItemCount(string name, int count, bool pending)
    {
        if (SceneChanger.IsTransitioning) return;
        if (string.IsNullOrWhiteSpace(name) || count < 0)
        {
            Reject("Enter an item name and a nonnegative count.");
            return;
        }
        var items = pending ? GameState.itemsBought : GameState.inventory;
        if (count == 0) items.Remove(name);
        else items[name] = count;
        if (!pending) RefreshExcursionInventory();
        Status = null;
    }

    public void SetCrewTalk(string id, bool talked)
    {
        if (SceneChanger.IsTransitioning) return;
        if (talked) GameState.npcsTalks.Add(id);
        else GameState.npcsTalks.Remove(id);
        Status = null;
    }

    public void SetDeliveryTutorialShown(bool shown)
    {
        if (SceneChanger.IsTransitioning) return;
        GameState.deliveryTutorialShown = shown;
        Status = null;
    }

    private static void RefreshExcursionInventory()
    {
        FindAnyObjectByType<OceanInteractions>()?.SyncDebugInventory();
        var inventory = FindAnyObjectByType<ExcursionInventory>();
        if (inventory != null && inventory.panel.activeInHierarchy) inventory.Refresh();
        FindAnyObjectByType<InteriorInventory>()?.Refresh();
    }

    private static void RefreshResults()
    {
        foreach (var result in FindObjectsByType<ResultsText>()) result.RefreshTotals();
    }

    private sealed class DayStart
    {
        public readonly string Scene;
        public readonly int Day = GameState.days;
        private readonly int credits = GameState.credits;
        private readonly int tellurium = GameState.tellurium;
        private readonly bool tutorialShown = GameState.deliveryTutorialShown;
        private readonly bool medicineDelivered = GameState.medicineDelivered;
        private readonly int shopDay = GameState.shopDay;
        private readonly Dictionary<string, int> dailyPurchases = new(GameState.dailyPurchases);
        private readonly GameState.DiveLoadout lastLoadout = GameState.lastLoadout;
        private readonly bool incapacitated = GameState.excursionIncapacitated;
        private readonly int oreLost = GameState.oreLost;
        private readonly Dictionary<string, GameState.MiningSpotState> mining = new(GameState.miningSpots);
        private readonly Dictionary<string, int> orders = new(GameState.itemsBought);
        private readonly Dictionary<string, int> inventory = new(GameState.inventory);
        private readonly HashSet<string> crewTalks = new(GameState.npcsTalks);

        public DayStart(string scene) => Scene = scene;

        public void Restore()
        {
            GameState.days = Day;
            GameState.credits = credits;
            GameState.tellurium = tellurium;
            GameState.deliveryTutorialShown = tutorialShown;
            GameState.medicineDelivered = medicineDelivered;
            GameState.shopDay = shopDay;
            GameState.dailyPurchases = new(dailyPurchases);
            GameState.lastLoadout = lastLoadout;
            GameState.excursionIncapacitated = incapacitated;
            GameState.oreLost = oreLost;
            GameState.miningSpots = new(mining);
            GameState.itemsBought = new(orders);
            GameState.inventory = new(inventory);
            GameState.npcsTalks = new(crewTalks);
        }
    }
}
#endif
