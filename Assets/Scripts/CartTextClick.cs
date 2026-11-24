using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class CartTextClick : MonoBehaviour, IPointerClickHandler
{
    public TextMeshProUGUI text;
    public Shop shop;

    private Canvas canvas;

    void Awake()
    {
        canvas = text.canvas;   // the canvas this text sits on
    }

    public void OnPointerClick(PointerEventData e)
    {
        Camera cam = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        int index = TMP_TextUtilities.FindIntersectingLink(text, e.position, cam);
        if (index == -1) return;

        string id = text.textInfo.linkInfo[index].GetLinkID();
        shop.RemoveFromCart(id);
    }
}