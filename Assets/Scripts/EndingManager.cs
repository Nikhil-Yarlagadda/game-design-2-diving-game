using UnityEngine;
using TMPro;

public class EndingManager : MonoBehaviour
{
    public TextMeshProUGUI good;
    public TextMeshProUGUI bad;
    /// <summary>Shows the ending earned by the end of the final excursion.</summary>
    void Start()
    {
        bool recoveredSample = GameState.dayFgfsCollected is >= 0 and <= GameState.LastDay;
        GameState.Playtest.completed = true;
        GameState.Playtest.Milestone("Ending reached", recoveredSample ? "Good ending" : "Bad ending");
        good.gameObject.SetActive(recoveredSample);
        bad.gameObject.SetActive(!recoveredSample);
    }

    public void onClicked()
    {
        SceneChanger.ChangeScene("Title Screen");
    }
}
