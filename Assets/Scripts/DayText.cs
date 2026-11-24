using UnityEngine;
using TMPro;

public class DayText : MonoBehaviour
{
    private TextMeshProUGUI label;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if(GameState.days < 10)
            label.text = "DAY 0" + GameState.days;
        else
            label.text = "DAY " + GameState.days;
    }
    public void OnClicked()
    {
        GameState.days++;
        GameState.npcsTalks.Add("1");
        GameState.npcsTalks.Add("2");
        GameState.npcsTalks.Add("3");
    }
}