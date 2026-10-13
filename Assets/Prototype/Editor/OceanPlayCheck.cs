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
            var oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            bool oldRunInBackground = Application.runInBackground;
            Keyboard keyboard = null;
            Action keepTestKeyboardCurrent = () =>
            {
                if (InputState.currentUpdateType == InputUpdateType.Dynamic && keyboard != null && keyboard.added) keyboard.MakeCurrent();
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
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                keyboard = InputSystem.AddDevice<Keyboard>("OceanCheckKeyboard");
                keyboard.MakeCurrent();
                InputSystem.onAfterUpdate += keepTestKeyboardCurrent;
                int frame = Time.frameCount;
                await Task.Delay(200);
                require(Time.frameCount > frame, "Real player loop is advancing");
                jump(new Vector2(0,-5)); keys(new[]{Key.D}); await Task.Delay(2900);
                float openSpeed = body.linearVelocity.magnitude;
                require(Mathf.Abs(openSpeed-2.5f)<.05f, "Open-water speed uses teammate tuning: " + openSpeed);
                keys(Array.Empty<Key>()); await Task.Delay(150);
                jump(new Vector2(-12,9)); keys(new[]{Key.D}); await Task.Delay(1550);
                float kelpSpeed = body.linearVelocity.magnitude;
                require(swimmer.SpeedMultiplier==.5f && Mathf.Abs(kelpSpeed-1.25f)<.05f, "Kelp passes at half speed");
                keys(Array.Empty<Key>()); await Task.Delay(150);
                foreach(string name in new[]{"Rock 5","Chasm NE"})
                {
                    var solid=ocean.obstacles.Single(c=>c.name==name);
                    jump(new Vector2(solid.bounds.min.x-.7f,solid.bounds.center.y));
                    keys(new[]{Key.D});await Task.Delay(3100);
                    var contacts=new ContactPoint2D[16];int count=body.GetContacts(contacts);
                    require(contacts.Take(count).Any(c=>c.collider==solid||c.otherCollider==solid), name+" stops swimming by actual contact");
                    require(!solid.OverlapPoint(body.position), name+" cannot be entered");
                    keys(Array.Empty<Key>());await Task.Delay(150);
                }
                jump(new Vector2(0,-1.5f));
                keys(new[]{Key.Z});await Task.Delay(80);keys(Array.Empty<Key>());await Task.Delay(150);
                require(ocean.CooldownRemaining>1f && !ocean.RadarActive, "Natural short Z press scans");
                require(ocean.targets.Any(t=>t.kind==OceanTarget.TargetKind.MiningSpot && t.highlight.enabled), "Prospect highlight is rendered");
                keys(new[]{Key.Z});await Task.Delay(500);
                require(ocean.RadarActive, "Natural held Z activates radar during prospect cooldown");
                var radarTexts=hud.radarRoot.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
                require(radarTexts.Length==2 && radarTexts.Any(t=>t.text.StartsWith("Submarine")) && radarTexts.Any(t=>t.text.StartsWith("Item")), "Only submarine and nearest item have labels");
                require(hud.radarRoot.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.gameObject.activeInHierarchy)==7, "Submarine plus six item arrows");
                var items=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item).ToArray();
                require(items.All(t=>ocean.CanSpawn(t.transform.position,t)), "All randomized pickups have clear spacing");
                var oldPositions=items.Select(t=>t.transform.position).ToArray();
                for(int i=0;i<items.Length;i++)items[i].transform.position=new Vector2(20,10+i);
                items[0].transform.position=new Vector2(0,2.5f);
                await Task.Delay(100);
                radarTexts=hud.radarRoot.GetComponentsInChildren<TMPro.TMP_Text>();
                var subLabel=radarTexts.Single(t=>t.text.StartsWith("Submarine"));
                var itemLabel=radarTexts.Single(t=>t.text.StartsWith("Item"));
                require(itemLabel.name.StartsWith(items[0].name) && itemLabel.text.Contains("4m"), "Nearest label and distance update live");
                require(Mathf.Abs(subLabel.rectTransform.anchoredPosition.y-itemLabel.rectTransform.anchoredPosition.y)>=65f, "Collinear target labels do not overlap");
                var rings = hud.radarRoot.GetComponentInChildren<RadarRings>();
                require(rings != null && rings.color.a == 0f, "Distance guide lines remain hidden while radar is held");
                require(rings.GetComponent<CanvasRenderer>() != null && rings.canvasRenderer.GetMesh()?.vertexCount > 0,
                    "Distance-band geometry is available");
                var arrow = (RectTransform)hud.radarRoot.Find(items[0].name + " Radar Arrow");
                float[] testDistances = { 9.9f, 10.1f, 19.9f, 20.1f };
                int[] expectedBands = { 0, 1, 1, 2 };
                var radii = new float[4];
                var widths = new float[4];
                var alphas = new float[4];
                for(int i=0;i<testDistances.Length;i++)
                {
                    items[0].transform.position = ocean.transform.position + Vector3.right * testDistances[i];
                    await Task.Delay(80);
                    require(Mathf.Abs(arrow.anchoredPosition.magnitude - RadarRings.Radius(hud.radarRoot.rect.size, expectedBands[i])) < .05f,
                        "Live arrow moves to actual distance band " + i);
                    radii[i] = arrow.anchoredPosition.magnitude;
                    widths[i] = arrow.sizeDelta.x;
                    alphas[i] = arrow.GetComponent<UnityEngine.UI.Image>().color.a;
                }
                require(radii[1]-radii[0] > Mathf.Min(hud.radarRoot.rect.width,hud.radarRoot.rect.height)*.12f
                    && radii[3]-radii[2] > Mathf.Min(hud.radarRoot.rect.width,hud.radarRoot.rect.height)*.12f,
                    "Distance bands have clearly separated radii");
                require(widths[0]>=widths[1]*1.2f && widths[1]==widths[2] && widths[2]>=widths[3]*1.2f
                    && widths[0]<=24f && widths[3]>=12f, "Arrow sizes balance distance hierarchy and visibility");
                require(alphas[0]>alphas[1] && alphas[1]==alphas[2] && alphas[2]>alphas[3] && alphas[3]>=.4f,
                    "Closer arrows are brighter while distant arrows remain visible");
                Vector2 beforeSwim = body.position;
                keys(new[]{Key.Z,Key.D}); await Task.Delay(650);
                require(ocean.RadarActive && body.position.x > beforeSwim.x + .05f, "Radar remains active while swimming with Z and D");
                keys(new[]{Key.Z}); await Task.Delay(100); jump(new Vector2(0,-1.5f));
                for(int i=0;i<items.Length;i++)items[i].transform.position=oldPositions[i];
                keys(Array.Empty<Key>());await Task.Delay(150);
                require(!ocean.RadarActive && hud.radarRoot.GetComponentsInChildren<TMPro.TMP_Text>().Length==0, "Radar disappears on release");
                require(!rings.gameObject.activeInHierarchy, "Distance guide rings disappear on release");
                var mine=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.MiningSpot&&t.level==5);
                jump(mine.transform.position); keys(new[]{Key.X});await Task.Delay(150);keys(Array.Empty<Key>());await Task.Delay(150);
                require(mine.HasMachine && ocean.availableMachines==2, "X deploys exactly one machine");
                var placed = typeof(OceanTarget).GetField("<PlacedAt>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                placed.SetValue(mine,Time.time-60.1f);
                keys(new[]{Key.Space});await Task.Delay(150);keys(Array.Empty<Key>());await Task.Delay(150);
                require(!mine.HasMachine && ocean.Tellurium==5 && ocean.availableMachines==3, "Space retrieves machine and one minute of tier five yield");
                var item=ocean.targets.First(t=>t.kind==OceanTarget.TargetKind.Item);
                require(ocean.CanSpawn(item.transform.position,item), "Random pickup has free placement");
                jump(item.transform.position);keys(new[]{Key.Space});await Task.Delay(150);keys(Array.Empty<Key>());await Task.Delay(150);
                require(ocean.ItemsCollected==1 && !item.gameObject.activeSelf, "Space collects pickup once");
                var remaining=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item && t.isActiveAndEnabled).Select(t=>t.transform.position).ToArray();
                mine.Place(Time.time);
                jump(new Vector2(0,-1));keys(new[]{Key.Space});await Task.Delay(150);keys(Array.Empty<Key>());
                for(int i=0;i<40&&!ocean.InsideSubmarine;i++)await Task.Delay(100);
                require(ocean.InsideSubmarine && SceneManager.GetActiveScene().name=="Interior" && !body.simulated, "Space enters actual Interior scene");
                keys(new[]{Key.Space});await Task.Delay(150);keys(Array.Empty<Key>());
                for(int i=0;i<40&&ocean.InsideSubmarine;i++)await Task.Delay(100);
                require(!ocean.InsideSubmarine && SceneManager.GetActiveScene().name=="Ocean Interactables" && body.simulated && swimmer.enabled, "Space returns to playable ocean");
                require(ocean.ItemsCollected==1 && ocean.Tellurium==5 && mine.HasMachine, "Inventory and placed mine survive scene round trip");
                require(remaining.SequenceEqual(ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.Item && t.isActiveAndEnabled).Select(t=>t.transform.position)), "Random items do not respawn on return");
                mine.Retrieve(Time.time);
                jump(new Vector2(0,-1.5f));
                return new { checks,openSpeed,kelpSpeed,radarLabels=2,sceneRoundTrip=true };
            }
            finally
            {
                foreach (var deliver in deliveries) InputSystem.onBeforeUpdate -= deliver;
                InputSystem.onAfterUpdate -= keepTestKeyboardCurrent;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
                Application.runInBackground = oldRunInBackground;
                if(originalKeyboard!=null && originalKeyboard.added)originalKeyboard.MakeCurrent();
                body.linearVelocity=Vector2.zero;
            }
        }
    }
}
