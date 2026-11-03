using UnityEngine;

// The reusable enemy brain. Every classmate uses this same script,
// with a different EnemyData file plugged into the Data slot.
// Needs on the same object: SpriteRenderer, Collider2D, Rigidbody2D.
// Keep the object's Scale at 1 so the bars aren't squashed.
public class Enemy : MonoBehaviour
{
    public EnemyData data;

    public enum State { Approach, Telegraph, Recover, Stunned, Exhausted, Defeated }
    public State CurrentState { get; private set; }

    private float health;
    private float stamina;
    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private PupcatController player;

    private float stateEndTime;
    private float attackDir = 1f;
    private float retreatDir = 1f;

    // A see-through box that shows where the swing will land.
    private SpriteRenderer swingZone;
    private float zoneHideTime;

    private int swingsLeft; // swings remaining in the current combo
    private float whiffDrain; // stamina lost per missed swing (worked out in Start)

    private PupcatHealth playerHealth; // where a connecting swing takes its damage
    private PupcatParry playerParry;   // tells us if the guard is up when a swing lands
    private int parries;               // how many times this enemy has been parried

    private float flashEndTime;        // white hit-flash runs until this time
    private float knockEndTime;        // brief shove after being struck
    private const float HitFlashTime = 0.08f;
    private const float HitKnockSpeed = 5f;
    private const float HitKnockTime = 0.1f;
    private Collider2D playerBody;  // the pupcat's collider, used to see if the swing connects
    private float edge = 0.5f;      // how far the enemy's body reaches sideways from its center

    private Transform healthBar;
    private Transform staminaBar;
    private const float BarWidth = 1.2f;
    private const float BarHeight = 0.14f;

    private static Sprite barSprite;

    void Start()
    {
        if (data == null)
        {
            Debug.LogError("Enemy has no Data assigned. Drag an Enemy Data file into the Data slot.", this);
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        player = FindFirstObjectByType<PupcatController>();

        if (player == null)
        {
            Debug.LogError("No PupcatController found in the scene.", this);
            enabled = false;
            return;
        }

        health = data.maxHealth;
        stamina = data.maxStamina;

        // Work out how much stamina each miss drains, so beating this enemy by dodging takes
        // dodgeRatio times as many misses as it takes strikes. Nobody sets this by hand.
        PupcatAttack attack = FindFirstObjectByType<PupcatAttack>();
        float strikeDamage = (attack != null && attack.damage > 0f) ? attack.damage : 10f;
        float strikesToWin = data.maxHealth / strikeDamage;
        float dodgesToWin = strikesToWin * Mathf.Max(data.dodgeRatio, 0.1f);
        whiffDrain = data.maxStamina / dodgesToWin;
        sprite.color = data.bodyColor;

        // Let the pupcat roll straight through the enemy.
        Collider2D mine = GetComponent<Collider2D>();
        playerBody = player.GetComponent<Collider2D>();
        playerHealth = player.GetComponent<PupcatHealth>();
        playerParry = player.GetComponent<PupcatParry>();
        if (mine != null && playerBody != null) Physics2D.IgnoreCollision(mine, playerBody);
        if (mine != null) edge = mine.bounds.extents.x;

        healthBar = MakeBar("HealthBar", 0.9f, new Color(0.9f, 0.2f, 0.2f));
        staminaBar = MakeBar("StaminaBar", 1.08f, new Color(1f, 0.85f, 0.2f));
        swingZone = MakeSwingZone();

        CurrentState = State.Approach;
    }

    void Update()
    {
        if (CurrentState == State.Defeated) return;

        float dx = player.transform.position.x - transform.position.x;
        float dirToPlayer = dx >= 0f ? 1f : -1f;

        // Hide the red swing flash a moment after the swing lands.
        if (swingZone.enabled && CurrentState != State.Telegraph && Time.time >= zoneHideTime)
        {
            swingZone.enabled = false;
        }

        // End the white hit-flash, going back to the color this state should have.
        if (flashEndTime > 0f && Time.time >= flashEndTime)
        {
            flashEndTime = 0f;
            sprite.color = StateColor();
        }

        // While being shoved by a strike, the enemy can't act. Hits briefly interrupt it.
        if (Time.time < knockEndTime) return;

        switch (CurrentState)
        {
            case State.Approach:
                if (Mathf.Abs(dx) > data.attackRange)
                {
                    rb.linearVelocity = new Vector2(dirToPlayer * data.moveSpeed, rb.linearVelocity.y);
                }
                else
                {
                    swingsLeft = Mathf.Max(1, data.swingsPerAttack);
                    StartTelegraph(dirToPlayer, data.telegraphTime);
                }
                break;

            case State.Telegraph:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (Time.time >= stateEndTime) DoSwing();
                break;

            case State.Recover:
                // Keep creeping toward the player so there's no free breathing room.
                float creep = (Mathf.Abs(dx) > 1.2f) ? dirToPlayer * data.moveSpeed * data.recoverChase : 0f;
                rb.linearVelocity = new Vector2(creep, rb.linearVelocity.y);
                if (Time.time >= stateEndTime)
                {
                    if (swingsLeft > 0)
                    {
                        // More swings in this combo: wind up again, faster, facing the player.
                        StartTelegraph(dirToPlayer, data.comboTelegraphTime);
                    }
                    else
                    {
                        sprite.color = data.bodyColor;
                        CurrentState = State.Approach;
                    }
                }
                break;

            case State.Stunned:
                // Parried: reeling in place until the stun runs out.
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (Time.time >= stateEndTime)
                {
                    sprite.color = data.bodyColor;
                    CurrentState = State.Approach;
                }
                break;

            case State.Exhausted:
                rb.linearVelocity = new Vector2(retreatDir * data.moveSpeed, rb.linearVelocity.y);
                if (Time.time >= stateEndTime)
                {
                    Debug.Log(data.characterName + ": OUTCOME = retreated (exhausted). Parries landed: " + parries);
                    gameObject.SetActive(false);
                }
                break;
        }
    }

    Color StateColor()
    {
        switch (CurrentState)
        {
            case State.Telegraph: return new Color(1f, 1f, 0.3f);
            case State.Stunned: return new Color(0.5f, 0.9f, 1f);
            case State.Exhausted: return new Color(0.6f, 0.6f, 0.6f);
            case State.Defeated: return new Color(0.25f, 0.25f, 0.25f);
            default: return data.bodyColor;
        }
    }

    void StartTelegraph(float dir, float windUp)
    {
        attackDir = dir;
        stateEndTime = Time.time + windUp;
        sprite.color = new Color(1f, 1f, 0.3f); // yellow = a swing is coming
        ShowZone(new Color(1f, 0.9f, 0.2f, 0.3f)); // faint yellow box = the danger zone
        CurrentState = State.Telegraph;
    }

    void DoSwing()
    {
        // The swing connects if ANY part of the pupcat's body is inside the danger zone.
        // Stepping back but staying partly inside the box still gets you hit.
        bool inZone = PlayerInZone() && !player.IsInvincible;

        // A swing that would land is parried if the guard is up and this enemy allows it.
        bool parried = inZone && data.canBeParried && playerParry != null && playerParry.IsParrying;
        bool hit = inZone && !parried;

        // Flash the danger zone red so the swing is visible.
        ShowZone(new Color(1f, 0.2f, 0.2f, 0.65f));
        zoneHideTime = Time.time + 0.2f;

        if (parried)
        {
            // A parry counts as a clean dodge for stamina, and it also staggers the enemy.
            stamina -= whiffDrain;
            parries++;
            UpdateBars();
            Debug.Log(data.characterName + " was PARRIED (" + parries + " so far). Stamina: " + Mathf.Max(stamina, 0f));
            GameFeel.HitStop(0.1f);
            GameFeel.Shake(0.1f, 0.15f);
        }
        else if (hit)
        {
            // A full attack (all swings) deals attackDamage, so each swing deals its share.
            float swingDamage = data.attackDamage / Mathf.Max(1, data.swingsPerAttack);
            if (playerHealth != null)
            {
                // Damage is in whole points (2 points = 1 heart), never less than 1.
                playerHealth.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(swingDamage)), attackDir);
            }
            Debug.Log(data.characterName + " HIT the pupcat for " + swingDamage + " of " + data.attackDamage + " (enemy bars unchanged)");
        }
        else
        {
            // A clean miss drains the enemy's stamina. This is the "dodge" path to winning.
            // Each swing is judged on its own, so a hit on one swing never cancels a dodge on another.
            stamina -= whiffDrain;
            UpdateBars();
            string why = player.IsInvincible ? "pupcat was rolling" : "pupcat was outside the red box";
            Debug.Log(data.characterName + " whiffed (" + why + "). Stamina: " + Mathf.Max(stamina, 0f));
        }

        if (stamina <= 0.01f) // tiny margin so rounding can't cost an extra dodge
        {
            retreatDir = -attackDir;
            stateEndTime = Time.time + data.exhaustedTime;
            sprite.color = new Color(0.6f, 0.6f, 0.6f); // grey = embarrassed
            CurrentState = State.Exhausted;
        }
        else if (parried)
        {
            // Combo broken: no more swings, just a stagger.
            swingsLeft = 0;
            stateEndTime = Time.time + data.parryStunTime;
            sprite.color = new Color(0.5f, 0.9f, 1f); // light blue = staggered
            CurrentState = State.Stunned;
        }
        else
        {
            swingsLeft--;
            // More swings in this combo? Pause only briefly before the next wind-up.
            stateEndTime = Time.time + (swingsLeft > 0 ? data.comboGap : data.recoverTime);
            sprite.color = data.bodyColor;
            CurrentState = State.Recover;
        }
    }

    // Called by the pupcat's attack. Drains the health bar.
    public void TakeHit(float amount)
    {
        if (CurrentState == State.Defeated || CurrentState == State.Exhausted) return;

        health -= amount;
        UpdateBars();

        // Make the hit feel solid: white flash, a small shove away, a tiny freeze, a camera bump.
        sprite.color = Color.white;
        flashEndTime = Time.time + HitFlashTime;
        float away = transform.position.x >= player.transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(away * HitKnockSpeed, rb.linearVelocity.y);
        knockEndTime = Time.time + HitKnockTime;
        GameFeel.HitStop(health <= 0f ? 0.14f : 0.06f);
        GameFeel.Shake(health <= 0f ? 0.18f : 0.08f, 0.12f);

        if (health <= 0f)
        {
            flashEndTime = 0f;
            CurrentState = State.Defeated;
            swingZone.enabled = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            sprite.color = new Color(0.25f, 0.25f, 0.25f);
            Debug.Log(data.characterName + ": OUTCOME = defeated");
        }
    }

    // Builds the (hidden) danger-zone box that shows where a swing reaches.
    SpriteRenderer MakeSwingZone()
    {
        GameObject go = new GameObject("SwingZone");
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBarSprite();
        sr.sortingOrder = 5;
        sr.enabled = false;
        return sr;
    }

    // True if the pupcat's body overlaps the danger zone (the same box that gets drawn).
    bool PlayerInZone()
    {
        if (playerBody == null) return false;

        Bounds p = playerBody.bounds;
        float start = transform.position.x + attackDir * edge;
        float end = transform.position.x + attackDir * data.hitRange;
        float xMin = Mathf.Min(start, end);
        float xMax = Mathf.Max(start, end);
        float yMin = transform.position.y - data.hitHeight;
        float yMax = transform.position.y + data.hitHeight;

        return p.max.x > xMin && p.min.x < xMax && p.max.y > yMin && p.min.y < yMax;
    }

    // Shows the danger zone in front of the enemy, from its edge out to hitRange.
    void ShowZone(Color color)
    {
        float length = Mathf.Max(data.hitRange - edge, 0.1f);
        swingZone.transform.localPosition = new Vector3(edge * attackDir, 0f, 0f);
        swingZone.transform.localScale = new Vector3(length * attackDir, data.hitHeight * 2f, 1f);
        swingZone.color = color;
        swingZone.enabled = true;
    }

    void UpdateBars()
    {
        float h = Mathf.Clamp01(health / data.maxHealth);
        float s = Mathf.Clamp01(stamina / data.maxStamina);
        healthBar.localScale = new Vector3(BarWidth * h, BarHeight, 1f);
        staminaBar.localScale = new Vector3(BarWidth * s, BarHeight, 1f);
    }

    // Builds a dark back strip and a colored fill strip above the enemy.
    Transform MakeBar(string barName, float y, Color color)
    {
        MakeQuad(barName + "Back", y, new Color(0f, 0f, 0f, 0.6f), 10);
        return MakeQuad(barName, y, color, 11);
    }

    Transform MakeQuad(string quadName, float y, Color color, int order)
    {
        GameObject go = new GameObject(quadName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(-BarWidth / 2f, y, 0f);
        go.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBarSprite();
        sr.color = color;
        sr.sortingOrder = order;
        return go.transform;
    }

    // A 1x1 white pixel that stretches into any bar. The pivot is on the left,
    // so shrinking the scale makes the bar empty from the right side.
    static Sprite GetBarSprite()
    {
        if (barSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            barSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
        }
        return barSprite;
    }
}