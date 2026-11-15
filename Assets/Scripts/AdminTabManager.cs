using UnityEngine;
using TMPro;

public class AdminTabManager : MonoBehaviour
{
    public TextMeshProUGUI description;

    private const string UnlockedFiles = @"Encrypted files: Unlocked

Image recognition passkey: Success

(Encrypted file contents):

Trident Intelligence - Hadal Organism Research Branch

Dormant <i>Mucor amentia</i> spores have been found in sediment samples collected from various locations on the abyssal plain. This parasitic organism follows a unique life cycle in four stages.

In its juvenile stage, <i>Mucor amentia</i> takes the form of microscopic spores. These spores can remain dormant indefinitely when submerged in water. When removed from water, they spread rapidly through the air to be inhaled by potential hosts. When many of these spores coalesce in one location, they form a viscous mass with an orange-yellow hue.

When occupying a human host, <i>Mucor amentia</i> is able to mature into its second life stage. Growths may eventually appear on the host’s skin, and host’s behavior will gradually shift as <i>Mucor amentia</i> compels them to seek deep open water. The host may appear manic and unnaturally invigorated.

When <i>Mucor amentia</i> has matured as much as possible in its human host, it enters stage three of its life cycle, where it’s no longer contagious to other humans. The host will be urgently compelled to submerge themself in deep open water at any cost. They will gain an unnatural ability to travel large distances underwater, seemingly without needing any oxygen at all. The growths will become very large and eventually immobilize the host at this stage.

This behavior allows <i>Mucor amentia</i> to use its human host as bait for very large marine organisms (codenamed Leviathans). Only when <i>Mucor amentia</i> is consumed by a Leviathan may it reach its final life stage and reproduce.

Since Leviathans are few and far between in the ocean, <i>Mucor amentia</i> has adapted itself to spread across as wide of an area as possible. Stage three <i>Mucor amentia</i> are highly territorial to other members of the species. When even just a severed piece of stage three <i>Mucor amentia</i> is confined in a space with younger members of the species, the younger <i>Mucor amentia</i> will all rapidly die, and any hosts will fully recover from the infection.";

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
        description.text = UnlockedFiles;
    }
}
