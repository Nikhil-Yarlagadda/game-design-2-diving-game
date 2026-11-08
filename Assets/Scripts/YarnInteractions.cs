using UnityEngine;
using Yarn.Unity;
using System.Collections.Generic;

public class WorldInteractions : MonoBehaviour
{
    [YarnCommand("NewDay")]
    public static void NewDay()
    {
        SceneChanger.ChangeSceneWithFade("Ocean Interactables", () =>
        {
            GameState.days++;
            GameState.npcsTalks = new HashSet<string>();
        });
    }     
}
