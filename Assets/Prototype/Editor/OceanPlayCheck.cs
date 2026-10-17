using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class OceanPlayCheck
    {
        [MenuItem("Diving Prototype/Check Ocean In Play Mode")]
        public static async void Run()
        {
            try { Debug.Log(await Main()); }
            catch (Exception error) { Debug.LogException(error); }
        }

        // This check changes session inventory; restart Play for a clean manual test.
        public static async Task<object> Main()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying, "Enter Play in Ocean Interactables first");
            var ocean = UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
            FlashlightCheck.Require(ocean != null && !ocean.InsideSubmarine && ocean.ItemsCollected == 0, "Start a fresh ocean session");
            var body = ocean.GetComponent<Rigidbody2D>();
            var swimmer = ocean.GetComponent<SwimmerController>();
            var hud = UnityEngine.Object.FindAnyObjectByType<OceanHud>();
            var originalKeyboard = Keyboard.current;
            var originalMouse = Mouse.current;
            var oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            bool oldRunInBackground = Application.runInBackground;
            int originalWidth=Screen.width,originalHeight=Screen.height;
            float originalTimeScale=Time.timeScale;
            string originalInterior=ocean.interiorScene;
            Keyboard keyboard = null;
            Mouse mouse = null;
            Action keepTestKeyboardCurrent = () =>
            {
                if (InputState.currentUpdateType == InputUpdateType.Dynamic && keyboard != null && keyboard.added) keyboard.MakeCurrent();
                if (InputState.currentUpdateType == InputUpdateType.Dynamic && mouse != null && mouse.added) mouse.MakeCurrent();
            };
            int checks = 0;
            var deliveries = new System.Collections.Generic.List<Action>();
            Action<bool,string> require = (value,label) => { FlashlightCheck.Require(value,label); checks++; };
            Action<Vector2> jump = p => { body.position = p; ocean.transform.position = p; body.linearVelocity = Vector2.zero; ocean.oceanCamera.GetComponent<CenteredCamera>().SendMessage("LateUpdate"); Physics2D.SyncTransforms(); };
            // Deliver to the game update, so an Editor input update cannot consume a key edge first.
            Action<Key[]> keys = k =>
            {
                Action deliver = null;
                deliver = () =>
                {
                    if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                    InputSystem.onBeforeUpdate -= deliver;
                    deliveries.Remove(deliver);
                    if (keyboard.added) InputSystem.QueueStateEvent(keyboard,new KeyboardState(k));
                };
                deliveries.Add(deliver);
                InputSystem.onBeforeUpdate += deliver;
            };
            Action<bool,bool,Vector2?> pointer = (left,right,position) =>
            {
                Action deliver = null;
                deliver = () =>
                {
                    if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                    InputSystem.onBeforeUpdate -= deliver;
                    deliveries.Remove(deliver);
                    var state = new MouseState { position = position ?? ocean.oceanCamera.pixelRect.center };
                    state = state.WithButton(MouseButton.Left,left).WithButton(MouseButton.Right,right);
                    if (mouse.added) InputSystem.QueueStateEvent(mouse,state);
                };
                deliveries.Add(deliver);
                InputSystem.onBeforeUpdate += deliver;
            };
            Func<Task> click = async () => {pointer(true,false,null);await Task.Delay(150);pointer(false,false,null);await Task.Delay(150);};
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                keyboard = InputSystem.AddDevice<Keyboard>("OceanCheckKeyboard");
                mouse = InputSystem.AddDevice<Mouse>("OceanCheckMouse");
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                InputSystem.onAfterUpdate += keepTestKeyboardCurrent;
                ocean.Hydrox.Refill();
                float initialHydrox=ocean.Hydrox.CurrentCapacity;
                int frame = Time.frameCount;
                await Task.Delay(200);
                require(Time.frameCount > frame, "Real player loop is advancing");
                require(ocean.Hydrox.CurrentCapacity < initialHydrox,"Hydrox consumes during ocean gameplay");
                Time.timeScale=0;
                float pausedCapacity=ocean.Hydrox.CurrentCapacity;
                await Task.Delay(200);
                require(ocean.Hydrox.CurrentCapacity==pausedCapacity,"Paused game time does not consume Hydrox");
                Time.timeScale=originalTimeScale;
                require(ocean.oceanMap.GetComponentsInChildren<SpriteRenderer>(true)
                    .All(r=>r.sharedMaterial.shader.name=="Diving Prototype/Ocean Flashlight"),
                    "Play mode replaces preview materials with gameplay flashlight");
                require(hud.GetComponent<Canvas>().sortingOrder>=100,"HUD draws above terrain sprites");
                jump(new Vector2(0,-5)); keys(new[]{Key.D}); await Task.Delay(2900);
                float openSpeed = body.linearVelocity.magnitude;
                require(Mathf.Abs(openSpeed-2.5f)<.05f, "Open-water speed uses teammate tuning: " + openSpeed);
                keys(Array.Empty<Key>()); await Task.Delay(150);
                jump(new Vector2(-26f,17.2f)); keys(new[]{Key.D}); await Task.Delay(1550);
                float kelpSpeed = body.linearVelocity.magnitude;
                require(swimmer.SpeedMultiplier==.5f && Mathf.Abs(kelpSpeed-1.25f)<.05f, "Kelp passes at half speed");
                keys(Array.Empty<Key>()); await Task.Delay(150);
                foreach(string name in new[]{"Rock 12","Chasm 4"})
                {
                    var solid=ocean.obstacles.Single(c=>c.name==name);
                    Vector2 from=new Vector2(solid.bounds.min.x-4,solid.bounds.center.y);
                    var hit=Physics2D.RaycastAll(from,Vector2.right,solid.bounds.size.x+8).First(h=>h.collider==solid);
                    jump(hit.point-Vector2.right*.7f);
                    keys(new[]{Key.D});await Task.Delay(3100);
                    var contacts=new ContactPoint2D[16];int count=body.GetContacts(contacts);
                    require(contacts.Take(count).Any(c=>c.collider==solid||c.otherCollider==solid), name+" stops swimming by actual contact");
                    require(!solid.OverlapPoint(body.position), name+" cannot be entered");
                    keys(Array.Empty<Key>());await Task.Delay(150);
                }
                jump(new Vector2(0,-1.5f));
                keys(new[]{Key.Z});await Task.Delay(80);keys(Array.Empty<Key>());await Task.Delay(150);
                require(ocean.CooldownRemaining==0f && hud.minimapRoot.gameObject.activeInHierarchy, "Short Z no longer scans");
                var scanMine=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot);
                jump(scanMine.transform.position);
                pointer(false,true,null);await Task.Delay(150);
                require(ocean.CooldownRemaining>1f,"Right mouse press scans immediately");
                require(ocean.targets.Any(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t.highlight.enabled), "Prospect highlight is rendered");
                await Task.Delay(2100);
                require(ocean.CooldownRemaining==0f,"Held right mouse does not rescan after cooldown");
                pointer(false,false,null);await Task.Delay(100);pointer(false,true,null);await Task.Delay(150);
                require(ocean.CooldownRemaining>1f,"Fresh right mouse press scans again");
                pointer(false,false,null);await Task.Delay(100);jump(new Vector2(0,-1.5f));
                require(hud.minimapRoot.gameObject.activeInHierarchy,"Minimap stays active without holding a key");
                var mapTexts=hud.minimapRoot.GetComponentsInChildren<TMPro.TMP_Text>();
                require(mapTexts.Length==2 && mapTexts.Any(t=>t.text=="N") && mapTexts.Any(t=>t.text.StartsWith("Submarine")),"North and one submarine summary only");
                require(hud.minimapRoot.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.name.EndsWith(" Minimap Marker"))==ocean.itemCount+1,"Submarine plus configured item points");
                require(hud.minimapRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g=>!g.raycastTarget),"Minimap does not capture gameplay mouse input");
                var items=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                require(items.All(t=>ocean.CanSpawn(t.transform.position,t)), "All randomized pickups have clear spacing");
                var oldPositions=items.Select(t=>t.transform.position).ToArray();
                require(items.All(t=>ocean.IsReachable(t.transform.position)),"All pickups connect to player-accessible water");
                var marker=(RectTransform)hud.minimapRoot.Find(items[0].name+" Minimap Marker");
                float mapRadius=hud.minimapRoot.rect.width*.5f-6;
                foreach(float distance in new[]{0f,12.5f,24.9f,25f,50f})
                {
                    Vector2 offset=Vector2.one.normalized*distance;
                    items[0].transform.position=ocean.transform.position+(Vector3)offset;
                    await Task.Delay(100);
                    require(Vector2.Distance(marker.anchoredPosition,OceanHud.MinimapPosition(offset,25,mapRadius))<.05f,"Live minimap distance and diagonal bearing: "+distance);
                    require(marker.anchoredPosition.magnitude<=mapRadius+.01f,"Marker remains within circular border: "+distance);
                }
                items[0].transform.position=ocean.transform.position+Vector3.up*10;await Task.Delay(100);
                var northPosition=marker.anchoredPosition;
                pointer(false,false,ocean.oceanCamera.WorldToScreenPoint(ocean.transform.position+Vector3.right*5));await Task.Delay(150);
                require(Vector2.Distance(northPosition,marker.anchoredPosition)<.01f && marker.anchoredPosition.y>0,"North-up minimap does not rotate with aiming");
                require(((RectTransform)hud.minimapRoot.Find("Minimap Facing")).anchoredPosition.x>9,"Central facing indicator follows aim");
                var subMarker=hud.minimapRoot.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i=>i.name=="Submarine Minimap Marker");
                require(subMarker.color.b>subMarker.color.r && marker.GetComponent<UnityEngine.UI.Image>().color.r==1,"Cyan submarine and white item markers");
                await Task.Delay(Mathf.CeilToInt(ocean.CooldownRemaining*1000)+150);
                Vector2 beforeSwim=body.position;
                keys(new[]{Key.Z,Key.D});await Task.Delay(650);
                require(hud.minimapRoot.gameObject.activeInHierarchy && ocean.CooldownRemaining==0 && body.position.x>beforeSwim.x+.05f,"Swimming works with Z held; Z neither scans nor gates minimap");
                keys(Array.Empty<Key>());await Task.Delay(100);jump(new Vector2(0,-1.5f));
                for(int i=0;i<items.Length;i++)items[i].transform.position=oldPositions[i];
                await Task.Delay(100);
                require(hud.minimapRoot.gameObject.activeInHierarchy && !hud.status.text.Contains("Hold Z"),"Release Z preserves minimap and removes legacy HUD hint");
                var mine=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot&&t.level==5);
                jump(mine.transform.position); keys(new[]{Key.X,Key.Space});await Task.Delay(150);keys(Array.Empty<Key>());await Task.Delay(150);
                require(!mine.HasMachine && ocean.availableMachines==3,"Old X and Space bindings are inactive");
                await click();
                require(mine.HasMachine && ocean.availableMachines==2, "Left click deploys exactly one machine");
                var placed = typeof(OceanTarget).GetField("<PlacedAt>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                placed.SetValue(mine,Time.time-60.1f);
                await click();
                require(!mine.HasMachine && ocean.Tellurium==5 && ocean.availableMachines==3, "Left click retrieves machine and one minute of tier five yield");
                pointer(false,false,null);await Task.Delay(100);
                require(mine.Retrieve(Time.time)==0 && ocean.Tellurium==5,"Duplicate retrieval cannot award resources");
                // Isolate the duplicate-click check from legitimate pickup of a neighboring target.
                var item=items.First(t=>ocean.targets.All(other=>other==t || !other.isActiveAndEnabled
                    || Vector2.Distance(other.transform.position,t.transform.position)>ocean.interactionRange+.5f));
                require(ocean.CanSpawn(item.transform.position,item), "Random pickup has free placement");
                jump(item.transform.position);await click();
                require(ocean.ItemsCollected==1 && !item.gameObject.activeSelf, "Left click collects pickup once");
                require(!hud.minimapRoot.Find(item.name+" Minimap Marker").gameObject.activeSelf,"Collected pickup disappears from minimap");
                await click();require(ocean.ItemsCollected==1,"Repeated left click cannot collect the same pickup twice");
                var remaining=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item && t.isActiveAndEnabled).Select(t=>t.transform.position).ToArray();
                mine.Place(Time.time);
                jump(Vector2.zero);await click();
                for(int i=0;i<40&&!ocean.InsideSubmarine;i++)await Task.Delay(100);
                require(!hud.minimapRoot.gameObject.activeInHierarchy,"Minimap hides inside submarine");
                require(ocean.InsideSubmarine && SceneManager.GetActiveScene().name=="Interior" && !body.simulated, "Left click enters actual Interior scene");
                require(!hud.hydroxRoot.gameObject.activeInHierarchy,"Hydrox bar hides inside submarine");
                float interiorCapacity=ocean.Hydrox.CurrentCapacity;await Task.Delay(200);
                require(ocean.Hydrox.CurrentCapacity==interiorCapacity,"Hydrox consumption stops inside submarine");
                require(!ocean.RequestEnterSubmarine(),"Duplicate entry is rejected inside submarine");
                await click();
                for(int i=0;i<40&&ocean.InsideSubmarine;i++)await Task.Delay(100);
                require(hud.minimapRoot.gameObject.activeInHierarchy,"Minimap returns on ocean re-entry");
                require(hud.hydroxRoot.gameObject.activeInHierarchy && ocean.Hydrox.CurrentCapacity>ocean.Hydrox.MaxCapacity-2,"Return refills Hydrox and restores its bar");
                require(!ocean.InsideSubmarine && SceneManager.GetActiveScene().name=="Ocean Interactables" && body.simulated && swimmer.enabled, "Left click returns to playable ocean");
                require(ocean.ItemsCollected==1 && ocean.Tellurium==5 && mine.HasMachine, "Inventory and placed mine survive scene round trip");
                require(remaining.SequenceEqual(ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item && t.isActiveAndEnabled).Select(t=>t.transform.position)), "Random items do not respawn on return");
                mine.Retrieve(Time.time);
                jump(new Vector2(0,-1.5f));
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1920,1200),new Vector2Int(2560,1080)})
                {
                    PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"Ocean Demo Check");
                    await Task.Delay(250);
                    var camera=ocean.oceanCamera;
                    require(Screen.width==size.x && Screen.height==size.y,"Actual Game View resolution "+size);
                    require(Mathf.Abs(camera.pixelWidth/(float)camera.pixelHeight-16f/9f)<.002f,"Camera fits 16:9 "+size);
                    var corners=camera.ViewportToWorldPoint(new Vector3(1,1,10))-camera.ViewportToWorldPoint(new Vector3(0,0,10));
                    require(Mathf.Abs(corners.y-18)<.01f && Mathf.Abs(corners.x-18f*16f/9f)<.01f,"Same world view "+size);
                    var canvas=hud.GetComponent<Canvas>();
                    require(canvas.worldCamera==camera && Mathf.Abs(canvas.pixelRect.width-camera.pixelRect.width)<1,"HUD stays in viewport "+size);
                    require(Mathf.Abs(hud.GetComponent<RectTransform>().rect.width-1280)<1 && Mathf.Abs(hud.GetComponent<RectTransform>().rect.height-720)<1,"Same UI reference layout "+size);
                    var mapCorners=new Vector3[4];hud.minimapRoot.GetWorldCorners(mapCorners);
                    require(mapCorners.All(c=>camera.pixelRect.Contains(RectTransformUtility.WorldToScreenPoint(camera,c))),"Minimap stays inside fitted viewport "+size);
                    require(Mathf.Abs(hud.minimapRoot.rect.width-180)<.01f,"Stable reference minimap size "+size);
                    var barCorners=new Vector3[4];hud.hydroxRoot.GetWorldCorners(barCorners);
                    require(barCorners.All(c=>camera.pixelRect.Contains(RectTransformUtility.WorldToScreenPoint(camera,c))),"Hydrox bar stays inside fitted viewport "+size);
                    require(Mathf.Abs(hud.hydroxFill.anchorMax.x-ocean.Hydrox.NormalizedFill)<.01f,"Live Hydrox fill tracks remaining capacity "+size);
                }
                PlayModeWindow.SetCustomRenderingResolution(1920,1200,"Ocean Demo Check");await Task.Delay(250);
                jump(mine.transform.position);
                var aim=ocean.GetComponent<FlashlightController>().AimDirection;
                pointer(true,true,new Vector2(960,5));await Task.Delay(150);pointer(false,false,new Vector2(960,5));await Task.Delay(150);
                require(!mine.HasMachine && ocean.CooldownRemaining==0,"Black bars ignore left and right click");
                require(Vector2.Distance(aim,ocean.GetComponent<FlashlightController>().AimDirection)<.001f,"Black bars preserve flashlight aim");
                var aimPoint=(Vector2)ocean.oceanCamera.WorldToScreenPoint(ocean.transform.position+Vector3.right*4);
                pointer(false,false,aimPoint);await Task.Delay(150);
                require(Vector2.Distance(Vector2.right,ocean.GetComponent<FlashlightController>().AimDirection)<.01f,"Mouse aim accounts for viewport offset: direction="+ocean.GetComponent<FlashlightController>().AimDirection+", mouse="+mouse.position.ReadValue()+", point="+aimPoint+", viewport="+ocean.oceanCamera.pixelRect);
                foreach(var fixedMine in ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.MiningSpot))
                    require(ocean.IsReachable(fixedMine.transform.position),"Reference mining route is reachable: "+fixedMine.name);
                var questMine=ocean.targets.Single(t=>t.name=="Mining Spot 36");
                jump(questMine.transform.position);await click();
                require(questMine.HasMachine && ocean.availableMachines==2,"Scaled quest alcove supports machine placement");
                await click();require(!questMine.HasMachine && ocean.availableMachines==3,"Scaled quest alcove supports retrieval");
                // This vertical crossing lay beyond the earlier 7.5-unit forward view.
                var bankNear=new Vector2(-10.74f,-27.09f);
                var bankFar=new Vector2(-10.74f,-19.15f);
                jump(bankNear);
                pointer(false,false,ocean.oceanCamera.WorldToScreenPoint(bankFar));await Task.Delay(200);
                require(Physics2D.LinecastAll(bankNear,bankFar).Any(h=>h.collider.name=="Chasm 4"),"Cross-bank sightline crosses a real chasm");
                var bankViewport=ocean.oceanCamera.WorldToViewportPoint(bankFar);
                require(bankViewport.x>0 && bankViewport.x<1 && bankViewport.y>0 && bankViewport.y<1,"Opposite bank enters the expanded view");
                var bankRt=new RenderTexture(1280,720,24);
                var bankPixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                var previousTarget=ocean.oceanCamera.targetTexture;
                var previousRect=ocean.oceanCamera.rect;
                try
                {
                    ocean.oceanCamera.targetTexture=bankRt;
                    ocean.oceanCamera.rect=new Rect(0,0,1,1);
                    ocean.GetComponent<FlashlightController>().RefreshLighting();
                    FlashlightCheck.Render(ocean.oceanCamera,bankRt,bankPixels);
                    var sample=bankPixels.GetPixel(Mathf.Clamp((int)(bankViewport.x*1280),0,1279),Mathf.Clamp((int)(bankViewport.y*720),0,719));
                    require(Mathf.Max(sample.r,sample.g,sample.b)>.005f,"Actual GPU frame shows flashlight illuminating the opposite bank");
                    System.IO.Directory.CreateDirectory("Temp/coverage-analysis");
                    System.IO.File.WriteAllBytes("Temp/coverage-analysis/cross-chasm-check.png",bankPixels.EncodeToPNG());
                }
                finally {ocean.oceanCamera.targetTexture=previousTarget;ocean.oceanCamera.rect=previousRect;UnityEngine.Object.DestroyImmediate(bankRt);UnityEngine.Object.DestroyImmediate(bankPixels);}
                var forcedPosition=body.position;
                int keptItems=ocean.ItemsCollected,keptTellurium=ocean.Tellurium,keptMachines=ocean.availableMachines;
                mine.Place(Time.time);
                ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity);
                require(!ocean.Prospect(Time.time+10) && !ocean.PlaceMachine(),"Empty tank blocks scanning and placement");
                ocean.Interact();
                require(ocean.ItemsCollected==keptItems && ocean.Tellurium==keptTellurium && ocean.availableMachines==keptMachines,"Empty tank blocks interactions");
                await Task.Delay(50);
                require(ocean.IsTransitioning || ocean.InsideSubmarine,"Empty tank starts forced return from far bank");
                require(!ocean.RequestEnterSubmarine(),"Forced entry cannot be requested twice");
                for(int i=0;i<40&&!ocean.InsideSubmarine;i++)await Task.Delay(100);
                require(ocean.InsideSubmarine && !swimmer.enabled && !body.simulated,"Depletion enters Interior and stops swimming");
                await click();
                for(int i=0;i<40&&(ocean.InsideSubmarine||ocean.IsTransitioning);i++)await Task.Delay(100);
                require(!ocean.InsideSubmarine && swimmer.enabled && ocean.Hydrox.CurrentCapacity>ocean.Hydrox.MaxCapacity-2,"Forced round trip refills before restoring controls");
                require(Vector2.Distance(body.position,forcedPosition)<.05f && mine.HasMachine && ocean.ItemsCollected==keptItems && ocean.Tellurium==keptTellurium,"Forced round trip preserves ocean position, inventory and deployed machine");
                require(remaining.SequenceEqual(ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item && t.isActiveAndEnabled).Select(t=>t.transform.position)),"Forced return preserves randomized item positions");
                mine.Retrieve(Time.time);
                ocean.interiorScene="Missing Hydrox Check Scene";
                int missingErrors=0;
                Application.LogCallback countMissing=(text,trace,type)=>{if(type==LogType.Error && text.StartsWith("Submarine scene is unavailable:"))missingErrors++;};
                Application.logMessageReceived+=countMissing;
                try
                {
                    require(!ocean.RequestEnterSubmarine(),"Unavailable scene rejects manual entry");
                    ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity);await Task.Delay(200);
                    require(missingErrors==1 && !ocean.RequestEnterSubmarine(),"Unavailable scene reports once without repeated load attempts");
                    require(!swimmer.enabled && ocean.Hydrox.IsDepleted && !ocean.InsideSubmarine && ocean.Message=="Submarine scene is unavailable.","Unavailable scene keeps depleted player stopped with a visible error");
                }
                finally {Application.logMessageReceived-=countMissing;}
                jump(new Vector2(0,-1.5f));
                return new { checks,openSpeed,kelpSpeed,minimapLabels=2,sceneRoundTrip=true,hydroxForcedReturn=true,expectedMissingSceneErrors=missingErrors };
            }
            finally
            {
                foreach (var deliver in deliveries) InputSystem.onBeforeUpdate -= deliver;
                InputSystem.onAfterUpdate -= keepTestKeyboardCurrent;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
                Application.runInBackground = oldRunInBackground;
                if(originalKeyboard!=null && originalKeyboard.added)originalKeyboard.MakeCurrent();
                if(originalMouse!=null && originalMouse.added)originalMouse.MakeCurrent();
                PlayModeWindow.SetCustomRenderingResolution((uint)originalWidth,(uint)originalHeight,"Ocean Demo Check");
                Time.timeScale=originalTimeScale;
                ocean.interiorScene=originalInterior;
                typeof(OceanInteractions).GetField("sceneUnavailable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ocean,false);
                ocean.Hydrox.Refill();
                if(!ocean.InsideSubmarine && !ocean.IsTransitioning)swimmer.enabled=true;
                ocean.Notify("Ocean checks finished; restart Play for a clean session.");
                body.linearVelocity=Vector2.zero;
            }
        }
    }
}
