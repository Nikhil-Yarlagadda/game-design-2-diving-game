using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class ReturnConfirmationPlayCheck
    {
        [MenuItem("Diving Prototype/Check Return Confirmation In Play Mode")]
        public static async void Run()
        {
            try { Debug.Log(await Main()); }
            catch (Exception error) { Debug.LogException(error); }
        }

        // Exercises real UI clicks and scene changes; restart Play afterward.
        public static async Task<object> Main()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying, "Run in Play mode");
            using var state = new GameStateSnapshot();
            bool background = Application.runInBackground;
            float timeScale = Time.timeScale;
            int width = Screen.width, height = Screen.height;
            var behavior = InputSystem.settings.backgroundBehavior;
            var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
            var originalKeyboard = Keyboard.current;
            var originalMouse = Mouse.current;
            Keyboard keyboard = null; Mouse mouse = null;
            Key[] held = Array.Empty<Key>();
            bool down = false; Vector2 pointer = Vector2.zero;
            OceanInteractions ocean = null;
            int checks = 0;
            Action<bool,string> check = (ok,label) => { FlashlightCheck.Require(ok,label); checks++; };
            Action deliver = () => {
                if (InputState.currentUpdateType != InputUpdateType.Dynamic || keyboard == null) return;
                keyboard.MakeCurrent(); mouse.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));
                InputSystem.QueueStateEvent(mouse,new MouseState {position=pointer}.WithButton(MouseButton.Left,down));
            };
            Action keep = () => {
                if (InputState.currentUpdateType == InputUpdateType.Dynamic && keyboard != null) { keyboard.MakeCurrent(); mouse.MakeCurrent(); }
            };
            Func<Vector2,Task> click = async position => {
                pointer=position; down=false; await Task.Delay(50);
                down=true; await Task.Delay(70); down=false; await Task.Delay(100);
            };
            try
            {
                Application.runInBackground=true;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Time.timeScale=1;
                GameState.reset(); GameState.days=4; GameState.credits=73;
                await SceneManager.LoadSceneAsync("Ocean Interactables"); await Task.Delay(150);
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                var hud=UnityEngine.Object.FindAnyObjectByType<OceanHud>();
                var tutorial=ocean.GetComponent<OceanTutorial>();
                var body=ocean.GetComponent<Rigidbody2D>();
                var light=ocean.GetComponent<FlashlightController>();
                keyboard=InputSystem.AddDevice<Keyboard>("ReturnCheckKeyboard");
                mouse=InputSystem.AddDevice<Mouse>("ReturnCheckMouse");
                InputSystem.onBeforeUpdate+=deliver; InputSystem.onAfterUpdate+=keep;
                PlayModeWindow.SetCustomRenderingResolution(1280,720,"Return Confirmation Check");
                pointer=ocean.oceanCamera.pixelRect.center; await Task.Delay(100);
                var mine=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot);
                mine.Reveal(Time.time); mine.Place(Time.time);
                typeof(OceanTarget).GetField("nextTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(mine,Time.time+.5f);
                // Establish a cooldown before pausing; it must remain frozen too.
                check(ocean.Prospect(Time.time),"Prepare scan cooldown"); ocean.CompleteProspect(Time.time+3);
                await click(ocean.oceanCamera.WorldToScreenPoint(ocean.transform.position));
                check(ocean.ReturnConfirmationOpen && SceneManager.GetActiveScene().name=="Ocean Interactables", "One world click opens confirmation without ending the excursion");
                check(Time.timeScale==0 && ocean.GetComponent<SwimmerController>().MovementLocked,"Modal pauses simulation and locks movement");
                var overlay=hud.status.transform.parent.Find("Return Confirmation");
                var buttons=overlay.GetComponentsInChildren<UnityEngine.UI.Button>();
                check(overlay.gameObject.activeInHierarchy && buttons.Length==2,"Exactly two modal buttons are shown");
                check(overlay.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("1 mining machine")),"Modal warns about the deployed machine");
                Func<string,Vector2> buttonPoint=name=> {
                    var rect=buttons.Single(b=>b.name==name).GetComponent<RectTransform>();
                    return RectTransformUtility.WorldToScreenPoint(hud.status.canvas.worldCamera,rect.TransformPoint(rect.rect.center));
                };
                float gas=ocean.Hydrox.CurrentCapacity, clock=Time.time, cooldown=ocean.CooldownRemaining, reading=tutorial.VisibleSeconds;
                int ore=mine.StoredOre; Vector2 position=body.position, aim=light.AimDirection;
                held=new[]{Key.Escape,Key.Enter,Key.Space,Key.Tab,Key.W,Key.E};
                await click(new Vector2(20,20)); await Task.Delay(350);
                check(ocean.ReturnConfirmationOpen && SceneManager.GetActiveScene().name=="Ocean Interactables" && !PauseManager.isPaused,"Esc, keyboard input and outside click do not close or replace the modal");
                check(ocean.Hydrox.CurrentCapacity==gas && Time.time==clock && ocean.CooldownRemaining==cooldown,"Gas, simulation time and cooldown freeze");
                check(mine.StoredOre==ore && tutorial.VisibleSeconds==reading,"Mining and tutorial time freeze");
                check(body.position==position && light.AimDirection==aim,"Position and flashlight aim freeze");
                check(!ocean.Prospect(Time.time) && !ocean.PlaceMachine() && !ocean.RequestEnterSubmarine(),"Gameplay and repeated return requests are blocked");
                held=Array.Empty<Key>();
                System.IO.Directory.CreateDirectory("Temp/return-confirmation-review");
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1920,1200),new Vector2Int(2560,1080)})
                {
                    PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"Return Confirmation Check");
                    await Task.Delay(120); Canvas.ForceUpdateCanvases();
                    foreach(var text in overlay.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); check(!text.isTextOverflowing,"Modal text fits "+size+": "+text.name); }
                    var dialog=(RectTransform)overlay.Find("Dialog"); var corners=new Vector3[4]; dialog.GetWorldCorners(corners);
                    check(corners.All(p=>ocean.oceanCamera.pixelRect.Contains(RectTransformUtility.WorldToScreenPoint(hud.status.canvas.worldCamera,p))),"Dialog fits gameplay viewport "+size);
                }
                PlayModeWindow.SetCustomRenderingResolution(1280,720,"Return Confirmation Check"); await Task.Delay(100);
                ScreenCapture.CaptureScreenshot("Temp/return-confirmation-review/one-machine.png"); await Task.Delay(100);
                await click(buttonPoint("Continue Exploring"));
                check(!ocean.ReturnConfirmationOpen && Time.timeScale==1 && !ocean.GetComponent<SwimmerController>().MovementLocked,"Continue button restores time and controls");
                await Task.Delay(500);
                check(!ocean.ReturnConfirmationOpen && SceneManager.GetActiveScene().name=="Ocean Interactables","Cancel click does not pass through and reopen confirmation");
                check(ocean.Hydrox.CurrentCapacity<gas && mine.StoredOre>ore,"Gas and mining resume after cancellation");
                mine.Retrieve(Time.time);
                await click(ocean.oceanCamera.WorldToScreenPoint(ocean.transform.position));
                check(ocean.ReturnConfirmationOpen && overlay.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="No mining machines will be left behind."),"Zero-machine case still confirms without a loss warning");
                await click(buttonPoint("Continue Exploring"));
                mine.Place(Time.time);
                await click(ocean.oceanCamera.WorldToScreenPoint(ocean.transform.position));
                check(ocean.DeployedMachineCount==1 && ocean.ReturnConfirmationOpen,"Reopened warning uses the current deployed count");
                var previous=ocean;
                await click(buttonPoint("End Excursion"));
                await SceneTransitionPlayCheck.WaitForIdle();
                check(SceneManager.GetActiveScene().name=="Result" && Time.timeScale==1,"End button restores time and loads Results");
                check(GameState.days==4 && GameState.credits==73 && !previous.ConfirmReturn(),"Confirmation cannot settle twice or advance the day early");
                return new {checks,layoutSizes=4,actualButtonClicks=true,simulationFrozen=true};
            }
            finally
            {
                if(ocean!=null && ocean.ReturnConfirmationOpen) ocean.CancelReturn();
                InputSystem.onBeforeUpdate-=deliver; InputSystem.onAfterUpdate-=keep;
                if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
                if(mouse!=null) InputSystem.RemoveDevice(mouse);
                originalKeyboard?.MakeCurrent(); originalMouse?.MakeCurrent();
                InputSystem.settings.backgroundBehavior=behavior;
                InputSystem.settings.editorInputBehaviorInPlayMode=routing;
                Application.runInBackground=background; Time.timeScale=timeScale;
                PlayModeWindow.SetCustomRenderingResolution((uint)width,(uint)height,"Original Game View");
            }
        }
    }
}
