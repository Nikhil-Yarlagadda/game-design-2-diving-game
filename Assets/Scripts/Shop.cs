using UnityEngine;

public class Shop : MonoBehaviour, IClickable
{
    public void OnClicked()
    {
        if(GameState.days > 1)
            SceneChanger.ChangeScene("Shop");
        else
            SceneChanger.ChangeScene("Introduction");
    }   
}
