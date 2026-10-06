using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;
    public List<Room> allRooms; // assign in Inspector

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // all rooms start grayed out / inactive
        Debug.Log("RoomManager Start - initializing " + allRooms.Count + " rooms");
        foreach (var r in allRooms)
            r.SetActiveState(false);
    }

    public void EnterRoom(Room room)
    {
        foreach (var r in allRooms)
            r.SetActiveState(r == room);
    }
}
