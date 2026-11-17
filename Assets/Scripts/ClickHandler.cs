using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ClickHandler : MonoBehaviour
{

    public LayerMask npcLayer;
    public LayerMask roomLayer;

    [SerializeField] private DialogueAdvancer roomDialogue;

    private Room hoveredRoom;
    private readonly List<RaycastResult> pointerHits = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (PauseManager.BlocksInput || InventoryButton.IsOpen || SceneChanger.IsTransitioning || Mouse.current == null ||
            (roomDialogue != null && roomDialogue.BlocksRoomInput)) return;
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        UpdateHover(mouseWorldPos);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (IsOverUiControl(mouseScreenPos)) return;

            Debug.Log("Click at world pos: " + mouseWorldPos);

            RaycastHit2D npcHit = Physics2D.Raycast(mouseWorldPos, Vector2.zero, Mathf.Infinity, npcLayer);
            Debug.Log("NPC hit: " + npcHit.collider);
            if (npcHit.collider != null)
            {
                npcHit.collider.GetComponentInParent<IClickable>()?.OnClicked();
                return;
            }

            RaycastHit2D roomHit = Physics2D.Raycast(mouseWorldPos, Vector2.zero, Mathf.Infinity, roomLayer);
            Debug.Log("Room hit: " + roomHit.collider);
            if (roomHit.collider != null)
            {
                Debug.Log("Hit object: " + roomHit.collider.gameObject.name); // ADD THIS
                IClickable clickable = roomHit.collider.GetComponentInParent<IClickable>();
                Debug.Log("Found IClickable: " + (clickable != null)); // ADD THIS
                clickable?.OnClicked();
            }
        }
    }

    /// <summary>Checks whether the UI target at the current screen position owns the mouse press.</summary>
    private bool IsOverUiControl(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        // Raycast the current press instead of relying on the input module's last pointer update.
        var pointer = new PointerEventData(eventSystem) { position = screenPosition };
        eventSystem.RaycastAll(pointer, pointerHits);
        RaycastResult hit = pointerHits.Count > 0 ? pointerHits[0] : default;
        pointerHits.Clear();

        return hit.module is GraphicRaycaster &&
            (ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject) != null ||
             ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) != null ||
             ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject) != null);
    }

    void UpdateHover(Vector2 mouseWorldPos)
    {
        Room newHover = null;

        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero, Mathf.Infinity, roomLayer);
        if (hit.collider != null)
            newHover = hit.collider.GetComponentInParent<Room>();

        if (newHover != hoveredRoom)
        {
            if (hoveredRoom != null) hoveredRoom.SetHovered(false);
            if (newHover != null) newHover.SetHovered(true);
            hoveredRoom = newHover;
        }
    }
}
