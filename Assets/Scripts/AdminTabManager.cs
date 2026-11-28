using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AdminTabManager : MonoBehaviour
{
    public Button tab;
    public TextMeshProUGUI buttonText;

    public GameObject textPanel;
    public Image lockIcon;
    public GameObject lockPanel;
    public GameObject hintPanel;

    /// <summary>Shows the passkey hint or unlocks the files after the Eye photo is uploaded.</summary>
    void Start()
    {
        hintPanel.SetActive(false);
        textPanel.SetActive(false);
        if (ColorUtility.TryParseHtmlString("#A4DDDB", out Color temp))
                temp.a = Mathf.Clamp01(0.9f);
                Debug.Log("Change button color next");
                tab.image.color = temp;
                if (ColorUtility.TryParseHtmlString("#04302D", out Color text))
                {
                    Debug.Log("Change text color next");
                    buttonText.color = text;
                }



        lockIcon.gameObject.SetActive(true);
        lockPanel.SetActive(true);
        if (GameState.infectionPhoto)
        {
            hintPanel.SetActive(true);
            GameState.passkeyHintSeen = true;
            GameState.Playtest.Milestone("Passkey hint viewed");
        }

        if (GameState.abyssPhoto)
        {
            lockIcon.gameObject.SetActive(false);
            lockPanel.SetActive(false);
            hintPanel.SetActive(false);
            textPanel.SetActive(true);
            GameState.adminUnlocked = true;
            GameState.Playtest.Milestone("Admin files unlocked");
        }
    }
}
