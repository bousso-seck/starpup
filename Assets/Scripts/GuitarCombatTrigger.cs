
using UnityEngine;
using UnityEngine.SceneManagement;

public class GuitarCombatTrigger : MonoBehaviour
{
    public Transform player;
    public float interactionDistance = 3f;
    public string minigameScene = "GuitarMinigame";

    private Enemy enemy;
    private bool freed;

    private static string targetName;
    private static string returnScene;
    private static bool pendingResult;
    private static bool cleanRun;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        targetName = null;
        returnScene = null;
        pendingResult = false;
        cleanRun = false;
    }

    void Start()
    {
        enemy = GetComponent<Enemy>();

        if (pendingResult &&
            gameObject.name == targetName)
        {
            freed = true;
            StopFighting();

            if (cleanRun)
            {
                PupcatHealth health =
                    FindFirstObjectByType<PupcatHealth>();

                if (health != null)
                    health.Heal(100);
            }

            pendingResult = false;
        }
    }

    void Update()
    {
        if (freed || player == null || enemy == null)
            return;

        if (!Input.GetKeyDown(KeyCode.G))
            return;

        if (Vector2.Distance(
            transform.position, player.position)
            > interactionDistance)
            return;

        if (enemy.data == null ||
            !enemy.data.isPossessed)
        {
            Debug.Log("Guitar fizzles! Not possessed.");
            return;
        }

        targetName = gameObject.name;
        returnScene = SceneManager.GetActiveScene().name;

        SceneManager.LoadScene(minigameScene);
    }

    void StopFighting()
    {
        if (enemy != null)
        {
            enemy.brainPaused = true;
            enemy.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SpriteRenderer sprite =
            GetComponent<SpriteRenderer>();

        if (sprite != null)
            sprite.color = Color.green;

        Debug.Log(gameObject.name + " is freed!");
    }

    public static void FinishCombat(bool success, bool clean)
    {
        if (success)
        {
            pendingResult = true;
            cleanRun = clean;
        }

        if (!string.IsNullOrEmpty(returnScene))
            SceneManager.LoadScene(returnScene);
        else
            Debug.LogWarning("No return scene assigned.");
    }
}
