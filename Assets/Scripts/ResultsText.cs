using UnityEngine;
using TMPro;

public class ResultsText : MonoBehaviour
{
    private TextMeshProUGUI label;
    public int text;
    private int Expense => GameState.excursionIncapacitated ? 100 : 50;
    private int SampleCredits => GameState.tellurium * 10;
    private int TotalCredits => GameState.credits + SampleCredits - Expense;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
        RefreshTotals();
    }

    internal void RefreshTotals()
    {
        if (text == 1)
            label.text = GameState.interiorDayResults
                ? $"Cole mined {GameState.tellurium} tellurium samples today = {SampleCredits} Credits"
                : $"{GameState.tellurium} Tellurium Samples Recovered = {SampleCredits} Credits";
        if (text == 2)
            label.text = $"Total credits: {TotalCredits}";
        if (text == 3)
            label.text = GameState.interiorDayResults
                ? $"Daily expenses: -{Expense} Credits"
                : GameState.excursionIncapacitated
                ? $"Incapacitated • {GameState.oreLost} samples lost\nDaily Expenses: -{Expense} Credits"
                : $"Daily Expenses: -{Expense} Credits";
    }

    /// <summary>Settles the displayed results and advances to the next day or the ending.</summary>
    public void onLeave()
    {
        bool interiorDay = GameState.interiorDayResults;
        string next = interiorDay ? "Ocean Interactables" :
            GameState.days + 1 >= GameState.LastDay ? "Ending" : "Interior";
        SceneChanger.ChangeSceneWithFade(next, () =>
        {
            GameState.credits = TotalCredits;
            GameState.tellurium = 0;
            GameState.oreLost = 0;
            GameState.excursionIncapacitated = false;
            GameState.interiorDayResults = false;
            if (interiorDay) GameState.npcsTalks.Clear();
            GameState.days++;
#if UNITY_EDITOR
            if (next != "Ending") GameStateDebugger.CaptureDayStart(next);
#endif
        });
    }
}
