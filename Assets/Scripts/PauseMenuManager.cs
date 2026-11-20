
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    private bool isPaused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;

        SceneManager.LoadScene("ExitMenu", LoadSceneMode.Additive);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        SceneManager.UnloadSceneAsync("ExitMenu");
    }

    public void GoToLevelSelect()
    {
        isPaused = false;
        Time.timeScale = 1f;

        SceneManager.LoadScene("LevelSelect");
    }

    public void ExitGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
