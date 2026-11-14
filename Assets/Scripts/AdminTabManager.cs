using UnityEngine;
using TMPro;

public class AdminTabManager : MonoBehaviour
{
    public TextMeshProUGUI description;
    private string message = "Nothing yet";

    void Start()
    {
       message = "Encrypted files: Unlocked\nImage recognition passkey: Success\n\n(Encrypted file contents):\n\nTrident Intelligence - Hadal Organism Research Branch\n\nDormant Mucor amentia spores have been found in sediment samples collected from various locations on the abyssal plain. This parasitic organism follows a unique life cycle in four stages.\n\nIn its juvenile stage, Mucor amentia takes the form of microscopic spores. These spores can remain dormant indefinitely when submerged in water. When removed from water, they spread rapidly through the air to be inhaled by potential hosts. When many of these spores coalesce in one location, they form a viscous mass with an orange-yellow hue.\n\nWhen occupying a human host, Mucor amentia is able to mature into its second life stage. Growths may eventually appear on the host’s skin, and host’s behavior will gradually shift as Mucor amentia compels them to seek deep open water. The host may appear manic and unnaturally invigorated.\n\nWhen Mucor amentia has matured as much as possible in its human host, it enters stage three of its life cycle, where it’s no longer contagious to other humans. The host will be urgently compelled to submerge themself in deep open water at any cost. They will gain an unnatural ability to travel large distances underwater, seemingly without needing any oxygen at all. The growths will become very large and eventually immobilize the host at this stage.\n\nThis behavior allows Mucor amentia to use its human host as bait for very large marine organisms (codenamed Leviathans). Only when Mucor amentia is consumed by a Leviathan may it reach its final life stage and reproduce.\n\nSince Leviathans are few and far between in the ocean, Mucor amentia has adapted itself to spread across as wide of an area as possible. Stage three Mucor amentia are highly territorial to other members of the species. When even just a severed piece of stage three Mucor amentia is confined in a space with younger members of the species, the younger Mucor amentia will all rapidly die, and any hosts will fully recover from the infection.";
        if(GameState.infectionPhoto && GameState.inventory.ContainsKey("Photograph of Infected Crewmate"))
        {
            message = "Encrypted files: Locked\nImage recognition passkey: Failed. Invalid image contents. View passkey hint?\n(Hint):\nMeet the void’s gaze, somewhere on the abyssal plain.";
        }
        if(GameState.abyssPhoto && GameState.inventory.ContainsKey("Photograph of the Eye of the Abyss"))
        {
            GameState.adminUnlocked = true;
            message = "Encrypted files: Unlocked\nImage recognition passkey: Success\n\n(Encrypted file contents):\n\nTrident Intelligence - Hadal Organism Research Branch\n\nDormant Mucor amentia spores have been found in sediment samples collected from various locations on the abyssal plain. This parasitic organism follows a unique life cycle in four stages.\n\nIn its juvenile stage, Mucor amentia takes the form of microscopic spores. These spores can remain dormant indefinitely when submerged in water. When removed from water, they spread rapidly through the air to be inhaled by potential hosts. When many of these spores coalesce in one location, they form a viscous mass with an orange-yellow hue.\n\nWhen occupying a human host, Mucor amentia is able to mature into its second life stage. Growths may eventually appear on the host’s skin, and host’s behavior will gradually shift as Mucor amentia compels them to seek deep open water. The host may appear manic and unnaturally invigorated.\n\nWhen Mucor amentia has matured as much as possible in its human host, it enters stage three of its life cycle, where it’s no longer contagious to other humans. The host will be urgently compelled to submerge themself in deep open water at any cost. They will gain an unnatural ability to travel large distances underwater, seemingly without needing any oxygen at all. The growths will become very large and eventually immobilize the host at this stage.\n\nThis behavior allows Mucor amentia to use its human host as bait for very large marine organisms (codenamed Leviathans). Only when Mucor amentia is consumed by a Leviathan may it reach its final life stage and reproduce.\n\nSince Leviathans are few and far between in the ocean, Mucor amentia has adapted itself to spread across as wide of an area as possible. Stage three Mucor amentia are highly territorial to other members of the species. When even just a severed piece of stage three Mucor amentia is confined in a space with younger members of the species, the younger Mucor amentia will all rapidly die, and any hosts will fully recover from the infection.";
        }
    }

    // Update is called once per frame
    void Update()
    {
        description.text = message;
    }
}
