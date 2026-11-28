using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class TerminalHandler : MonoBehaviour, IClickable
{
    public Image notif;
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

        notif.gameObject.SetActive(false);
        if(!GameState.emailsRead.Contains("intro")){
            notif.gameObject.SetActive(true);
        }
        if(GameState.days == 7 && !GameState.emailsRead.Contains("medicine")){
            notif.gameObject.SetActive(true);
        }
        if(GameState.evidenceRequestDay >= 0
                    && GameState.days >= GameState.evidenceRequestDay + 2
                    && !GameState.emailsRead.Contains("photo")){
            notif.gameObject.SetActive(true);
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
            SceneChanger.ChangeScene("Shop");
        else
            SceneChanger.ChangeScene("Contact Management");
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

    public void LocalClicked()
    {
        SceneChanger.ChangeScene("Local");
    }
}
