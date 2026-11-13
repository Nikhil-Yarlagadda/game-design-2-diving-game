using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ContactManager : MonoBehaviour
{
    public TextMeshProUGUI description;
    public Button draft;
    public Button inbox;

    public GameObject popup;            // InboxPopup
    public TextMeshProUGUI popupText;   // PopupText
    public Button popupButton;          // the Button on InboxPopup

    private TextMeshProUGUI draftLabel;
    private string message = "";
    private bool drafted = false;

    void Awake()
    {
        message = "";
        drafted = false;

        draftLabel = draft.GetComponentInChildren<TextMeshProUGUI>();

        if (GameState.days == 7 && !GameState.checkTerminal)
        {
            if (ColorUtility.TryParseHtmlString("#D41D1D", out Color red))
                inbox.image.color = red;
        }

        popupButton.onClick.AddListener(ClosePopup);
        inbox.onClick.AddListener(OnInboxClicked);
    }

    void Update()
    {
        description.text = message;
    }

    public void OnClicked()
    {
        if (GameState.days == 5 && !GameState.draftMessage && !drafted)
        {
            message = "This is the foreman of the HZ-3280. Our medic found an unidentifiable orange substance in the mineral lab air filter and suspects that it might be a sign of some health risk present on the vessel. Therefore, we request a shipment of antimicrobial medicine, as well as any information that the Company might have regarding what this substance might be.";
            draftLabel.text = "Send";
            drafted = true;
        }
        else if (GameState.days == 5 && !GameState.draftMessage && drafted)
        {
            message = "Your message has been sent. You may expect to receive a response from the Company within 2 to 4 business days. It is our top priority to provide prompt and effective assistance to our employees whenever needed. After all, you are our greatest assets!";
            draftLabel.text = "Draft";
            GameState.draftMessage = true;
        }
        else if(GameState.days >= 9 && !GameState.draftEvidenceMessage && !drafted)
        {
            message = "SOS. The HZ-3280 requests urgent aid and a second shipment of medicine; a member of our crew is dangerously ill and speaking irrationally. We do not have the resources to treat them, nor do we understand what this disease is.\n <<Image Attachment>>";
            draftLabel.text = "Send";
            drafted = true;
        }
        else if (GameState.days >= 9 && !GameState.draftEvidenceMessage && drafted)
        {
            message = "Your message has been sent. You may expect to receive a response from the Company within 2 to 4 business days. It is our top priority to provide prompt and effective assistance to our employees whenever needed. After all, you are our greatest assets!";
            draftLabel.text = "Draft";
            GameState.draftEvidenceMessage = true;
        }
        else
        {
            message = "No message to draft right now.";
        }
    }

    public void OnInboxClicked()
    {   
        ShowPopup(GetInboxMessage());
        if (ColorUtility.TryParseHtmlString("#FFFFFF", out Color white))
                inbox.image.color = white;
    }

    string GetInboxMessage()
    {
        // replace with your real messages, for example choosing by GameState.days
        if(GameState.days == 7)
        {
            GameState.checkTerminal = true;
            return "Thank you for contacting the Trident Excavation human resources department. Your concerns are very important to us. Our professionals have reviewed the relevant records and found no data resembling the reported “orange substance.” Therefore, we assure you that it is nothing to worry about. \n\nAs per your request, one (1) Antimicrobial Medicine is now available for purchase in your credit shop, which will be effective for treating most mild to moderate infections. Trident Excavation is unable to supply more than this, as that would be outside of your operation’s allotted budget. Exceptions may only be made in the case that you provide us with photographic evidence of a major employee health concern. \n\nThank you for your flexibility, and happy mining!";
        }
         if(GameState.days >= 11 && GameState.draftEvidenceMessage)
        {
            return "Thank you for contacting the Trident Excavation human resources department. Your concerns are very important to us. \n\nThe Employee Safety and Budget Analyst boards have reviewed your image and determined that the recommended course of action is a standard quarantining procedure. Please instruct this employee to remain in an isolated space on the vessel for the remainder of the operation. If no space can be reserved for this purpose, simply ensure that the employee sleeps in a separate room from the rest of the crew, and maintain a distance of at least four (4) meters from them at all times. No more medicine can be supplied at this time. \n\nHappy mining!";
        }
        return "No new messages.";
    }

    void ShowPopup(string text)
    {
        popupText.text = text;
        popup.SetActive(true);
    }

    public void ClosePopup()
    {
        popup.SetActive(false);
    }
}
