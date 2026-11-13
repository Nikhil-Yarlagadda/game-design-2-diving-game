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
            for (int i = 0; i < 250 && ocean != null && !ocean.IsTransitioning; i++) await Task.Delay(100);
            FlashlightCheck.Require(ocean == null || ocean.IsTransitioning, "Gas failure transitions after the grace window");
            await WaitForIdle();
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
                WorldInteractions.NewDay();
                await WaitForIdle();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(GameState.days==2 && ocean.GetComponent<OceanTutorial>().Mode==OceanTutorial.TutorialMode.Mining,"First bed departure starts day-two mining tutorial");
                check(ocean.DeliveredItemCount==0 && ocean.targets.All(t=>t.kind!=OceanTarget.TargetKind.Item),"No free test pickups without purchases");
                float full = ocean.Hydrox.CurrentCapacity;
                await Task.Delay(200);
                check(ocean.LoadoutOpen && ocean.Hydrox.CurrentCapacity == full && ocean.Loadout.Machines == 4
                    && ocean.Loadout.Tank == ItemCatalog.LightTank, "First loadout starts with four machines and does not consume gas before confirming");
                await BeginDive(ocean);
                var knownSpot=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t.level==5);
                string knownId=knownSpot.PersistentId;
                string hiddenId=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t!=knownSpot).PersistentId;
                knownSpot.Reveal(Time.time);
                jump(knownSpot.transform.position);
                var training = ocean.GetComponent<OceanTutorial>();
                training.Advance(0);
                check(ocean.PlaceMachine(), "One carried machine is deployed before testing overnight damage");
                typeof(OceanTarget).GetField("nextTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(knownSpot,Time.time-150.1f);
                knownSpot.AdvanceMining(Time.time);
                check(knownSpot.DisplayedTier==5 && knownSpot.ActualTier==4,"Mining leaves the discovered tier stale before day two ends");
                check(!ocean.RequestEnterSubmarine(),"Day-two tutorial cannot be skipped");
                jump(ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Submarine).transform.position);
                training.Advance(0);
                training.OnScan(1); training.Advance(0);
                training.OnPlaced(); training.Advance(5);
                training.OnProduced(knownSpot); training.Advance(5);
                training.OnRetrieved(); training.Advance(5);
                training.OnPlaced(); training.Advance(5);
                training.Advance(30); training.Advance(30);
                check(training.IsMiningComplete,"Complete training milestones before testing later-day deliveries");
                jump(ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Submarine).transform.position);
                check(ocean.RequestEnterSubmarine() && ocean.ReturnConfirmationOpen && ocean.ConfirmReturn(),"First excursion ends through confirmation and Results");
                await WaitForIdle();
                check(SceneManager.GetActiveScene().name=="Result" && GameState.days==2,"Entering Results does not advance the day early");
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                await WaitForIdle();
                check(GameState.days==3 && GameState.credits==-50,"Leaving Results starts interior day three and settles credits");
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
                WorldInteractions.NewDay();
                await WaitForIdle();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                await BeginDive(ocean);
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
                var tutorial=ocean.GetComponent<OceanTutorial>();
                var hud=UnityEngine.Object.FindAnyObjectByType<OceanHud>();
                var items=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                Vector2 center=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Submarine).transform.position;
                check(GameState.days==4 && items.Length==2 && ocean.RemainingDeliveryCount==2,"Day four receives exactly the two purchased packages");
                check(GameState.itemsBought[GameState.MiningMachine]==0,"Successful delivery consumes pending orders");
                check(items.All(t=>Vector2.Distance(t.transform.position,center)<=ocean.deliveryRadius && ocean.CanSpawn(t.transform.position,t)),"Deliveries are within adjustable radius and safely reachable");
                check(items.All(item => hud.minimapRoot.Find(item.name + " Minimap Marker").gameObject.activeSelf),
                    "Both deliveries appear on the existing minimap");
                check(items.SelectMany(t=>t.GetComponentsInChildren<SpriteRenderer>(true)).All(r=>r.sharedMaterial.shader.name=="Diving Prototype/Ocean Flashlight"),"Runtime delivery sprites are cached by flashlight");
                check(tutorial.Mode==OceanTutorial.TutorialMode.Delivery && tutorial.Text==OceanTutorial.DeliveryMessage && GameState.deliveryTutorialShown,"First purchase excursion displays exact delivery message once");
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
                check(ocean.RemainingDeliveryCount==0 && tutorial.Mode==OceanTutorial.TutorialMode.Depletion && tutorial.Text==OceanTutorial.DepletionMessage,"Final collection switches to day-four depletion guidance");
                tutorial.Advance(30);
                check(!tutorial.IsVisible,"Day-four depletion guidance dismisses after thirty seconds");
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
                WorldInteractions.NewDay(); await WaitForIdle();
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
                WorldInteractions.NewDay(); await WaitForIdle();
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
                check(ocean.DropItem(ItemCatalog.Machine, 2) && ocean.availableMachines == initialMachines - 1, "Dropping a selected quantity removes two machines");
                var dropped = ocean.targets.Last();
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
                PauseManager.Pause(); await Task.Delay(250);
                check(Mathf.Approximately(ocean.StimulantTimeLeft, cooldown) && Mathf.Approximately(ocean.Hydrox.CurrentCapacity, gas),
                    "Pause freezes the stimulant and tank");
                PauseManager.Resume();
                for (int i = 0; i < 50 && PauseManager.BlocksInput; i++) await Task.Delay(30);
                UnityEngine.Object.FindAnyObjectByType<GameStateDebugger>().SetTellurium(110);
                check(swimmer.EquipmentSpeed == 0f, "Excessive weight clamps the final speed without reversing movement");
                check(ocean.DropItem(ItemCatalog.Ore, 110) && swimmer.EquipmentSpeed > 0f, "Dropping weight restores swimming");
                jump(center); ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity); await Task.Delay(150);
                float grace = ocean.BreathingTimeLeft;
                check(ocean.RequestEnterSubmarine(), "Grace window permits return confirmation");
                await Task.Delay(220);
                check(Mathf.Approximately(ocean.BreathingTimeLeft, grace), "Return confirmation freezes the grace window");
                check(ocean.ConfirmReturn(), "End equipment excursion");
                await WaitForIdle();
                GameState.days = 14;
                await loadOcean(false);
                check(ocean.Loadout.Tank == ItemCatalog.HeavyTank && ocean.Loadout.Camera && ocean.Loadout.Flippers
                    && ocean.Loadout.Stimulants == 1, "Next dive remembers the loadout and clamps consumables to remaining stock");
                GameState.reset(); GameState.days=2; GameState.credits=73;
                GameState.itemsBought["Camera Module"]=1; GameState.itemsBought[GameState.MiningMachine]=1;
                await loadOcean(true);
                foreach(var delivery in ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray())
                { jump(delivery.transform.position); ocean.Interact(); }
                check(ocean.CarriedCount(ItemCatalog.Camera) == 1 && ocean.availableMachines == 5, "Tutorial attempt collects generic and machine orders");
                ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot).Reveal(Time.time);
                await WaitForGasFailure(ocean);
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(ocean!=null && ocean.GetComponent<OceanTutorial>().Stage==1 && GameState.days==2 && GameState.credits==73,"Tutorial gas exhaustion retries without settlement");
                check(!GameState.inventory.ContainsKey(ItemCatalog.Camera) && GameState.inventory[GameState.MiningMachine]==4 && GameState.tellurium==0,
                    "Retry restores the starting inventory without duplicating pickups");
                check(ocean.RemainingDeliveryCount==2 && GameState.itemsBought.Values.All(c=>c==0) && GameState.miningSpots.Count==0,"Retry reissues orders once and restores mining discoveries");
                var tank = ocean.Hydrox;
                await BeginDive(ocean);
                jump(center);
                tank.Refill();
                tank.Advance(tank.MaxCapacity * .85f / tank.ConsumptionRate);
                check(!ocean.RequestEnterSubmarine(),"An unfinished tutorial blocks return at exactly 15% Hydrox");
                tank.Advance(.1f);
                check(ocean.RequestEnterSubmarine() && ocean.ReturnConfirmationOpen && ocean.ConfirmReturn(),"Less than 15% Hydrox permits ending an unfinished tutorial excursion");
                await WaitForIdle();
                check(SceneManager.GetActiveScene().name=="Result","Low-gas return reaches Results");
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
