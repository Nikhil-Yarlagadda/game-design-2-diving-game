using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class MachineMinimapPlayCheck
    {
        [MenuItem("Diving Prototype/Check Machine Minimap In Play Mode")]
        public static async void Run()
        {
            try { Debug.Log(await Main()); }
            catch(Exception error) { Debug.LogException(error); }
        }

        // Uses real placement/retrieval handlers; restart Play after this check.
        public static async Task<object> Main()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying,"Run in Play mode");
            using var state=new GameStateSnapshot();
            bool background=Application.runInBackground;
            SwimmerController swimmer=null;
            int checks=0;
            Action<bool,string> check=(ok,label)=>{FlashlightCheck.Require(ok,label);checks++;};
            try
            {
                Application.runInBackground=true;
                GameState.reset();GameState.days=4;GameState.itemsBought[GameState.MiningMachine]=1;
                await SceneManager.LoadSceneAsync("Ocean Interactables");await Task.Delay(150);
                var ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                var hud=UnityEngine.Object.FindAnyObjectByType<OceanHud>();
                var body=ocean.GetComponent<Rigidbody2D>();swimmer=ocean.GetComponent<SwimmerController>();swimmer.enabled=false;
                Action<Vector2> jump=p=>{body.position=p;ocean.transform.position=p;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();};
                var mines=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.MiningSpot).OrderBy(t=>t.transform.position.sqrMagnitude).ToArray();
                var home=ocean.targets.Single(t=>t.kind==OceanTarget.TargetKind.Submarine);
                Func<OceanTarget,UnityEngine.UI.Image> marker=t=>hud.minimapRoot.Find(t.name+" Machine Minimap Marker").GetComponent<UnityEngine.UI.Image>();
                check(mines.All(t=>!marker(t).gameObject.activeSelf),"Undiscovered and empty spots have no visible machine markers");
                int allocated=hud.minimapRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length;
                var near=mines.First();var far=mines.Last();
                near.Reveal(Time.time);far.Reveal(Time.time);await Task.Delay(50);
                check(!marker(near).gameObject.activeSelf && !marker(far).gameObject.activeSelf,"Prospecting alone does not create a visible machine marker");
                jump(near.transform.position);check(ocean.PlaceMachine(),"Place nearby machine");await Task.Delay(50);
                var nearMarker=marker(near);
                check(nearMarker.gameObject.activeSelf && nearMarker.sprite==hud.squareSprite,"Placement shows a square machine marker");
                check(nearMarker.color.r>nearMarker.color.g && nearMarker.color.g>nearMarker.color.b && nearMarker.rectTransform.localEulerAngles.z==0,"Machine marker is orange and unrotated");
                jump(far.transform.position);check(ocean.PlaceMachine(),"Place distant machine");
                jump(new Vector2(0,-5));await Task.Delay(100);
                float radius=Mathf.Min(hud.minimapRoot.rect.width,hud.minimapRoot.rect.height)*.5f-6;
                foreach(var mine in new[]{near,far})
                {
                    Vector2 offset=mine.transform.position-ocean.transform.position;
                    var image=marker(mine);
                    check(image.gameObject.activeSelf && Vector2.Distance(image.rectTransform.anchoredPosition,OceanHud.MinimapPosition(offset,hud.minimapRange,radius))<.01f,"Machine marker tracks relative bearing and distance: "+mine.name);
                    check(image.rectTransform.anchoredPosition.magnitude<=radius+.01f,"Machine marker remains inside the minimap: "+mine.name);
                }
                var farMarker=marker(far);
                check(Vector2.Distance(far.transform.position,ocean.transform.position)>hud.minimapRange && Mathf.Abs(farMarker.rectTransform.anchoredPosition.magnitude-radius)<.01f && Mathf.Abs(farMarker.color.a-.65f)<.001f,"Out-of-range machine clamps to the border with existing dimming");
                var homeMarker=hud.minimapRoot.Find(home.name+" Minimap Marker").GetComponent<UnityEngine.UI.Image>();
                check(homeMarker.color.b>homeMarker.color.r && Mathf.Abs(homeMarker.rectTransform.localEulerAngles.z-45)<.01f,"Submarine retains its cyan diamond");
                var item=ocean.targets.Single(t=>t.kind==OceanTarget.TargetKind.Item);
                var itemMarker=hud.minimapRoot.Find(item.name+" Minimap Marker").GetComponent<UnityEngine.UI.Image>();
                check(itemMarker.sprite==hud.circleSprite && itemMarker.color.r==1 && itemMarker.color.g==1 && itemMarker.color.b==1,"Purchased item retains its white dot");
                check(!nearMarker.raycastTarget && !farMarker.raycastTarget,"Machine markers do not intercept clicks");
                System.IO.Directory.CreateDirectory("Temp/machine-minimap-review");
                ScreenCapture.CaptureScreenshot("Temp/machine-minimap-review/two-machines.png");await Task.Delay(80);
                PauseManager.Pause();await Task.Delay(100);PauseManager.Resume();await Task.Delay(150);
                check(ReferenceEquals(marker(near),nearMarker) && ReferenceEquals(marker(far),farMarker) && nearMarker.gameObject.activeSelf && farMarker.gameObject.activeSelf,"Pause/resume preserves both markers and their instances");
                jump(far.transform.position);await Task.Delay(80);
                check(farMarker.rectTransform.anchoredPosition.magnitude<.01f && farMarker.color.a==1,"Returning within range restores position and full opacity");
                jump(near.transform.position);ocean.Interact();await Task.Delay(80);
                check(!near.HasMachine && near.IsProspected && !nearMarker.gameObject.activeSelf && farMarker.gameObject.activeSelf,"Retrieval hides only that machine marker while retaining discovery");
                check(ocean.PlaceMachine(),"Re-place the retrieved machine");await Task.Delay(80);
                check(ReferenceEquals(marker(near),nearMarker) && nearMarker.gameObject.activeSelf && hud.minimapRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length==allocated,"Re-placement reuses the existing marker without extra objects");
                await SceneManager.LoadSceneAsync("Ocean Interactables");await Task.Delay(150);
                ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();hud=UnityEngine.Object.FindAnyObjectByType<OceanHud>();
                check(ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.MiningSpot).All(t=>!t.HasMachine && !hud.minimapRoot.Find(t.name+" Machine Minimap Marker").gameObject.activeSelf),"Fresh scene has no phantom deployed-machine markers");
                return new{checks,orangeSquares=true,reusedMarkers=true,borderClamping=true};
            }
            finally
            {
                if(PauseManager.isPaused){PauseManager.Resume();await Task.Delay(150);}
                if(swimmer!=null)swimmer.enabled=true;
                Application.runInBackground=background;
            }
        }
    }
}
