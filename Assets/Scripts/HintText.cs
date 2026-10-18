using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class HintText : MonoBehaviour
{
    public LayerMask npcLayer;
    private TextMeshProUGUI label;

    void Start()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero, Mathf.Infinity, npcLayer);

        label.text = hit.collider != null ? hit.collider.gameObject.name : "";
    }
}