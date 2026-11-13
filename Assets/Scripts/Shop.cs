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
    private static readonly System.Collections.Generic.HashSet<string> restockable =
        new System.Collections.Generic.HashSet<string> { "Mining Machine", "Stimulant Canister"};
    private static int stock = 0;
    public GameObject pill;
    
    void Awake()
    {
        if(GameState.days >= 7)
            pill.SetActive(true);
        else
            pill.SetActive(false);
    }

    void Start()
    {
        cost = 0;
        item = null;
        desc = null;
    }
    void Update()
    {

        if(string.IsNullOrEmpty(item))
            return;

        button.interactable = true;
        
        if(item != null)
            description.text = item + "\n"+  "Cost - " + cost +"\n"+  "Stock - " + stock +"\n"+ "\n"+ desc;

        if(GameState.credits < cost)
        {
            button.interactable = false;
        }

        

        Debug.Log("reaches stock logic");
        if(GameState.itemsBought.ContainsKey(item))
        {
            if(restockable.Contains(item))
            {
                if(GameState.itemsBought[item] >= 5)
                {
                    stock = 0;
                    button.interactable = false;
                }
                else
                    stock = 5 - GameState.itemsBought[item];
            }
            else 
            {
                if(GameState.itemsBought[item] >= 1)
                {
                    stock = 0;
                    button.interactable = false;
                }
                else
                    stock = 1;
            }
        }
        else if(GameState.inventory.ContainsKey(item))
        {
            if(restockable.Contains(item))
            {
                stock = 5;
                return;
            }
            else
            {
                if(GameState.inventory[item] >= 1)
                {
                    stock = 0;
                    button.interactable = false;
                }
                else
                    stock = 1;
            }
        }

        else
        {
            if(restockable.Contains(item))
                stock = 5;
            else
                stock = 1;
        }

       
        
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

