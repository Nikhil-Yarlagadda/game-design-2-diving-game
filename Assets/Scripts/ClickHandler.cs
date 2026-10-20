using UnityEngine;
using UnityEngine.InputSystem;

public class ClickHandler : MonoBehaviour
{

    public LayerMask npcLayer;
    public LayerMask roomLayer;

    private Room hoveredRoom;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        UpdateHover(mouseWorldPos);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
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
