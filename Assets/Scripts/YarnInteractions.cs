using UnityEngine;
using Yarn.Unity;
using System.Collections.Generic;

public class WorldInteractions : MonoBehaviour
{
    [YarnCommand("NewDay")]
    public static void NewDay()
    {
        GameState.days++;
        GameState.npcsTalks = new HashSet<string>();
        SceneChanger.ChangeScene("Ocean Interactables");
    }     
}
