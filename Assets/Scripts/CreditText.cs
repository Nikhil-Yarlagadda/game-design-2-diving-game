using UnityEngine;
using TMPro;

public class CreditText : MonoBehaviour
{
    private TextMeshProUGUI label;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        label.text = "Credits: " + GameState.credits;
    }
    public void OnClicked()
    {
        Debug.Log("Clicked credit text");
        GameState.credits += 10;
    }
}