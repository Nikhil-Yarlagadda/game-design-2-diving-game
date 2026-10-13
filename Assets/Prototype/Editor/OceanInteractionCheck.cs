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
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Ocean Interactables.unity");
            try
            {
                var interactions = scene.GetRootGameObjects().Select(o => o.GetComponent<OceanInteractions>()).First(o => o != null);
                var targets = interactions.targets;
                var mines = targets.Where(t => t.kind == OceanTarget.TargetKind.MiningSpot).ToArray();
                var hud = scene.GetRootGameObjects().Select(o => o.GetComponent<OceanHud>()).First(o => o != null);
                FlashlightCheck.Require(hud.DistanceBand(0) == 0 && hud.DistanceBand(9.99f) == 0
                    && hud.DistanceBand(10) == 1 && hud.DistanceBand(19.99f) == 1
                    && hud.DistanceBand(20) == 2 && hud.DistanceBand(100) == 2, "Radar distance bands and exact boundaries");
                FlashlightCheck.Require(targets.Count(t => t.kind == OceanTarget.TargetKind.Submarine) == 1, "One central submarine");
                FlashlightCheck.Require(mines.Select(t => t.level).Distinct().Count() == 5, "All five mining tiers exist");
                foreach (var mine in mines)
                {
                    FlashlightCheck.Require(mine.Place(10), "Place a machine");
                    FlashlightCheck.Require(!mine.Place(20), "A second placement must not reset the timer");
                    FlashlightCheck.Require(mine.YieldAt(69) == Mathf.FloorToInt(59f * mine.level / 60f), "Partial minutes use tier rate");
                    FlashlightCheck.Require(mine.Retrieve(130) == 2 * mine.level && !mine.HasMachine && !mine.machine.activeSelf, "Retrieve two minutes and machine");
                    FlashlightCheck.Require(mine.Retrieve(200) == 0, "Retrieving twice cannot duplicate resources");
                    FlashlightCheck.Require(interactions.obstacles.All(b => Vector2.Distance(mine.transform.position, b.ClosestPoint(mine.transform.position)) >= .9f), "Mining location accessible: " + mine.name);
                }
                interactions.HandleProspectInput(true, true, false, 100);
                interactions.HandleProspectInput(false, false, true, 100.1f);
                FlashlightCheck.Require(!interactions.RadarActive && !interactions.Prospect(101), "Tap scans and starts cooldown");
                FlashlightCheck.Require(interactions.Prospect(102.1f), "Scan is available after two seconds");
                var visible = mines.Where(t => { var v = interactions.oceanCamera.WorldToViewportPoint(t.transform.position); return v.z > 0 && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1; }).ToArray();
                FlashlightCheck.Require(visible.Length > 0 && visible.Length < mines.Length, "Test includes on-screen and off-screen mines");
                FlashlightCheck.Require(visible.All(t => t.RevealAlpha(102.1f) > .99f && t.RevealAlpha(102.85f) > .49f && t.RevealAlpha(103.6f) == 0), "Reveal fades to zero");
                FlashlightCheck.Require(mines.Except(visible).All(t => t.RevealAlpha(102.1f) == 0), "Off-screen mines stay hidden");
                interactions.HandleProspectInput(true, true, false, 110);
                interactions.HandleProspectInput(false, true, false, 110.5f);
                FlashlightCheck.Require(interactions.RadarActive, "Hold activates radar");
                interactions.HandleProspectInput(false, false, true, 110.6f);
                FlashlightCheck.Require(!interactions.RadarActive && interactions.Prospect(110.6f), "Release hides radar without consuming prospect cooldown");
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
                Debug.Log("Ocean interaction check passed: tiers, resource accounting, accessible positions, tap/hold, cooldown, screen filtering, reveal fade, obstacle and scene wiring.");
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
