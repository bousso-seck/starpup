using UnityEngine;

// A classmate who THROWS things instead of swinging. Put this on the same object as the Enemy script.
//
// How the fight works:
//   1. When the pupcat gets close, the classmate winds up (flashes) for Wind Up Time.
//   2. Then it throws a burst of projectiles, each at a random height: LOW, MIDDLE or HIGH.
//   3. Heights: LOW (jump it), MIDDLE (roll or parry it), HIGH (crouch under it). Each is randomly CYAN (can be sent back) or RED (not parriable). Cyan chance = Parriable Chance.
//   4. CYAN  -> parry it and it flies back and hits the thrower (it counts as a parry on them).
//      RED   -> don't get hit: roll through it, jump a low one, or crouch under a high one.
//      A projectile that misses you wastes the thrower's stamina. Enough wasted throws = it gives up.
//   5. The usual hidden consequence rules apply (parries = respect, striking the thrower up close = haunt).
//
// Setup: make the classmate as usual (Enemy + Enemy Data), then add THIS script to the same object.
// You can leave Attack Range small in the Enemy Data: this script replaces the melee swings.
[RequireComponent(typeof(Enemy))]
public class ThrowerEnemy : MonoBehaviour
{
    [Header("When it fights")]
    public float noticeRange = 12f;      // starts throwing when the pupcat is this close
    public float keepDistance = 7f;      // tries to stay about this far away
    public float moveSpeed = 2.5f;       // how fast it shuffles to keep that distance (only between bursts)
    public float firstThrowDelay = 1f;   // pause after it notices the pupcat, before the first wind-up

    [Header("The throw")]
    public float windUpTime = 0.6f;      // flashes this long before each throw (the warning)
    public float recoverTime = 1.4f;     // rest between bursts
    public int throwsPerBurst = 2;       // how many projectiles per burst
    public float timeBetweenThrows = 0.45f;
    public float projectileSpeed = 8f;
    public float projectileSize = 0.2f;  // radius (keep it under about 0.2 so a crouch really clears a high one)
    public int damage = 1;               // points. 1 = half a heart.

    [Header("Parriable projectiles")]
    [Range(0f, 1f)] public float parriableChance = 0.4f;   // chance each projectile is CYAN (can be sent back)

    [Header("Heights (as a share of the pupcat's standing height)")]
    public float lowHeight = 0.12f;      // skims the floor: JUMP over it, or ROLL
    public float midHeight = 0.5f;       // body height: ROLL, or PARRY if cyan (crouching does NOT save you)
    public float highHeight = 0.95f;     // head height: CROUCH under it (it flies over a crouching pupcat), or ROLL
    [Range(0f, 1f)] public float lowChance = 0.35f;
    [Range(0f, 1f)] public float highChance = 0.3f;        // the rest is middle

    private enum Phase { Waiting, WindUp, Throwing, Recover, Done }
    private Phase phase = Phase.Waiting;

    private Enemy enemy;
    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private PupcatController player;
    private float phaseEnd;
    private int thrownInBurst;
    private float nextThrow;
    private bool pendingParriable;
    private float groundY;           // the floor level (the pupcat's feet)
    private float pupcatHeight = 1f; // the pupcat's standing height

    void Start()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        player = FindFirstObjectByType<PupcatController>();
        if (player == null)
        {
            Debug.LogError("No PupcatController found in the scene.", this);
            enabled = false;
            return;
        }
        enemy.brainPaused = true; // this script drives; the Enemy script handles health, stamina, parries and retreating
    }

    void Update()
    {
        if (enemy.CurrentState == Enemy.State.Defeated) { phase = Phase.Done; return; }
        if (enemy.CurrentState == Enemy.State.Exhausted) { phase = Phase.Done; return; } // Enemy walks it off screen now
        if (phase == Phase.Done) return;

        float dx = player.transform.position.x - transform.position.x;
        float dir = dx >= 0f ? 1f : -1f;
        if (sprite != null) sprite.flipX = dir < 0f; // remove this line if your sprite already faces the right way

        Collider2D pc = player.GetComponent<Collider2D>();
        if (pc != null)
        {
            groundY = pc.bounds.min.y;
            pupcatHeight = pc.bounds.size.y / (player.IsCrouching ? 0.65f : 1f); // 0.65 = the pupcat's Crouch Height
        }

        switch (phase)
        {
            case Phase.Waiting:
                Stand();
                if (Mathf.Abs(dx) <= noticeRange)
                {
                    enemy.Wake();
                    enemy.brainPaused = true;
                    phase = Phase.Recover;                       // a short beat before the first wind-up
                    phaseEnd = Time.time + firstThrowDelay;
                }
                break;

            case Phase.WindUp:
                Stand();              // planted: no moving while it winds up
                // Flash cyan if the next throw is parriable, red if not: the pupcat can read it BEFORE the throw.
                float flash = Mathf.PingPong(Time.time * 10f, 1f);
                Color tell = pendingParriable ? Projectile.ParriableColor : Projectile.DangerColor;
                if (sprite != null) sprite.color = Color.Lerp(enemy.data.bodyColor, tell, 0.35f + 0.4f * flash);
                if (Time.time >= phaseEnd)
                {
                    phase = Phase.Throwing;
                    thrownInBurst = 0;
                    nextThrow = Time.time;
                }
                break;

            case Phase.Throwing:
                Stand();              // planted: no moving while it throws
                if (Time.time >= nextThrow)
                {
                    Throw(dir);
                    thrownInBurst++;
                    nextThrow = Time.time + timeBetweenThrows;
                    pendingParriable = Random.value < parriableChance;
                    if (thrownInBurst >= throwsPerBurst)
                    {
                        if (sprite != null) sprite.color = enemy.data.bodyColor;
                        phase = Phase.Recover;
                        phaseEnd = Time.time + recoverTime;
                    }
                }
                break;

            case Phase.Recover:
                Shuffle(dx, dir);
                if (Time.time >= phaseEnd) StartWindUp();
                break;
        }
    }

    void StartWindUp()
    {
        phase = Phase.WindUp;
        phaseEnd = Time.time + windUpTime;
        pendingParriable = Random.value < parriableChance;
        GameFeel.Play(GameFeel.Sound.EnemyWindup);
    }

    void Throw(float dir)
    {
        float roll = Random.value;
        float h = roll < lowChance ? lowHeight : (roll < lowChance + highChance ? highHeight : midHeight);
        Vector3 from = new Vector3(transform.position.x + dir * 0.6f, groundY + h * pupcatHeight, 0f);

        // The first throw of a burst uses the colour the wind-up showed; later ones roll again.
        bool parriable = pendingParriable;
        Projectile.Spawn(this, from, dir, projectileSpeed, projectileSize, damage, parriable);
        GameFeel.Play(GameFeel.Sound.EnemyThrow);
    }

    // Planted on the spot (it still slides if the pupcat knocks it back, then settles).
    void Stand()
    {
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, 0f, 40f * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    // Slowly drifts toward keepDistance from the pupcat.
    void Shuffle(float dx, float dir)
    {
        float want = 0f;
        float dist = Mathf.Abs(dx);
        if (dist > keepDistance + 1f) want = dir * moveSpeed;        // too far: step closer
        else if (dist < keepDistance - 1f) want = -dir * moveSpeed;  // too close: back off
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, want, 40f * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(noticeRange * 2f, 6f, 0f));
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(keepDistance * 2f, 4f, 0f));
    }
}