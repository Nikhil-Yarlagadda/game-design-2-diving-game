using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameStateDebugger))]
public sealed class GameStateDebuggerInspector : Editor
{
    private int destinationDay;
    private string ownedName = "";
    private int ownedCount;
    private string orderName = "";
    private int orderCount;
    private string crewId = "";

    private void OnEnable() => destinationDay = GameState.days;

    public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

    public override void OnInspectorGUI()
    {
        var debugger = (GameStateDebugger)target;
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableNextDayShortcut"), new GUIContent("Enable ] Next Day"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableResetDayShortcut"), new GUIContent("Enable [ Reset Day"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Current day", GameState.days.ToString());
        EditorGUILayout.LabelField("Day-start checkpoint", debugger.CheckpointScene ?? "Unavailable");
        using (new EditorGUI.DisabledScope(SceneChanger.IsTransitioning))
        {
            destinationDay = EditorGUILayout.IntSlider("Destination day", destinationDay, 1, GameStateDebugger.LastDay);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Go to Day")) debugger.GoToDay(destinationDay);
                if (GUILayout.Button("Reset Current Day")) debugger.ResetCurrentDay();
            }

            EditorGUILayout.Space();
            EditInt("Credits", GameState.credits, debugger.SetCredits);
            EditInt("Tellurium", GameState.tellurium, debugger.SetTellurium);
            bool shown = EditorGUILayout.Toggle("Delivery tutorial shown", GameState.deliveryTutorialShown);
            if (shown != GameState.deliveryTutorialShown) debugger.SetDeliveryTutorialShown(shown);

            DrawItems("Owned items", GameState.inventory, ref ownedName, ref ownedCount,
                (name, count) => debugger.SetItemCount(name, count, pending: false));
            DrawItems("Pending orders", GameState.itemsBought, ref orderName, ref orderCount,
                (name, count) => debugger.SetItemCount(name, count, pending: true));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Crew conversations", EditorStyles.boldLabel);
            foreach (string id in GameState.npcsTalks.OrderBy(id => id).ToArray())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(id) ? "(Empty ID)" : id);
                    if (GUILayout.Button("Remove", GUILayout.Width(70))) debugger.SetCrewTalk(id, false);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                crewId = EditorGUILayout.TextField("Crew ID", crewId);
                string addLabel = crewId.Length == 0 ? "Add Empty ID" : "Add";
                if (GUILayout.Button(addLabel)) debugger.SetCrewTalk(crewId, true);
            }
        }
        if (!string.IsNullOrEmpty(debugger.Status)) EditorGUILayout.HelpBox(debugger.Status, MessageType.Info);
    }

    private static void EditInt(string label, int current, System.Action<int> apply)
    {
        int value = EditorGUILayout.DelayedIntField(label, current);
        if (value != current) apply(value);
    }

    private static void DrawItems(string heading, Dictionary<string, int> items, ref string name,
        ref int count, System.Action<string, int> apply)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        foreach (var item in items.OrderBy(item => item.Key).ToArray())
            EditInt(item.Key, item.Value, value => apply(item.Key, value));
        name = EditorGUILayout.TextField("Exact item name", name);
        using (new EditorGUILayout.HorizontalScope())
        {
            count = EditorGUILayout.IntField("Count", count);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(name)))
                if (GUILayout.Button("Set", GUILayout.Width(70))) apply(name, count);
        }
    }
}
