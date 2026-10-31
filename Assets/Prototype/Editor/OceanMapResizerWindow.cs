using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public sealed class OceanMapResizerWindow : EditorWindow
    {
        [NonSerialized] float targetScale = 1f;
        string failure;

        [MenuItem("Diving Prototype/Ocean Map Resizer")]
        public static void Open() => GetWindow<OceanMapResizerWindow>("Ocean Map Resizer");

        void OnEnable()
        {
            minSize = new Vector2(360, 390);
            Undo.undoRedoPerformed += Refresh;
        }
        void OnDisable() => Undo.undoRedoPerformed -= Refresh;
        void Refresh() { failure = null; Repaint(); }
        void OnHierarchyChange() => Repaint();
        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            EditorGUILayout.LabelField("Ocean Map Resizer", EditorStyles.boldLabel);
            var scene = SceneManager.GetSceneByPath(OceanMapResizeService.ScenePath);
            var ocean = OceanMapResizeService.FindOcean(scene);
            var baseline = OceanMapResizeService.FindBaseline(scene);
            EditorGUILayout.LabelField("Target scene", "Ocean Interactables");
            EditorGUILayout.LabelField("Current scale", baseline == null ? "Not captured" : baseline.appliedScale.ToString("0.####"));
            if (baseline != null)
            {
                EditorGUILayout.LabelField("Submarine anchor", $"({baseline.anchor.x:0.###}, {baseline.anchor.y:0.###})");
                EditorGUILayout.LabelField("Current dimensions", Dimensions(baseline.dimensions * baseline.appliedScale));
            }
            targetScale = EditorGUILayout.FloatField("Target scale", targetScale);
            bool validScale = targetScale > 0 && !float.IsNaN(targetScale) && !float.IsInfinity(targetScale);
            if (baseline != null && validScale)
                EditorGUILayout.LabelField("Resulting dimensions", Dimensions(baseline.dimensions * targetScale));

            EditorGUILayout.HelpBox("Scale 1 is the captured layout for this editor session. Apply changes only the scene; use Edit > Undo to revert. No baseline or resized art files are saved. Smaller maps can overlap fixed-size objects.", MessageType.Info);
            string error = OceanMapResizeService.ValidationError(ocean, baseline);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (!validScale) EditorGUILayout.HelpBox("Enter a finite scale greater than zero.", MessageType.Warning);
            if (failure != null) EditorGUILayout.HelpBox(failure, MessageType.Error);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || ocean == null))
            {
                if (GUILayout.Button(baseline == null ? "Capture Baseline" : "Recapture Baseline (current layout becomes 1)"))
                {
                    if (baseline == null || EditorUtility.DisplayDialog("Replace Ocean Baseline?",
                        "The current layout will become scale 1. Reset to 1 will then restore this layout instead of the previous baseline.", "Recapture", "Cancel"))
                        Execute(() => { OceanMapResizeService.Capture(ocean); targetScale = 1; });
                }
            }
            using (new EditorGUI.DisabledScope(error != null || !validScale))
                if (GUILayout.Button("Apply Scale")) Execute(() => OceanMapResizeService.Apply(ocean, baseline, targetScale));
            using (new EditorGUI.DisabledScope(error != null))
                if (GUILayout.Button("Reset to 1")) Execute(() => { OceanMapResizeService.Apply(ocean, baseline, 1); targetScale = 1; });
        }

        static string Dimensions(Vector2 size) => $"{size.x:0.###} × {size.y:0.###} world units";
        void Execute(Action action)
        {
            try { action(); failure = null; }
            catch (Exception exception) { failure = exception.Message; Debug.LogException(exception); }
            Repaint();
        }
    }
}
