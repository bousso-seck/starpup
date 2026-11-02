using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Developer shortcuts for testing. Nothing to attach: it creates itself when you press Play.
//   R = restart the current scene instantly, without leaving Play mode.
//   H = heal one heart (extra healing becomes golden bonus hearts).
// It only exists in the Unity Editor and development builds, never in a final release.
public class DevTools : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameObject go = new GameObject("DevTools");
        go.AddComponent<DevTools>();
        DontDestroyOnLoad(go);
#endif
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            Restart();
        }

        // H = heal one heart, so you can test golden bonus hearts.
        if (kb != null && kb.hKey.wasPressedThisFrame)
        {
            PupcatHealth health = FindFirstObjectByType<PupcatHealth>();
            if (health != null) health.Heal(2);
        }
    }

    // Reloads the current scene. Also used when the pupcat runs out of hearts.
    public static void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // Works even if the scene isn't in the Build Profiles scene list.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }
}