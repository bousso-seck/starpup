using UnityEngine;

// Hazard 1: the speeding bike.
// A red warning lane lights up and pulses, then a bike zips across it.
// You avoid it by jumping over it or rolling through it (a roll is invincible).
// It can't be parried. If it hits you, it takes half a heart and shoves you along its path.
//
// Setup: GameObject > Create Empty, name it "Speeding Bike", add this script.
// The object's position is where the bike STARTS. Put its Y near the pupcat's Y.
// Select it in the Scene view to see the lane (red box) and the wake-up zone (yellow box).
public class SpeedingBike : MonoBehaviour
{
    [Header("Path")]
    public float direction = 1f;      // 1 = the bike rides to the right, -1 = to the left
    public float distance = 26f;      // how far it travels from this object
    public Vector2 bikeSize = new Vector2(1.6f, 0.9f);

    [Header("Timing")]
    public float triggerRange = 8f;   // wakes up when the pupcat is this close to the middle of the lane
    public float warningTime = 1f;    // how long the lane pulses before the bike goes (seconds)
    public float speed = 18f;         // how fast the bike rides
    public float repeatDelay = 4f;    // pause before it can go again. 0 = only once.

    [Header("Damage")]
    public int damage = 1;            // in points. 1 = half a heart.

    private enum Phase { Idle, Warning, Riding, Cooldown, Done }
    private Phase phase = Phase.Idle;

    private PupcatController player;
    private PupcatHealth playerHealth;
    private Collider2D playerBody;

    private SpriteRenderer warning;
    private SpriteRenderer bike;
    private float phaseEnd;
    private float traveled;
    private bool hitThisPass;

    private static Sprite boxSprite;

    float Dir { get { return direction >= 0f ? 1f : -1f; } }
    float CenterX { get { return transform.position.x + Dir * distance / 2f; } }

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

        warning = MakeBox("BikeWarning", 3, new Color(1f, 0.2f, 0.2f, 0.3f));
        warning.transform.localPosition = new Vector3(Dir * distance / 2f, 0f, 0f);
        warning.transform.localScale = new Vector3(distance, bikeSize.y, 1f);
        warning.enabled = false;

        bike = MakeBox("Bike", 8, new Color(0.15f, 0.7f, 0.9f));
        bike.transform.localScale = new Vector3(bikeSize.x, bikeSize.y, 1f);
        bike.enabled = false;

        // A small yellow "headlight" on the front so you can see which way it rides.
        GameObject light = new GameObject("Headlight");
        light.transform.SetParent(bike.transform, false);
        light.transform.localPosition = new Vector3(Dir * 0.42f, 0.1f, 0f);
        light.transform.localScale = new Vector3(0.14f, 0.3f, 1f);
        SpriteRenderer lr = light.AddComponent<SpriteRenderer>();
        lr.sprite = GetBoxSprite();
        lr.color = new Color(1f, 0.9f, 0.3f);
        lr.sortingOrder = 9;
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.Idle:
                if (Mathf.Abs(player.transform.position.x - CenterX) <= triggerRange) StartWarning();
                break;

            case Phase.Warning:
                // Pulse the lane so it reads as "danger, move!".
                float pulse = 0.15f + 0.3f * Mathf.PingPong(Time.time * 4f, 1f);
                warning.color = new Color(1f, 0.2f, 0.2f, pulse);
                if (Time.time >= phaseEnd) StartRide();
                break;

            case Phase.Riding:
                float step = speed * Time.deltaTime;
                traveled += step;
                bike.transform.position += new Vector3(Dir * step, 0f, 0f);
                CheckHit();
                if (traveled >= distance) EndRide();
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
        warning.enabled = true;
        GameFeel.Play(GameFeel.Sound.BikeBell); // the bike announces itself: the lane is live
    }

    void StartRide()
    {
        phase = Phase.Riding;
        traveled = 0f;
        hitThisPass = false;
        warning.enabled = false;
        bike.transform.position = transform.position;
        bike.enabled = true;
        GameFeel.Play(GameFeel.Sound.BikeRide); // whoosh as it takes off
    }

    void EndRide()
    {
        bike.enabled = false;
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

    // The bike hits if ANY part of the pupcat overlaps it. Rolling is invincible, and a jump clears it.
    void CheckHit()
    {
        if (hitThisPass || playerBody == null || player.IsInvincible) return;

        Bounds p = playerBody.bounds;
        Vector3 c = bike.transform.position;
        float halfW = bikeSize.x / 2f;
        float halfH = bikeSize.y / 2f;

        bool overlap = p.max.x > c.x - halfW && p.min.x < c.x + halfW
                    && p.max.y > c.y - halfH && p.min.y < c.y + halfH;

        if (overlap)
        {
            hitThisPass = true; // one hit per pass, so it can't chain you
            if (playerHealth != null) playerHealth.TakeDamage(damage, Dir);
            Debug.Log("Speeding bike HIT the pupcat for " + damage + " point(s).");
        }
    }

    SpriteRenderer MakeBox(string boxName, int order, Color color)
    {
        GameObject go = new GameObject(boxName);
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

    // In the Scene view, select the object to see the lane (red) and the wake-up zone (yellow).
    void OnDrawGizmosSelected()
    {
        float dir = direction >= 0f ? 1f : -1f;
        float centerX = transform.position.x + dir * distance / 2f;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(centerX, transform.position.y, 0f), new Vector3(distance, bikeSize.y, 0f));

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(centerX, transform.position.y, 0f), new Vector3(triggerRange * 2f, 6f, 0f));
    }
}