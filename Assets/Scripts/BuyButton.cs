using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class BuyButton : MonoBehaviour
{
    public int cost;
    public string item;
    public Button button;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(GameState.credits < cost)
            button.interactable = false;
        else
            button.interactable = true;
    }

    public void onClicked(){
        GameState.credits -= 20;
        GameState.itemsBought[item]++;
        Debug.Log("Amount of item is " + GameState.itemsBought[item]);
    }
}
