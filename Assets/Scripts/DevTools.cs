using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Developer shortcuts for testing. Nothing to attach: it creates itself when you press Play.
//   R = restart the current scene instantly, without leaving Play mode.
//   H = heal one heart (extra healing becomes golden bonus hearts).
//   K = defeat the boss instantly (only while the fight is on), to test what happens after.
//   B = jump into the boss arena: the gate is thrown and the fight starts at once.
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

        // B = jump to the boss arena, so you don't have to run the whole level to test the fight.
        if (kb != null && kb.bKey.wasPressedThisFrame)
        {
            BossArena arena = FindFirstObjectByType<BossArena>();
            PupcatController pup = FindFirstObjectByType<PupcatController>();
            if (arena != null && pup != null)
            {
                Vector3 p = pup.transform.position;
                p.x = arena.transform.position.x + arena.entryDepth + 0.5f; // just inside the arena: the gate is thrown and the fight starts
                pup.transform.position = p;
                Rigidbody2D rb = pup.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
                Debug.Log("DevTools: jumped into the boss arena. The fight starts. Press K to defeat the boss.");
            }
            else
            {
                Debug.Log("DevTools: no BossArena or Pupcat in this scene.");
            }
        }

        // K = defeat the boss at once, so you can test the gate falling, the goal and the level select.
        if (kb != null && kb.kKey.wasPressedThisFrame)
        {
            GymTeacherBoss boss = FindFirstObjectByType<GymTeacherBoss>();
            Enemy bossEnemy = (boss != null) ? boss.GetComponent<Enemy>() : null;
            if (bossEnemy != null && !boss.IsOver)
            {
                bossEnemy.TakeHit(100000f, 2f);
                Debug.Log("DevTools: boss defeated.");
            }
            else
            {
                Debug.Log("DevTools: no boss fight to end right now.");
            }
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