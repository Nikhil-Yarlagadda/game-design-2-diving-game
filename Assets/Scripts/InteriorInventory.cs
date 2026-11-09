using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class InteriorInventory : MonoBehaviour
{
    public TextMeshProUGUI listText;

    void Start()
    {
        StringBuilder sb = new StringBuilder();

        foreach (var item in GameState.inventory)
        {
            if (item.Value <= 0) continue;
            sb.AppendLine(item.Key + " x" + item.Value);
        }

        /*sb.AppendLine("items bought:");

        foreach (var item in GameState.itemsBought)
        {
            if (item.Value <= 0) continue;
            sb.AppendLine(item.Key + " x" + item.Value);
        }*/

        listText.text = sb.Length > 0 ? sb.ToString() : "Nothing yet.";
    }

    public void Close()
    {
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
