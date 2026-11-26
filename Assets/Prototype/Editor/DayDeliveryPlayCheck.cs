using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class DayDeliveryPlayCheck
    {
        public static async Task WaitForIdle()
        {
            for (int i = 0; i < 500 && SceneChanger.IsTransitioning; i++) await Task.Delay(20);
            FlashlightCheck.Require(!SceneChanger.IsTransitioning, "Scene transition completes");
        }

        internal static async Task BeginDive(OceanInteractions ocean)
        {
            if (ocean.GearReminderOpen)
            {
                ocean.ResolveGearReminder(equip: false);
                await Task.Delay(80);
            }
            var hud = UnityEngine.Object.FindAnyObjectByType<OceanHud>();
            var begin = hud.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Single(button => button.name == "Begin dive");
            FlashlightCheck.Require(begin.interactable, "Loadout fits and can begin");
            begin.onClick.Invoke();
            FlashlightCheck.Require(!ocean.LoadoutOpen, "Begin dive button commits the loadout");
            await Task.Delay(80);
        }

        internal static async Task WaitForGasFailure(OceanInteractions ocean)
        {
            ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity);
            for (int i = 0; i < 250 && ocean != null && !ocean.FailureScreenOpen; i++) await Task.Delay(100);
            FlashlightCheck.Require(ocean != null && ocean.FailureScreenOpen, "Gas failure opens the failure screen after the grace window");
            ocean.ResolveFailure();
            FlashlightCheck.Require(ocean == null || ocean.IsTransitioning, "Gas failure transitions after the failure screen");
            await WaitForIdle();
        }

        /// <summary>Opens stats through the authored pause button, then resumes the retained gameplay scene.</summary>
        private static async Task OpenStatsAndResume(Action whilePaused = null)
        {
            Scene gameplay = SceneManager.GetActiveScene();
            float timeScale = Time.timeScale;
            PauseManager.Pause();
            await WaitForOverlay("Pause Screen", loaded: true);
            var statsButton = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>()
                .Single(button => button.gameObject.scene.name == "Pause Screen" && button.name == "PlaytestStats");
            statsButton.onClick.Invoke();
            await WaitForOverlay("Playtest Stats", loaded: true);
            FlashlightCheck.Require(SceneManager.GetActiveScene() == gameplay && Time.timeScale == 0f,
                "Stats retains and pauses the gameplay scene");
            var report = UnityEngine.Object.FindAnyObjectByType<PlaytestStatsScreen>();
            var scroll = report.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            var page = scroll.transform.parent;
            var header = (RectTransform)page.Find("Header");
            var views = (RectTransform)page.Find("Views");
            var handle = scroll.verticalScrollbar.handleRect;
            FlashlightCheck.Require(header.rect.height <= 44.1f && views.rect.height <= 34.1f &&
                handle.offsetMin == Vector2.zero && handle.offsetMax == Vector2.zero,
                "Stats keeps its controls compact and its scroll thumb inside the track");
            whilePaused?.Invoke();
            var back = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>()
                .Single(button => button.gameObject.scene.name == "Playtest Stats" && button.name == "Back");
            back.onClick.Invoke();
            await WaitForOverlay("Playtest Stats", loaded: false);
            FlashlightCheck.Require(PauseManager.isPaused, "Back from stats keeps gameplay paused");
            PauseManager.Resume();
            await WaitForOverlay("Pause Screen", loaded: false);
            FlashlightCheck.Require(SceneManager.GetActiveScene() == gameplay && Time.timeScale == timeScale,
                "Resuming restores the same scene and its previous time scale");
        }

        /// <summary>Waits for an additive overlay and its completion callback to finish.</summary>
        private static async Task WaitForOverlay(string scene, bool loaded)
        {
            for (int attempt = 0; attempt < 100 && SceneManager.GetSceneByName(scene).isLoaded != loaded; attempt++)
                await Task.Delay(20);
            await Task.Delay(40);
            FlashlightCheck.Require(SceneManager.GetSceneByName(scene).isLoaded == loaded,
                $"Overlay {scene} reaches its expected load state");
        }

        [MenuItem("Diving Prototype/Check Day Deliveries In Play Mode")]
        public static async void Run()
        {
            try { Debug.Log(await Main()); }
            catch(Exception error) { Debug.LogException(error); }
        }

        // Exercises real scene transitions; restart Play after running this check.
        public static async Task<object> Main()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying,"Run in Play mode");
            using var state=new GameStateSnapshot();
            bool originalBackground=Application.runInBackground;
            int originalWidth=Screen.width, originalHeight=Screen.height;
            int checks=0;
            Action<bool,string> check=(ok,label)=> { FlashlightCheck.Require(ok,label); checks++; };
            OceanInteractions ocean=null;
            Func<bool,Task> loadOcean=async begin=> {
                await SceneManager.LoadSceneAsync("Ocean Interactables");
                await Task.Delay(100);
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                if (begin) await BeginDive(ocean);
            };
            Func<Task> sleepAndContinue = async () =>
            {
                int day = GameState.days;
                int bank = GameState.credits;
                WorldInteractions.NewDay();
                await WaitForIdle();
                check(SceneManager.GetActiveScene().name == "Result" && GameState.days == day &&
                    GameState.credits == bank, "Bed opens Results before advancing the day or settling credits");
                int samples = GameState.tellurium;
                if (day == 1)
                {
                    await OpenStatsAndResume();
                    check(GameState.days == day && GameState.credits == bank && GameState.tellurium == samples,
                        "Viewing stats preserves pending interior-day settlement");
                }
                check(day == 1 ? samples == 10 : samples is >= 5 and <= 10,
                    "Cole mines ten samples on day one and five to ten samples on these later days");
                int expense = Mathf.Clamp(bank + samples * 10 + 100, 0, 50);
                int total = bank + samples * 10 - expense;
                var results = UnityEngine.Object.FindObjectsByType<ResultsText>();
                check(results.Single(row => row.text == 1).GetComponent<TMP_Text>().text ==
                    $"Cole mined {samples} tellurium samples today = {samples * 10} Credits" &&
                    results.Single(row => row.text == 2).GetComponent<TMP_Text>().text == $"Total credits: {total}" &&
                    results.Single(row => row.text == 3).GetComponent<TMP_Text>().text == $"Daily expenses: {-expense} Credits",
                    "Interior Results displays Cole's earnings, expenses and the new bank balance");
                var continueButton = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>()
                    .Single(button => button.onClick.GetPersistentTarget(0) is ResultsText);
                continueButton.onClick.Invoke();
                continueButton.onClick.Invoke();
                await WaitForIdle();
                check(SceneManager.GetActiveScene().name == "Ocean Interactables" &&
                    GameState.days == day + 1 && GameState.credits == total && GameState.tellurium == 0,
                    "Continuing from interior Results settles credits once and starts the next dive");
            };
            async Task WaitPastAcknowledgement()
            {
                int frame = Time.frameCount;
                do { await Task.Delay(10); } while (Time.frameCount == frame);
            }
            Action<Vector2> jump=point=> {
                ocean.transform.position=point;
                var body=ocean.GetComponent<Rigidbody2D>(); body.position=point; body.linearVelocity=Vector2.zero;
                Physics2D.SyncTransforms();
                ocean.RefreshClosest();
            };
            try
            {
                Application.runInBackground=true;
                GameState.reset();
                check(GameState.days==1 && GameState.credits==0,"New game begins with 0 credits on interior day one");
                await sleepAndContinue();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(GameState.days==2,"First bed departure loads the day-two excursion");
                check(ocean.DeliveredItemCount==0 && ocean.targets.All(t=>t.kind!=OceanTarget.TargetKind.Item),"No free test pickups without purchases");
                float full = ocean.Hydrox.CurrentCapacity;
                await Task.Delay(200);
                check(ocean.LoadoutOpen && ocean.Hydrox.CurrentCapacity == full && ocean.Loadout.Machines == GameState.lastLoadout.Machines
                    && ocean.Loadout.Tank == ItemCatalog.LightTank, "First loadout uses the starting machine selection and preserves gas until confirmed");
                await BeginDive(ocean);
                check(ocean.GetComponent<OceanTutorial>().Mode == OceanTutorial.TutorialMode.Mining, "Beginning the dive starts the day-two mining tutorial");
                var knownSpot=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t.level==5);
                string knownId=knownSpot.PersistentId;
                string hiddenId=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t!=knownSpot).PersistentId;
                knownSpot.Reveal(Time.time);
                jump(knownSpot.transform.position);
                var training = ocean.GetComponent<OceanTutorial>();
                training.Acknowledge();
                training.Advance(0);
                training.Acknowledge();
                await WaitPastAcknowledgement();
                check(ocean.PlaceMachine(), "One carried machine is deployed before testing overnight damage");
                typeof(OceanTarget).GetField("nextTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(knownSpot,Time.time-150.1f);
                knownSpot.AdvanceMining(Time.time);
                check(knownSpot.DisplayedTier==5 && knownSpot.ActualTier==4,"Mining leaves the discovered tier stale before day two ends");
                check(!ocean.RequestEnterSubmarine(),"Day-two tutorial cannot be skipped");
                jump(ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Submarine).transform.position);
                training.Advance(0);
                training.OnScan(1); training.Advance(0);
                training.Acknowledge(); training.OnPlaced(); training.Advance(5);
                training.Acknowledge(); training.OnProduced(knownSpot); training.Advance(5);
                training.Acknowledge(); training.OnRetrieved(); training.Advance(5);
                training.Acknowledge(); training.OnPlaced(); training.Advance(5);
                training.Acknowledge(); training.Advance(30);
                training.Acknowledge(); training.Advance(30);
                await WaitPastAcknowledgement();
                check(training.IsMiningComplete,"Complete training milestones before testing later-day deliveries");
                jump(ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Submarine).transform.position);
                check(ocean.RequestEnterSubmarine() && ocean.ReturnConfirmationOpen && ocean.ConfirmReturn(),"First excursion ends through confirmation and Results");
                await WaitForIdle();
                check(SceneManager.GetActiveScene().name=="Result" && GameState.days==2,"Entering Results does not advance the day early");
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                await WaitForIdle();
                check(GameState.days==3 && GameState.credits==0,"Leaving excursion Results starts interior day three and settles credits");
                await SceneManager.LoadSceneAsync("Shop");
                await Task.Delay(100);
                var buy=UnityEngine.Object.FindAnyObjectByType<Shop>();
                var machine=UnityEngine.Object.FindObjectsByType<Item>().First(i=>i.title==GameState.MiningMachine);
                machine.onClicked();
                int machineCost = ItemCatalog.Find(machine.title).Price;
                GameState.credits=machineCost * 3 - 1;
                buy.buyClicked(); buy.buyClicked();
                int paidCredits=GameState.credits;
                buy.buyClicked();
                check(paidCredits==machineCost-1 && GameState.credits==paidCredits && GameState.itemsBought[GameState.MiningMachine]==2,"Shop queues two paid orders and rejects unaffordable purchase");
                var cameraItem = UnityEngine.Object.FindObjectsByType<Item>().Single(i => ItemCatalog.Find(i.title)?.Id == ItemCatalog.Camera);
                cameraItem.onClicked();
                GameState.credits = 2000;
                buy.buyClicked();
                int cameraCost = ItemCatalog.Find(ItemCatalog.Camera).Price;
                buy.buyClicked();
                check(GameState.credits == 2000 - cameraCost && GameState.itemsBought[ItemCatalog.Camera] == 1,
                    "A pending unique upgrade prevents a second purchase");
                GameState.itemsBought.Remove(ItemCatalog.Camera);
                check(ItemCatalog.RemainingStock(ItemCatalog.Find(ItemCatalog.Camera)) == 1, "A lost unique upgrade can be purchased again");
                var machineDefinition = ItemCatalog.Find(ItemCatalog.Machine);
                check(ItemCatalog.RemainingStock(machineDefinition) == 3 && ItemCatalog.RemainingStock(ItemCatalog.Find(ItemCatalog.Medicine)) == 0,
                    "Daily stock counts accepted purchases and medicine is unavailable before day seven");
                GameState.credits = paidCredits;
                await sleepAndContinue();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                await BeginDive(ocean);
                var tutorial=ocean.GetComponent<OceanTutorial>();
                check(tutorial.Mode==OceanTutorial.TutorialMode.Delivery && tutorial.Text==OceanTutorial.DeliveryMessage && GameState.deliveryTutorialShown,"First purchase excursion displays exact delivery message once");
                tutorial.Acknowledge();
                await WaitPastAcknowledgement();
                var restoredSpot=ocean.targets.First(t=>t.PersistentId==knownId);
                var hiddenSpot=ocean.targets.First(t=>t.PersistentId==hiddenId);
                check(restoredSpot.IsProspected && restoredSpot.DisplayedTier==5 && restoredSpot.ActualTier==3,"Day four retains the discovered tier and applies the overnight downgrade");
                check(restoredSpot.GetComponent<SpriteRenderer>().enabled && restoredSpot.highlight.enabled && restoredSpot.GetComponentInChildren<TMP_Text>().text.StartsWith("Tier 5"),"Restored spot sprite, marker and last-known tier are visible");
                check(!hiddenSpot.IsProspected && !hiddenSpot.GetComponent<SpriteRenderer>().enabled && !hiddenSpot.highlight.enabled,"Unprospected spots remain hidden on later days");
                check(restoredSpot.HasWreck && !restoredSpot.HasMachine && restoredSpot.WreckOre <= 48,
                    "Abandoned machine persists as a wreck with at most 80% of its ore");
                int wreckOre = restoredSpot.WreckOre, beforeSalvage = ocean.availableMachines;
                jump(restoredSpot.transform.position); ocean.Interact();
                check(!restoredSpot.HasWreck && ocean.Tellurium == wreckOre && ocean.availableMachines == beforeSalvage
                    && !GameState.miningSpots[knownId].HasWreck, "Salvage grants ore once and never returns a working machine");
                restoredSpot.Place(Time.time);
                restoredSpot.AdvanceMining(Time.time+195.1f);
                check(restoredSpot.ActualTier==3 && restoredSpot.StoredOre==19 && restoredSpot.DisplayedTier==5,"Mining a downgraded site uses the actual tier before re-prospecting");
                restoredSpot.Retrieve(Time.time+195.1f);
                restoredSpot.Reveal(Time.time);
                check(restoredSpot.DisplayedTier==3,"Re-prospecting refreshes the restored spot to its actual tier");
                var hud=UnityEngine.Object.FindAnyObjectByType<OceanHud>();
                var items=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                Vector2 center=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Submarine).transform.position;
                check(GameState.days==4 && items.Length==2 && ocean.RemainingDeliveryCount==2,"Day four receives exactly the two purchased packages");
                check(GameState.itemsBought[GameState.MiningMachine]==0,"Successful delivery consumes pending orders");
                check(items.All(t=>Vector2.Distance(t.transform.position,center)<=ocean.deliveryRadius && ocean.CanSpawn(t.transform.position,t)),"Deliveries are within adjustable radius and safely reachable");
                check(items.All(item => hud.minimapRoot.Find(item.name + " Minimap Marker").gameObject.activeSelf),
                    "Both deliveries appear on the existing minimap");
                check(items.SelectMany(t=>t.GetComponentsInChildren<SpriteRenderer>(true)).All(r=>r.sharedMaterial.shader.name=="Diving Prototype/Ocean Flashlight"),"Runtime delivery sprites are cached by flashlight");
                tutorial.Advance(1000);
                check(tutorial.IsVisible,"Delivery guidance has no time limit");
                var panel=(RectTransform)hud.status.transform.parent.Find("Excursion Tutorial");
                var text=panel.GetComponentInChildren<TMP_Text>();
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1920,1200),new Vector2Int(2560,1080)})
                {
                    PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"Delivery Tutorial Check");
                    await Task.Delay(200); Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
                    check(!text.isTextOverflowing && panel.gameObject.activeInHierarchy,"Delivery text fits "+size);
                    var corners=new Vector3[4]; panel.GetWorldCorners(corners);
                    check(corners.All(p=>ocean.oceanCamera.pixelRect.Contains(RectTransformUtility.WorldToScreenPoint(ocean.oceanCamera,p))),"Delivery panel fits gameplay viewport "+size);
                    check(panel.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g=>!g.raycastTarget),"Delivery panel does not intercept clicks");
                    System.IO.Directory.CreateDirectory("Temp/delivery-review");
                    ScreenCapture.CaptureScreenshot("Temp/delivery-review/delivery-"+size.x+"x"+size.y+".png");
                    await Task.Delay(100);
                }
                int machines=ocean.availableMachines, collectionEvents=0;
                ocean.DeliveryCollected += remaining=>collectionEvents++;
                var debugger = UnityEngine.Object.FindAnyObjectByType<GameStateDebugger>();
                debugger.SetTellurium(48 * ItemCatalog.Find(ItemCatalog.Ore).Stack);
                jump(items[0].transform.position); ocean.Interact();
                check(items[0].gameObject.activeSelf && ocean.RemainingDeliveryCount == 2 && ocean.availableMachines == machines,
                    "A full bag leaves the delivery and its counter untouched");
                debugger.SetTellurium(wreckOre);
                jump(items[0].transform.position); ocean.Interact();
                check(ocean.RemainingDeliveryCount==1 && tutorial.IsVisible && ocean.availableMachines==machines+1,"Partial collection keeps guidance visible and grants one machine");
                await Task.Delay(50);
                check(!hud.minimapRoot.Find(items[0].name+" Minimap Marker").gameObject.activeSelf,"Collected delivery marker disappears");
                // Isolate duplicate collection from legitimate interaction with the submarine.
                items[1].transform.position=new Vector2(0,-5);
                jump(items[1].transform.position); ocean.Interact();
                check(ocean.RemainingDeliveryCount == 0 && tutorial.Mode == OceanTutorial.TutorialMode.Markers && tutorial.Text.Contains("SPACE"),
                    "Final collection starts day-four marker guidance");
                tutorial.Acknowledge(); tutorial.Advance(30);
                tutorial.Acknowledge(); tutorial.Advance(30);
                tutorial.Acknowledge();
                await WaitPastAcknowledgement();
                check(tutorial.Mode == OceanTutorial.TutorialMode.Inventory, "Marker and depletion timeouts lead to inventory guidance");
                var inventory = UnityEngine.Object.FindAnyObjectByType<ExcursionInventory>();
                inventory.Open();
                tutorial.Advance(30);
                check(tutorial.Mode == OceanTutorial.TutorialMode.Inventory, "The inventory timer waits until the open panel is closed");
                inventory.Close();
                tutorial.Advance(9.99f);
                check(tutorial.Mode == OceanTutorial.TutorialMode.Inventory, "Deposit guidance waits ten seconds after closing inventory");
                tutorial.Advance(.02f);
                check(tutorial.Mode == OceanTutorial.TutorialMode.Deposit && tutorial.Text.Contains("cannot be re-equipped"), "Closing inventory leads to deposit guidance");
                tutorial.Acknowledge(); tutorial.Advance(30);
                await WaitPastAcknowledgement();
                check(!tutorial.IsVisible, "Day-four guidance ends thirty seconds after the deposit message");
                ocean.Interact();
                check(ocean.availableMachines==machines+2 && ocean.ItemsCollected==2 && collectionEvents==2,"Duplicate clicks cannot grant extra packages or events");
                jump(center);
                check(ocean.RequestEnterSubmarine() && ocean.ConfirmReturn(), "Return to the submarine with both deliveries");
                await WaitForIdle();
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                await WaitForIdle();
                check(GameState.days==5,"Second Results advances to interior day five");
                // Fund later orders explicitly; this check does not simulate mining income.
                GameState.credits=100;
                GameState.itemsBought[GameState.MiningMachine]=2;
                await sleepAndContinue();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                await BeginDive(ocean);
                check(ocean.targets.First(t=>t.PersistentId==knownId).DisplayedTier==3,"Refreshed tier is retained on another excursion");
                check(GameState.days==6 && ocean.RemainingDeliveryCount==2 && !ocean.GetComponent<OceanTutorial>().IsVisible,"Later orders arrive without repeating either tutorial");
                var missed=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                bool manualExpired=false;
                ocean.ExcursionEnded += ()=> manualExpired=ocean.RemainingDeliveryCount==0 && missed.All(t=>!t.gameObject.activeSelf);
                jump(center); ocean.Interact();
                check(ocean.ReturnConfirmationOpen && ocean.ConfirmReturn(),"Manual submarine return requires confirmation");
                await WaitForIdle();
                check(manualExpired && SceneManager.GetActiveScene().name=="Result","Manual submarine return expires uncollected deliveries");
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                await WaitForIdle();
                await sleepAndContinue();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(GameState.days==8 && ocean.DeliveredItemCount==0 && !ocean.GetComponent<OceanTutorial>().IsVisible,"Expired orders do not respawn on the next excursion");
                GameState.itemsBought[GameState.MiningMachine]=1;
                await loadOcean(true);
                missed=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                bool gasExpired=false;
                ocean.ExcursionEnded += ()=>gasExpired=ocean.RemainingDeliveryCount==0 && missed.All(t=>!t.gameObject.activeSelf);
                UnityEngine.Object.FindAnyObjectByType<GameStateDebugger>().SetTellurium(100);
                await WaitForGasFailure(ocean);
                check(gasExpired && SceneManager.GetActiveScene().name=="Result","Gas depletion expires deliveries through the same Results path");
                check(GameState.itemsBought[GameState.MiningMachine]==0,"Gas expiry does not requeue lost packages");
                check(GameState.excursionIncapacitated && GameState.oreLost >= 70 && GameState.oreLost <= 90
                    && GameState.tellurium + GameState.oreLost == 100, "Incapacitation deducts 70–90% of carried ore");
                int expectedCredits = GameState.credits + GameState.tellurium * 10 - 100;
                var results = UnityEngine.Object.FindAnyObjectByType<ResultsText>();
                results.onLeave(); results.onLeave(); await WaitForIdle();
                check(GameState.credits == expectedCredits && GameState.days == 9 && GameState.tellurium == 0,
                    "Double-clicking the results button settles ore, fee, and day exactly once");
                GameState.itemsBought["Camera Module"] = 1;
                GameState.itemsBought[GameState.MiningMachine] = 1;
                GameState.days = 10;
                await loadOcean(true);
                int initialMachines = ocean.availableMachines;
                int ownedMachines = GameState.inventory[GameState.MiningMachine];
                check(initialMachines <= ownedMachines, "Excursion starts with the selected owned machines");
                foreach (var delivery in ocean.targets.Where(t => t.kind == OceanTarget.TargetKind.Item).ToArray())
                {
                    jump(delivery.transform.position); ocean.Interact();
                }
                check(ocean.CarriedCount(ItemCatalog.Camera) == 1 && !GameState.inventory.ContainsKey(ItemCatalog.Camera)
                    && ocean.RemainingDeliveryCount == 0, "Collected camera remains in the excursion bag until return");
                check(ocean.availableMachines == initialMachines + 1 && GameState.inventory[GameState.MiningMachine] == ownedMachines,
                    "Machine delivery changes carried quantity without granting ownership at sea");
                check(!ocean.DropItem(ItemCatalog.Camera), "Camera cannot be dropped");
                jump(center);
                float pickupRange = ocean.interactionRange;
                ocean.interactionRange = 1.4f;
                check(!ocean.DropItem(ItemCatalog.Machine, 2) && ocean.availableMachines == initialMachines + 1,
                    "When no drop position clears the submarine, the stack stays in the bag");
                ocean.interactionRange = pickupRange;
                check(ocean.DropItem(ItemCatalog.Machine, 2) && ocean.availableMachines == initialMachines - 1, "Dropping a selected quantity removes two machines");
                var dropped = ocean.targets.Last();
                ocean.Interact();
                check(ocean.ReturnConfirmationOpen && dropped.gameObject.activeSelf,
                    "Dropping at the submarine center keeps normal return available");
                ocean.CancelReturn();
                await Task.Delay(80);
                jump(dropped.transform.position); ocean.Interact();
                check(ocean.availableMachines == initialMachines + 1 && ocean.RemainingDeliveryCount == 0, "Dropped stack can be collected without counting as a delivery");
                var mine = ocean.targets.First(t => t.kind == OceanTarget.TargetKind.MiningSpot && t.ActualTier > 0);
                mine.Reveal(Time.time); jump(mine.transform.position);
                check(ocean.PlaceMachine() && ocean.availableMachines == initialMachines, "Placement removes one carried machine");
                ocean.Interact();
                check(!mine.HasMachine && ocean.availableMachines == initialMachines + 1, "Retrieval returns machine to the excursion bag");
                jump(center);
                ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity);
                await Task.Delay(180);
                check(ocean.BreathingTimeLeft > 0f && !ocean.Incapacitated && ocean.GetComponent<SwimmerController>().BreathingSpeed < 1f,
                    "Empty tank leaves a playable grace window with a reduced speed cap");
                check(ocean.RequestEnterSubmarine() && ocean.ConfirmReturn(), "Submarine can be reached and confirmed during the grace window");
                await WaitForIdle();
                check(!GameState.excursionIncapacitated && GameState.oreLost == 0 && GameState.inventory[ItemCatalog.Camera] == 1
                    && GameState.inventory[GameState.MiningMachine] == ownedMachines + 1, "Grace-window return banks deliveries without a failure penalty");

                GameState.inventory[ItemCatalog.HeavyTank] = 1;
                GameState.inventory[ItemCatalog.Flippers] = 1;
                GameState.inventory[ItemCatalog.Stimulant] = 2;
                GameState.days = 12;
                await loadOcean(false);
                ocean.SetLoadout(new GameState.DiveLoadout { Tank = ItemCatalog.HeavyTank, Flippers = true, Camera = true, Stimulants = 2 });
                await BeginDive(ocean);
                var swimmer = ocean.GetComponent<SwimmerController>();
                var flashlight = ocean.GetComponent<FlashlightController>();
                check(ocean.Hydrox.MaxCapacity / ocean.Hydrox.ConsumptionRate == 480 && flashlight.EquippedConeBonus > 0f
                    && Mathf.Approximately(swimmer.EquipmentSpeed, 1.3f), "Selected tank, flippers and camera apply their effects");
                int weight = ocean.CarriedWeight;
                check(ocean.UseStimulant() && !ocean.UseStimulant() && ocean.CarriedCount(ItemCatalog.Stimulant) == 1
                    && ocean.CarriedWeight == weight - ItemCatalog.Find(ItemCatalog.Stimulant).Weight
                    && swimmer.EquipmentSpeed > 1.7f, "Stimulant gives a boost, removes its weight, and cannot be consumed during cooldown");
                float cooldown = ocean.StimulantTimeLeft, gas = ocean.Hydrox.CurrentCapacity;
                await OpenStatsAndResume(() => check(Mathf.Approximately(ocean.StimulantTimeLeft, cooldown) &&
                    Mathf.Approximately(ocean.Hydrox.CurrentCapacity, gas), "Stats freezes the stimulant and tank"));
                for (int i = 0; i < 50 && PauseManager.BlocksInput; i++) await Task.Delay(30);
                // Overweight speed is 1.75 - W; exceed it plus the largest flipper and stimulant bonuses at any capacity.
                int overweightOre = Mathf.CeilToInt((1.75f + .3f + 1f) * ocean.WeightCapacity / ItemCatalog.Find(ItemCatalog.Ore).Weight);
                UnityEngine.Object.FindAnyObjectByType<GameStateDebugger>().SetTellurium(overweightOre);
                check(swimmer.EquipmentSpeed == 0f, "Excessive weight clamps the final speed without reversing movement");
                check(ocean.DropItem(ItemCatalog.Ore, overweightOre) && swimmer.EquipmentSpeed > 0f, "Dropping weight restores swimming");
                jump(center); ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity); await Task.Delay(150);
                float grace = ocean.BreathingTimeLeft;
                check(ocean.RequestEnterSubmarine(), "Grace window permits return confirmation");
                await OpenStatsAndResume();
                check(ocean.ReturnConfirmationOpen && Mathf.Approximately(ocean.BreathingTimeLeft, grace),
                    "Stats preserves the return confirmation and its frozen grace window");
                check(ocean.ConfirmReturn(), "End equipment excursion");
                await WaitForIdle();
                GameState.days = 14;
                await loadOcean(false);
                check(ocean.Loadout.Tank == ItemCatalog.HeavyTank && ocean.Loadout.Camera && ocean.Loadout.Flippers
                    && ocean.Loadout.Stimulants == 1, "Next dive remembers the loadout and clamps consumables to remaining stock");
                GameState.reset(); GameState.days=2; GameState.credits=73;
                int startingOwnedMachines = GameState.inventory[GameState.MiningMachine];
                GameState.itemsBought["Camera Module"]=1; GameState.itemsBought[GameState.MiningMachine]=1;
                await loadOcean(true);
                int startingCarriedMachines = ocean.availableMachines;
                var retryTutorial = ocean.GetComponent<OceanTutorial>();
                retryTutorial.Acknowledge();
                await WaitPastAcknowledgement();
                foreach(var delivery in ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray())
                { jump(delivery.transform.position); ocean.Interact(); }
                check(ocean.CarriedCount(ItemCatalog.Camera) == 1 && ocean.availableMachines == startingCarriedMachines + 1, "Tutorial attempt collects generic and machine orders");
                ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot).Reveal(Time.time);
                await WaitForGasFailure(ocean);
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(ocean!=null && GameState.days==2 && GameState.credits==73,"Tutorial gas exhaustion retries without settlement");
                check(!GameState.inventory.ContainsKey(ItemCatalog.Camera) && GameState.inventory[GameState.MiningMachine] == startingOwnedMachines && GameState.tellurium==0,
                    "Retry restores the starting inventory without duplicating pickups");
                check(ocean.RemainingDeliveryCount==2 && GameState.itemsBought.Values.All(c=>c==0) && GameState.miningSpots.Count==0,"Retry reissues orders once and restores mining discoveries");
                await BeginDive(ocean);
                ocean.GetComponent<OceanTutorial>().Acknowledge();
                await WaitPastAcknowledgement();
                var tank = ocean.Hydrox;
                jump(center);
                tank.Refill();
                tank.Advance(tank.MaxCapacity * .849f / tank.ConsumptionRate);
                check(ocean.RequestEnterSubmarine() && ocean.ReturnConfirmationOpen && !ocean.ConfirmReturn(),
                    "An unfinished tutorial opens the submarine but blocks ending the excursion just above 15% Hydrox");
                tank.Advance(tank.MaxCapacity * .002f / tank.ConsumptionRate);
                check(ocean.ConfirmReturn(),"Less than 15% Hydrox permits ending an unfinished tutorial excursion");
                await WaitForIdle();
                check(SceneManager.GetActiveScene().name=="Result","Low-gas return reaches Results");
                foreach (var (bank, samples, expense, balance) in new[] { (-70, 0, -30, -100), (-100, 0, 0, -100), (-100, 7, -50, -80) })
                {
                    GameState.credits = bank;
                    GameState.tellurium = samples;
                    await SceneManager.LoadSceneAsync("Result");
                    var rows = UnityEngine.Object.FindObjectsByType<ResultsText>();
                    check(rows.Single(row => row.text == 3).GetComponent<TMP_Text>().text == $"Daily Expenses: {expense} Credits"
                        && rows.Single(row => row.text == 2).GetComponent<TMP_Text>().text == $"Total credits: {balance}",
                        $"Results shows the capped expense and balance for {bank} credits and {samples} samples");
                    rows[0].onLeave();
                    await WaitForIdle();
                    check(GameState.credits == balance, "Continuing commits the balance displayed in Results");
                }
                GameState.reset();
                check(GameState.credits==0 && GameState.days==1 && !GameState.deliveryTutorialShown && GameState.miningSpots.Count==0,"New game resets credits, tutorial history and mining discoveries");
                await loadOcean(false);
                check(ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.MiningSpot).All(t=>!t.IsProspected && t.ActualTier==t.level && !t.GetComponent<SpriteRenderer>().enabled),"New-game spots start hidden with fresh reserves");
                return new {checks,firstExcursionDay=2,firstDeliveryDay=4,manualExpiry=true,gasExpiry=true,layoutSizes=4};
            }
            finally
            {
                Application.runInBackground=originalBackground;
                PlayModeWindow.SetCustomRenderingResolution((uint)originalWidth,(uint)originalHeight,"Original Game View");
            }
        }
    }
}
