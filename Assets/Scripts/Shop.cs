using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class Shop : MonoBehaviour
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
        if(string.IsNullOrEmpty(item) || GameState.credits < cost)
            button.interactable = false;
        else
            button.interactable = true;

        if(item != null)
            description.text = item + "\n"+  "Cost - " + cost +"\n"+ "\n"+ desc;
        
    }

    public void buyClicked()
    {
        if (string.IsNullOrEmpty(item) || cost < 0 || GameState.credits < cost) return;
        GameState.credits -= cost;
        if(GameState.itemsBought.ContainsKey(item))
            GameState.itemsBought[item]++;
        else
            GameState.itemsBought[item] = 1;
        Debug.Log("Amount of item is " + GameState.itemsBought[item]);
    }

    public static void itemClicked(string description, string name, int price)
    {
        desc = description;
        item = name;
        cost = price;
    }
}

