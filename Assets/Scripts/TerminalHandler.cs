using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class TerminalHandler : MonoBehaviour, IClickable
{
    public TextMeshProUGUI description;
    public Button admin;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(GameState.adminUnlocked)
        {
            description.text = "Admin Tab";
            admin.interactable = true;
        }
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

     public void ContactClicked()
    {
        SceneChanger.ChangeScene("Contact Management");
    }  
}
