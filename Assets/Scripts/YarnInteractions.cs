using UnityEngine;
using Yarn.Unity;

public class WorldInteractions : MonoBehaviour
{
    [YarnCommand("NewDay")]
    public static void NewDay()
    {
        GameState.days++;
        SceneChanger.ChangeScene("Ocean Floor");
    }     
}
