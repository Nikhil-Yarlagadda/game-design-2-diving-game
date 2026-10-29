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
            Func<Task> loadOcean=async()=> {
                await SceneManager.LoadSceneAsync("Ocean Interactables");
                await Task.Delay(100);
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
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
                check(GameState.days==1 && GameState.credits==100,"New game begins with 100 credits on interior day one");
                WorldInteractions.NewDay();
                await SceneTransitionPlayCheck.WaitForIdle();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(GameState.days==2 && ocean.GetComponent<OceanTutorial>().Mode==OceanTutorial.TutorialMode.Mining,"First bed departure starts day-two mining tutorial");
                check(ocean.DeliveredItemCount==0 && ocean.targets.All(t=>t.kind!=OceanTarget.TargetKind.Item),"No free test pickups without purchases");
                var knownSpot=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t.level==5);
                string knownId=knownSpot.PersistentId;
                string hiddenId=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t!=knownSpot).PersistentId;
                knownSpot.Reveal(Time.time);
                knownSpot.Place(Time.time);
                typeof(OceanTarget).GetField("nextTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(knownSpot,Time.time-150.1f);
                knownSpot.AdvanceMining(Time.time);
                check(knownSpot.DisplayedTier==5 && knownSpot.ActualTier==4,"Mining leaves the discovered tier stale before day two ends");
                var training=ocean.GetComponent<OceanTutorial>();
                check(!ocean.RequestEnterSubmarine(),"Day-two tutorial cannot be skipped");
                ocean.transform.position+=Vector3.right*1.1f; training.Advance(0);
                training.OnScan(1); training.Advance(0);
                training.OnPlaced(); training.Advance(5);
                training.OnProduced(knownSpot); training.Advance(5);
                training.OnRetrieved(); training.Advance(5);
                training.OnPlaced(); training.Advance(5);
                training.Advance(30); training.Advance(30);
                check(training.IsMiningComplete,"Complete training milestones before testing later-day deliveries");
                check(ocean.RequestEnterSubmarine() && ocean.ReturnConfirmationOpen && ocean.ConfirmReturn(),"First excursion ends through confirmation and Results");
                await SceneTransitionPlayCheck.WaitForIdle();
                check(SceneManager.GetActiveScene().name=="Result" && GameState.days==2,"Entering Results does not advance the day early");
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                check(GameState.days==3 && GameState.credits==50,"Leaving Results starts interior day three and settles credits");
                await SceneManager.LoadSceneAsync("Shop");
                await Task.Delay(100);
                var buy=UnityEngine.Object.FindAnyObjectByType<BuyButton>();
                buy.onClicked(); buy.onClicked();
                int paidCredits=GameState.credits;
                buy.onClicked();
                check(paidCredits==10 && GameState.credits==10 && GameState.itemsBought[GameState.MiningMachine]==2,"Shop queues two paid orders and rejects unaffordable purchase");
                WorldInteractions.NewDay();
                await SceneTransitionPlayCheck.WaitForIdle();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                var restoredSpot=ocean.targets.First(t=>t.PersistentId==knownId);
                var hiddenSpot=ocean.targets.First(t=>t.PersistentId==hiddenId);
                check(restoredSpot.IsProspected && restoredSpot.DisplayedTier==5 && restoredSpot.ActualTier==4,"Day four retains discovery, stale tier and actual depletion");
                check(restoredSpot.GetComponent<SpriteRenderer>().enabled && restoredSpot.highlight.enabled && restoredSpot.GetComponentInChildren<TMP_Text>().text=="Tier 5","Restored spot sprite, marker and last-known tier are visible");
                check(!hiddenSpot.IsProspected && !hiddenSpot.GetComponent<SpriteRenderer>().enabled && !hiddenSpot.highlight.enabled,"Unprospected spots remain hidden on later days");
                check(!restoredSpot.HasMachine && restoredSpot.StoredOre==0,"Spot persistence does not recreate abandoned machines or stored ore");
                restoredSpot.Place(Time.time);
                restoredSpot.AdvanceMining(Time.time+195.1f);
                check(restoredSpot.ActualTier==3 && restoredSpot.StoredOre==39 && restoredSpot.DisplayedTier==5,"Partial tier depletion survives scene reload without refreshing displayed information");
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
                check(hud.minimapRoot.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.name.EndsWith(" Minimap Marker"))==3,"Both deliveries appear on the existing minimap");
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
                ocean.RequestEnterSubmarine(); ocean.ConfirmReturn(); await SceneTransitionPlayCheck.WaitForIdle();
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                check(GameState.days==5,"Second Results advances to interior day five");
                // Fund later orders explicitly; this check does not simulate mining income.
                GameState.credits=100;
                GameState.itemsBought[GameState.MiningMachine]=2;
                WorldInteractions.NewDay(); await SceneTransitionPlayCheck.WaitForIdle();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(ocean.targets.First(t=>t.PersistentId==knownId).DisplayedTier==3,"Refreshed tier is retained on another excursion");
                check(GameState.days==6 && ocean.RemainingDeliveryCount==2 && !ocean.GetComponent<OceanTutorial>().IsVisible,"Later orders arrive without repeating either tutorial");
                var missed=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                bool manualExpired=false;
                ocean.ExcursionEnded += ()=> manualExpired=ocean.RemainingDeliveryCount==0 && missed.All(t=>!t.gameObject.activeSelf);
                jump(center); ocean.Interact();
                check(ocean.ReturnConfirmationOpen && ocean.ConfirmReturn(),"Manual submarine return requires confirmation");
                await SceneTransitionPlayCheck.WaitForIdle();
                check(manualExpired && SceneManager.GetActiveScene().name=="Result","Manual submarine return expires uncollected deliveries");
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                WorldInteractions.NewDay(); await SceneTransitionPlayCheck.WaitForIdle();
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(GameState.days==8 && ocean.DeliveredItemCount==0 && !ocean.GetComponent<OceanTutorial>().IsVisible,"Expired orders do not respawn on the next excursion");
                GameState.itemsBought[GameState.MiningMachine]=1;
                await loadOcean();
                missed=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                bool gasExpired=false;
                ocean.ExcursionEnded += ()=>gasExpired=ocean.RemainingDeliveryCount==0 && missed.All(t=>!t.gameObject.activeSelf);
                ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity); await Task.Delay(150);
                await SceneTransitionPlayCheck.WaitForIdle();
                check(gasExpired && SceneManager.GetActiveScene().name=="Result","Gas depletion expires deliveries through the same Results path");
                check(GameState.itemsBought[GameState.MiningMachine]==0,"Gas expiry does not requeue lost packages");
                GameState.reset();
                check(GameState.credits==100 && GameState.days==1 && !GameState.deliveryTutorialShown && GameState.miningSpots.Count==0,"New game resets credits, tutorial history and mining discoveries");
                await loadOcean();
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
