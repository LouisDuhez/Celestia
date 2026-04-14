using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Type the exact name of your game scene here.")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Tooltip("Type the exact name of your main menu scene here.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

 
    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }


    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

  
    public void QuitApplication()
    {
        Debug.Log("Application is quitting...");

        Application.Quit();
    }
}