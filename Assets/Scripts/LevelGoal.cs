using UnityEngine;

// The finish line of a level. When the pupcat reaches it, the bell timer stops and a
// "made it" message shows. For now the level then restarts. Later this will load the next
// level or the level select screen.
//
// Setup: GameObject > Create Empty, name it "Level Goal", add this script.
// It adds its own trigger box, so you only need to position it at the end of the street.
// Select it in the Scene view to see the green finish zone.
public class LevelGoal : MonoBehaviour
{
    public string levelName = "Level 1";
    public Vector2 zoneSize = new Vector2(2f, 10f); // the size of the finish zone
    public float restartDelay = 5f;                 // seconds before the level restarts

    private bool reached;
    private float timeLeft;

    void Awake()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = zoneSize;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (reached) return;
        if (other.GetComponent<PupcatController>() == null) return;

        reached = true;

        // Stop the bell timer so it doesn't run out while the message shows.
        BellTimer bell = FindFirstObjectByType<BellTimer>();
        if (bell != null)
        {
            timeLeft = bell.remainingTime;
            bell.enabled = false;
        }

        GameFeel.Play(GameFeel.Sound.Parry); // a bright ping
        Debug.Log(levelName + " complete. Time left: " + Mathf.CeilToInt(timeLeft) + "s");
        Invoke(nameof(Finish), restartDelay);
    }

    void Finish()
    {
        // Placeholder: restart. Later: go to the next level or the level select screen.
        DevTools.Restart();
    }

    void OnGUI()
    {
        if (!reached) return;

        GUIStyle big = new GUIStyle(GUI.skin.label);
        big.fontSize = 48;
        big.alignment = TextAnchor.MiddleCenter;
        big.normal.textColor = Color.white;

        GUIStyle small = new GUIStyle(big);
        small.fontSize = 26;

        float w = Screen.width;
        float h = Screen.height;
        GUI.Label(new Rect(0f, h * 0.35f, w, 70f), levelName + " complete!", big);

        int minutes = Mathf.FloorToInt(timeLeft / 60f);
        int seconds = Mathf.FloorToInt(timeLeft % 60f);
        GUI.Label(new Rect(0f, h * 0.35f + 70f, w, 40f),
            "You made it with " + string.Format("{0:00}:{1:00}", minutes, seconds) + " to spare", small);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(zoneSize.x, zoneSize.y, 0f));
    }
}