using UnityEngine;
using TMPro;

public class EndingManager : MonoBehaviour
{
    public TextMeshProUGUI good;
    public TextMeshProUGUI bad;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(GameState.inventory.ContainsKey("Full-grown Fungus Sample") && GameState.inventory["Full-grown Fungus Sample"] > 0)
        {
            good.gameObject.SetActive(true);
            bad.gameObject.SetActive(false);
        }
        else
        {
            good.gameObject.SetActive(false);
            bad.gameObject.SetActive(true);
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void onClicked()
    {
        SceneChanger.ChangeScene("Title Screen");
    }
}
