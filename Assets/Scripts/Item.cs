using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class Item : MonoBehaviour
{
    public int cost;
    public string title;
    public string description;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void onClicked()
    {
        Shop.itemClicked(description, title, cost);
    }
}