using UnityEngine;

// Hazard 2: the falling flowerpot.
// When the pupcat gets close, a shadow appears on the ground and grows while the pot trembles up high.
// Then the pot drops. Step out of the shadow, or roll through it (a roll is invincible).
// It can't be parried. If it lands on you it takes half a heart.
//
// Setup: GameObject > Create Empty, name it "Flowerpot", add this script.
// The object's position is where the pot LANDS (its center when it rests on the ground).
// Select it in the Scene view to see the landing box (red), the drop height (yellow line)
// and the wake-up zone (yellow box).
public class FallingFlowerpot : MonoBehaviour
{
    [Header("Pot")]
    public Vector2 potSize = new Vector2(0.8f, 0.8f);
    public float dropHeight = 7f;     // how high above the landing spot the pot hangs

    [Header("Timing")]
    public float triggerRange = 4f;   // wakes up when the pupcat is this close (sideways)
    public float warningTime = 0.9f;  // how long the shadow grows before the drop (seconds)
    public float fallGravity = 35f;   // how fast the pot speeds up while falling
    public float repeatDelay = 5f;    // pause before another pot can fall. 0 = only once.

    [Header("Damage")]
    public int damage = 1;            // in points. 1 = half a heart.

    private enum Phase { Idle, Warning, Falling, Cooldown, Done }
    private Phase phase = Phase.Idle;

    private PupcatController player;
    private PupcatHealth playerHealth;
    private Collider2D playerBody;

    private SpriteRenderer shadow;
    private SpriteRenderer pot;
    private float phaseEnd;
    private float potY;
    private float fallSpeed;
    private bool hitThisDrop;

    private static Sprite boxSprite;

    void Start()
    {
        player = FindFirstObjectByType<PupcatController>();
        if (player == null)
        {
            Debug.LogError("No PupcatController found in the scene.", this);
            enabled = false;
            return;
        }

        playerHealth = player.GetComponent<PupcatHealth>();
        playerBody = player.GetComponent<Collider2D>();

        shadow = MakeBox("PotShadow", 3, new Color(0f, 0f, 0f, 0.4f));
        shadow.enabled = false;

        pot = MakeBox("Pot", 8, new Color(0.8f, 0.4f, 0.2f));
        pot.transform.localScale = new Vector3(potSize.x, potSize.y, 1f);
        pot.enabled = false;

        // A little green plant on top, so it reads as a flowerpot.
        GameObject plant = new GameObject("Plant");
        plant.transform.SetParent(pot.transform, false);
        plant.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        plant.transform.localScale = new Vector3(0.7f, 0.4f, 1f);
        SpriteRenderer pr = plant.AddComponent<SpriteRenderer>();
        pr.sprite = GetBoxSprite();
        pr.color = new Color(0.25f, 0.7f, 0.3f);
        pr.sortingOrder = 9;
    }

    void Update()
    {
        float x = transform.position.x;
        float groundY = transform.position.y - potSize.y / 2f;

        switch (phase)
        {
            case Phase.Idle:
                if (Mathf.Abs(player.transform.position.x - x) <= triggerRange) StartWarning();
                break;

            case Phase.Warning:
                // The shadow grows and darkens as the drop gets closer. The pot trembles up high.
                float t = 1f - Mathf.Clamp01((phaseEnd - Time.time) / warningTime);
                float width = potSize.x * 1.4f * Mathf.Lerp(0.3f, 1f, t);
                shadow.transform.position = new Vector3(x, groundY + 0.05f, 0f);
                shadow.transform.localScale = new Vector3(width, 0.18f, 1f);
                shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.2f, 0.6f, t));

                float shake = Mathf.Sin(Time.time * 60f) * 0.04f;
                pot.transform.position = new Vector3(x + shake, transform.position.y + dropHeight, 0f);

                if (Time.time >= phaseEnd) StartFall();
                break;

            case Phase.Falling:
                fallSpeed += fallGravity * Time.deltaTime;
                potY -= fallSpeed * Time.deltaTime;
                pot.transform.position = new Vector3(x, potY, 0f);
                CheckHit();
                if (potY <= transform.position.y) Land();
                break;

            case Phase.Cooldown:
                if (Time.time >= phaseEnd) phase = Phase.Idle;
                break;
        }
    }

    void StartWarning()
    {
        phase = Phase.Warning;
        phaseEnd = Time.time + warningTime;
        shadow.enabled = true;
        pot.enabled = true;
        GameFeel.Play(GameFeel.Sound.Guard); // a tick to say a pot is about to fall
    }

    void StartFall()
    {
        phase = Phase.Falling;
        potY = transform.position.y + dropHeight;
        fallSpeed = 0f;
        hitThisDrop = false;
    }

    void Land()
    {
        pot.enabled = false;
        shadow.enabled = false;

        // Smash: dust, a thud and a small camera bump.
        Vector3 ground = new Vector3(transform.position.x, transform.position.y - potSize.y / 2f, 0f);
        GameFeel.Dust(ground, 7, 0f);
        GameFeel.Play(GameFeel.Sound.Land);
        GameFeel.Shake(0.08f, 0.12f);

        if (repeatDelay > 0f)
        {
            phase = Phase.Cooldown;
            phaseEnd = Time.time + repeatDelay;
        }
        else
        {
            phase = Phase.Done;
        }
    }

    // The pot hits if ANY part of the pupcat overlaps it while it falls.
    // Rolling is invincible, and stepping out of the shadow avoids it.
    void CheckHit()
    {
        if (hitThisDrop || playerBody == null || player.IsInvincible) return;

        Bounds p = playerBody.bounds;
        Vector3 c = pot.transform.position;
        float halfW = potSize.x / 2f;
        float halfH = potSize.y / 2f;

        bool overlap = p.max.x > c.x - halfW && p.min.x < c.x + halfW
                    && p.max.y > c.y - halfH && p.min.y < c.y + halfH;

        if (overlap)
        {
            hitThisDrop = true; // one hit per pot
            float push = (player.transform.position.x >= c.x) ? 1f : -1f;
            if (playerHealth != null) playerHealth.TakeDamage(damage, push);
            Debug.Log("Flowerpot HIT the pupcat for " + damage + " point(s).");
        }
    }

    SpriteRenderer MakeBox(string boxName, int order, Color color)
    {
        GameObject go = new GameObject(boxName);
        // Positions are set in world space below, so keep this object's Scale at 1.
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBoxSprite();
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    // A 1x1 white pixel, centered, that scales into any box.
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

    // In the Scene view, select the object to see where the pot lands and where it drops from.
    void OnDrawGizmosSelected()
    {
        Vector3 landing = transform.position;
        Vector3 top = landing + new Vector3(0f, dropHeight, 0f);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(landing, new Vector3(potSize.x, potSize.y, 0f));

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(landing, top);
        Gizmos.DrawWireCube(top, new Vector3(potSize.x, potSize.y, 0f));
        Gizmos.DrawWireCube(landing, new Vector3(triggerRange * 2f, 6f, 0f));
    }
}