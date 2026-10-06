using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DivingPrototype
{
    public static class FlashlightPlayCheck
    {
        [MenuItem("Diving Prototype/Check Flashlight In Play Mode")]
        public static void Run()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying, "Enter Play mode in Ocean Floor before running this check.");
            var controller = UnityEngine.Object.FindAnyObjectByType<FlashlightController>();
            FlashlightCheck.Require(controller != null, "Ocean Floor flashlight must be active.");
            var settings = new SerializedObject(controller);
            var camera = settings.FindProperty("aimCamera").objectReferenceValue as Camera;
            var swimmer = controller.GetComponent<SwimmerController>();
            var body = controller.GetComponent<Rigidbody2D>();
            var centered = camera.GetComponent<CenteredCamera>();
            int oldMode = settings.FindProperty("aimMode").enumValueIndex;
            float oldAmbient = settings.FindProperty("ambientBrightness").floatValue;
            float oldRadius = settings.FindProperty("playerLightRadius").floatValue;
            var oldPosition = body.position;
            var oldScale = controller.transform.localScale;
            var oldVelocity = body.linearVelocity;
            var oldCameraPosition = camera.transform.position;
            var oldTarget = camera.targetTexture;
            var oldBackground = camera.backgroundColor;
            float oldSize = camera.orthographicSize;
            bool oldPaused = EditorApplication.isPaused;
            var oldKeyboard = Keyboard.current;
            var oldMouse = Mouse.current;
            Keyboard keyboard = null;
            Mouse mouse = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                EditorApplication.isPaused = true;
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                keyboard.MakeCurrent();
                mouse.MakeCurrent();
                target = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.Create();
                pixels = new Texture2D(1024, 1024, TextureFormat.RGBAFloat, false, true);
                camera.targetTexture = target;
                camera.orthographicSize = 6.5f;
                body.position = Vector2.zero;
                controller.transform.position = Vector3.zero;
                body.linearVelocity = Vector2.zero;
                centered.SendMessage("LateUpdate");
                Set(settings, "aimMode", 1);
                var keys = new[] {
                    new[] { Key.D }, new[] { Key.W, Key.D }, new[] { Key.W }, new[] { Key.W, Key.A },
                    new[] { Key.A }, new[] { Key.S, Key.A }, new[] { Key.S }, new[] { Key.S, Key.D },
                    new[] { Key.UpArrow, Key.RightArrow }
                };
                var directions = new[] { Vector2.right, Vector2.one.normalized, Vector2.up,
                    new Vector2(-1f, 1f).normalized, Vector2.left, -Vector2.one.normalized,
                    Vector2.down, new Vector2(1f, -1f).normalized, Vector2.one.normalized };
                for (int i = 0; i < keys.Length; i++)
                {
                    InputState.Change(keyboard, new KeyboardState(keys[i]));
                    swimmer.SendMessage("Update");
                    controller.RefreshLighting();
                    Direction(controller.AimDirection, directions[i], "Keyboard direction " + i);
                    Direction(swimmer.MovementInput, directions[i], "Shared movement input " + i);
                }
                InputState.Change(keyboard, new KeyboardState());
                swimmer.SendMessage("Update");
                controller.RefreshLighting();
                Direction(controller.AimDirection, Vector2.one.normalized, "Retain keyboard aim on release");
                Set(settings, "aimMode", 0);
                AimMouse(mouse, camera, controller, Vector2.right);
                Direction(controller.AimDirection, Vector2.right, "Mouse aim right");
                var material = controller.GetComponent<SpriteRenderer>().sharedMaterial;
                FlashlightCheck.Require(material.name == "Ocean Flashlight (Instance)", "Lighting uses a scene-local material instance");
                FlashlightCheck.Require(material.GetInt("_BlockerCount") == 11, "Runtime uploads all blockers");
                var playerProperties = new MaterialPropertyBlock();
                controller.GetComponent<SpriteRenderer>().GetPropertyBlock(playerProperties);
                FlashlightCheck.Near(playerProperties.GetFloat("_SelfBlocker"), -1f, "Player does not block light");
                var boxes = settings.FindProperty("blockers");
                for (int i = 0; i < boxes.arraySize; i++)
                {
                    var box = boxes.GetArrayElementAtIndex(i).objectReferenceValue as BoxCollider2D;
                    var properties = new MaterialPropertyBlock();
                    box.GetComponent<SpriteRenderer>().GetPropertyBlock(properties);
                    FlashlightCheck.Near(properties.GetFloat("_SelfBlocker"), i, "Rock self-shadow exclusion");
                }
                foreach (float radius in new[] { 0f, .1f, .25f, .8f })
                {
                    Set(settings, "playerLightRadius", radius);
                    controller.RefreshLighting();
                    FlashlightCheck.Near(material.GetFloat("_PlayerLightRadius"), radius, "Inspector radius uploads directly");
                }
                FlashlightCheck.Render(camera, target, pixels);
                FlashlightCheck.Near(Pixel(camera, pixels, new Vector2(-.35f, .35f)).r, 1f, "Large configured circle lights rear sprite corner", .005f);
                Set(settings, "playerLightRadius", .1f);
                controller.RefreshLighting();
                FlashlightCheck.Render(camera, target, pixels);
                FlashlightCheck.Near(Pixel(camera, pixels, Vector2.zero).r, 1f, "Small configured circle lights player center", .005f);
                FlashlightCheck.Near(Pixel(camera, pixels, new Vector2(-.35f, .35f)).r, 0f, "Small configured circle leaves rear sprite corner dark", .005f);
                controller.transform.localScale = oldScale * 2f;
                controller.RefreshLighting();
                FlashlightCheck.Near(material.GetFloat("_PlayerLightRadius"), .1f, "Sprite scaling does not override configured radius");
                controller.transform.localScale = oldScale;
                Set(settings, "playerLightRadius", oldRadius);
                controller.RefreshLighting();
                FlashlightCheck.Render(camera, target, pixels);
                var darkness = Pixel(camera, pixels, new Vector2(-3f, 0f));
                FlashlightCheck.Near(darkness.r + darkness.g + darkness.b, 0f, "Actual ocean outside cone black", .001f);
                var shadow = Pixel(camera, pixels, new Vector2(6.25f, 0f));
                FlashlightCheck.Near(shadow.r + shadow.g + shadow.b, 0f, "Actual ocean behind east rock black", .001f);
                var rockFace = Pixel(camera, pixels, new Vector2(4.5f, 0f));
                FlashlightCheck.Require(rockFace.g > .001f, "Actual east rock illuminated despite its own collider");
                var ground = Pixel(camera, pixels, new Vector2(2f, 0f));
                FlashlightCheck.Require(ground.g > .001f, "Actual ocean ground illuminated before rock");
                AimMouse(mouse, camera, controller, new Vector2(1f, .55f));
                FlashlightCheck.Render(camera, target, pixels);
                var corner = Pixel(camera, pixels, new Vector2(6f, 3.3f));
                FlashlightCheck.Require(corner.g > .001f, "Actual light passes around east rock corner");
                // Move the player and run the same LateUpdate ordering as the live scene.
                body.position = new Vector2(2f, -3f);
                controller.transform.position = new Vector3(2f, -3f, 0f);
                centered.SendMessage("LateUpdate");
                AimMouse(mouse, camera, controller, Vector2.up);
                Direction(controller.AimDirection, Vector2.up, "Mouse aim after camera follows swimming");
                Direction((Vector2)camera.transform.position, body.position, "Camera stays centered");
                InputState.Change(mouse, new MouseState { position = camera.WorldToScreenPoint(controller.transform.position) });
                controller.RefreshLighting();
                Direction(controller.AimDirection, Vector2.up, "Retain aim when cursor overlaps player");
                InputSystem.RemoveDevice(mouse);
                mouse = null;
                if (Mouse.current == null)
                {
                    controller.RefreshLighting();
                    Direction(controller.AimDirection, Vector2.up, "Retain aim without mouse");
                }
                else
                {
                    // Real mice can still exist; test the zero-candidate fallback directly.
                    Direction(FlashlightMath.RetainDirection(Vector2.zero, controller.AimDirection), Vector2.up, "Missing input fallback");
                }
                Set(settings, "ambientBrightness", .05f);
                controller.RefreshLighting();
                FlashlightCheck.Render(camera, target, pixels);
                var ambient = Pixel(camera, pixels, body.position + Vector2.down * 2f);
                FlashlightCheck.Require(ambient.g > .001f, "Actual ocean tunable ambient reveals unexposed ground");
                FlashlightCheck.Near(material.GetFloat("_AmbientBrightness"), .05f, "Runtime ambient uploads");
                FlashlightCheck.Require(camera.backgroundColor.g > 0f, "Camera background follows ambient setting");
                SwimmingCheck.Run();
                Debug.Log("Flashlight Play mode check passed: direct Inspector radius control, small-circle player visibility and sprite-size independence, trapezoid, 8 keyboard directions/arrows, mouse aim, camera following, idle/overlap fallback, actual rock shadows/surfaces/corners, ambient, swimming.");
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
                if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent();
                Set(settings, "aimMode", oldMode);
                Set(settings, "ambientBrightness", oldAmbient);
                Set(settings, "playerLightRadius", oldRadius);
                controller.transform.localScale = oldScale;
                body.position = oldPosition;
                controller.transform.position = new Vector3(oldPosition.x, oldPosition.y, controller.transform.position.z);
                body.linearVelocity = oldVelocity;
                swimmer.SendMessage("Update");
                camera.targetTexture = oldTarget;
                camera.orthographicSize = oldSize;
                camera.transform.position = oldCameraPosition;
                controller.RefreshLighting();
                camera.backgroundColor = oldBackground;
                EditorApplication.isPaused = oldPaused;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
        }

        private static void AimMouse(Mouse mouse, Camera camera, FlashlightController controller, Vector2 direction)
        {
            mouse.MakeCurrent();
            InputState.Change(mouse, new MouseState { position = camera.WorldToScreenPoint(controller.transform.position + (Vector3)direction * 3f) });
            controller.RefreshLighting();
        }
        private static Color Pixel(Camera camera, Texture2D pixels, Vector2 world)
        {
            Vector3 point = camera.WorldToScreenPoint(world);
            return pixels.GetPixel(Mathf.Clamp(Mathf.FloorToInt(point.x), 0, pixels.width - 1),
                Mathf.Clamp(Mathf.FloorToInt(point.y), 0, pixels.height - 1));
        }
        private static void Direction(Vector2 actual, Vector2 expected, string message) =>
            FlashlightCheck.Require(Vector2.Distance(actual, expected) < .001f, message + $": expected {expected}, got {actual}");
        private static void Set(SerializedObject settings, string name, int value)
        {
            settings.Update();
            settings.FindProperty(name).enumValueIndex = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(SerializedObject settings, string name, float value)
        {
            settings.Update();
            settings.FindProperty(name).floatValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
