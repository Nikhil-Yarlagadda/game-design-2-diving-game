using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace DivingPrototype
{
    public static class OceanVfxCheck
    {
        [MenuItem("Diving Prototype/Check Underwater VFX In Play Mode")]
        public static void Run()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying, "Enter an ocean scene in Play mode first.");
            var vfx = UnityEngine.Object.FindAnyObjectByType<OceanVfx>();
            FlashlightCheck.Require(vfx != null && vfx.Bubbles != null && vfx.Suspended != null, "Ocean particle systems exist");
            var light = vfx.GetComponent<FlashlightController>();
            var update = (Action)Delegate.CreateDelegate(typeof(Action), vfx, typeof(OceanVfx).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance));
            var end = (Action)Delegate.CreateDelegate(typeof(Action), vfx, typeof(OceanVfx).GetMethod("EndOcean", BindingFlags.NonPublic | BindingFlags.Instance));
            var body = vfx.GetComponent<Rigidbody2D>();
            var previousVelocity = body.linearVelocity;
            float timeScale = Time.timeScale;
            try
            {
                FlashlightCheck.Require(vfx.Bubbles.main.maxParticles == OceanVfx.BubbleLimit && vfx.Suspended.main.maxParticles == OceanVfx.MoteLimit, "Particle limits bounded");
                FlashlightCheck.Require(vfx.Bubbles.main.simulationSpace == ParticleSystemSimulationSpace.World, "Bubbles use world simulation");
                FlashlightCheck.Require(vfx.Suspended.particleCount > 0, "Suspended particles seeded");
                var renderer = light.GetComponent<SpriteRenderer>();
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                FlashlightCheck.Near(properties.GetFloat("_SceneryEffects"), 0f, "Player excluded from refraction");
                light.RefreshLighting();
                FlashlightCheck.Require(vfx.WaterOverlay != null && vfx.WaterOverlay.sortingOrder == 25, "Water ripples overlay exists below labels and UI");
                float viewCorner = light.AimCamera.orthographicSize * Mathf.Sqrt(1f + light.AimCamera.aspect * light.AimCamera.aspect);
                FlashlightCheck.Require(renderer.sharedMaterial.GetFloat("_BeamFadeStart") > viewCorner
                    && renderer.sharedMaterial.GetFloat("_SpillFadeStart") > viewCorner, "Both distance fades begin beyond the viewport corners");
                foreach (var particles in new[] { vfx.Bubbles, vfx.Suspended })
                {
                    var material = particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                    FlashlightCheck.Near(material.GetFloat("_SpillIntensity"), .18f, "Particle spill matches scenery");
                    FlashlightCheck.Require(material.GetVector("_FlashlightOrigin") == renderer.sharedMaterial.GetVector("_FlashlightOrigin"), "Particle light tracks player");
                }
                CheckWaterOverlay(vfx, light);
                body.linearVelocity = Vector2.right * 3f;
                for (int i = 0; i < 100; i++) { update(); light.RefreshLighting(); }
                FlashlightCheck.Require(vfx.Bubbles.particleCount > 0 && vfx.Bubbles.particleCount <= OceanVfx.BubbleLimit, "Movement emits bounded bubble trail");
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 100; i++) { update(); light.RefreshLighting(); }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                FlashlightCheck.Require(allocated == 0, "VFX and light steady-state allocate zero managed bytes: " + allocated);
                Time.timeScale = 0f;
                // Unity applies deltaTime on the next frame; the material uses scaled Time.time.
                float frozenTime = renderer.sharedMaterial.GetFloat("_OceanTime");
                light.RefreshLighting();
                FlashlightCheck.Near(renderer.sharedMaterial.GetFloat("_OceanTime"), frozenTime, "Water shader time stable within paused frame");
                end();
                FlashlightCheck.Require(vfx.Bubbles.particleCount == 0 && vfx.Suspended.particleCount == 0 && !vfx.WaterOverlay.enabled, "Ocean departure clears particles");
                Debug.Log("Underwater VFX Play check passed: caps, world particles, player exclusion, synchronized lighting, movement emission, zero steady-state allocations, scaled shader clock, ocean departure cleanup.");
            }
            finally
            {
                Time.timeScale = timeScale;
                body.linearVelocity = previousVelocity;
                vfx.enabled = false;
                vfx.enabled = true;
                light.RefreshLighting();
            }
        }

        private static void CheckWaterOverlay(OceanVfx vfx, FlashlightController light)
        {
            var camera = light.AimCamera;
            var previousTarget = camera.targetTexture;
            var previousRect = camera.rect;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(640, 360, TextureFormat.RGBAFloat, false, true);
            var water = vfx.WaterOverlay.sharedMaterial;
            float previousTime = water.GetFloat("_OceanTime");
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                vfx.WaterOverlay.enabled = true;
                FlashlightCheck.Render(camera, target, pixels);
                var withWater = pixels.GetPixels();
                vfx.WaterOverlay.enabled = false;
                FlashlightCheck.Render(camera, target, pixels);
                var withoutWater = pixels.GetPixels();
                int visibleWater = 0;
                for (int i = 0; i < withWater.Length; i++)
                {
                    if (Mathf.Abs(withWater[i].g - withoutWater[i].g) > .001f) visibleWater++;
                    if (withoutWater[i].r > .85f && withoutWater[i].g > .85f && withoutWater[i].b > .85f)
                        FlashlightCheck.Require(Mathf.Abs(withWater[i].r - withoutWater[i].r) < .001f, "Water veil preserves white HUD glyphs");
                }
                FlashlightCheck.Require(visibleWater > 100, "Ripple overlay visibly changes the lit world");
                vfx.WaterOverlay.enabled = true;
                water.SetFloat("_OceanTime", previousTime + 4f);
                FlashlightCheck.Render(camera, target, pixels);
                var animated = pixels.GetPixels();
                int moved = 0;
                for (int i = 0; i < withWater.Length; i++)
                    if (Mathf.Abs(withWater[i].g - animated[i].g) > .001f) moved++;
                FlashlightCheck.Require(moved > 100, "Water ripples animate across the world");
            }
            finally
            {
                water.SetFloat("_OceanTime", previousTime);
                vfx.WaterOverlay.enabled = true;
                camera.targetTexture = previousTarget;
                camera.rect = previousRect;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static double profileStart;
        private static int profileFrames;
        private static int lastFrame;
        private static Camera profileCamera;
        private static RenderTexture captureTarget;
        private static RenderTexture previousTarget;
        private static Rect previousRect;
        private static Vector3 oldPosition;
        private static Vector3 oldCameraPosition;
        private static Quaternion oldRotation;
        private static bool oldSwimmerEnabled;
        private static FlashlightController profileLight;
        private static SwimmerController profileSwimmer;
        private static Mouse testMouse, oldMouse;
        private static bool profiling;
        private static int previousCaptureRate;
        private static FixedOceanView fixedView;
        private static bool previousFixedViewEnabled;
        private static readonly double[] renderMilliseconds = new double[60];
        private static double renderStart;

        [MenuItem("Diving Prototype/Capture And Profile Underwater VFX")]
        public static void Capture()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying && !profiling, "Enter ocean Play mode; wait for any existing capture to finish.");
            profileLight = UnityEngine.Object.FindAnyObjectByType<FlashlightController>();
            FlashlightCheck.Require(profileLight != null, "Ocean flashlight required");
            profileCamera = profileLight.AimCamera;
            profileSwimmer = profileLight.GetComponent<SwimmerController>();
            oldSwimmerEnabled = profileSwimmer.enabled;
            oldPosition = profileLight.transform.position;
            oldRotation = profileLight.transform.rotation;
            oldCameraPosition = profileCamera.transform.position;
            previousRect = profileCamera.rect;
            previousTarget = profileCamera.targetTexture;
            captureTarget = new RenderTexture(1920, 1080, 24);
            captureTarget.Create();
            profileCamera.targetTexture = captureTarget;
            profileCamera.rect = new Rect(0f, 0f, 1f, 1f);
            profileSwimmer.enabled = false;
            oldMouse = Mouse.current;
            testMouse = InputSystem.AddDevice<Mouse>();
            profileLight.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            Vector3 previewPosition = new Vector3(4f, -5f, oldPosition.z);
            var ocean = profileLight.GetComponent<OceanInteractions>();
            Collider2D previewRock = null;
            if (ocean != null)
                foreach (var obstacle in ocean.obstacles)
                    if (obstacle != null && obstacle.name.StartsWith("Rock")
                        && (previewRock == null || obstacle.bounds.center.sqrMagnitude < previewRock.bounds.center.sqrMagnitude))
                        previewRock = obstacle;
            if (previewRock != null)
                previewPosition = new Vector3(previewRock.bounds.min.x - 2.8f, previewRock.bounds.center.y - .8f, oldPosition.z);
            profileLight.transform.position = previewPosition;
            profileCamera.transform.position = new Vector3(previewPosition.x, previewPosition.y, oldCameraPosition.z);
            previousCaptureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            fixedView = UnityEngine.Object.FindAnyObjectByType<FixedOceanView>();
            if (fixedView != null) { previousFixedViewEnabled = fixedView.enabled; fixedView.enabled = false; }
            profileFrames = 0;
            lastFrame = -1;
            profiling = true;
            profileStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += ProfileFrame;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
            Directory.CreateDirectory("Temp/underwater-vfx");
        }

        private static void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == profileCamera) renderStart = EditorApplication.timeSinceStartup;
        }
        private static void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == profileCamera && profileFrames >= 120 && profileFrames < 180)
                renderMilliseconds[profileFrames - 120] = (EditorApplication.timeSinceStartup - renderStart) * 1000.0;
        }

        private static void ProfileFrame()
        {
            if (!EditorApplication.isPlaying || profileLight == null) { FinishProfile(false); return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                // Aim smoothly across rocks; move, stop and turn using an isolated test input device.
                float t = profileFrames / 60f;
                Vector2 aim = new Vector2(Mathf.Cos(.2f + Mathf.Sin(t * 1.3f) * .35f), Mathf.Sin(.2f + Mathf.Sin(t * 1.3f) * .35f));
                InputState.Change(testMouse, new MouseState { position = profileCamera.WorldToScreenPoint(profileLight.transform.position + (Vector3)aim * 3f) });
                profileLight.GetComponent<Rigidbody2D>().linearVelocity = profileFrames < 80 ? new Vector2(.7f, .3f) : Vector2.zero;
                profileLight.RefreshLighting();
                if (profileFrames % 6 == 0) SaveFrame("motion-" + (profileFrames / 6).ToString("D3") + ".png");
                if (profileFrames == 60 || profileFrames == 120 || profileFrames == 175) SaveFrame("vfx-" + profileFrames + ".png");
                profileFrames++;
                if (profileFrames >= 180) FinishProfile(true);
            }
            catch (Exception exception) { Debug.LogException(exception); FinishProfile(false); }
        }

        private static void SaveFrame(string name)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(captureTarget.width, captureTarget.height, TextureFormat.RGB24, false);
            RenderTexture.active = captureTarget;
            pixels.ReadPixels(new Rect(0, 0, captureTarget.width, captureTarget.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Temp/underwater-vfx/" + name, pixels.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(pixels);
            RenderTexture.active = previous;
        }

        private static void FinishProfile(bool success)
        {
            EditorApplication.update -= ProfileFrame;
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            if (success)
            {
                double elapsed = EditorApplication.timeSinceStartup - profileStart;
                Array.Sort(renderMilliseconds);
                string report = $"1080p Unity Editor preview: {profileFrames} frames in {elapsed:F2}s; average {profileFrames / elapsed:F1} FPS.\\nCamera callback median: {renderMilliseconds[30]:F3}ms (zero means unsupported by SRP).\\nEditor timing includes UI overhead and is not a standalone player benchmark.\\n";
                File.WriteAllText("Temp/underwater-vfx/profile.txt", report.Replace("\\n", "\n"));
                Debug.Log(report);
            }
            if (profileCamera != null) { profileCamera.targetTexture = previousTarget; profileCamera.rect = previousRect; profileCamera.transform.position = oldCameraPosition; }
            if (profileLight != null)
            {
                profileLight.transform.SetPositionAndRotation(oldPosition, oldRotation);
                profileLight.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                profileSwimmer.enabled = oldSwimmerEnabled;
            }
            if (testMouse != null) InputSystem.RemoveDevice(testMouse);
            if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent();
            if (captureTarget != null) { captureTarget.Release(); UnityEngine.Object.DestroyImmediate(captureTarget); }
            Time.captureFramerate = previousCaptureRate;
            if (fixedView != null) fixedView.enabled = previousFixedViewEnabled;
            profiling = false;
        }
    }
}
