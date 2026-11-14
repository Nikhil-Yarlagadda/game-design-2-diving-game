using UnityEngine;
using System.Collections.Generic;
using Yarn.Unity;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;
    public List<Room> allRooms; 
    public DialogueRunner runner;

    void Awake()
    {
        Instance = this;
        if(GameState.dayFgfsCollected == GameState.days - 1 && !GameState.fgfsSpread)
        {
            GameState.fgfsSpread = true;
            runner.StartDialogue("FungusSpread");
        }
            
    }

    void Start()
    {
        // all rooms start grayed out / inactive
        Debug.Log("RoomManager Start - initializing " + allRooms.Count + " rooms");
        foreach (var r in allRooms)
            r.SetHovered(false);
    }

    public void EnterRoom(Room room)
    {
        foreach (var r in allRooms)
            r.SetActiveState(r == room);
    }
}
