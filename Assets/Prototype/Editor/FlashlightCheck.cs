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
            Debug.Log("Flashlight check passed: player circle, trapezoid, exponential fade, ambient, transformed blockers, GPU pixels, scene wiring, swimming.");
        }

        private static void CheckMath()
        {
            Near(FlashlightMath.Strength(0f, 10f), 1f, "Origin strength");
            Near(FlashlightMath.Strength(10f, 10f), .5f, "Half strength at 10 units");
            Near(FlashlightMath.Strength(20f, 10f), .25f, "Quarter strength at 20 units");
            Require(FlashlightMath.Strength(100f, 10f) > 0f, "No distance cutoff");
            Near(FlashlightMath.BeamMask(Vector2.right, Vector2.right, 60f, 0f), 1f, "Beam center");
            Near(FlashlightMath.BeamMask(AtAngle(30f) * 10f, Vector2.right, 60f, 0f), 1f, "Beam core edge");
            float feather = FlashlightMath.BeamMask(AtAngle(30.3f) * 10f, Vector2.right, 60f, 0f);
            Require(feather > 0f && feather < 1f, "Shared outward beam feather");
            Near(FlashlightMath.BeamMask(AtAngle(-32f) * 10f, Vector2.right, 60f, 0f), 0f, "Outside feathered beam");
            Require(FlashlightMath.RetainDirection(Vector2.zero, Vector2.up) == Vector2.up, "Retain idle direction");
            Near(FlashlightMath.RetainDirection(Vector2.one, Vector2.right).magnitude, 1f, "Normalized diagonal aim");
            Near(FlashlightMath.Illumination(new Vector2(-.35f, .35f), Vector2.right, 60f, 10f, .8f), 1f, "Player rear corner fully bright");
            Near(FlashlightMath.Illumination(new Vector2(.35f, .35f), Vector2.right, 60f, 10f, .8f), 1f, "Player front corner fully bright");
            Near(FlashlightMath.CircleMask(.86f, .8f), .5f, "Circle outer feather");
            Near(FlashlightMath.BeamMask(new Vector2(-.6f, 1f), Vector2.right, 60f, .8f), 0f, "Trapezoid rejects points behind its tangent base");
            Near(FlashlightMath.BeamMask(new Vector2(1f, .85f), Vector2.right, 60f, .8f), 1f, "Trapezoid wider than a triangle near player");
            Near(FlashlightMath.BeamMask(new Vector2(2f, (.8f + 2f * Mathf.Sin(30f * Mathf.Deg2Rad)) / Mathf.Cos(30f * Mathf.Deg2Rad)), Vector2.right, 60f, .8f), 1f, "Trapezoid full-bright side boundary");
            Near(FlashlightMath.Illumination(Vector2.up * .35f, Vector2.down, 60f, 10f, .8f), 1f, "Circle brightness independent of aim");
            float halfAngle = 30f * Mathf.Deg2Rad;
            var tangent = new Vector2(-.8f * Mathf.Sin(halfAngle), .8f * Mathf.Cos(halfAngle));
            var sideNormal = new Vector2(-Mathf.Sin(halfAngle), Mathf.Cos(halfAngle));
            Near(tangent.magnitude, .8f, "Tangent point on circle");
            Near(Vector2.Dot(tangent, sideNormal), .8f, "Side line one radius from center");
            Near(Vector2.Dot(sideNormal, new Vector2(Mathf.Cos(halfAngle), Mathf.Sin(halfAngle))), 0f, "Circle radius perpendicular to beam side");
            Near(FlashlightMath.BeamMask(tangent, Vector2.right, 60f, .8f), 1f, "Upper circle-beam tangent joins");
            Near(FlashlightMath.BeamMask(new Vector2(tangent.x, -tangent.y), Vector2.right, 60f, .8f), 1f, "Lower circle-beam tangent joins");
            Near(FlashlightMath.BeamMask(tangent + sideNormal * .13f, Vector2.right, 60f, .8f), 0f, "Outside tangent-side feather");
            Near(FlashlightMath.Illumination(new Vector2(.8f, 0f), Vector2.right, 60f, 2f, .8f), 1f, "No brightness step at circle-beam join");
            Require(FlashlightMath.Illumination(new Vector2(.81f, 0f), Vector2.right, 60f, 2f, .8f) > .999f, "Smooth brightness immediately beyond circle");
            Near(FlashlightMath.FadeDistance(1.3f, .8f), .25f, "Smooth fade reaches linear section");
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
                Require(settings.FindProperty("halfStrengthDistance").floatValue > 0f, "Positive tunable half-strength distance");
                Require(settings.FindProperty("playerLightRadius").floatValue >= 0f, "Nonnegative tunable player circle radius");
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
                material.SetFloat("_PlayerLightRadius", 0f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(1f, 0f), Expected, "GPU near strength");
                CheckPixel(camera, pixels, new Vector2(10f, 0f), Expected, "GPU half strength");
                CheckPixel(camera, pixels, new Vector2(20f, 0f), Expected, "GPU quarter strength");
                CheckPixel(camera, pixels, new Vector2(-2f, 0f), _ => 0f, "GPU outside cone black");
                CheckPixel(camera, pixels, AtAngle(30.3f) * 10f, Expected, "GPU outward edge feather");
                CheckPixel(camera, pixels, AtAngle(32f) * 10f, _ => 0f, "GPU beam limit");
                material.SetFloat("_PlayerLightRadius", .8f);
                Render(camera, target, pixels);
                foreach (var point in new[] { new Vector2(-.35f, -.35f), new Vector2(-.35f, .35f),
                    new Vector2(.35f, -.35f), new Vector2(.35f, .35f) })
                    CheckPixel(camera, pixels, point, _ => 1f, "GPU player corner fully bright");
                CheckPixel(camera, pixels, new Vector2(-.86f, 0f), CircleExpected, "GPU circle feather");
                CheckPixel(camera, pixels, new Vector2(1f, .85f), CircleExpected, "GPU trapezoid near width");
                CheckPixel(camera, pixels, new Vector2(-.2f, 1.1f), _ => 0f, "GPU trapezoid flat base");
                CheckPixel(camera, pixels, new Vector2(2f, 2.3f), _ => 0f, "GPU outside widened beam");
                var upperTangent = new Vector2(-.4f, .8f * Mathf.Cos(30f * Mathf.Deg2Rad));
                var lowerTangent = new Vector2(upperTangent.x, -upperTangent.y);
                foreach (var tangentPoint in new[] { upperTangent, lowerTangent })
                    foreach (float offset in new[] { -.1f, 0f, .1f })
                        CheckPixel(camera, pixels, tangentPoint + new Vector2(offset, 0f), CircleExpected, "GPU smooth tangent join");
                material.SetFloat("_HalfStrengthDistance", 2f);
                Render(camera, target, pixels);
                foreach (float forward in new[] { .7f, .79f, .8f, .81f, .9f, 1.1f, 1.3f, 2f })
                    CheckPixel(camera, pixels, new Vector2(forward, 0f),
                        p => FlashlightMath.Illumination(p, Vector2.right, 60f, 2f, .8f), "GPU seamless join at short fade distance");
                material.SetFloat("_HalfStrengthDistance", 10f);
                material.SetFloat("_PlayerLightRadius", .1f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, Vector2.zero, _ => 1f, "GPU small circle keeps player center bright");
                CheckPixel(camera, pixels, new Vector2(-.35f, .35f), _ => 0f, "GPU small circle leaves rear sprite corner unexposed");
                material.SetFloat("_PlayerLightRadius", .8f);
                material.SetVector("_FlashlightDirection", Vector2.up);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(-.35f, -.35f), _ => 1f, "GPU circle remains bright after aiming up");
                CheckPixel(camera, pixels, new Vector2(.85f, 1f), p => FlashlightMath.Illumination(p, Vector2.up, 60f, 10f, .8f), "GPU trapezoid rotates with aim");
                material.SetVector("_FlashlightDirection", Vector2.right);
                material.SetFloat("_AmbientBrightness", .05f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(-2f, 0f), _ => .05f, "GPU adjustable ambient");
                CheckPixel(camera, pixels, new Vector2(10f, 0f), p => .05f + .95f * CircleExpected(p), "GPU ambient plus flashlight");
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
                CheckPixel(camera, pixels, new Vector2(3f, 0f), CircleExpected, "GPU light before obstacle");
                CheckPixel(camera, pixels, new Vector2(6f, 3f), CircleExpected, "GPU light around corner");
                material.SetFloat("_SelfBlocker", 0f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(5f, 0f), CircleExpected, "GPU rock ignores own collider");
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
                CheckPixel(camera, pixels, new Vector2(10f, 0f), CircleExpected, "GPU disabled blocker transmits");
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

        private static float CircleExpected(Vector2 point) => FlashlightMath.Illumination(point, Vector2.right, 60f, 10f, .8f);
        private static float Expected(Vector2 point) => FlashlightMath.Illumination(point, Vector2.right, 60f, 10f, 0f);
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
