using UnityEngine;
using TMPro;

public class ResultsText : MonoBehaviour
{
   private TextMeshProUGUI label;
   public int text;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
        RefreshTotals();
    }

    internal void RefreshTotals()
    {
        if(text == 1)
            label.text = GameState.tellurium + " Tellurium Samples Collected = " + GameState.tellurium*10 +" Credits";
        if(text == 2)
            label.text = "Total credits: " +  (GameState.credits + (GameState.tellurium*10) - 50);
    }

    public void onLeave(){
        if (SceneChanger.IsTransitioning) return;
        GameState.credits += (GameState.tellurium*10) - 50;
        GameState.tellurium = 0;
        GameState.days++;

        if(GameState.days > 20)
            SceneChanger.ChangeScene("Ending");
        else
        {
#if UNITY_EDITOR
            GameStateDebugger.CaptureDayStart("Interior");
#endif
        }
    }
}
