using System.Collections.Generic;
using System.Linq;
using DivingPrototype;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameStateDebugger))]
public sealed class GameStateDebuggerInspector : Editor
{
    private int destinationDay;
    private bool showOwned = true, showOrders, showCarried, showQuest = true;
    private string crewId = "";
    private static readonly ItemCatalog.Definition[] Items = ItemCatalog.All
        .Where(item => item.Id != ItemCatalog.Ore).OrderBy(item => item.Name).ToArray();
    private static readonly ItemCatalog.Definition[] Orderable = Items.Where(item => item.Stock > 0).ToArray();

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
            var ocean = FindAnyObjectByType<OceanInteractions>();
            bool diving = ocean != null && !ocean.LoadoutOpen;
            EditInt("Tellurium", diving ? ocean.Tellurium : GameState.tellurium, debugger.SetTellurium);
            bool shown = EditorGUILayout.Toggle("Delivery tutorial shown", GameState.deliveryTutorialShown);
            if (shown != GameState.deliveryTutorialShown) debugger.SetDeliveryTutorialShown(shown);

            DrawItems("Owned items", Items, GameState.inventory, ref showOwned,
                (name, count) => debugger.SetItemCount(name, count, pending: false));
            if (diving)
                DrawItems("Excursion bag", Items, ocean.CarriedItems, ref showCarried,
                    (name, count) => debugger.SetCarriedItemCount(ocean, name, count));
            DrawItems("Pending orders", Orderable, GameState.itemsBought, ref showOrders,
                (name, count) => debugger.SetItemCount(name, count, pending: true));

            showQuest = EditorGUILayout.Foldout(showQuest, "Quest state", true);
            if (showQuest) DrawQuestState();

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
            if (GUILayout.Button("Talk to All Crewmates")) debugger.TalkToAllCrewmates();
        }
        if (!string.IsNullOrEmpty(debugger.Status)) EditorGUILayout.HelpBox(debugger.Status, MessageType.Info);
    }

    private static void EditInt(string label, int current, System.Action<int> apply)
    {
        int value = EditorGUILayout.DelayedIntField(label, current);
        if (value != current) apply(value);
    }

    /// <summary>Shows an editable count even for catalog items the player does not yet have.</summary>
    private static void DrawItems(string heading, ItemCatalog.Definition[] definitions,
        IReadOnlyDictionary<string, int> items, ref bool expanded, System.Action<string, int> apply)
    {
        expanded = EditorGUILayout.Foldout(expanded, heading, true);
        if (!expanded) return;
        using var indent = new EditorGUI.IndentLevelScope();
        foreach (var item in definitions)
            EditInt(item.Name, ItemCatalog.Count(items, item.Id), value => apply(item.Id, value));
    }

    /// <summary>Edits the quest milestones independently of possession of their associated items.</summary>
    private static void DrawQuestState()
    {
        using var indent = new EditorGUI.IndentLevelScope();
        GameState.draftMessage = EditorGUILayout.Toggle("Medicine requested", GameState.draftMessage);
        GameState.checkTerminal = EditorGUILayout.Toggle("Medicine reply read", GameState.checkTerminal);
        GameState.medicineDelivered = EditorGUILayout.Toggle("Medicine delivered", GameState.medicineDelivered);
        GameState.infectionPhoto = EditorGUILayout.Toggle("Crew photo uploaded", GameState.infectionPhoto);
        GameState.passkeyHintSeen = EditorGUILayout.Toggle("Passkey hint seen", GameState.passkeyHintSeen);
        GameState.abyssPhoto = EditorGUILayout.Toggle("Eye photo uploaded", GameState.abyssPhoto);
        GameState.adminUnlocked = EditorGUILayout.Toggle("Admin files unlocked", GameState.adminUnlocked);
        GameState.fgfsSpread = EditorGUILayout.Toggle("Fungus spread aboard", GameState.fgfsSpread);
        EditInt("Evidence request day", GameState.evidenceRequestDay, value => GameState.evidenceRequestDay = QuestDay(value));
        EditInt("Kirsten cured day", GameState.dayOfKirstenCured, value => GameState.dayOfKirstenCured = QuestDay(value));
        EditInt("Cole cured day", GameState.dayOfColeCured, value => GameState.dayOfColeCured = QuestDay(value));
        EditInt("Fungus recovered day", GameState.dayFgfsCollected, value => GameState.dayFgfsCollected = QuestDay(value));
        EditorGUILayout.LabelField("Enter -1 for milestones that have not happened.", EditorStyles.miniLabel);
    }

    /// <summary>Normalizes an unset or out-of-range quest milestone day.</summary>
    private static int QuestDay(int value) => value < 1 ? -1 : Mathf.Min(value, GameStateDebugger.LastDay);
}
