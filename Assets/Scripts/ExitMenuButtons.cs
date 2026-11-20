
using UnityEngine;

public class ExitMenuButtons : MonoBehaviour
{
    public void Resume()
    {
        PauseMenuManager.Instance?.ResumeGame();
    }

    public void LevelSelect()
    {
        PauseMenuManager.Instance?.GoToLevelSelect();
    }

    public void ExitGame()
    {
        PauseMenuManager.Instance?.ExitGame();
    }
}
