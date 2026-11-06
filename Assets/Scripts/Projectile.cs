using UnityEngine;

// A thrown projectile (a crumpled paper ball, a dodgeball, whatever the classmate throws).
// Made by ThrowerEnemy: you never place this by hand.
//
//   CYAN  = can be sent back. PARRY it or STRIKE it and it flies back and hits the thrower (counts as a parry on them).
//   RED   = cannot be parried. Dodge it (roll through it, jump it, duck it).
//   A projectile that sails past you without hitting wastes the thrower's stamina, like a missed swing.
public class Projectile : MonoBehaviour
{
    public static readonly Color ParriableColor = new Color(0.3f, 0.9f, 1f);
    public static readonly Color DangerColor = new Color(0.95f, 0.25f, 0.25f);

    private ThrowerEnemy owner;
    private Enemy ownerEnemy;
    private Collider2D ownerBody;
    private PupcatController player;
    private PupcatHealth playerHealth;
    private PupcatParry playerParry;
    private Collider2D playerBody;

    private float dir;
    private float speed;
    private float radius;
    private int damage;
    private bool parriable;
    private bool reflected;
    private bool passedPlayer;
    private float lifeEnd;
    private SpriteRenderer sr;
    private float spin;

    private static Sprite discSprite;
    public static readonly System.Collections.Generic.List<Projectile> All = new System.Collections.Generic.List<Projectile>();

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public bool CanBeSentBack { get { return parriable && !reflected; } }

    // The pupcat's strike box. Any cyan projectile inside it is knocked back at the thrower.
    public static bool TryStrike(Vector2 center, Vector2 size, GameFeel.Sound sound)
    {
        bool any = false;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            Projectile p = All[i];
            if (p == null || !p.CanBeSentBack) continue;
            Vector3 c = p.transform.position;
            if (Mathf.Abs(c.x - center.x) <= size.x / 2f + p.radius && Mathf.Abs(c.y - center.y) <= size.y / 2f + p.radius)
            {
                p.Reflect(sound);
                any = true;
            }
        }
        return any;
    }

    public static Projectile Spawn(ThrowerEnemy owner, Vector3 position, float dir, float speed,
                                   float radius, int damage, bool parriable)
    {
        GameObject go = new GameObject("Projectile");
        go.transform.position = position;
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        Projectile p = go.AddComponent<Projectile>();
        p.owner = owner;
        p.ownerEnemy = owner.GetComponent<Enemy>();
        p.ownerBody = owner.GetComponent<Collider2D>();
        p.dir = dir;
        p.speed = speed;
        p.radius = radius;
        p.damage = damage;
        p.parriable = parriable;
        p.lifeEnd = Time.time + 6f;

        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sprite = GetDisc();
        p.sr.sortingOrder = 12;
        p.sr.color = parriable ? ParriableColor : DangerColor;

        // A dark dot inside, so the spin is visible.
        GameObject dot = new GameObject("Dot");
        dot.transform.SetParent(go.transform, false);
        dot.transform.localPosition = new Vector3(0.22f, 0.12f, 0f);
        dot.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        SpriteRenderer ds = dot.AddComponent<SpriteRenderer>();
        ds.sprite = GetDisc();
        ds.color = new Color(0f, 0f, 0f, 0.35f);
        ds.sortingOrder = 13;
        return p;
    }

    void Start()
    {
        player = FindFirstObjectByType<PupcatController>();
        if (player != null)
        {
            playerHealth = player.GetComponent<PupcatHealth>();
            playerParry = player.GetComponent<PupcatParry>();
            playerBody = player.GetComponent<Collider2D>();
        }
    }

    void Update()
    {
        transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
        spin += -dir * 720f * Time.deltaTime;
        transform.rotation = Quaternion.Euler(0f, 0f, spin);

        if (player == null || Time.time > lifeEnd) { Finish(false); return; }

        if (reflected)
        {
            CheckHitThrower();
            return;
        }

        if (Overlaps(playerBody))
        {
            if (player.IsInvincible) return; // rolling through it: it passes harmlessly

            if (parriable && playerParry != null && playerParry.IsParrying)
            {
                Reflect(GameFeel.Sound.Parry);
                return;
            }

            if (playerHealth != null) playerHealth.TakeDamage(damage, dir);
            Debug.Log("Projectile HIT the pupcat for " + damage + " point(s).");
            Finish(false);
            return;
        }

        // Past the pupcat without touching: it was dodged.
        float rel = (transform.position.x - player.transform.position.x) * dir;
        if (rel > 1.5f) passedPlayer = true;
        if (passedPlayer && rel > 6f) Finish(true);
    }

    // The parry sends it straight back.
    void Reflect(GameFeel.Sound sound)
    {
        reflected = true;
        dir = -dir;
        speed *= 1.4f;
        sr.color = Color.white;
        GameFeel.Play(sound);
        GameFeel.HitStop(0.06f);
        GameFeel.Shake(0.08f, 0.1f);
        Debug.Log("Projectile sent back at the thrower.");
    }

    void CheckHitThrower()
    {
        if (ownerBody == null || ownerEnemy == null || !ownerBody.gameObject.activeInHierarchy) { Finish(false); return; }
        if (ownerEnemy.CurrentState == Enemy.State.Defeated || ownerEnemy.CurrentState == Enemy.State.Exhausted) { Finish(false); return; }

        if (Overlaps(ownerBody))
        {
            ownerEnemy.TakeParry(-dir); // counts as a parry on the thrower: stamina, health, hidden parry total
            Finish(false);
        }
    }

    // A dodged projectile makes the thrower waste stamina, just like a missed swing.
    void Finish(bool dodged)
    {
        if (dodged && !reflected && ownerEnemy != null && ownerBody != null && ownerBody.gameObject.activeInHierarchy
            && ownerEnemy.CurrentState != Enemy.State.Defeated && ownerEnemy.CurrentState != Enemy.State.Exhausted)
        {
            ownerEnemy.TakeWhiff(dir);
            Debug.Log("Projectile dodged. The thrower wasted some stamina.");
        }
        Destroy(gameObject);
    }

    bool Overlaps(Collider2D body)
    {
        if (body == null) return false;
        Bounds b = body.bounds;
        Vector3 c = transform.position;
        return b.max.x > c.x - radius && b.min.x < c.x + radius
            && b.max.y > c.y - radius && b.min.y < c.y + radius;
    }

    static Sprite GetDisc()
    {
        if (discSprite == null)
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * size / 2f)));
                }
            tex.Apply();
            discSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return discSprite;
    }
}