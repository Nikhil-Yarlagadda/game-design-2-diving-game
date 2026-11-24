using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class TerminalHandler : MonoBehaviour, IClickable
{
    public TextMeshProUGUI description;
    public Button admin;
    [SerializeField] private GameObject environmentScreen;
    [SerializeField] private Button environmentMenuEntry, environmentBack;

    public GameObject popup;            // InboxPopup
    public TextMeshProUGUI popupText;   // PopupText
    public Button popupButton;          // the Button on InboxPopup


    private static bool HasCrewPhotoToUpload => !GameState.infectionPhoto && ItemCatalog.Count(GameState.inventory, ItemCatalog.CrewPhoto) > 0;
    private static bool HasEyePhotoToUpload => !GameState.abyssPhoto && ItemCatalog.Count(GameState.inventory, ItemCatalog.EyePhoto) > 0;

    internal static bool HasPendingAction => HasCrewPhotoToUpload || HasEyePhotoToUpload
        || ContactManager.CanRequestMedicine || ContactManager.HasUnreadMedicineReply || ContactManager.CanRequestAid
        || (GameState.infectionPhoto && (GameState.abyssPhoto ? !GameState.adminUnlocked : !GameState.passkeyHintSeen));

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (admin == null) return;

        if (HasCrewPhotoToUpload)
        {
            popupButton.onClick.AddListener(ClosePopup);
            popupText.text = "Camera module connected. Uploading files on disk…\nFiles uploaded successfully. Administrator Access available.";
            popup.SetActive(true);
            GameState.infectionPhoto = true;
            GameState.Playtest.Milestone("Infected crewmate photo uploaded");
        }

        if (HasEyePhotoToUpload)
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

    /// <summary>Opens the fixed environmental reference and focuses its only control.</summary>
    public void OpenEnvironmentData()
    {
        environmentScreen.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(environmentBack.gameObject);
    }

    /// <summary>Returns from the reference screen to its terminal menu entry.</summary>
    public void CloseEnvironmentData()
    {
        environmentScreen.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(environmentMenuEntry.gameObject);
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
