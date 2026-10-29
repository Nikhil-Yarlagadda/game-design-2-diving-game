using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class OceanPausePlayCheck
    {
        [MenuItem("Diving Prototype/Check Ocean Pause In Play Mode")]
        public static async void Run()
        {
            try { Debug.Log(await Main()); }
            catch (Exception error) { Debug.LogException(error); }
        }

        // Exercises live scene/menu input; restart Play afterward.
        public static async Task<object> Main()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying,"Run in Play mode");
            using var state=new GameStateSnapshot();
            bool background=Application.runInBackground;
            float timeScale=Time.timeScale;
            var behavior=InputSystem.settings.backgroundBehavior;
            var routing=InputSystem.settings.editorInputBehaviorInPlayMode;
            var originalKeyboard=Keyboard.current; var originalMouse=Mouse.current;
            Keyboard keyboard=null; Mouse mouse=null;
            Key[] held=Array.Empty<Key>(); bool down=false; Vector2 pointer=Vector2.zero;
            int checks=0;
            Action<bool,string> check=(ok,label)=>{FlashlightCheck.Require(ok,label);checks++;};
            Action deliver=()=>{
                if(InputState.currentUpdateType!=InputUpdateType.Dynamic||keyboard==null)return;
                keyboard.MakeCurrent();mouse.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Left,down));
            };
            Action keep=()=>{if(InputState.currentUpdateType==InputUpdateType.Dynamic&&keyboard!=null){keyboard.MakeCurrent();mouse.MakeCurrent();}};
            Func<Key[],Task> keys=async k=>{held=k;await Task.Delay(100);};
            Func<Task> clickResume=async()=>{
                var menu=SceneManager.GetSceneByName("Pause Screen");
                var button=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    .Single(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="Resume"));
                var rect=button.GetComponent<RectTransform>();var canvas=button.GetComponentInParent<Canvas>();
                pointer=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
                down=false;await Task.Delay(50);down=true;await Task.Delay(80);down=false;await Task.Delay(150);
            };
            try
            {
                Application.runInBackground=true;Time.timeScale=1;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                GameState.reset();GameState.days=2;GameState.credits=73;GameState.itemsBought[GameState.MiningMachine]=2;
                await SceneManager.LoadSceneAsync("Ocean Interactables");await Task.Delay(150);
                var ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                var body=ocean.GetComponent<Rigidbody2D>();var tutorial=ocean.GetComponent<OceanTutorial>();
                Action<Vector2> jump=p=>{body.position=p;ocean.transform.position=p;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();};
                var mine=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot);
                jump(mine.transform.position);mine.Reveal(Time.time);
                check(ocean.PlaceMachine(),"Deploy a machine through the real interaction path");
                mine.AdvanceMining(Time.time+30);
                typeof(OceanTarget).GetField("nextTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(mine,Time.time+100);
                var package=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Item);
                jump(package.transform.position);ocean.Interact();
                check(ocean.RemainingDeliveryCount==1 && !package.gameObject.activeSelf,"Collect one package before pausing");
                jump(new Vector2(0,-5));tutorial.Advance(0);tutorial.OnScan(1);tutorial.Advance(0);
                keyboard=InputSystem.AddDevice<Keyboard>("PauseCheckKeyboard");mouse=InputSystem.AddDevice<Mouse>("PauseCheckMouse");
                pointer=ocean.oceanCamera.pixelRect.center;
                InputSystem.onBeforeUpdate+=deliver;InputSystem.onAfterUpdate+=keep;
                await keys(new[]{Key.E});
                check(ocean.IsProspecting,"Start a scan before pausing");
                await keys(new[]{Key.E,Key.Escape});await keys(new[]{Key.E});
                check(PauseManager.isPaused && Time.timeScale==0 && SceneManager.GetSceneByName("Pause Screen").isLoaded,"Esc opens the paused menu");
                check(SceneManager.GetSceneByName("Ocean Interactables").isLoaded && ReferenceEquals(ocean,UnityEngine.Object.FindAnyObjectByType<OceanInteractions>()),"The original ocean remains loaded as the same instance");
                check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e=>e.isActiveAndEnabled)==1,"Exactly one active UI event system while paused");
                check(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled)==1,"Pause menu does not add a second audio listener");
                float gas=ocean.Hydrox.CurrentCapacity,clock=Time.time,progress=ocean.ScanProgress,reading=tutorial.VisibleSeconds;
                Vector2 position=body.position;int ore=mine.StoredOre,stage=tutorial.Stage,machines=ocean.availableMachines;
                check(stage>1,"Pause fixture has progressed beyond the initial tutorial step");
                await Task.Delay(400);
                check(ocean.Hydrox.CurrentCapacity==gas && Time.time==clock && ocean.ScanProgress==progress,"Gas, simulation clock and active scan freeze");
                check(body.position==position && mine.StoredOre==ore && tutorial.Stage==stage && tutorial.VisibleSeconds==reading,"Position, machine ore and tutorial progress freeze");
                check(!ocean.PlaceMachine() && !ocean.RequestEnterSubmarine(),"Paused ocean rejects direct gameplay requests");
                System.IO.Directory.CreateDirectory("Temp/ocean-pause-review");
                ScreenCapture.CaptureScreenshot("Temp/ocean-pause-review/pause-menu.png");await Task.Delay(80);
                await clickResume();
                check(!PauseManager.isPaused && Time.timeScale==1 && !SceneManager.GetSceneByName("Pause Screen").isLoaded,"Resume button removes only the pause scene");
                check(ReferenceEquals(ocean,UnityEngine.Object.FindAnyObjectByType<OceanInteractions>()) && body.position==position,"Resume keeps the original ocean and position");
                check(ocean.availableMachines==machines && mine.HasMachine && mine.StoredOre==ore,"Resume keeps machine inventory, deployment and stored ore");
                check(ocean.RemainingDeliveryCount==1 && !package.gameObject.activeSelf && ocean.ItemsCollected==1,"Resume preserves collected and uncollected packages");
                check(ocean.IsProspecting && ocean.ScanProgress>=progress && tutorial.Stage==stage,"Active scan and tutorial continue from their previous progress");
                check(GameState.days==2 && GameState.credits==73 && SceneManager.sceneCount==1,"Pause does not settle, advance the day or leave a duplicate scene");
                await keys(Array.Empty<Key>());
                check(!ocean.IsProspecting,"Releasing E after resume cancels the retained scan normally");
                typeof(OceanTarget).GetField("nextTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(mine,Time.time+.1f);
                await Task.Delay(250);
                check(ocean.Hydrox.CurrentCapacity<gas && mine.StoredOre>ore,"Gas and mining advance after resuming");
                for(int i=0;i<2;i++)
                {
                    await keys(new[]{Key.Escape});await keys(Array.Empty<Key>());
                    check(PauseManager.isPaused && SceneManager.sceneCount==2,"One Esc press pauses exactly once: cycle "+i);
                    await keys(new[]{Key.Escape});await keys(Array.Empty<Key>());
                    check(!PauseManager.isPaused && SceneManager.sceneCount==1 && ReferenceEquals(ocean,UnityEngine.Object.FindAnyObjectByType<OceanInteractions>()),"Esc resumes the same ocean without duplicate toggles: cycle "+i);
                }
                check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e=>e.isActiveAndEnabled)==1,"Ocean input routing is restored after repeated pauses");
                await SceneManager.LoadSceneAsync("Shop");await Task.Delay(100);
                await keys(new[]{Key.Escape});await keys(Array.Empty<Key>());
                check(PauseManager.isPaused && SceneManager.sceneCount==1 && SceneManager.GetActiveScene().name=="Pause Screen","Non-ocean pause retains its existing single-scene behavior");
                await clickResume();
                check(!PauseManager.isPaused && SceneManager.GetActiveScene().name=="Shop" && Time.timeScale==1,"Non-ocean resume still returns to its original scene");
                return new{checks,oceanInstancePreserved=true,escapeCycles=2,actualResumeButton=true};
            }
            finally
            {
                held=Array.Empty<Key>();
                if(PauseManager.isPaused){PauseManager.Resume();await Task.Delay(150);}
                InputSystem.onBeforeUpdate-=deliver;InputSystem.onAfterUpdate-=keep;
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
                originalKeyboard?.MakeCurrent();originalMouse?.MakeCurrent();
                InputSystem.settings.backgroundBehavior=behavior;InputSystem.settings.editorInputBehaviorInPlayMode=routing;
                Application.runInBackground=background;Time.timeScale=timeScale;
            }
        }
    }
}
