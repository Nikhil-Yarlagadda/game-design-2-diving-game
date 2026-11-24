using TMPro;
using UnityEngine;
using System.Collections.Generic;
using Yarn.Unity;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;
    public List<Room> allRooms;
    public DialogueRunner runner;

    private TextMeshPro[] objectiveMarkers;

    void Awake()
    {
        Instance = this;    }

    void Start()
    {
        if (GameState.dayFgfsCollected >= 0 && GameState.dayFgfsCollected == GameState.days - 1 &&
            !GameState.fgfsSpread)
        {
            GameState.fgfsSpread = true;
            GameState.Playtest.Milestone("Fungus spread aboard");
            runner.StartDialogue("FungusSpread");
        }

        // all rooms start grayed out / inactive
        Debug.Log("RoomManager Start - initializing " + allRooms.Count + " rooms");
        objectiveMarkers = new TextMeshPro[allRooms.Count];
        for (int i = 0; i < allRooms.Count; i++)
        {
            var room = allRooms[i];
            room.SetHovered(false);
            objectiveMarkers[i] = CreateObjectiveMarker(room);
        }
        RefreshObjectives();
    }

    /// <summary>Creates a world-space map indicator that follows the room artwork and has no input target.</summary>
    private static TextMeshPro CreateObjectiveMarker(Room room)
    {
        var sprite = room.backgroundSprite;
        var marker = new GameObject("Pending objective", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        marker.transform.SetParent(room.transform);
        var bounds = sprite.bounds;
        marker.transform.position = bounds.center + Vector3.up * bounds.extents.y * .58f;
        marker.text = "<b>!</b>";
        marker.fontSize = 7;
        marker.color = new Color(1f, .79f, .3f);
        marker.outlineWidth = .22f;
        marker.alignment = TextAlignmentOptions.Center;
        var mesh = marker.GetComponent<MeshRenderer>();
        mesh.sortingLayerID = sprite.sortingLayerID;
        mesh.sortingOrder = sprite.sortingOrder + 1;
        return marker;
    }

    /// <summary>Refreshes map indicators after crew or terminal progress changes.</summary>
    private void LateUpdate() => RefreshObjectives();

    /// <summary>Shows a marker on each room with an available conversation or a pending terminal action.</summary>
    private void RefreshObjectives()
    {
        string jonah = PendingCrewRoom("Mineral Processor");
        string kirsten = PendingCrewRoom("Medic");
        string cole = PendingCrewRoom("Apprentice Diver");
        bool terminal = TerminalHandler.HasPendingAction;
        for (int i = 0; i < allRooms.Count; i++)
        {
            string room = allRooms[i].roomName;
            bool pending = room == jonah || room == kirsten || room == cole || (room == "Shared Space" && terminal);
            if (objectiveMarkers[i].gameObject.activeSelf != pending)
                objectiveMarkers[i].gameObject.SetActive(pending);
        }
    }

    /// <summary>Returns the location of an available crew conversation still required today.</summary>
    private static string PendingCrewRoom(string id) =>
        GameState.npcsTalks.Contains(id) ? null : GameState.GetAvailableCrewRoom(id);

    public void EnterRoom(Room room)
    {
        foreach (var r in allRooms)
            r.SetActiveState(r == room);
    }
}
