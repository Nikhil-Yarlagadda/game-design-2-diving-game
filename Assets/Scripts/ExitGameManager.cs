using UnityEngine;

public class QuitManager : MonoBehaviour
{
    public void QuitGame()
    {
        // Log to the console so you can see it working inside the editor
        Debug.Log("Game is exiting..."); 
        
        // This closes the actual built game executable
        Application.Quit(); 
    }
}
