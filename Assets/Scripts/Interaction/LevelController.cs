using UnityEngine.SceneManagement;

public class LevelController : TriggerController
{
    protected override void Interact()
    {
        LoadNextLevel();
    }

    public void LoadNextLevel()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        int nextLevelIndex = activeScene.buildIndex + 1;

        if (nextLevelIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextLevelIndex);
        }
        else
        {
            UnityEngine.Debug.LogErrorFormat("Can't find any level with index {0}. Please add one to the Build Profiles", nextLevelIndex);
        }
    }
}