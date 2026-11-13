using UnityEngine;

public class Item : MonoBehaviour
{
    public string title;
    private void Start()
    {
        if (ItemCatalog.Find(title)?.Id == ItemCatalog.Medicine && GameState.days < 7)
            gameObject.SetActive(false);
    }
    public void onClicked() => Shop.itemClicked(title);
}
