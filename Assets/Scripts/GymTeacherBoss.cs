using UnityEngine;

// The Level 1 boss: the gym teacher. Put this on the boss object NEXT TO the Enemy script.
// The boss is a normal Enemy (same bars, parry, dodge and strikes) with two unique mechanics on top:
//
//  1. GRUDGE BOOST   He drinks in the grudge left behind by the classmates you faced earlier. When the fight
//                    starts he reads the hidden grudge tally (see Grudge.cs): the more you haunted the earlier
//                    enemies, the harder he hits, the faster he moves and the shorter his wind-ups are, and the
//                    redder the glow around him. A good run (respect, parries) leaves him at normal strength.
//                    At half health the grudge SURGES and he gets a little stronger again.
//
//  2. WHISTLE RUSH   He blows a whistle (a red lane shows where he will run), then charges in a straight line.
//                    Jump over him, roll through him (even to the other side, then he runs the wrong way), or
//                    parry him for a big stagger. A clean dodge drains his stamina. Every rush ends with him out
//                    of breath for a moment, which is your window to strike. Hitting him with a heavy strike or
//                    a combo finisher while he whistles breaks the rush. With a high grudge he rushes twice.
//
// If the scene has a BossArena, the gym teacher stands still until the arena starts the fight (see BossArena.cs).
// With no arena he simply wakes up when the pupcat comes close, like any other enemy.
// The Enemy Data for the boss should have "Records Grudge" switched ON: how this fight ends carries on to the final boss.
// (His own boost is locked in when the fight starts, so recording at the end never changes this fight.)
[RequireComponent(typeof(Enemy))]
public class GymTeacherBoss : MonoBehaviour
{
    [Header("Mechanic 1: Grudge boost (at full grudge)")]
    public float damageBonus = 1f;      // +100% damage on his swings
    public float speedBonus = 0.3f;     // +30% walking speed
    public float windupBonus = 0.25f;   // wind-ups 25% shorter
    public float surgeAt = 0.5f;        // at this share of his health the grudge surges
    public float surgeExtra = 0.25f;    // the surge adds this much to the boost (1 = full)
    public int testGrudge = -1;         // for testing: 0 to 9 forces the grudge. -1 = use the real tally.

    [Header("Mechanic 2: Whistle rush")]
    public float firstRushDelay = 3f;   // seconds after he wakes up before the first whistle
    public float rushCooldown = 7f;     // seconds between rushes (shorter with a high grudge)
    public float whistleTime = 0.9f;    // the warning before he runs
    public float rushSpeed = 14f;       // how fast he runs
    public float rushDistance = 10f;    // how far he runs at most
    public float rushDamage = 2f;       // points of damage if it lands (2 = one heart)
    public float windedTime = 1.6f;     // how long he is out of breath afterwards
    public float crashBonusTime = 0.6f; // extra time if he crashes into a wall

    private enum Phase { Idle, Whistle, Rush, Winded }
    private Phase phase = Phase.Idle;

    private Enemy enemy;
    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Collider2D body;
    private PupcatController player;
    private PupcatHealth playerHealth;
    private PupcatParry playerParry;
    private Collider2D playerBody;

    private float boost;            // 0 to 1: how strong the grudge makes him
    private bool fightStarted;
    private bool surged;
    private bool dormant;           // waiting for the arena to start the fight
    private float throwPoseEnd;     // until then he flashes (rearing back to throw the gate)
    private float flareEnd;
    private float baseDamage, baseSpeed, baseTelegraph, baseComboTelegraph;

    private float nextRush;
    private int rushesLeft;
    private float phaseEnd;
    private float rushDir = 1f;
    private float travelled;
    private float rushStartTime;
    private float nextTrail;
    private bool judged;            // the rush has already been judged against the pupcat
    private bool dodged;            // the pupcat avoided it (so his stamina drains)

    private SpriteRenderer aura;
    private SpriteRenderer lane;
    private static Sprite softSprite;
    private static Sprite leftBoxSprite;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();

        if (enemy.data == null)
        {
            Debug.LogError("GymTeacherBoss: the Enemy has no Data assigned.", this);
            enabled = false;
            return;
        }

        // Work on a private copy, so the boost never changes the saved Enemy Data file.
        enemy.data = Instantiate(enemy.data);
        baseDamage = enemy.data.attackDamage;
        baseSpeed = enemy.data.moveSpeed;
        baseTelegraph = enemy.data.telegraphTime;
        baseComboTelegraph = enemy.data.comboTelegraphTime;

        aura = MakeAura();
        lane = MakeLane();

        // With an arena in the scene he stays still until the arena starts the fight.
        if (FindFirstObjectByType<BossArena>() != null)
        {
            dormant = true;
            enemy.brainPaused = true;
        }
    }

    void OnEnable()
    {
        if (enemy != null) enemy.Hit += OnEnemyHit;
    }

    void OnDisable()
    {
        if (enemy != null)
        {
            enemy.Hit -= OnEnemyHit;
            enemy.brainPaused = false;
        }
    }

    void Start()
    {
        player = FindFirstObjectByType<PupcatController>();
        if (player == null)
        {
            Debug.LogError("GymTeacherBoss: no PupcatController found in the scene.", this);
            enabled = false;
            return;
        }
        playerBody = player.GetComponent<Collider2D>();
        playerHealth = player.GetComponent<PupcatHealth>();
        playerParry = player.GetComponent<PupcatParry>();
    }

    void Update()
    {
        if (player == null) return;

        UpdateAura();

        if (dormant)
        {
            // Standing still. Flash while he rears back to throw the gate.
            bool posing = Time.time < throwPoseEnd;
            sprite.color = posing
                ? (Mathf.FloorToInt(Time.time * 10f) % 2 == 0 ? new Color(1f, 0.6f, 0.2f) : Color.white)
                : enemy.data.bodyColor;
            return;
        }

        Enemy.State state = enemy.CurrentState;
        if (state == Enemy.State.Defeated || state == Enemy.State.Exhausted)
        {
            if (phase != Phase.Idle) EndEverything();
            return;
        }

        // Until the fight begins the boost follows the grudge tally. When he wakes up it is locked in.
        if (!fightStarted)
        {
            boost = CurrentBoost();
            if (!enemy.IsAwake) return;
            StartFight();
        }

        if (!surged && enemy.HealthFraction <= surgeAt) Surge();

        switch (phase)
        {
            case Phase.Idle:
                float dx = Mathf.Abs(player.transform.position.x - transform.position.x);
                if (Time.time >= nextRush && state == Enemy.State.Approach && dx <= rushDistance + 2f)
                {
                    rushesLeft = (boost >= 0.66f) ? 2 : 1;
                    StartWhistle(1f);
                }
                break;

            case Phase.Whistle:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                // Flash yellow and white, and pulse the red lane, so the warning is easy to see.
                bool on = Mathf.FloorToInt(Time.time * 12f) % 2 == 0;
                sprite.color = on ? new Color(1f, 1f, 0.3f) : Color.white;
                lane.color = new Color(1f, 0.2f, 0.2f, on ? 0.35f : 0.2f);
                if (Time.time >= phaseEnd) StartRush();
                break;

            case Phase.Rush:
                UpdateRush();
                break;

            case Phase.Winded:
                rb.linearVelocity = new Vector2(rb.linearVelocity.x * Mathf.Clamp01(1f - 8f * Time.deltaTime), rb.linearVelocity.y);
                if (Time.time >= phaseEnd)
                {
                    rushesLeft--;
                    if (rushesLeft > 0) StartWhistle(0.6f); // the second rush of a double rush: a shorter warning
                    else ReturnToIdle();
                }
                break;
        }
    }

    // ---------- Used by BossArena ----------

    // True once he has been beaten, or has walked away exhausted.
    public bool IsOver
    {
        get
        {
            return enemy.CurrentState == Enemy.State.Defeated || enemy.CurrentState == Enemy.State.Exhausted
                   || !gameObject.activeInHierarchy;
        }
    }

    public float HealthFraction { get { return enemy != null ? enemy.HealthFraction : 0f; } }

    // He rears back (flashing) for this long. The arena throws the gate during it.
    public void PlayThrowPose(float duration)
    {
        throwPoseEnd = Time.time + duration;
    }

    // The fight starts: he wakes up and goes for the pupcat.
    public void BeginFight()
    {
        dormant = false;
        enemy.brainPaused = false;
        enemy.Wake();
        sprite.color = enemy.data.bodyColor;
    }

    // ---------- Mechanic 1: grudge boost ----------

    float CurrentBoost()
    {
        int points = (testGrudge >= 0) ? Mathf.Min(testGrudge, Grudge.MaxPoints) : Grudge.Points;
        return Mathf.Clamp01(points / (float)Grudge.MaxPoints);
    }

    void StartFight()
    {
        fightStarted = true;
        ApplyBoost();
        nextRush = Time.time + firstRushDelay;
        int points = (testGrudge >= 0) ? testGrudge : Grudge.Points;
        Debug.Log("Gym Teacher wakes up. Grudge " + points + "/" + Grudge.MaxPoints + " = boost " + Mathf.RoundToInt(boost * 100f)
                  + "%: damage x" + (1f + damageBonus * boost).ToString("0.00") + ", speed x" + (1f + speedBonus * boost).ToString("0.00")
                  + ", wind-ups x" + (1f - windupBonus * boost).ToString("0.00") + (boost >= 0.66f ? ", double rush." : "."));
    }

    void ApplyBoost()
    {
        EnemyData d = enemy.data;
        d.attackDamage = baseDamage * (1f + damageBonus * boost);
        d.moveSpeed = baseSpeed * (1f + speedBonus * boost);
        d.telegraphTime = baseTelegraph * (1f - windupBonus * boost);
        d.comboTelegraphTime = baseComboTelegraph * (1f - windupBonus * boost);
    }

    // At half health the grudge boils over: a little stronger again, with a flare and a shake.
    void Surge()
    {
        surged = true;
        boost = Mathf.Clamp01(boost + surgeExtra);
        ApplyBoost();
        flareEnd = Time.time + 1f;
        GameFeel.Shake(0.2f, 0.4f);
        GameFeel.Play(GameFeel.Sound.BossSurge);
        Debug.Log("Gym Teacher: the grudge SURGES. Boost is now " + Mathf.RoundToInt(boost * 100f) + "%.");
    }

    // ---------- Mechanic 2: whistle rush ----------

    void StartWhistle(float timeScale)
    {
        phase = Phase.Whistle;
        phaseEnd = Time.time + Mathf.Lerp(whistleTime, whistleTime * 0.75f, boost) * timeScale;
        rushDir = (player.transform.position.x >= transform.position.x) ? 1f : -1f;
        enemy.brainPaused = true;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // The red lane shows exactly where he will run.
        float edge = body.bounds.extents.x;
        lane.transform.localPosition = new Vector3(edge * rushDir, 0f, 0f);
        lane.transform.localScale = new Vector3(rushDistance * rushDir, body.bounds.size.y, 1f);
        lane.enabled = true;

        GameFeel.Play(GameFeel.Sound.BossWhistle);
        GameFeel.Shake(0.05f, 0.2f);
        Debug.Log("Gym Teacher blows the whistle: a rush is coming to the " + (rushDir > 0f ? "right" : "left") + ".");
    }

    void StartRush()
    {
        phase = Phase.Rush;
        lane.enabled = false;
        travelled = 0f;
        rushStartTime = Time.time;
        nextTrail = 0f;
        judged = false;
        dodged = false;
        sprite.color = enemy.data.bodyColor;

        // If the pupcat is already behind him (rolled through during the whistle), he simply misses.
        float ahead = (player.transform.position.x - transform.position.x) * rushDir;
        if (ahead <= 0f)
        {
            judged = true;
            dodged = true;
        }

        GameFeel.Play(GameFeel.Sound.BossRush);
        GameFeel.Shake(0.08f, 0.15f);
    }

    void UpdateRush()
    {
        float speed = rushSpeed * (1f + 0.25f * boost);
        rb.linearVelocity = new Vector2(rushDir * speed, rb.linearVelocity.y);
        travelled += speed * Time.deltaTime;

        // Dust behind his feet while he runs.
        if (Time.time >= nextTrail)
        {
            nextTrail = Time.time + 0.1f;
            Vector3 feet = new Vector3(transform.position.x, body.bounds.min.y, 0f);
            if (!DashEffect.Play(feet, -rushDir, 1.6f, 0.8f)) GameFeel.Dust(feet, 3, -rushDir);
        }

        if (!judged)
        {
            if (body.bounds.Intersects(playerBody.bounds))
            {
                judged = true;
                Judge();
                if (phase != Phase.Rush) return;
            }
            else if ((player.transform.position.x - transform.position.x) * rushDir < 0f)
            {
                // He ran past without touching: the pupcat jumped over him or got out of the way.
                judged = true;
                dodged = true;
            }
        }

        bool stalled = Time.time - rushStartTime > 0.2f && Mathf.Abs(rb.linearVelocity.x) < 2f; // ran into a wall
        if (travelled >= rushDistance || stalled) EndRush(stalled);
    }

    // The rush touched the pupcat: rolled through, parried, or a clean hit.
    void Judge()
    {
        if (player.IsInvincible)
        {
            dodged = true;
            GameFeel.SlowMo(0.45f, 0.3f); // a close dodge
            return;
        }

        if (playerParry != null && playerParry.IsParrying)
        {
            bool exhausted = enemy.TakeParry(rushDir);
            if (exhausted || enemy.CurrentState == Enemy.State.Defeated)
            {
                EndEverything();
                return;
            }
            // Parried: shoved back and stunned. No second rush.
            phase = Phase.Winded;
            phaseEnd = Time.time + enemy.data.parryStunTime;
            rushesLeft = 0;
            rb.linearVelocity = new Vector2(-rushDir * enemy.data.parryPushEnemy, rb.linearVelocity.y);
            sprite.color = new Color(0.5f, 0.9f, 1f);
            Debug.Log("Gym Teacher's rush was PARRIED.");
            return;
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt(rushDamage * (1f + 0.5f * boost)));
        if (playerHealth != null) playerHealth.TakeDamage(damage, rushDir);
        GameFeel.Shake(0.2f, 0.2f);
        GameFeel.HitStop(0.05f);
        Debug.Log("Gym Teacher's rush HIT the pupcat for " + damage + " point(s).");
    }

    void EndRush(bool crashed)
    {
        if (dodged)
        {
            // A clean dodge drains his stamina, like a missed swing.
            bool exhausted = enemy.TakeWhiff(rushDir);
            Debug.Log("Gym Teacher's rush missed.");
            if (exhausted)
            {
                EndEverything();
                return;
            }
        }

        phase = Phase.Winded;
        phaseEnd = Time.time + windedTime * (rushesLeft > 1 ? 0.55f : 1f) + (crashed ? crashBonusTime : 0f);
        sprite.color = new Color(0.6f, 0.75f, 0.95f); // out of breath
        if (crashed) GameFeel.Shake(0.2f, 0.25f);
    }

    // A heavy strike or combo finisher while he whistles breaks the rush.
    void OnEnemyHit(float amount, float power)
    {
        if (phase != Phase.Whistle || power < 2f) return;
        lane.enabled = false;
        phase = Phase.Winded;
        phaseEnd = Time.time + 0.7f;
        rushesLeft = 0;
        sprite.color = new Color(0.6f, 0.75f, 0.95f);
        Debug.Log("Gym Teacher's whistle was broken by a heavy strike.");
    }

    void ReturnToIdle()
    {
        phase = Phase.Idle;
        enemy.brainPaused = false;
        sprite.color = enemy.data.bodyColor;
        nextRush = Time.time + Mathf.Lerp(rushCooldown, rushCooldown * 0.55f, boost);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    void EndEverything()
    {
        phase = Phase.Idle;
        lane.enabled = false;
        enemy.brainPaused = false;
    }

    // ---------- Looks ----------

    // A soft red glow behind him. The bigger and redder, the more grudge he carries.
    void UpdateAura()
    {
        if (aura == null) return;

        float b = fightStarted ? boost : CurrentBoost();
        bool alive = enemy.CurrentState != Enemy.State.Defeated;
        bool flaring = Time.time < flareEnd;
        float size = (2.2f + 1.8f * b) * (1f + Mathf.Sin(Time.time * 4f) * 0.06f) * (flaring ? 1.5f : 1f);
        float alpha = (b > 0f && alive) ? (0.15f + 0.45f * b) * (flaring ? 1.4f : 1f) : 0f;

        aura.transform.localScale = new Vector3(size, size, 1f);
        aura.color = new Color(1f, Mathf.Lerp(0.5f, 0.15f, b), 0.2f, Mathf.Clamp01(alpha));
    }

    SpriteRenderer MakeAura()
    {
        GameObject go = new GameObject("GrudgeAura");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSoftSprite();
        sr.sortingOrder = sprite.sortingOrder - 1;
        sr.color = new Color(1f, 0.3f, 0.2f, 0f);
        return sr;
    }

    SpriteRenderer MakeLane()
    {
        GameObject go = new GameObject("RushLane");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetLeftBoxSprite();
        sr.sortingOrder = 2;
        sr.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        sr.enabled = false;
        return sr;
    }

    // A 1x1 white pixel whose pivot is on the left, so it stretches out in front of the boss.
    static Sprite GetLeftBoxSprite()
    {
        if (leftBoxSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            leftBoxSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
        }
        return leftBoxSprite;
    }

    // A soft round glow, drawn from code.
    static Sprite GetSoftSprite()
    {
        if (softSprite == null)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            softSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size); // 1 world unit wide at scale 1
        }
        return softSprite;
    }
}