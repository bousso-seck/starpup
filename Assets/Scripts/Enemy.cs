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
    private bool noticed;              // true once the pupcat got close enough to wake the enemy

    // The consequence rule (from the design doc):
    //  Rival:     parries up to hauntLine (40%) of their max health earn RESPECT (the best result).
    //             Past that, the parries count as a full attack. Dodging them tired = mildly upset.
    //  Possessed: dodging them tired leaves no mark. ANY parry counts as an attack (a mild haunt).
    //  Anyone:    a direct strike, or defeating them, is a full haunt.
    public enum Consequence { None, Respect, MildHaunt, FullHaunt }
    private float parryDamageTotal;    // health taken off by parries so far
    private bool struckDirectly;       // true once a real strike has landed
    private Transform parryBar;        // shows how close the parry total is to the haunt line

    public Consequence CurrentConsequence
    {
        get
        {
            if (struckDirectly || CurrentState == State.Defeated) return Consequence.FullHaunt;

            if (data.isPossessed)
            {
                // Dodging alone leaves no mark, but any parry counts as an attack.
                return parryDamageTotal > 0f ? Consequence.MildHaunt : Consequence.None;
            }

            // Rival: parries up to the haunt line earn respect, past it they count as a full attack.
            if (parryDamageTotal > data.maxHealth * data.hauntLine) return Consequence.FullHaunt;
            if (parries > 0) return Consequence.Respect;
            return Consequence.MildHaunt; // dodged until tired: it resolves, but they are mildly upset
        }
    }

    private float flashEndTime;        // white hit-flash runs until this time
    private float knockEndTime;        // brief shove after being struck
    private float staggerVelocity;     // sideways shove when parried
    private float staggerEnd;
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
        noticed = data.noticeRange <= 0f; // 0 means always awake

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
        parryBar = MakeBar("ParryBar", 1.26f, new Color(0.3f, 0.9f, 1f));
        UpdateBars();
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

        // Asleep until the pupcat comes within noticeRange. After that it stays awake.
        if (!noticed)
        {
            if (Mathf.Abs(dx) <= data.noticeRange)
            {
                noticed = true;
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }
        }

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
                // Parried: pushed back a little, then reeling in place until the stun runs out.
                float shoveLeft = Mathf.Clamp01((staggerEnd - Time.time) / Mathf.Max(0.01f, data.parryEnemyStagger));
                rb.linearVelocity = new Vector2(staggerVelocity * shoveLeft, rb.linearVelocity.y);
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
                    Debug.Log(data.characterName + ": OUTCOME = retreated (exhausted). Parries landed: " + parries + ". Consequence: " + CurrentConsequence);
                    gameObject.SetActive(false);
                }
                break;
        }
    }

    // In the Scene view, selecting the enemy draws a yellow box showing its notice range.
    void OnDrawGizmosSelected()
    {
        if (data == null || data.noticeRange <= 0f) return;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(data.noticeRange * 2f, 6f, 0f));
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
        GameFeel.Play(GameFeel.Sound.EnemyWindup); // the wind-up before a swing
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
        GameFeel.Play(GameFeel.Sound.EnemySwing);
        zoneHideTime = Time.time + 0.2f;

        if (parried)
        {
            // A parry counts as a clean dodge for stamina, it staggers the enemy, and because
            // parrying counts as attacking it also takes a little health.
            stamina -= whiffDrain;
            health -= data.parryDamage;
            parryDamageTotal += data.parryDamage;
            parries++;
            UpdateBars();
            float share = parryDamageTotal / data.maxHealth * 100f;
            Debug.Log(data.characterName + " was PARRIED (" + parries + " so far). Parry damage: " + Mathf.RoundToInt(share)
                + "% of max health (" + (data.isPossessed ? "possessed: any parry counts as an attack (mild haunt)" : "rival: respect up to " + Mathf.RoundToInt(data.hauntLine * 100f) + "%, then a full attack")
                + "). Stamina: " + Mathf.Max(stamina, 0f));
            GameFeel.HitStop(0.1f);
            GameFeel.Shake(0.1f, 0.15f);
            GameFeel.Play(GameFeel.Sound.Parry);

            // Both stagger. How far each one is pushed comes from the Enemy Data (parryPush...).
            float awayFromEnemy = (player.transform.position.x >= transform.position.x) ? 1f : -1f;
            player.Shove(awayFromEnemy * data.parryPushPupcat, data.parryPupcatStagger);
            staggerVelocity = -awayFromEnemy * data.parryPushEnemy;
            staggerEnd = Time.time + data.parryEnemyStagger;

            // Parried to zero health: that is a defeat, not a stagger.
            if (health <= 0f)
            {
                Defeat();
                return;
            }
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

            // A close dodge: the swing would have landed, but the pupcat was rolling. Slow motion.
            if (PlayerInZone() && player.IsInvincible)
            {
                GameFeel.SlowMo(0.45f, 0.3f);
            }
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
    // power scales the shove, freeze and shake (1 = normal strike, 2 = heavy strike or combo finisher).
    public void TakeHit(float amount, float power = 1f, GameFeel.Sound hitSound = GameFeel.Sound.Hit)
    {
        if (CurrentState == State.Defeated || CurrentState == State.Exhausted) return;

        health -= amount;
        noticed = true;        // getting hit wakes it up
        struckDirectly = true; // a direct strike is a full attack
        UpdateBars();

        // Make the hit feel solid: white flash, a small shove away, a tiny freeze, a camera bump.
        sprite.color = Color.white;
        flashEndTime = Time.time + HitFlashTime;
        float away = transform.position.x >= player.transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(away * HitKnockSpeed * power, rb.linearVelocity.y);
        knockEndTime = Time.time + HitKnockTime * Mathf.Max(1f, power);
        GameFeel.Play(hitSound); // each kind of strike brings its own hit sound
        GameFeel.HitStop(health <= 0f ? 0.14f : 0.06f * power);
        GameFeel.Shake(health <= 0f ? 0.18f : 0.08f * power, 0.12f);

        // A heavy strike or combo finisher knocks the enemy out of a wind-up, breaking its attack.
        if (power >= 2f && health > 0f && CurrentState == State.Telegraph)
        {
            swingsLeft = 0;
            swingZone.enabled = false;
            sprite.color = data.bodyColor;
            stateEndTime = knockEndTime + 0.3f;
            CurrentState = State.Recover;
        }

        if (health <= 0f) Defeat();
    }

    void Defeat()
    {
        GameFeel.Play(GameFeel.Sound.EnemyDefeat);

        flashEndTime = 0f;
        CurrentState = State.Defeated;
        swingZone.enabled = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        sprite.color = new Color(0.25f, 0.25f, 0.25f);

        // The spirit flame comes out of the body and flies off, sideways and up, out of the screen.
        if (data.leavesSpirit)
        {
            float side = (transform.position.x >= player.transform.position.x) ? 1f : -1f;
            SpiritRise.Spawn(data.spiritFlamePrefab, transform.position, side); // the middle of the body
        }
        Debug.Log(data.characterName + ": OUTCOME = defeated. Consequence: " + CurrentConsequence);
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

        // The thin bar shows the parry mark.
        //  Rival: cyan, fills toward the haunt line (40%). Past it, it turns red (a full attack).
        //  Possessed: any parry fills it orange at once (a mild haunt).
        float limit = Mathf.Max(data.maxHealth * data.hauntLine, 0.01f);
        float p;
        Color barColor;
        if (data.isPossessed)
        {
            p = (parryDamageTotal > 0f) ? 1f : 0f;
            barColor = new Color(1f, 0.6f, 0.2f);
        }
        else
        {
            p = Mathf.Clamp01(parryDamageTotal / limit);
            barColor = (parryDamageTotal > limit) ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.9f, 1f);
        }
        parryBar.localScale = new Vector3(BarWidth * p, BarHeight * 0.6f, 1f);
        parryBar.GetComponent<SpriteRenderer>().color = barColor;
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