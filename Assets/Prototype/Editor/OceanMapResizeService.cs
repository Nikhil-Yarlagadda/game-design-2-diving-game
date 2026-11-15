using System;
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
            var entries = transforms.Select(t => new OceanMapResizeBaseline.Entry
            {
                id = Id(t), parentId = Id(t.parent), name = t.name, componentIds = ComponentIds(t),
                position = t.position, localPosition = t.localPosition, localRotation = t.localRotation,
                localScale = t.localScale, movePosition = IsIndependent(t, map), shape = Classify(t)
            }).ToArray();
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
            baseline.spawnMin = ocean.spawnMin;
            baseline.spawnMax = ocean.spawnMax;
            baseline.entries = entries;
            return baseline;
        }

        static bool IsIndependent(Transform t, Transform map)
        {
            // An ordinary object is a unit: its highlights, machine and artwork retain local offsets.
            for (var ancestor = t.parent; ancestor != null && ancestor != map; ancestor = ancestor.parent)
            {
                if (ancestor.GetComponent<OceanTarget>() != null) return false;
                if (ancestor.GetComponent<SpriteRenderer>() != null ||
                    Classify(ancestor) is OceanMapResizeBaseline.Shape.Polygon or OceanMapResizeBaseline.Shape.Boundary)
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
            if (baseline.spawnMin.x >= baseline.spawnMax.x || baseline.spawnMin.y >= baseline.spawnMax.y)
                return "The baseline lacks valid spawn bounds. Check the bounds and recapture the baseline.";
            if (ocean.oceanMap.transform.localToWorldMatrix != baseline.mapMatrix)
                return "The Map root transform changed. Recapture the baseline.";
            var transforms = ocean.oceanMap.GetComponentsInChildren<Transform>(true).Where(t => t != ocean.oceanMap.transform).ToArray();
            var byId = transforms.ToDictionary(Id);
            if (transforms.Length != baseline.entries.Length) return "The map hierarchy changed. Recapture the baseline.";
            foreach (var entry in baseline.entries)
            {
                if (!byId.TryGetValue(entry.id, out var t) || Id(t.parent) != entry.parentId || t.name != entry.name || ComponentIds(t) != entry.componentIds)
                    return "A map object or component changed or is missing. Recapture the baseline.";
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
                var transforms = entries.Select(entry => Resolve<Transform>(entry.id)).ToArray();
                var changed = new Object[] { baseline, ocean }.Concat(transforms).ToArray();
                Undo.RegisterCompleteObjectUndo(changed, "Resize Ocean Map");
                // Terrain art and colliders share their transform, including the sprite pivot.
                // Process parents first so attached artwork inherits its parent's changes.
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    var t = transforms[i];
                    t.localRotation = entry.localRotation;
                    t.localScale = ScaledLocalSize(entry, scale);
                    if (scale != 1f && entry.movePosition && entry.id != baseline.submarineId)
                        t.position = ScalePoint(entry.position, baseline.anchor, scale);
                    else
                        t.localPosition = entry.localPosition;
                }
                ocean.spawnMin = scale == 1f ? baseline.spawnMin : (Vector2)ScalePoint(baseline.spawnMin, baseline.anchor, scale);
                ocean.spawnMax = scale == 1f ? baseline.spawnMax : (Vector2)ScalePoint(baseline.spawnMax, baseline.anchor, scale);
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

        /// <summary>Scales terrain in both axes and grid lines only along their length.</summary>
        static Vector3 ScaledLocalSize(OceanMapResizeBaseline.Entry entry, float scale)
        {
            if (!entry.movePosition) return entry.localScale;
            var axes = entry.shape switch
            {
                OceanMapResizeBaseline.Shape.Polygon or OceanMapResizeBaseline.Shape.Boundary
                    or OceanMapResizeBaseline.Shape.Seabed => new Vector3(scale, scale, 1),
                OceanMapResizeBaseline.Shape.GridX => new Vector3(1, scale, 1),
                OceanMapResizeBaseline.Shape.GridY => new Vector3(scale, 1, 1),
                _ => Vector3.one
            };
            return Vector3.Scale(entry.localScale, axes);
        }

        static void ValidateScale(OceanMapResizeBaseline baseline, float scale)
        {
            var dimensions = baseline.dimensions * scale;
            Vector2 spawnMin = ScalePoint(baseline.spawnMin, baseline.anchor, scale);
            Vector2 spawnMax = ScalePoint(baseline.spawnMax, baseline.anchor, scale);
            if (!Finite(dimensions.x) || !Finite(dimensions.y) || dimensions.x <= 0 || dimensions.y <= 0 ||
                !Finite(spawnMin.x) || !Finite(spawnMin.y) || !Finite(spawnMax.x) || !Finite(spawnMax.y) ||
                spawnMin.x >= spawnMax.x || spawnMin.y >= spawnMax.y)
                throw new InvalidOperationException("The resulting map dimensions cannot be represented.");

            foreach (var entry in baseline.entries)
            {
                var position = ScalePoint(entry.position, baseline.anchor, scale);
                var size = ScaledLocalSize(entry, scale);
                if (!Finite(position.x) || !Finite(position.y) || !Finite(size.x) || !Finite(size.y) ||
                    (entry.localScale.x != 0 && size.x == 0) || (entry.localScale.y != 0 && size.y == 0))
                    throw new InvalidOperationException("The resulting map geometry cannot be represented.");
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    }
}
