using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DivingPrototype
{
    [InitializeOnLoad]
    public static class OceanMapResizeService
    {
        public const string ScenePath = "Assets/Scenes/Ocean Interactables.unity";

        static OceanMapResizeService()
        {
            EditorSceneManager.sceneClosed += scene => {
                foreach (var baseline in Resources.FindObjectsOfTypeAll<OceanMapResizeBaseline>())
                    if (!EditorUtility.IsPersistent(baseline) && baseline.sceneHandle == scene.handle.GetRawData())
                        Object.DestroyImmediate(baseline);
            };
        }

        public static string Id(Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();

        public static T Resolve<T>(string id) where T : Object =>
            GlobalObjectId.TryParse(id, out var parsed) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as T : null;

        public static OceanInteractions FindOcean(Scene scene) => scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<OceanInteractions>(true)).SingleOrDefault()
            : null;

        public static OceanMapResizeBaseline FindBaseline(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            return Resources.FindObjectsOfTypeAll<OceanMapResizeBaseline>()
                .FirstOrDefault(b => !EditorUtility.IsPersistent(b) && b.sceneHandle == scene.handle.GetRawData());
        }

        public static OceanMapResizeBaseline Capture(OceanInteractions ocean)
        {
            RequireEditable(ocean);
            var map = ocean.oceanMap.transform;
            var submarines = map.GetComponentsInChildren<OceanTarget>(true)
                .Where(t => t.kind == OceanTarget.TargetKind.Submarine).ToArray();
            if (submarines.Length != 1) throw new InvalidOperationException("The map must contain exactly one submarine.");
            var transforms = map.GetComponentsInChildren<Transform>(true).Where(t => t != map).ToArray();
            var entries = new List<OceanMapResizeBaseline.Entry>();
            foreach (var t in transforms)
            {
                var shape = Classify(t);
                var renderer = t.GetComponent<SpriteRenderer>();
                var polygons = t.GetComponents<PolygonCollider2D>();
                if (polygons.Length > 0 && (renderer == null || renderer.sprite == null || renderer.drawMode == SpriteDrawMode.Tiled))
                    throw new InvalidOperationException(t.name + " needs a Simple or Sliced SpriteRenderer for polygon resizing.");
                if (polygons.Length > 0 && renderer.sprite.border != Vector4.zero)
                    throw new InvalidOperationException(t.name + " has an unsupported bordered terrain sprite.");
                var entry = new OceanMapResizeBaseline.Entry {
                    id = Id(t), parentId = Id(t.parent), name = t.name, componentIds = ComponentIds(t),
                    position = t.position, localPosition = t.localPosition, localRotation = t.localRotation,
                    localScale = t.localScale, worldMatrix = t.localToWorldMatrix,
                    movePosition = IsIndependent(t, map), shape = shape,
                    rendererId = renderer == null ? null : Id(renderer),
                    originalSprite = renderer == null ? null : renderer.sprite,
                    rendererDrawMode = renderer == null ? SpriteDrawMode.Simple : renderer.drawMode,
                    rendererSize = renderer == null ? Vector2.zero : renderer.size,
                    visualSize = renderer == null || renderer.sprite == null ? Vector2.zero :
                        renderer.drawMode == SpriteDrawMode.Simple ? (Vector2)renderer.sprite.bounds.size : renderer.size,
                    polygons = polygons.Select(p => new OceanMapResizeBaseline.Polygon {
                        id = Id(p), offset = p.offset,
                        paths = Enumerable.Range(0, p.pathCount).Select(i => new OceanMapResizeBaseline.Path { points = p.GetPath(i) }).ToArray()
                    }).ToArray(),
                    boxes = shape == OceanMapResizeBaseline.Shape.Boundary ? t.GetComponents<BoxCollider2D>()
                        .Select(b => new OceanMapResizeBaseline.Box { id = Id(b), offset = b.offset, size = b.size }).ToArray()
                        : Array.Empty<OceanMapResizeBaseline.Box>()
                };
                if (polygons.Length > 0 && t.localToWorldMatrix.determinant == 0)
                    throw new InvalidOperationException(t.name + " has a non-invertible terrain transform.");
                entries.Add(entry);
            }
            var seabed = transforms.SingleOrDefault(t => t.name == "Seabed")?.GetComponent<SpriteRenderer>();
            if (seabed == null) throw new InvalidOperationException("The map needs its Seabed renderer to measure dimensions.");

            var baseline = FindBaseline(ocean.gameObject.scene);
            if (baseline == null)
            {
                baseline = ScriptableObject.CreateInstance<OceanMapResizeBaseline>();
                baseline.hideFlags = HideFlags.HideAndDontSave;
            }
            Undo.RegisterCompleteObjectUndo(baseline, "Capture Ocean Map Baseline");
            baseline.sceneGuid = AssetDatabase.AssetPathToGUID(ocean.gameObject.scene.path);
            baseline.sceneHandle = ocean.gameObject.scene.handle.GetRawData();
            baseline.mapId = Id(map);
            baseline.submarineId = Id(submarines[0].transform);
            baseline.anchor = submarines[0].transform.position;
            baseline.mapMatrix = map.localToWorldMatrix;
            baseline.dimensions = seabed.bounds.size;
            baseline.appliedScale = 1f;
            baseline.entries = entries.ToArray();
            return baseline;
        }

        static bool IsIndependent(Transform t, Transform map)
        {
            // An ordinary object is a unit: its highlights, machine and artwork retain local offsets.
            for (var ancestor = t.parent; ancestor != null && ancestor != map; ancestor = ancestor.parent)
            {
                if (ancestor.GetComponent<OceanTarget>() != null) return false;
                if (ancestor.GetComponent<SpriteRenderer>() != null && Classify(ancestor) == OceanMapResizeBaseline.Shape.Object)
                    return false;
            }
            return true;
        }

        static OceanMapResizeBaseline.Shape Classify(Transform t)
        {
            if (t.GetComponent<PolygonCollider2D>() != null) return OceanMapResizeBaseline.Shape.Polygon;
            if (t.name == "Boundary" && t.GetComponent<BoxCollider2D>() != null) return OceanMapResizeBaseline.Shape.Boundary;
            if (t.name == "Seabed") return OceanMapResizeBaseline.Shape.Seabed;
            if (t.name.StartsWith("Grid X ", StringComparison.Ordinal)) return OceanMapResizeBaseline.Shape.GridX;
            if (t.name.StartsWith("Grid Y ", StringComparison.Ordinal)) return OceanMapResizeBaseline.Shape.GridY;
            return OceanMapResizeBaseline.Shape.Object;
        }

        static string ComponentIds(Transform t) => string.Join("|", t.GetComponents<Component>().Select(c => c == null ? "missing" : Id(c)));

        public static string ValidationError(OceanInteractions ocean, OceanMapResizeBaseline baseline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play mode before resizing.";
            if (ocean == null || ocean.oceanMap == null) return "Open Ocean Interactables and select its scene.";
            if (string.IsNullOrEmpty(ocean.gameObject.scene.path) || EditorSceneManager.IsPreviewScene(ocean.gameObject.scene))
                return "Resize a saved ocean scene outside a preview or prefab stage.";
            if (baseline == null) return "Capture a baseline to establish the current layout as scale 1.";
            if (baseline.sceneGuid != AssetDatabase.AssetPathToGUID(ocean.gameObject.scene.path) || baseline.mapId != Id(ocean.oceanMap.transform))
                return "The baseline belongs to a different map. Recapture the baseline.";
            if (ocean.oceanMap.transform.localToWorldMatrix != baseline.mapMatrix)
                return "The Map root transform changed. Recapture the baseline.";
            var transforms = ocean.oceanMap.GetComponentsInChildren<Transform>(true).Where(t => t != ocean.oceanMap.transform).ToArray();
            var byId = transforms.ToDictionary(Id);
            if (transforms.Length != baseline.entries.Length) return "The map hierarchy changed. Recapture the baseline.";
            foreach (var entry in baseline.entries)
            {
                if (!byId.TryGetValue(entry.id, out var t) || Id(t.parent) != entry.parentId || t.name != entry.name || ComponentIds(t) != entry.componentIds)
                    return "A map object or component changed or is missing. Recapture the baseline.";
                if (entry.polygons.Length > 0 && entry.originalSprite == null)
                    return "A baseline terrain sprite is missing. Restore it or recapture the baseline.";
            }
            if (Resolve<Transform>(baseline.submarineId) == null) return "The submarine is missing. Recapture the baseline.";
            return null;
        }

        static void RequireEditable(OceanInteractions ocean)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before resizing.");
            if (ocean == null || ocean.oceanMap == null || string.IsNullOrEmpty(ocean.gameObject.scene.path) || EditorSceneManager.IsPreviewScene(ocean.gameObject.scene))
                throw new InvalidOperationException("Open the saved Ocean Interactables scene outside a preview or prefab stage.");
            if (ocean.oceanMap.scene != ocean.gameObject.scene) throw new InvalidOperationException("The map must belong to the ocean scene.");
            if (EditorUtility.IsPersistent(ocean)) throw new InvalidOperationException("Prefab assets cannot be resized.");
        }

        public static Vector3 ScalePoint(Vector3 point, Vector3 anchor, float scale) =>
            new Vector3(anchor.x + (point.x - anchor.x) * scale, anchor.y + (point.y - anchor.y) * scale, point.z);

        public static void Apply(OceanInteractions ocean, OceanMapResizeBaseline baseline, float scale)
        {
            RequireEditable(ocean);
            var error = ValidationError(ocean, baseline);
            if (error != null) throw new InvalidOperationException(error);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be finite and greater than zero.");

            var entries = baseline.entries;
            // Validate derived values before touching the scene.
            ValidateScale(baseline, scale);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Resize Ocean Map");
            try
            {
                var changed = new List<Object> { baseline };
                foreach (var entry in entries)
                {
                    changed.Add(Resolve<Transform>(entry.id));
                    changed.AddRange(entry.polygons.Select(p => Resolve<PolygonCollider2D>(p.id)));
                    changed.AddRange(entry.boxes.Select(b => Resolve<BoxCollider2D>(b.id)));
                    if (entry.polygons.Length > 0) changed.Add(Resolve<SpriteRenderer>(entry.rendererId));
                }
                Undo.RegisterCompleteObjectUndo(changed.ToArray(), "Resize Ocean Map");
                // GetComponentsInChildren returns parents before their descendants.
                foreach (var entry in entries)
                {
                    var t = Resolve<Transform>(entry.id);
                    t.localRotation = entry.localRotation;
                    t.localScale = entry.localScale;
                    if (entry.shape == OceanMapResizeBaseline.Shape.Seabed)
                        t.localScale = Vector3.Scale(entry.localScale, new Vector3(scale, scale, 1));
                    else if (entry.shape == OceanMapResizeBaseline.Shape.GridX)
                        t.localScale = Vector3.Scale(entry.localScale, new Vector3(1, scale, 1));
                    else if (entry.shape == OceanMapResizeBaseline.Shape.GridY)
                        t.localScale = Vector3.Scale(entry.localScale, new Vector3(scale, 1, 1));
                    if (entry.id == baseline.submarineId) t.position = baseline.anchor;
                    else if (entry.movePosition) t.position = ScalePoint(entry.position, baseline.anchor, scale);
                    else t.localPosition = entry.localPosition;

                    foreach (var polygon in entry.polygons)
                    {
                        var collider = Resolve<PolygonCollider2D>(polygon.id);
                        collider.offset = polygon.offset;
                        collider.pathCount = polygon.paths.Length;
                        for (int i = 0; i < polygon.paths.Length; i++)
                        {
                            var points = polygon.paths[i].points.Select(p => {
                                var world = entry.worldMatrix.MultiplyPoint3x4(p + polygon.offset);
                                return (Vector2)t.InverseTransformPoint(ScalePoint(world, baseline.anchor, scale)) - collider.offset;
                            }).ToArray();
                            collider.SetPath(i, points);
                        }
                    }
                    foreach (var box in entry.boxes)
                    {
                        var collider = Resolve<BoxCollider2D>(box.id);
                        collider.offset = box.offset * scale;
                        collider.size = box.size * scale;
                    }
                    if (entry.polygons.Length > 0)
                    {
                        var renderer = Resolve<SpriteRenderer>(entry.rendererId);
                        // With zero borders, Sliced stretches the whole silhouette around its
                        // existing pivot. Size and draw mode are scene properties covered by Undo.
                        renderer.sprite = entry.originalSprite;
                        renderer.drawMode = scale == 1f ? entry.rendererDrawMode : SpriteDrawMode.Sliced;
                        renderer.size = scale == 1f ? entry.rendererSize : entry.visualSize * scale;
                        renderer.ResetLocalBounds();
                        renderer.ResetBounds();
                    }
                }
                baseline.appliedScale = scale;
                foreach (var value in changed)
                {
                    EditorUtility.SetDirty(value);
                    if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
                }
                EditorSceneManager.MarkSceneDirty(ocean.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                SceneView.RepaintAll();
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        static void ValidateScale(OceanMapResizeBaseline baseline, float scale)
        {
            foreach (var entry in baseline.entries)
            {
                var p = ScalePoint(entry.position, baseline.anchor, scale);
                if (!Finite(p.x) || !Finite(p.y) || !Finite(baseline.dimensions.x * scale) || !Finite(baseline.dimensions.y * scale))
                    throw new InvalidOperationException("The resulting map is too large. Choose a smaller scale.");
                if (entry.polygons.Length > 0 && (!Finite(entry.visualSize.x * scale) || !Finite(entry.visualSize.y * scale)
                    || entry.visualSize.x * scale <= 0 || entry.visualSize.y * scale <= 0))
                    throw new InvalidOperationException("The resulting terrain size cannot be represented.");
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    }
}
