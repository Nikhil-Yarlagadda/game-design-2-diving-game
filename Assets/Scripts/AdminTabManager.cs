using UnityEngine;
using TMPro;

public class AdminTabManager : MonoBehaviour
{
    public TextMeshProUGUI description;

    /// <summary>Shows the passkey hint or unlocks the files after the Eye photo is uploaded.</summary>
    void Start()
    {
        if (!GameState.infectionPhoto)
        {
            description.text = "Encrypted files: Locked";
            return;
        }

        if (!GameState.abyssPhoto)
        {
            GameState.passkeyHintSeen = true;
            description.text = "Encrypted files: Locked\nImage recognition passkey: Failed. Invalid image contents. View passkey hint?\n(Hint):\nMeet the void’s gaze, somewhere on the abyssal plain.";
            return;
        }

        GameState.adminUnlocked = true;
        description.text = "Encrypted files: Locked\nImage recognition passkey: Failed. Invalid image contents. View passkey hint?\n(Hint):\nMeet the void’s gaze, somewhere on the abyssal plain.";
    }
}
