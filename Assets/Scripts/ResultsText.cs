using UnityEngine;
using TMPro;

public class ResultsText : MonoBehaviour
{
    private TextMeshProUGUI label;
    public int text;
    private int Expense => GameState.excursionIncapacitated ? 100 : 50;
    private int TotalCredits => GameState.credits + GameState.tellurium * 10 - Expense;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
        RefreshTotals();
    }

    internal void RefreshTotals()
    {
        if (text == 1)
            label.text = $"{GameState.tellurium} Tellurium Samples Recovered = {GameState.tellurium * 10} Credits";
        if (text == 2)
            label.text = $"Total credits: {TotalCredits}";
        if (text == 3)
            label.text = GameState.excursionIncapacitated
                ? $"Incapacitated • {GameState.oreLost} samples lost\nDaily Expenses: -{Expense} Credits"
                : $"Daily Expenses: -{Expense} Credits";
    }

    public void onLeave()
    {
        SceneChanger.ChangeSceneWithFade("Interior", () =>
        {
            GameState.credits = TotalCredits;
            GameState.tellurium = 0;
            GameState.oreLost = 0;
            GameState.excursionIncapacitated = false;
            GameState.days++;
#if UNITY_EDITOR
            GameStateDebugger.CaptureDayStart("Interior");
#endif
        });
    }
}
