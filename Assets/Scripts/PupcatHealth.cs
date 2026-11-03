using UnityEngine;

// The pupcat's courage hearts. 1 heart = 2 points, so a half heart is 1 point.
// Attach this to the Pupcat, next to PupcatController.
//  - Hits take points off the golden bonus hearts first, then the normal hearts.
//  - Healing past full turns into golden bonus hearts (up to Max Bonus Hearts).
//  - At zero hearts the pupcat goes home (for now, the scene restarts).
// The hearts on screen are plain colored boxes for now; real heart art can replace them later.
[RequireComponent(typeof(PupcatController))]
public class PupcatHealth : MonoBehaviour
{
    [Header("Hearts")]
    public int startingHearts = 4;
    public int maxBonusHearts = 2;

    [Header("When hit")]
    public float hurtInvincibility = 0.3f; // short on purpose, so both swings of a combo can land
    public float knockbackSpeed = 5f;      // how hard a hit shoves the pupcat away
    public float knockbackUp = 3f;         // and a little upward
    public float knockbackTime = 0.12f;    // how long the shove lasts
    public float goHomeDelay = 1.5f;       // pause at zero hearts before going home

    public int Points { get; private set; }       // normal hearts, in points
    public int BonusPoints { get; private set; }  // golden hearts, in points
    public int MaxPoints { get { return startingHearts * 2; } }
    public bool IsDefeated { get; private set; }

    private PupcatController controller;
    private SpriteRenderer sprite;
    private float hurtEndTime;

    void Awake()
    {
        controller = GetComponent<PupcatController>();
        sprite = GetComponent<SpriteRenderer>();
        Points = MaxPoints;
    }

    void Update()
    {
        // Blink while shaking off a hit.
        if (sprite != null && !IsDefeated)
        {
            bool hurt = Time.time < hurtEndTime;
            sprite.enabled = !hurt || ((int)(Time.time * 25f) % 2 == 0);
        }
    }

    // Called by enemies. pushDirection is 1 to shove right, -1 to shove left.
    public void TakeDamage(int amount, float pushDirection)
    {
        if (IsDefeated || amount <= 0) return;
        if (Time.time < hurtEndTime) return; // still shaking off the last hit

        hurtEndTime = Time.time + hurtInvincibility;

        int remaining = amount;
        int absorbed = Mathf.Min(BonusPoints, remaining); // golden hearts soak damage first
        BonusPoints -= absorbed;
        remaining -= absorbed;
        Points = Mathf.Max(0, Points - remaining);

        controller.Knockback(new Vector2(pushDirection * knockbackSpeed, knockbackUp), knockbackTime);
        GameFeel.HitStop(0.08f);
        GameFeel.Shake(0.2f, 0.2f);
        GameFeel.Play(GameFeel.Sound.Hurt);
        Debug.Log("Pupcat took " + amount + " damage. Hearts: " + Points / 2f + " + bonus " + BonusPoints / 2f);

        if (Points <= 0) Defeat();
    }

    // Restores points. Anything over full becomes golden bonus hearts. Used by rewards later.
    public void Heal(int amount)
    {
        if (IsDefeated || amount <= 0) return;

        int toFill = Mathf.Min(amount, MaxPoints - Points);
        Points += toFill;

        int overflow = amount - toFill;
        BonusPoints = Mathf.Min(BonusPoints + overflow, maxBonusHearts * 2);
    }

    void Defeat()
    {
        IsDefeated = true;
        if (sprite != null) sprite.enabled = true;
        Debug.Log("Out of courage hearts. Going home.");
        GameFeel.Play(GameFeel.Sound.Defeat);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        controller.enabled = false;
        PupcatAttack attack = GetComponent<PupcatAttack>();
        if (attack != null) attack.enabled = false;

        Invoke(nameof(GoHome), goHomeDelay);
    }

    void GoHome()
    {
        // For now "going home" just restarts the scene. The home screen and route choice come later.
        DevTools.Restart();
    }

    // Draws the hearts in the top-left corner with simple boxes.
    void OnGUI()
    {
        float size = 36f;
        float gap = 8f;
        float x = 20f;
        float y = 20f;

        for (int i = 0; i < startingHearts; i++)
        {
            int fill = Mathf.Clamp(Points - i * 2, 0, 2);
            DrawHeart(x + i * (size + gap), y, size, fill, new Color(0.9f, 0.2f, 0.3f));
        }

        int bonusHearts = Mathf.CeilToInt(BonusPoints / 2f);
        for (int i = 0; i < bonusHearts; i++)
        {
            int fill = Mathf.Clamp(BonusPoints - i * 2, 0, 2);
            DrawHeart(x + (startingHearts + i) * (size + gap), y, size, fill, new Color(1f, 0.85f, 0.2f));
        }

        if (IsDefeated)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 24;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + size + 12f, 500f, 40f), "Out of hearts. Going home...", style);
        }
    }

    // fill: 0 = empty, 1 = half, 2 = full.
    void DrawHeart(float x, float y, float size, int fill, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);

        if (fill > 0)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, size * fill / 2f, size), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }
}