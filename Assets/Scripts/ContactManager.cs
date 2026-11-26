using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public class EmailEntry
{
    public Button button;
    public TMP_Text titleLabel;      // title text on the button
    public TMP_Text captionLabel;    // caption text on the button
    public Image icon;
    public Image notif;
    public string id; 
    public string title;
    public string caption;
    [TextArea(3, 12)] public string body;
    public enum Requirement { Always, Day7, EvidenceReply }
    public Requirement requirement;
}

public class ContactManager : MonoBehaviour
{
    public TextMeshProUGUI description;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI captionText;
    public List<EmailEntry> emails;

    private Color selectedTextColor;
    private Color normalTextColor;

    public Image request;
    public Image lockIcon;
    public TextMeshProUGUI lockText;
    public Image DraftPanel;

    public Button draft;
    public Button send;
    public Button inbox;
    public Image error;
    public Image notif;

    public GameObject popup;            // InboxPopup
    public Button popupButton;          // the Button on InboxPopup

    private TextMeshProUGUI draftLabel;
    private string message = "";

    public Button tab;
    public TextMeshProUGUI buttonText;

    void Awake()
    {
        message = "";

        send.gameObject.SetActive(false);
        error.gameObject.SetActive(false);

        draftLabel = draft.GetComponentInChildren<TextMeshProUGUI>();

        popupButton.onClick.AddListener(ClosePopup);

        if (ColorUtility.TryParseHtmlString("#A4DDDB", out Color temp))
                temp.a = Mathf.Clamp01(0.9f);
                tab.image.color = temp;
                if (ColorUtility.TryParseHtmlString("#04302D", out Color text))
                {
                    buttonText.color = text;
                }

        ColorUtility.TryParseHtmlString("#042825", out selectedTextColor);
        ColorUtility.TryParseHtmlString("#A4DDDB", out normalTextColor);

        for (int i = 0; i < emails.Count; i++)
        {
            int index = i;   // copy, so each lambda keeps its own number
            emails[i].button.onClick.AddListener(() =>
            {
                SelectEmail(index);

                if (index == 1)
                {
                    GameState.checkTerminal = true;
                    GameState.Playtest.Milestone("Medicine reply read");
                }
            });
        }

        SelectEmail(-1);     // nothing selected, all text empty
        foreach (var e in emails)
        {
            bool available = IsAvailable(e);
            e.button.gameObject.SetActive(available);

            if (e.notif != null)
                e.notif.gameObject.SetActive(available && !GameState.emailsRead.Contains(e.id));
        }

        if(GameState.days > 5 || (GameState.days == 5 && GameState.npcsTalks.Contains("Medic")))
        {
            Color t = request.color;
            t.a = 0f;
            request.color = t;
            lockIcon.gameObject.SetActive(false);
            lockText.gameObject.SetActive(false);
            DraftPanel.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        description.text = message;
    }

    public void OnClicked()
    {
        SelectEmail(-1);
        if (GameState.days == 5 && !GameState.draftMessage && GameState.npcsTalks.Contains("Medic"))
        {
            message = "This is the foreman of the HZ-3280. Our medic found an unidentifiable orange substance in the mineral lab air filter and suspects that it might be a sign of some health risk present on the vessel. Therefore, we request a shipment of antimicrobial medicine, as well as any information that the Company might have regarding what this substance might be.";
            titleText.text = "DRAFT MESSAGE";
            captionText.text = "TO: Trident Excavation";  
            send.gameObject.SetActive(true);
        }
        else if(GameState.infectionPhoto && GameState.evidenceRequestDay < 0)
        {
            message = "SOS. The HZ-3280 requests urgent aid and a second shipment of medicine; a member of our crew is dangerously ill and speaking irrationally. We do not have the resources to treat them, nor do we understand what this disease is.\n <<Image Attachment>>";
            titleText.text = "DRAFT MESSAGE";
            captionText.text = "TO: Trident Excavation";  
            send.gameObject.SetActive(true);
        }
        else
        {
            message = "\n\n\nYou don't have any messages to send at this time.";
            titleText.text = "DRAFT MESSAGE";
            captionText.text = "TO: Trident Excavation"; 
            error.gameObject.SetActive(true);
        }
    }

    public void SendClicked()
    {
        if (GameState.days == 5 && !GameState.draftMessage)
        {
            GameState.draftMessage = true;
            GameState.Playtest.Milestone("Medicine requested");
            OnInboxClicked();
        }

        else if (GameState.infectionPhoto && GameState.evidenceRequestDay < 0)
        {
            GameState.evidenceRequestDay = GameState.days;
            GameState.Playtest.Milestone("Evidence request sent");
            OnInboxClicked();
        }
    }

    public void OnInboxClicked()
    {
        ShowPopup();
        if (ColorUtility.TryParseHtmlString("#FFFFFF", out Color white))
                inbox.image.color = white;
    }

    void ShowPopup()
    {
        popup.SetActive(true);
    }

    public void ClosePopup()
    {
        popup.SetActive(false);
        titleText.text = "";
        captionText.text = "";
        message = "";
        send.gameObject.SetActive(false);
    }

    void SelectEmail(int selected)
    {
        send.gameObject.SetActive(false);
        error.gameObject.SetActive(false);
        notif.gameObject.SetActive(false);
        for (int i = 0; i < emails.Count; i++)
        {
            bool on = i == selected;
            var b = emails[i];
            Color c = on ? selectedTextColor : normalTextColor;

            b.button.image.fillCenter = on;

            if (b.titleLabel != null)   b.titleLabel.color = c;
            if (b.captionLabel != null) b.captionLabel.color = c;
            if (b.icon != null)         b.icon.color = c;
        }

        if (selected < 0)
        {
            titleText.text = "";
            captionText.text = "";
            message = "";
            return;
        }

        var read = emails[selected];
        if (!string.IsNullOrEmpty(read.id))
            GameState.emailsRead.Add(read.id);
        if (read.notif != null)
            read.notif.gameObject.SetActive(false);

        var e = emails[selected];
        titleText.text = e.title;
        captionText.text = e.caption;
        message = e.body;        // Update() copies this into the description

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

    bool IsAvailable(EmailEntry e)
    {
        switch (e.requirement)
        {
            case EmailEntry.Requirement.Day7:
                return GameState.days >= 7;

            case EmailEntry.Requirement.EvidenceReply:
                return GameState.evidenceRequestDay >= 0
                    && GameState.days >= GameState.evidenceRequestDay + 2;

            default:
                return true;
        }
    }
}
