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
            Debug.Log("Flashlight check passed: screen-edge beam and spill, exponential falloff, persistent halo, water shading, shadows, reveal, GPU pixels, both ocean scenes, swimming.");
        }

        private static void CheckMath()
        {
            Near(FlashlightMath.Strength(10f, 10f), .5f, "Half brightness reaches ten units");
            float axialLight = FlashlightMath.Illumination(Vector2.right * 16f, Vector2.right, 125f, 10f, .75f);
            Require(axialLight > .3f, "Bright beam reaches the horizontal screen edge");
            Require(FlashlightMath.Illumination(AtAngle(55f) * 16f, Vector2.right, 125f, 10f, .75f) > .02f, "Faint spill reaches beyond the camera edge");
            Near(FlashlightMath.Illumination(Vector2.left * 12f, Vector2.right, 125f, 10f, .75f), 0f, "Unlit rear remains black");
            Near(FlashlightMath.Illumination(Vector2.right * 40f, Vector2.right, 125f, 10f, .75f), 0f, "Terminal fade lies beyond viewport");
            Near(FlashlightMath.CircleMask(.75f, .75f), 1f, "Player halo center stays lit");
            Require(FlashlightMath.CircleMask(3f, .75f) > .2f && FlashlightMath.CircleMask(6f, .75f) < .02f, "Long exponential halo shoulder");
            Require(FlashlightMath.RetainDirection(Vector2.zero, Vector2.up) == Vector2.up, "Retain idle direction");
            Near(FlashlightMath.RetainDirection(Vector2.one, Vector2.right).magnitude, 1f, "Normalized diagonal aim");
            float previous = 1f;
            for (int i = 0; i <= 2000; i++)
            {
                float value = FlashlightMath.Illumination(Vector2.right * (i * .01f), Vector2.right, 125f, 10f, .75f);
                Require(value <= previous + .00001f && previous - value < .01f, "No visible halo/beam radial seam");
                previous = value;
            }
            previous = FlashlightMath.Illumination(Vector2.right * 6f, Vector2.right, 125f, 10f, .75f);
            for (int i = 0; i <= 1800; i++)
            {
                float value = FlashlightMath.Illumination(AtAngle(i * .1f) * 6f, Vector2.right, 125f, 10f, .75f);
                Require(value <= previous + .00001f && previous - value < .01f, "No hard cone/spill angular division");
                previous = value;
            }
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
            foreach (var scenePath in new[] { "Assets/Scenes/Ocean Floor.unity", "Assets/Scenes/Ocean Interactables.unity" })
            {
            var scene = EditorSceneManager.OpenPreviewScene(scenePath);
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
                        Require(renderer.sharedMaterial != null, "Ocean sprites retain their materials");
                    }
                    foreach (var collider in root.GetComponentsInChildren<BoxCollider2D>(true))
                        if (collider.name.StartsWith("Rock ") || collider.name.EndsWith(" boundary")) solids++;
                    foreach (var light in root.GetComponentsInChildren<Light2D>(true))
                        Require(!light.enabled, "Ocean global light disabled");
                }
                Require(controller != null && sprites > 0 && (scenePath.Contains("Interactables") || solids == 11), "Ocean controller and solids present");
                var vfx = controller.GetComponent<OceanVfx>();
                Require(vfx != null && new SerializedObject(vfx).FindProperty("particleShader").objectReferenceValue != null, "Ocean particle effects wired");
                Require(new SerializedObject(vfx).FindProperty("waterOverlayShader").objectReferenceValue != null, "Water overlay wired in both ocean scenes");
                var settings = new SerializedObject(controller);
                Require(settings.FindProperty("aimMode").enumValueIndex == 0, "Mouse aim default");
                Near(settings.FindProperty("ambientBrightness").floatValue, 0f, "Black ambient default");
                float configuredAngle = settings.FindProperty("coneAngle").floatValue;
                Require(configuredAngle >= 2f && configuredAngle <= 180f, "Valid tunable cone angle");
                Require(settings.FindProperty("halfStrengthDistance").floatValue > 0f, "Positive tunable half-strength distance");
                Require(settings.FindProperty("playerLightRadius").floatValue >= 0f, "Nonnegative tunable player circle radius");
                Require(settings.FindProperty("aimCamera").objectReferenceValue != null, "Camera assigned");
                Require(settings.FindProperty("map").objectReferenceValue != null, "Map assigned");
                Require(settings.FindProperty("spriteMaterial").objectReferenceValue != null, "Material assigned");
                var blockers = settings.FindProperty("blockers");
                Require(blockers.arraySize == (scenePath.Contains("Interactables") ? 4 : 11), "Scene solids registered");
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
                material.SetFloat("_PlayerLightRadius", .75f);
                material.SetFloat("_WaterStrength", 0f);
                foreach (var aim in new[] { Vector2.right, Vector2.up, new Vector2(-1f, -1f).normalized })
                {
                    material.SetVector("_FlashlightDirection", aim);
                    Render(camera, target, pixels);
                    foreach (float distance in new[] { .5f, .75f, 1.35f, 3f, 6f, 9f, 12f, 16f, 18f })
                        foreach (float angle in new[] { 0f, 22f, 26f, 30f, 36f, 45f, 51f, 120f, 180f })
                        {
                            float rotation = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                            CheckPixel(camera, pixels, AtAngle(rotation + angle) * distance,
                                point => FlashlightMath.Illumination(point, aim, 125f, 10f, .75f), "GPU layered light field");
                        }
                }
                material.SetVector("_FlashlightDirection", Vector2.right);
                var boxObject = new GameObject("GPU check blocker");
                SceneManager.MoveGameObjectToScene(boxObject, scene);
                var box = boxObject.AddComponent<BoxCollider2D>();
                boxObject.transform.position = new Vector3(3f, 0f, 0f);
                box.size = new Vector2(.5f, 6f);
                var xs = new Vector4[FlashlightMath.MaxBlockers];
                var ys = new Vector4[FlashlightMath.MaxBlockers];
                FlashlightMath.PackBox(box, out xs[0], out ys[0]);
                material.SetInt("_BlockerCount", 1);
                material.SetVectorArray("_BlockerRowsX", xs);
                material.SetVectorArray("_BlockerRowsY", ys);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(4f, 0f), p => FlashlightMath.CircleMask(p.magnitude, .75f), "GPU beam blocked");
                CheckPixel(camera, pixels, AtAngle(33f) * 4f, p => FlashlightMath.CircleMask(p.magnitude, .75f), "GPU spill blocked");
                material.SetFloat("_SelfBlocker", 0f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(3f, 0f), p => FlashlightMath.Illumination(p, Vector2.right, 125f, 10f, .75f), "GPU self-shadow exclusion");
                material.SetFloat("_SelfBlocker", -1f);
                boxObject.transform.position = Vector3.zero;
                FlashlightMath.PackBox(box, out xs[0], out ys[0]);
                material.SetVectorArray("_BlockerRowsX", xs);
                material.SetVectorArray("_BlockerRowsY", ys);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(-.4f, .1f), _ => 1f, "GPU halo survives blocker");
                CheckPixel(camera, pixels, new Vector2(-1.35f, 0f), p => FlashlightMath.CircleMask(p.magnitude, .75f), "GPU halo feather survives blocker");
                material.SetInt("_BlockerCount", 0);
                material.SetInt("_EdgeCount", 1);
                material.SetVectorArray("_PolygonEdges", new[] { new Vector4(3f, -2f, 3f, 2f) });
                material.SetFloatArray("_EdgeOwners", new[] { 32f });
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(4f, 0f), p => FlashlightMath.CircleMask(p.magnitude, .75f), "GPU polygon shadow");
                material.SetInt("_EdgeCount", 0);
                material.SetFloat("_SceneryEffects", 1f);
                material.SetVector("_SpriteUVRect", new Vector4(0f, 0f, 1f, 1f));
                material.SetFloat("_WaterStrength", .1f);
                material.SetFloat("_OceanTime", 5f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(-12f, 0f), _ => 0f, "GPU waves cannot reveal darkness");
                var lit = ReadPixel(camera, pixels, new Vector2(3f, 0f), out var litWorld);
                float reference = FlashlightMath.Illumination(litWorld, Vector2.right, 125f, 10f, .75f);
                Require(lit.r >= reference * .9f && lit.r <= reference * 1.1f, "Water shimmer within ten percent");
                material.SetFloat("_Reveal", 1f);
                Render(camera, target, pixels);
                CheckPixel(camera, pixels, new Vector2(-3f, 0f), _ => 1f, "GPU prospecting reveal preserved");
                material.SetFloat("_Reveal", 0f);
                material.SetFloat("_SceneryEffects", 0f);
                var particleShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Prototype/Lighting/OceanParticles.shader");
                Require(particleShader != null && particleShader.isSupported && !ShaderUtil.ShaderHasError(particleShader), "Particle shader compiles");
                renderer.color = new Color(.4f, .7f, .2f, .6f);
                material.SetFloat("_AmbientBrightness", 1f);
                Render(camera, target, pixels);
                var tinted = ReadPixel(camera, pixels, Vector2.zero, out _);
                Near(tinted.a, .6f, "GPU preserves sprite alpha", .005f);
                // RGB blending against a clear target multiplies by alpha; tint conversion depends on project color space.
                var tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? renderer.color.linear : renderer.color;
                Near(tinted.r, tint.r * .6f, "GPU preserves red sprite tint", .005f);
                Near(tinted.g, tint.g * .6f, "GPU preserves green sprite tint", .005f);
                CheckSurfaceAndParticles(camera, renderer, material, target, pixels, particleShader);
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

        private static void CheckSurfaceAndParticles(Camera camera, SpriteRenderer renderer, Material material,
            RenderTexture target, Texture2D pixels, Shader particleShader)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Sprite patterned = null;
            Material particles = null;
            try
            {
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        bool painted = new Vector2(x - 15.5f, y - 15.5f).magnitude < 13f;
                        texture.SetPixel(x, y, new Color(x / 31f, y / 31f, .3f, painted ? 1f : 0f));
                    }
                texture.Apply();
                patterned = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32f);
                renderer.sprite = patterned;
                renderer.transform.localScale = Vector3.one * 2f;
                renderer.color = Color.white;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographicSize = 2f;
                material.SetFloat("_AmbientBrightness", 0f);
                material.SetFloat("_SceneryEffects", 0f);
                Render(camera, target, pixels);
                var original = pixels.GetPixels();
                material.SetFloat("_SceneryEffects", 1f);
                material.SetFloat("_RefractionTexels", 1f);
                material.SetFloat("_WaterStrength", .08f);
                material.SetFloat("_OceanTime", 7f);
                Render(camera, target, pixels);
                var refracted = pixels.GetPixels();
                bool changed = false;
                for (int i = 0; i < original.Length; i++)
                {
                    Require(Mathf.Abs(original[i].a - refracted[i].a) < .0001f, "Water refraction preserves original alpha silhouette");
                    if (Mathf.Abs(original[i].r - refracted[i].r) > .005f) changed = true;
                }
                Require(changed, "Water refraction animates painted colors");
                particles = new Material(particleShader);
                particles.SetVector("_FlashlightOrigin", Vector4.zero);
                particles.SetVector("_FlashlightDirection", Vector2.right);
                particles.SetFloat("_PlayerLightRadius", .75f);
                particles.SetFloat("_HaloFeather", 2.5f);
                particles.SetFloat("_CoreAngle", 44f);
                particles.SetFloat("_BeamIntensity", 1f);
                particles.SetFloat("_ConeAngle", 125f);
                particles.SetFloat("_HalfStrengthDistance", 10f);
                particles.SetFloat("_BeamFadeStart", 24f);
                particles.SetFloat("_BeamRange", 36f);
                particles.SetFloat("_SpillInnerAngle", 90f);
                particles.SetFloat("_SpillAngle", 178f);
                particles.SetFloat("_SpillIntensity", .18f);
                particles.SetFloat("_SpillFadeStart", 22f);
                particles.SetFloat("_SpillRange", 34f);
                particles.SetFloat("_SelfBlocker", -1f);
                particles.SetFloat("_BubbleStyle", 1f);
                renderer.sharedMaterial = particles;
                Render(camera, target, pixels);
                Require(ReadPixel(camera, pixels, new Vector2(-.32f, .38f), out _).a > .2f, "Procedural bubble highlight visible in halo");
                renderer.transform.position = new Vector3(-12f, 0f, 0f);
                camera.transform.position = new Vector3(-12f, 0f, -10f);
                Render(camera, target, pixels);
                Near(ReadPixel(camera, pixels, new Vector2(-12.32f, .38f), out _).a, 0f, "Bubbles disappear in darkness");
                particles.SetFloat("_BubbleStyle", 0f);
                Render(camera, target, pixels);
                Near(ReadPixel(camera, pixels, new Vector2(-12f, 0f), out _).a, 0f, "Suspended particles disappear in darkness");
            }
            finally
            {
                renderer.sharedMaterial = material;
                renderer.sprite = null;
                if (particles != null) UnityEngine.Object.DestroyImmediate(particles);
                if (patterned != null) UnityEngine.Object.DestroyImmediate(patterned);
                UnityEngine.Object.DestroyImmediate(texture);
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
