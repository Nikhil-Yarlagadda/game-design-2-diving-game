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

        if(!GameState.infectionPhoto && GameState.inventory.ContainsKey("Photograph of Infected Crewmate"))
        {
            popupButton.onClick.AddListener(ClosePopup);
            popupText.text = "Camera module connected. Uploading files on disk…\nFiles uploaded successfully. New message attachments available.";
            popup.SetActive(true);
            GameState.infectionPhoto = true;
        }

        if(!GameState.abyssPhoto && GameState.inventory.ContainsKey("Photograph of the Eye of the Abyss"))
        {
            popupButton.onClick.AddListener(ClosePopup);
            popupText.text = "Camera module connected. Uploading files on disk…\nFiles uploaded successfully. Admin files unlocked";
            popup.SetActive(true);
            GameState.abyssPhoto = true;
        }

        if(GameState.infectionPhoto)
        {
            description.text = "Admin Tab";
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
