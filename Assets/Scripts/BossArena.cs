using System.Collections;
using UnityEngine;

// The Level 1 boss arena. When the pupcat walks into the arena past the school gates, the gym teacher
// rears back and THROWS an iron gate over the pupcat's head. It crashes down behind the pupcat and the
// way back is shut. The camera locks to the arena, the boss music fades in, and the fight begins.
// When the boss is beaten (or gives up and walks away), the music fades and the gate falls over and the way opens.
//
// SETUP
//   1. GameObject > Create Empty, name it "BossArena", add this script.
//   2. Move it to where the GATE should land: its X is the gate's position, and its Y must be the top of the
//      ground. Select it to see the arena in the Scene view (cyan box, red line = gate, yellow line = where
//      the pupcat has to walk to start the fight).
//   3. Set Arena Width so the right edge of the cyan box is on your right wall (Wall Right).
//   4. Drag the gym teacher into the Boss slot. Put him inside the arena, on the right.
//   5. Optional: drag a music track into Boss Music, and your own gate picture into Gate Sprite.
public class BossArena : MonoBehaviour
{
    [Header("The boss")]
    public GymTeacherBoss boss;           // drag the gym teacher here
    public AudioClip bossMusic;           // optional: your track. Empty = Resources/Music/BossTheme, else the placeholder loop.
    public GameObject levelGoal;          // optional: drag the Level Goal here. It stays switched off until the boss is beaten.

    [Header("Shape (this object sits where the gate lands, on top of the ground)")]
    public float arenaWidth = 22f;        // from the gate to the right wall
    public float entryDepth = 3f;         // how far into the arena the pupcat walks before the gate is thrown
    public float gateHeight = 7f;         // how tall the gate looks
    public float gateWidth = 1.2f;        // how wide the gate is (it is also how wide the invisible wall is)
    public Sprite gateSprite;             // optional: your own gate picture. Empty = a gate drawn from code.

    [Header("The throw")]
    public float windUpTime = 1f;         // the boss rears back (flashing) before he throws
    public float throwTime = 0.9f;        // how long the gate is in the air
    public float arcHeight = 4f;          // how high it flies
    public float endDelay = 1.5f;         // pause after the boss is beaten, before the gate opens

    private PupcatController player;
    private CameraFollow cam;
    private bool started;
    private bool fightOn;

    private bool savedLimits;
    private bool oldUseLimits;
    private float oldMin, oldMax;

    private Transform gate;
    private GameObject blocker;
    private GUIStyle titleStyle;
    private static Sprite boxSprite;

    float GateX { get { return transform.position.x; } }
    float GroundY { get { return transform.position.y; } }
    float RightX { get { return GateX + arenaWidth; } }

    void Start()
    {
        player = FindFirstObjectByType<PupcatController>();
        cam = FindFirstObjectByType<CameraFollow>();
        if (boss == null) boss = FindFirstObjectByType<GymTeacherBoss>();

        if (player == null || boss == null)
        {
            Debug.LogError("BossArena needs a Pupcat and a GymTeacherBoss in the scene. Drag the boss into the Boss slot.", this);
            enabled = false;
            return;
        }

        // The goal can't be reached until the boss is beaten, so the fight can't be skipped.
        if (levelGoal != null) levelGoal.SetActive(false);
    }

    void Update()
    {
        if (!started)
        {
            if (player.transform.position.x >= GateX + entryDepth)
            {
                started = true;
                StartCoroutine(Intro());
            }
            return;
        }

        if (fightOn && boss.IsOver)
        {
            fightOn = false;
            StartCoroutine(Outro());
        }
    }

    // ---------- The fight starts ----------

    IEnumerator Intro()
    {
        Debug.Log("Boss arena: the pupcat is in. The gate is coming.");

        // The way back is shut at once (an invisible wall where the gate will land), so there is no running out.
        blocker = new GameObject("GateBlocker");
        blocker.transform.position = new Vector3(GateX, GroundY + 12f, 0f);
        BoxCollider2D wall = blocker.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(gateWidth, 26f);

        // The camera stays inside the arena from now on.
        LockCamera();

        // Music in, and the boss rears back.
        Music.Play(PickMusic(), 0.6f);
        boss.PlayThrowPose(windUpTime);
        GameFeel.Play(GameFeel.Sound.BossWhistle);
        yield return new WaitForSeconds(windUpTime);

        // The throw: the gate flies from the boss, over the pupcat, and lands upright where the arena starts.
        gate = BuildGate();
        Vector3 from = boss.transform.position + Vector3.up;
        Vector3 to = new Vector3(GateX, GroundY + gateHeight / 2f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, throwTime);
            float k = Mathf.Clamp01(t);
            Vector3 pos = Vector3.Lerp(from, to, k);
            pos.y += arcHeight * Mathf.Sin(Mathf.PI * k);
            gate.position = pos;
            gate.rotation = Quaternion.Euler(0f, 0f, 360f * k); // one full flip, so it lands the right way up
            yield return null;
        }

        // Slam.
        gate.position = to;
        gate.rotation = Quaternion.identity;
        GameFeel.Play(GameFeel.Sound.GateSlam);
        GameFeel.Shake(0.35f, 0.5f);
        GameFeel.HitStop(0.08f);
        GameFeel.Dust(new Vector3(GateX, GroundY, 0f), 14, 0f);

        yield return new WaitForSeconds(0.5f);

        boss.BeginFight();
        fightOn = true;
        Debug.Log("Boss arena: the fight begins.");
    }

    // ---------- The fight ends ----------

    IEnumerator Outro()
    {
        yield return new WaitForSeconds(endDelay);

        Music.Stop(1.5f);
        
        if (blocker != null) Destroy(blocker);
        UnlockCamera();
        if (levelGoal != null) levelGoal.SetActive(true); // the way out is open

        // The gate FALLS: it tips over toward the street, crashes flat, then fades away.
        if (gate != null)
        {
            Vector3 pivot = new Vector3(GateX, GroundY, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.9f;
                float k = Mathf.Clamp01(t);
                float angle = 90f * k * k;                         // slow at first, then it drops
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);
                gate.rotation = rot;
                gate.position = pivot + rot * new Vector3(0f, gateHeight / 2f, 0f);
                yield return null;
            }
            GameFeel.Play(GameFeel.Sound.GateSlam);
            GameFeel.Shake(0.3f, 0.4f);
            GameFeel.Dust(new Vector3(GateX - gateHeight, GroundY, 0f), 12, 0f);

            yield return new WaitForSeconds(1.2f);

            SpriteRenderer[] parts = gate.GetComponentsInChildren<SpriteRenderer>();
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.8f;
                foreach (SpriteRenderer sr in parts)
                {
                    if (sr == null) continue;
                    Color col = sr.color;
                    col.a = 1f - Mathf.Clamp01(t);
                    sr.color = col;
                }
                yield return null;
            }
            Destroy(gate.gameObject);
        }
        Debug.Log("Boss arena: the gate is open.");
    }

    // ---------- Camera ----------

    void LockCamera()
    {
        if (cam == null) return;
        oldUseLimits = cam.useLimits;
        oldMin = cam.minX;
        oldMax = cam.maxX;
        savedLimits = true;

        cam.useLimits = true;
        cam.minX = GateX;
        cam.maxX = RightX;
    }

    void UnlockCamera()
    {
        if (cam == null || !savedLimits) return;
        cam.useLimits = oldUseLimits;
        cam.minX = oldMin;
        cam.maxX = oldMax;
        savedLimits = false;
    }

    // ---------- Music ----------

    AudioClip PickMusic()
    {
        if (bossMusic != null) return bossMusic;

        AudioClip fromResources = Resources.Load<AudioClip>("Music/BossTheme");
        if (fromResources != null) return fromResources;

        Debug.Log("Boss music: using the placeholder loop. Drag your track into Boss Music, or save it as Assets/Resources/Music/BossTheme.wav.");
        return Music.Placeholder();
    }

    // ---------- The gate ----------

    Transform BuildGate()
    {
        GameObject root = new GameObject("ArenaGate");

        if (gateSprite != null)
        {
            SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = gateSprite;
            sr.sortingOrder = 15;
            return root.transform;
        }

        // A plain iron gate: two posts, bars and two rails.
        Color iron = new Color(0.16f, 0.18f, 0.2f);
        Color post = new Color(0.3f, 0.32f, 0.35f);
        int bars = 5;
        for (int i = 0; i < bars; i++)
        {
            float x = Mathf.Lerp(-gateWidth / 2f + 0.15f, gateWidth / 2f - 0.15f, i / (bars - 1f));
            Part(root.transform, new Vector2(x, 0f), new Vector2(0.1f, gateHeight), iron);
        }
        Part(root.transform, new Vector2(0f, gateHeight * 0.36f), new Vector2(gateWidth, 0.14f), iron);
        Part(root.transform, new Vector2(0f, -gateHeight * 0.36f), new Vector2(gateWidth, 0.14f), iron);
        Part(root.transform, new Vector2(-gateWidth / 2f + 0.08f, 0f), new Vector2(0.18f, gateHeight), post);
        Part(root.transform, new Vector2(gateWidth / 2f - 0.08f, 0f), new Vector2(0.18f, gateHeight), post);
        return root.transform;
    }

    void Part(Transform parent, Vector2 localPos, Vector2 size, Color color)
    {
        GameObject go = new GameObject("GatePart");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBoxSprite();
        sr.color = color;
        sr.sortingOrder = 15;
    }

    static Sprite GetBoxSprite()
    {
        if (boxSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            boxSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
        return boxSprite;
    }

    // ---------- The boss's health bar, along the bottom of the screen ----------

    void OnGUI()
    {
        if (!fightOn || boss == null) return;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 18;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
        }

        float w = Mathf.Min(Screen.width * 0.6f, 700f);
        Rect bar = new Rect((Screen.width - w) / 2f, Screen.height - 46f, w, 16f);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(bar.x - 3f, bar.y - 3f, bar.width + 6f, bar.height + 6f), Texture2D.whiteTexture);
        GUI.color = new Color(0.9f, 0.2f, 0.2f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * boss.HealthFraction, bar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(bar.x, bar.y - 28f, bar.width, 26f), "GYM TEACHER", titleStyle);
    }

    // ---------- Scene view ----------

    void OnDrawGizmosSelected()
    {
        float x = transform.position.x;
        float y = transform.position.y;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(x + arenaWidth / 2f, y + 5f, 0f), new Vector3(arenaWidth, 10f, 0f));

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);   // where the gate lands
        Gizmos.DrawLine(new Vector3(x, y, 0f), new Vector3(x, y + gateHeight, 0f));
        Gizmos.DrawWireCube(new Vector3(x, y + gateHeight / 2f, 0f), new Vector3(gateWidth, gateHeight, 0f));

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);   // where the pupcat must walk to start the fight
        Gizmos.DrawLine(new Vector3(x + entryDepth, y, 0f), new Vector3(x + entryDepth, y + 10f, 0f));
    }
}