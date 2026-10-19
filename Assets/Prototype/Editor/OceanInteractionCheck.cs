using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class OceanInteractionCheck
    {
        [MenuItem("Diving Prototype/Check Ocean Interactions")]
        public static void Run()
        {
            FlashlightCheck.Require(!EditorApplication.isPlaying, "Run outside Play mode.");
            var view = new Rect(-1, -1, 2, 2);
            FlashlightCheck.Require(FlashlightController.EdgeIntersectsView(new Vector2(-10,0),new Vector2(10,0),view), "Crossing edge stays even when both endpoints are off-screen");
            FlashlightCheck.Require(FlashlightController.EdgeIntersectsView(new Vector2(1,-10),new Vector2(1,10),view), "Boundary-touching shadow edge stays");
            FlashlightCheck.Require(!FlashlightController.EdgeIntersectsView(new Vector2(2,2),new Vector2(3,3),view), "Distant shadow edge is excluded");
            FlashlightCheck.Require(FlashlightController.EdgeIntersectsView(Vector2.zero,Vector2.zero,view), "Degenerate visible edge remains conservative");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Ocean Interactables.unity");
            try
            {
                var interactions = scene.GetRootGameObjects().Select(o => o.GetComponent<OceanInteractions>()).First(o => o != null);
                interactions.Hydrox.Refill();
                var tank = interactions.Hydrox;
                FlashlightCheck.Near(tank.MaxCapacity,180,"Default Hydrox capacity",.001f);
                FlashlightCheck.Near(tank.ConsumptionRate,1,"Default Hydrox rate",.001f);
                tank.Advance(60);
                FlashlightCheck.Near(tank.CurrentCapacity,120,"One minute consumes 60 units",.001f);
                tank.Advance(-1); tank.Advance(float.NaN);
                FlashlightCheck.Near(tank.CurrentCapacity,120,"Invalid elapsed time is ignored",.001f);
                tank.Advance(1000);
                FlashlightCheck.Require(tank.IsDepleted && tank.NormalizedFill==0,"Capacity clamps at zero");
                tank.Refill();
                FlashlightCheck.Require(tank.NormalizedFill==1,"Refill restores full capacity");
                var settings = new SerializedObject(tank);
                settings.FindProperty("maxCapacity").floatValue=360;
                settings.FindProperty("consumptionRate").floatValue=2;
                settings.ApplyModifiedPropertiesWithoutUndo(); tank.Refill(); tank.Advance(60);
                FlashlightCheck.Near(tank.CurrentCapacity,240,"Configured capacity and consumption",.001f);
                settings.Update();settings.FindProperty("maxCapacity").floatValue=float.NaN;
                settings.FindProperty("consumptionRate").floatValue=float.PositiveInfinity;
                settings.ApplyModifiedPropertiesWithoutUndo();tank.Refill();
                FlashlightCheck.Near(tank.MaxCapacity,180,"Invalid capacity restores default",.001f);
                FlashlightCheck.Near(tank.ConsumptionRate,1,"Invalid rate restores default",.001f);
                settings.Update();settings.FindProperty("maxCapacity").floatValue=-1;
                settings.FindProperty("consumptionRate").floatValue=0;
                settings.ApplyModifiedPropertiesWithoutUndo();tank.Refill();
                FlashlightCheck.Require(tank.MaxCapacity>0 && tank.ConsumptionRate>0,"Settings clamp positive");
                settings.Update();settings.FindProperty("maxCapacity").floatValue=180;
                settings.FindProperty("consumptionRate").floatValue=1;
                settings.ApplyModifiedPropertiesWithoutUndo();tank.Refill();
                var targets = interactions.targets;
                FlashlightCheck.Near(interactions.transform.localScale.x,.75f,"Smaller player and matching collider scale",.001f);
                FlashlightCheck.Near(interactions.oceanCamera.orthographicSize,9f,"Trial camera height is 18 units",.001f);
                var smooth=interactions.oceanMap.GetComponentsInChildren<PolygonCollider2D>();
                FlashlightCheck.Require(smooth.All(p=>p.GetComponent<SpriteRenderer>().sprite.pixelsPerUnit==40),"Terrain silhouettes use 40 PPU");
                foreach(var name in new[]{"Circle","Ring"})
                    FlashlightCheck.Require(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/Interactions/Art/"+name+".asset").rect.width==256,"Smooth marker resolution: "+name);
                var grid = interactions.oceanMap.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Grid X "))
                    .OrderBy(t=>t.position.x).Take(2).ToArray();
                FlashlightCheck.Near(grid[1].position.x-grid[0].position.x,1.6f,"Draft reference square is 1.6 world units",.001f);
                FlashlightCheck.Require(interactions.oceanMap.GetComponentsInChildren<SpriteRenderer>(true)
                    .All(r=>r.sharedMaterial.shader.name=="Universal Render Pipeline/2D/Sprite-Unlit-Default"),
                    "Saved map previews in terrain colors without gameplay lighting");
                var mines = targets.Where(t => t.kind == OceanTarget.TargetKind.MiningSpot).ToArray();
                var hud = scene.GetRootGameObjects().Select(o => o.GetComponent<OceanHud>()).First(o => o != null);
                FlashlightCheck.Require(hud.hydroxRoot!=null && hud.hydroxFill.parent==hud.hydroxRoot,"Hydrox HUD is saved and wired");
                FlashlightCheck.Require(hud.hydroxRoot.sizeDelta==new Vector2(240,20),"Hydrox bar reference size");
                FlashlightCheck.Require(!hud.hydroxRoot.GetComponent<UnityEngine.UI.Image>().raycastTarget && !hud.hydroxFill.GetComponent<UnityEngine.UI.Image>().raycastTarget,"Hydrox HUD ignores clicks");
                float statusTop=hud.status.rectTransform.anchoredPosition.y + hud.status.rectTransform.rect.yMax;
                FlashlightCheck.Require(statusTop < hud.hydroxRoot.anchoredPosition.y-20,"Status sits below tank bar");
                FlashlightCheck.Near(hud.minimapRange,25,"Minimap range",.001f);
                FlashlightCheck.Require(hud.minimapRoot.anchorMin==Vector2.one && hud.minimapRoot.anchorMax==Vector2.one,"Minimap anchored top right");
                FlashlightCheck.Require(hud.minimapRoot.sizeDelta==Vector2.one*180,"Minimap diameter is 180 reference pixels");
                FlashlightCheck.Require(OceanHud.MinimapPosition(Vector2.zero,25,84)==Vector2.zero,"Coincident target stays centered");
                FlashlightCheck.Near(OceanHud.MinimapPosition(Vector2.right*12.5f,25,84).x,42,"Linear in-range distance",.001f);
                FlashlightCheck.Near(OceanHud.MinimapPosition(Vector2.one*50,25,84).magnitude,84,"Diagonal distant target clamps to border",.001f);
                FlashlightCheck.Require(targets.Count(t => t.kind == OceanTarget.TargetKind.Submarine) == 1, "One central submarine");
                FlashlightCheck.Require(mines.Select(t => t.level).Distinct().Count() == 5, "All five mining tiers exist");
                foreach (var mine in mines)
                {
                    FlashlightCheck.Require(mine.Place(10), "Place a machine");
                    FlashlightCheck.Require(!mine.Place(20), "A second placement must not reset the timer");
                    FlashlightCheck.Require(mine.YieldAt(69) == Mathf.FloorToInt(59f * mine.level / 60f), "Partial minutes use tier rate");
                    FlashlightCheck.Require(mine.Retrieve(130) == 2 * mine.level && !mine.HasMachine && !mine.machine.activeSelf, "Retrieve two minutes and machine");
                    FlashlightCheck.Require(mine.Retrieve(200) == 0, "Retrieving twice cannot duplicate resources");
                    FlashlightCheck.Require(interactions.obstacles.All(b => Vector2.Distance(mine.transform.position, b.ClosestPoint(mine.transform.position)) >= .6f), "Mining center has 0.6-unit clearance: " + mine.name);
                }
                var nearby = mines.OrderBy(m=>Vector2.Distance(m.transform.position, interactions.transform.position)).First();
                interactions.oceanCamera.transform.position = nearby.transform.position + new Vector3(0,0,-10);
                FlashlightCheck.Require(interactions.Prospect(100.1f), "Prospecting starts directly");
                FlashlightCheck.Require(!interactions.Prospect(101), "Prospect starts cooldown");
                FlashlightCheck.Require(interactions.Prospect(102.1f), "Scan is available after two seconds");
                var visible = mines.Where(t => { var v = interactions.oceanCamera.WorldToViewportPoint(t.transform.position); return v.z > 0 && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1; }).ToArray();
                FlashlightCheck.Require(visible.Length > 0 && visible.Length < mines.Length, "Test includes on-screen and off-screen mines");
                FlashlightCheck.Require(visible.All(t => t.RevealAlpha(102.1f) > .99f && t.RevealAlpha(102.85f) > .49f && t.RevealAlpha(103.6f) == 0), "Reveal fades to zero");
                FlashlightCheck.Require(mines.Except(visible).All(t => t.RevealAlpha(102.1f) == 0), "Off-screen mines stay hidden");
                FlashlightCheck.Require(interactions.Prospect(110.6f), "A later scan is available");
                var light = new SerializedObject(interactions.GetComponent<FlashlightController>());
                var polygons = light.FindProperty("polygonBlockers");
                int edgeCount = 0;
                for (int i = 0; i < polygons.arraySize; i++)
                {
                    var p = (PolygonCollider2D)polygons.GetArrayElementAtIndex(i).objectReferenceValue;
                    FlashlightCheck.Require(!p.name.StartsWith("Chasm"), "Chasms transmit flashlight");
                    edgeCount += p.GetTotalPointCount();
                }
                FlashlightCheck.Require(edgeCount <= 256 && edgeCount > 0, "Polygon shader capacity");
                FlashlightCheck.Require(interactions.obstacles.Where(b => b.name.StartsWith("Kelp")).All(b => b.isTrigger), "Kelp passable");
                FlashlightCheck.Require(interactions.obstacles.Where(b => b.name.StartsWith("Rock") || b.name.StartsWith("Chasm")).All(b => !b.isTrigger), "Rocks and chasms solid");
                FlashlightCheck.Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Interior.unity"), "Interior included in build");
                FlashlightCheck.Require(mines.Length == 37 && interactions.itemCount == 15 && targets.Count(t=>t.kind==OceanTarget.TargetKind.Item)>=interactions.itemCount, "Full reference map and initial item count");
                foreach (var size in new[] {new Vector2(1920,1080),new Vector2(1280,720),new Vector2(1920,1200),new Vector2(2560,1080)})
                {
                    var r=FixedOceanView.Viewport(size.x,size.y);
                    FlashlightCheck.Near(r.width*size.x/(r.height*size.y),16f/9f,"Fitted 16:9 viewport",.001f);
                }
                Debug.Log("Ocean interaction check passed: full draft map, tiers, resource accounting, minimap geometry, cooldown, screen filtering, reveal fade, obstacle and scene wiring, fixed aspect ratios.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            CheckPolygonPixels();
        }

        private static void CheckPolygonPixels()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var material = new Material(Shader.Find("Diving Prototype/Ocean Flashlight"));
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
            try
            {
                var ground = new GameObject("Polygon GPU check"); SceneManager.MoveGameObjectToScene(ground, scene);
                var renderer = ground.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/Interactions/Art/Square.asset"); renderer.sharedMaterial = material; ground.transform.localScale = Vector3.one * 30;
                var co = new GameObject("Camera"); SceneManager.MoveGameObjectToScene(co, scene);
                var camera = co.AddComponent<Camera>(); camera.scene = scene; camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 8; camera.transform.position = new Vector3(0,0,-10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target;
                material.SetVector("_FlashlightOrigin", Vector4.zero); material.SetVector("_FlashlightDirection", Vector2.right);
                material.SetFloat("_ConeAngle", 60); material.SetFloat("_HalfStrengthDistance", 10); material.SetFloat("_AmbientBrightness", 0); material.SetFloat("_PlayerLightRadius", .5f); material.SetFloat("_SelfBlocker", -1);
                var edges = new Vector4[256]; edges[0]=new Vector4(3,-1,5,-1); edges[1]=new Vector4(5,-1,5,1); edges[2]=new Vector4(5,1,3,1); edges[3]=new Vector4(3,1,3,-1);
                var owners = Enumerable.Repeat(32f,256).ToArray();
                material.SetVectorArray("_PolygonEdges",edges); material.SetFloatArray("_EdgeOwners",owners);
                target.Create();
                Func<Vector2,float> sample = p => { FlashlightCheck.Render(camera,target,pixels); var v=camera.WorldToViewportPoint(p); return pixels.GetPixel(Mathf.Clamp((int)(v.x*256),0,255),Mathf.Clamp((int)(v.y*256),0,255)).r; };
                FlashlightCheck.Require(sample(new Vector2(6,0))>.5f, "Unblocked polygon GPU baseline");
                material.SetInt("_EdgeCount",4);
                FlashlightCheck.Near(sample(new Vector2(6,0)),0,"Polygon casts a GPU shadow",.005f);
                FlashlightCheck.Require(sample(new Vector2(2,0))>.5f,"Light before polygon");
                material.SetFloat("_SelfBlocker",32);
                FlashlightCheck.Require(sample(new Vector2(6,0))>.5f,"Polygon self-shadow exclusion");
                material.SetFloat("_SelfBlocker",33);
                FlashlightCheck.Near(sample(new Vector2(6,0)),0,"Other polygon still blocks",.005f);
                material.SetFloat("_Reveal",1);
                FlashlightCheck.Require(sample(new Vector2(-3,0))>.99f,"Prospecting highlight visible outside beam");
                Debug.Log("Ocean polygon GPU checks passed: shadow, self exclusion and prospect reveal.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
