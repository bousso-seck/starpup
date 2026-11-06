
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    // Main Menu: Play -> Level 1
    public void PlayGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    // Main Menu: Level Select -> Level Select screen
    public void OpenLevelSelect()
    {
        SceneManager.LoadScene("LevelSelect");
    }

    // Level Select: Level 1 -> Gameplay
    public void PlayLevel1()
    {
        SceneManager.LoadScene("SampleScene");
    }

    // Level Select: Back -> Main Menu
    public void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    // Main Menu: Exit -> Quit game
    public void ExitGame()
    {
        Application.Quit();
    }
}
