using UnityEngine;

public class Shop : MonoBehaviour, IClickable
{
    public void OnClicked()
    {
        SceneChanger.ChangeScene("Shop");
    }
}
