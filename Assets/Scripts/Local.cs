using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Local : MonoBehaviour
{
    public TextMeshProUGUI buttonText;
    public Button tab;
    
    void Start()
    {
        if (ColorUtility.TryParseHtmlString("#A4DDDB", out Color temp))
                temp.a = Mathf.Clamp01(0.9f);
                tab.image.color = temp;
                if (ColorUtility.TryParseHtmlString("#04302D", out Color text))
                {
                    buttonText.color = text;
                }
    }
}
