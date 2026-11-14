using UnityEngine;
using DivingPrototype;

public class CorpseManager : MonoBehaviour
{
    public OceanTarget[] corpses; 

    void Awake()
    {
        foreach (var corpse in corpses)
            corpse.gameObject.SetActive(IsDead(corpse.corpseId));
    }

    bool IsDead(string id)
    {
        switch (id)
        {
            case "Jonah":   return GameState.days >= 12;  
            case "Kirsten": return GameState.days > 16 && GameState.dayOfKirstenCured < 0;                 
            default:        return false;
        }
    }
}