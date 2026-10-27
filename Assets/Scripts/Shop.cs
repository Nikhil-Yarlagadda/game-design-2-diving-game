using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class Shop : MonoBehaviour, IClickable
{
    public TextMeshProUGUI description;
    public Button button;
    private static int cost;
    private static string item;
    private static string desc;
    void Start()
    {
        cost = 0;
        item = null;
        desc = null;
    }
    void Update()
    {
        if(GameState.credits < cost)
            button.interactable = false;
        else
            button.interactable = true;

        if(item != null)
            description.text = item + "\n"+  "Cost - " + cost +"\n"+ "\n"+ desc;
        
    }

    public void OnClicked()
    {
        if(GameState.days > 1)
            SceneChanger.ChangeScene("Shop");
        else
            SceneChanger.ChangeScene("Introduction");
    }  

    public void buyClicked()
    {
        GameState.credits -= cost;
        GameState.itemsBought[item]++;
        Debug.Log("Amount of item is " + GameState.itemsBought[item]);
    }

    public static void itemClicked(string description, string name, int price)
    {
        desc = description;
        item = name;
        cost = price;
    }
}

