using UnityEngine;
using DivingPrototype;

public class CorpseManager : MonoBehaviour
{
    public OceanTarget[] corpses;

    /// <summary>Shows the offshore remains of crew members who have disappeared.</summary>
    void Awake()
    {
        foreach (var corpse in corpses)
            corpse.gameObject.SetActive(corpse.corpseId switch
            {
                "Jonah" => !GameState.IsCrewAboard("Mineral Processor"),
                "Kirsten" => !GameState.IsCrewAboard("Medic"),
                _ => false
            });
    }
}
