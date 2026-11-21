using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class TerminalHandler : MonoBehaviour, IClickable
{
    public TextMeshProUGUI description;
    public Button admin;

    public GameObject popup;            // InboxPopup
    public TextMeshProUGUI popupText;   // PopupText
    public Button popupButton;          // the Button on InboxPopup


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        if(!GameState.infectionPhoto && ItemCatalog.Count(GameState.inventory, ItemCatalog.CrewPhoto) > 0)
        {
            popupButton.onClick.AddListener(ClosePopup);
            popupText.text = "Camera module connected. Uploading files on disk…\nFiles uploaded successfully. Administrator Access available.";
            popup.SetActive(true);
            GameState.infectionPhoto = true;
            GameState.Playtest.Milestone("Infected crewmate photo uploaded");
        }

        if(!GameState.abyssPhoto && ItemCatalog.Count(GameState.inventory, ItemCatalog.EyePhoto) > 0)
        {
            popupButton.onClick.AddListener(ClosePopup);
            popupText.text = "Camera module connected. Uploading files on disk…\nFiles uploaded successfully. Eye photograph available in Administrator Access";
            popup.SetActive(true);
            GameState.abyssPhoto = true;
            GameState.Playtest.Milestone("Eye photo uploaded");
        }

        if(GameState.infectionPhoto)
        {
            description.text = "Admin Access";
            admin.interactable = true;
        }
    }

    public void ClosePopup()
    {
        popup.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnClicked()
    {
        Debug.Log("Terminal hit");
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

    public void AdminClicked()
    {
        SceneChanger.ChangeScene("AdminTab");
    }
}
