using UnityEngine;

public class TerminalHandler : MonoBehaviour, IClickable
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnClicked()
    {
        if(GameState.days > 1)
            SceneChanger.ChangeScene("Terminal");
        else
            SceneChanger.ChangeScene("Introduction");
    }  

    public void ShopClicked()
    {
        SceneChanger.ChangeScene("Shop");
    }  
}
