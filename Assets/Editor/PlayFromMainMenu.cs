#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor only (put this file in Assets/Editor). Makes the Play button always start at the Main Menu,
// whichever scene you have open. Turn it off from the menu bar: Starpup > Play From Main Menu
// (handy when you want to test Level 1 directly).
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    const string MenuPath = "Starpup/Play From Main Menu";
    const string PrefKey = "Starpup.PlayFromMainMenu";
    const string MenuScene = "Assets/Scenes/MainMenu.unity";

    static PlayFromMainMenu()
    {
        Apply();
    }

    static bool Enabled
    {
        get { return EditorPrefs.GetBool(PrefKey, true); }
        set { EditorPrefs.SetBool(PrefKey, value); }
    }

    static void Apply()
    {
        if (Enabled)
        {
            SceneAsset menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene);
            EditorSceneManager.playModeStartScene = menu; // null if the scene isn't found = normal behaviour
        }
        else
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
}
#endif