using UnityEngine;
using UnityEngine.SceneManagement;

public class InventoryButton : MonoBehaviour
{
    private static bool loading = false;

    public static bool IsOpen
    {
        get { return loading || SceneManager.GetSceneByName("InteriorInventory").isLoaded; }
    }

    public void Open()
    {
        if (IsOpen) return;

        loading = true;
        AsyncOperation op = SceneManager.LoadSceneAsync("InteriorInventory", LoadSceneMode.Additive);
        op.completed += _ => loading = false;
    }
}
