using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour, IClickable
{
    public string roomName;
    public List<GameObject> roomContents;
    public SpriteRenderer backgroundSprite;
    public Transform cameraTarget; // NEW — drag the CameraTarget child here, or leave empty to use this room's own position

    public Color inactiveColor = Color.gray;
    public Color activeColor = Color.white;



    public void OnClicked()
    {
        Debug.Log("Room clicked: " + roomName);
        RoomManager.Instance.EnterRoom(this);

        Transform target = cameraTarget != null ? cameraTarget : transform;
        CameraController.Instance.MoveToRoom(target);
    }

    public void SetActiveState(bool isActive)
    {
        Debug.Log(roomName + " SetActiveState: " + isActive);
        backgroundSprite.color = isActive ? activeColor : inactiveColor;

        if (roomContents != null)
            foreach (var c in roomContents)
                c.SetActive(isActive);
    }
}
