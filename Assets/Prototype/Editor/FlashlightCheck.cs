using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class FlashlightCheck
    {
        [MenuItem("Diving Prototype/Check Flashlight")]
        public static void Run()
        {
            Require(!EditorApplication.isPlaying, "Run this check outside Play mode.");
            var flashlightScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Prototype/Lighting/FlashlightController.cs");
            var cameraScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Prototype/Character/CenteredCamera.cs");
            Require(MonoImporter.GetExecutionOrder(flashlightScript) > MonoImporter.GetExecutionOrder(cameraScript), "Flashlight must update after camera following");
            CheckMath();
            CheckScene();
            CheckRenderedPixels();
            SwimmingCheck.Run();
            Debug.Log("Flashlight check passed: cone, exponential fade, ambient, transformed blockers, GPU pixels, scene wiring, swimming.");
        }

        private static void CheckMath()
        {
            Near(FlashlightMath.Strength(0f, 10f), 1f, "Origin strength");
            Near(FlashlightMath.Strength(10f, 10f), .5f, "Half strength at 10 units");
            Near(FlashlightMath.Strength(20f, 10f), .25f, "Quarter strength at 20 units");
            Require(FlashlightMath.Strength(100f, 10f) > 0f, "No distance cutoff");
            Near(FlashlightMath.ConeMask(Vector2.right, Vector2.right, 60f), 1f, "Cone center");
            Near(FlashlightMath.ConeMask(AtAngle(28f), Vector2.right, 60f), 1f, "Cone inner edge");
            float feather = FlashlightMath.ConeMask(AtAngle(29.5f), Vector2.right, 60f);
            Require(feather > 0f && feather < 1f, "Cone feather");
            Near(FlashlightMath.ConeMask(AtAngle(30f), Vector2.right, 60f), 0f, "Cone outer edge");
            Near(FlashlightMath.ConeMask(AtAngle(-31f), Vector2.right, 60f), 0f, "Outside cone");
            Require(FlashlightMath.RetainDirection(Vector2.zero, Vector2.up) == Vector2.up, "Retain idle direction");
            Near(FlashlightMath.RetainDirection(Vector2.one, Vector2.right).magnitude, 1f, "Normalized diagonal aim");
            var go = new GameObject("Flashlight math check") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(2f, 3f);
                box.offset = new Vector2(.4f, -.2f);
                go.transform.SetPositionAndRotation(new Vector3(5f, 2f, 0f), Quaternion.Euler(0f, 0f, 37f));
                go.transform.localScale = new Vector3(-2f, .5f, 1f);
                FlashlightMath.PackBox(box, out var x, out var y);
                Vector2 center = go.transform.TransformPoint(box.offset);
                Vector2 axis = go.transform.TransformVector(Vector3.right).normalized;
                Require(FlashlightMath.SegmentBlocked(center - axis * 5f, center + axis * 5f, x, y), "Rotated/scaled/offset box blocks");
                Require(!FlashlightMath.SegmentBlocked(center - axis * 5f, center - axis * 3f, x, y), "Box beyond endpoint does not block");
                Vector2 across = go.transform.TransformVector(Vector3.up).normalized;
                Require(!FlashlightMath.SegmentBlocked(center - axis * 5f + across * 3f, center + axis * 5f + across * 3f, x, y), "Parallel ray outside box");
                Require(FlashlightMath.SegmentBlocked(center, center + axis * 5f, x, y), "Origin inside box blocks outgoing light");
                box.enabled = false;
                FlashlightMath.PackBox(box, out x, out y);
                Require(!FlashlightMath.SegmentBlocked(center - axis * 5f, center + axis * 5f, x, y), "Disabled collider does not block");
                box.enabled = true;
                go.SetActive(false);
                FlashlightMath.PackBox(box, out x, out y);
                Require(x.w == 0f, "Inactive blocker excluded");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void CheckScene()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Ocean Floor.unity");
            try
            {
                FlashlightController controller = null;
                int sprites = 0, solids = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    controller = controller != null ? controller : root.GetComponentInChildren<FlashlightController>(true);
                    foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        sprites++;
                        Require(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "Diving Prototype/Ocean Flashlight", "All ocean sprites use flashlight material");
                    }
                    foreach (var collider in root.GetComponentsInChildren<BoxCollider2D>(true))
                        if (collider.name.StartsWith("Rock ") || collider.name.EndsWith(" boundary")) solids++;
                    foreach (var light in root.GetComponentsInChildren<Light2D>(true))
                        Require(!light.enabled, "Ocean global light disabled");
                }
                Require(controller != null && sprites > 0 && solids == 11, "Ocean controller and solids present");
                var settings = new SerializedObject(controller);
                Require(settings.FindProperty("aimMode").enumValueIndex == 0, "Mouse aim default");
                Near(settings.FindProperty("ambientBrightness").floatValue, 0f, "Black ambient default");
                Near(settings.FindProperty("coneAngle").floatValue, 60f, "60 degree cone");
                Near(settings.FindProperty("halfStrengthDistance").floatValue, 10f, "10 unit half-strength distance");
                Require(settings.FindProperty("aimCamera").objectReferenceValue != null, "Camera assigned");
                Require(settings.FindProperty("map").objectReferenceValue != null, "Map assigned");
                Require(settings.FindProperty("spriteMaterial").objectReferenceValue != null, "Material assigned");
                var blockers = settings.FindProperty("blockers");
                Require(blockers.arraySize == 11, "All 11 solids registered");
                var seen = new System.Collections.Generic.HashSet<BoxCollider2D>();
                for (int i = 0; i < blockers.arraySize; i++)
                {
                    var box = blockers.GetArrayElementAtIndex(i).objectReferenceValue as BoxCollider2D;
                    Require(box != null && !box.transform.IsChildOf(controller.transform), "Player excluded from blockers");
                    Require(seen.Add(box), "No duplicate blocker entries");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void CheckRenderedPixels()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Prototype/Lighting/OceanFlashlight.shader");
            Require(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Flashlight shader must compile and be supported");
            var scene = EditorSceneManager.NewPreviewScene();
            Material material = null;
            Sprite sprite = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            var previousActive = RenderTexture.active;
            try
            {
                material = new Material(shader);
                var ground = new GameObject("GPU check ground");
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.localScale = new Vector3(100f, 100f, 1f);
                var renderer = ground.AddComponent<SpriteRenderer>();
                sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                var cameraObject = new GameObject("GPU check camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 25f;
                camera.transform.position = new Vector3(10f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                target = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(1024, 1024, TextureFormat.RGBAFloat, false, true);
                material.SetVector("_FlashlightOrigin", Vector4.zero);
                material.SetVector("_FlashlightDirection", Vector2.right);
                material.SetFloat("_AmbientBrightness", 0f);
                material.SetFloat("_ConeAngle", 60f);
                material.SetFloat("_HalfStrengthDistance", 10f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(1f, 0f), Expected, "GPU near strength");
                CheckPixel(camera, pixels, new Vector2(10f, 0f), Expected, "GPU half strength");
                CheckPixel(camera, pixels, new Vector2(20f, 0f), Expected, "GPU quarter strength");
                CheckPixel(camera, pixels, new Vector2(-2f, 0f), _ => 0f, "GPU outside cone black");
                CheckPixel(camera, pixels, AtAngle(29.5f) * 10f, Expected, "GPU angular feather");
                CheckPixel(camera, pixels, AtAngle(31f) * 10f, _ => 0f, "GPU cone limit");
                material.SetFloat("_AmbientBrightness", .05f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(-2f, 0f), _ => .05f, "GPU adjustable ambient");
                CheckPixel(camera, pixels, new Vector2(10f, 0f), p => .05f + .95f * Expected(p), "GPU ambient plus flashlight");
                var boxObject = new GameObject("GPU check blocker");
                SceneManager.MoveGameObjectToScene(boxObject, scene);
                boxObject.transform.position = new Vector3(5f, 0f, 0f);
                var box = boxObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(2f, 3f);
                var xs = new Vector4[FlashlightMath.MaxBlockers];
                var ys = new Vector4[FlashlightMath.MaxBlockers];
                FlashlightMath.PackBox(box, out xs[0], out ys[0]);
                material.SetInt("_BlockerCount", 1);
                material.SetVectorArray("_BlockerRowsX", xs);
                material.SetVectorArray("_BlockerRowsY", ys);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(10f, 0f), _ => .05f, "GPU shadow retains only ambient");
                material.SetFloat("_AmbientBrightness", 0f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(10f, 0f), _ => 0f, "GPU solid shadow black");
                CheckPixel(camera, pixels, new Vector2(3f, 0f), Expected, "GPU light before obstacle");
                CheckPixel(camera, pixels, new Vector2(6f, 3f), Expected, "GPU light around corner");
                material.SetFloat("_SelfBlocker", 0f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(5f, 0f), Expected, "GPU rock ignores own collider");
                xs[1] = xs[0]; ys[1] = ys[0];
                material.SetInt("_BlockerCount", 2);
                material.SetVectorArray("_BlockerRowsX", xs);
                material.SetVectorArray("_BlockerRowsY", ys);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(5f, 0f), _ => 0f, "GPU other rock still blocks");
                boxObject.transform.SetPositionAndRotation(new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 0f, 37f));
                boxObject.transform.localScale = new Vector3(-1.5f, .75f, 1f);
                box.offset = new Vector2(.25f, -.2f);
                FlashlightMath.PackBox(box, out xs[0], out ys[0]);
                material.SetFloat("_SelfBlocker", -1f);
                material.SetInt("_BlockerCount", 1);
                material.SetVectorArray("_BlockerRowsX", xs);
                material.SetVectorArray("_BlockerRowsY", ys);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(10f, 0f), _ => 0f, "GPU transformed box shadow");
                box.enabled = false;
                FlashlightMath.PackBox(box, out xs[0], out ys[0]);
                material.SetVectorArray("_BlockerRowsX", xs);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(10f, 0f), Expected, "GPU disabled blocker transmits");
                renderer.color = new Color(.4f, .7f, .2f, .6f);
                material.SetFloat("_AmbientBrightness", 1f);
                Render(camera, target, pixels);
                var tinted = ReadPixel(camera, pixels, Vector2.zero, out _);
                Near(tinted.a, .6f, "GPU preserves sprite alpha", .005f);
                // RGB blending against a clear target multiplies by alpha; tint conversion depends on project color space.
                var tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? renderer.color.linear : renderer.color;
                Near(tinted.r, tint.r * .6f, "GPU preserves red sprite tint", .005f);
                Near(tinted.g, tint.g * .6f, "GPU preserves green sprite tint", .005f);
                Debug.Log("Flashlight GPU checks passed in a linear floating-point render target (including tint and alpha).");
            }
            finally
            {
                RenderTexture.active = previousActive;
                EditorSceneManager.ClosePreviewScene(scene);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
        }

        public static void Render(Camera camera, RenderTexture target, Texture2D pixels)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
        }

        private static float Expected(Vector2 point) => FlashlightMath.Strength(point.magnitude, 10f)
            * FlashlightMath.ConeMask(point, Vector2.right, 60f);
        private static Vector2 AtAngle(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        private static Color ReadPixel(Camera camera, Texture2D pixels, Vector2 point, out Vector2 sampledWorld)
        {
            Vector3 screen = camera.WorldToScreenPoint(point);
            int x = Mathf.Clamp(Mathf.FloorToInt(screen.x), 0, pixels.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(screen.y), 0, pixels.height - 1);
            sampledWorld = camera.ScreenToWorldPoint(new Vector3(x + .5f, y + .5f, 10f));
            return pixels.GetPixel(x, y);
        }
        private static void CheckPixel(Camera camera, Texture2D pixels, Vector2 point, Func<Vector2, float> expected, string message)
        {
            var value = ReadPixel(camera, pixels, point, out var sampledWorld);
            Near(value.r, expected(sampledWorld), message, .005f);
        }
        public static void Near(float actual, float expected, string message, float tolerance = .001f) =>
            Require(Mathf.Abs(actual - expected) <= tolerance, $"{message}: expected {expected}, got {actual}");
        public static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
