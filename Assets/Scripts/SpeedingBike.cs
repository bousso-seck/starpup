using UnityEngine;

// Hazard 1: the speeding bike.
// A red warning lane lights up and pulses, then a BICYCLE zips across it (drawn from code: spinning wheels,
// a frame, a pedalling rider and a headlight). Drop your own picture into Bike Sprite to replace it.
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
    public Vector2 bikeSize = new Vector2(1.6f, 0.9f);   // the HIT BOX (the drawing fits inside it)

    [Header("Look")]
    public Sprite bikeSprite;         // optional: your own bike picture (drawn facing RIGHT). Empty = the bicycle drawn from code.
    public bool showRider = true;     // the kid on the bike
    public Color frameColor = new Color(0.15f, 0.7f, 0.9f);
    public Color riderColor = new Color(0.95f, 0.75f, 0.2f);

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
    private Transform bike;           // the whole bicycle (a parent of the wheels, frame and rider)
    private Transform wheelBack, wheelFront;
    private Transform legA, legB, torso, head;
    private float crank;              // pedal angle
    private const float WheelR = 0.28f;
    private static readonly Vector2 Back = new Vector2(-0.5f, -0.17f);
    private static readonly Vector2 Front = new Vector2(0.5f, -0.17f);
    private static readonly Vector2 Bracket = new Vector2(-0.02f, -0.17f);
    private static readonly Vector2 SeatTop = new Vector2(-0.2f, 0.16f);
    private static readonly Vector2 HeadTop = new Vector2(0.34f, 0.2f);
    private float phaseEnd;
    private float traveled;
    private bool hitThisPass;

    private static Sprite boxSprite;
    private static Sprite ringSprite;

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

        BuildBike();
        bike.gameObject.SetActive(false);
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
                bike.position += new Vector3(Dir * step, 0f, 0f);
                Animate(step);
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
        bike.position = transform.position;
        bike.gameObject.SetActive(true);
        GameFeel.Play(GameFeel.Sound.BikeRide); // whoosh as it takes off
    }

    void EndRide()
    {
        bike.gameObject.SetActive(false);
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
        Vector3 c = bike.position;
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

    // ---------- The bicycle, drawn from code ----------

    void BuildBike()
    {
        GameObject root = new GameObject("Bike");
        root.transform.SetParent(transform, false);
        root.transform.localScale = new Vector3(Dir, 1f, 1f); // drawn facing right; flipped when it rides left
        bike = root.transform;

        if (bikeSprite != null)
        {
            SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = bikeSprite;
            sr.sortingOrder = 8;
            return;
        }

        Color dark = new Color(0.1f, 0.1f, 0.12f);

        // The frame: a classic diamond.
        Bar(root.transform, Back, Bracket, 0.05f, frameColor, 8);
        Bar(root.transform, Back, SeatTop, 0.045f, frameColor, 8);
        Bar(root.transform, Bracket, SeatTop, 0.05f, frameColor, 8);
        Bar(root.transform, Bracket, HeadTop, 0.055f, frameColor, 8);
        Bar(root.transform, SeatTop, HeadTop, 0.05f, frameColor, 8);
        Bar(root.transform, HeadTop, Front, 0.045f, frameColor, 8);                       // the fork
        Bar(root.transform, SeatTop, SeatTop + new Vector2(0f, 0.07f), 0.04f, dark, 9);   // seat post
        Bar(root.transform, SeatTop + new Vector2(-0.1f, 0.08f), SeatTop + new Vector2(0.08f, 0.08f), 0.07f, dark, 9); // seat
        Bar(root.transform, HeadTop, HeadTop + new Vector2(0.06f, 0.18f), 0.04f, dark, 9);   // stem
        Bar(root.transform, HeadTop + new Vector2(0.06f, 0.18f), HeadTop + new Vector2(0.22f, 0.15f), 0.05f, dark, 9); // handlebar

        wheelBack = MakeWheel(root.transform, Back);
        wheelFront = MakeWheel(root.transform, Front);

        // The kid riding it.
        if (showRider)
        {
            Vector2 hip = SeatTop + new Vector2(-0.02f, 0.14f);
            Vector2 shoulder = hip + new Vector2(0.2f, 0.28f);
            torso = Bar(root.transform, hip, shoulder, 0.17f, riderColor, 10);
            head = Disc(root.transform, shoulder + new Vector2(0.06f, 0.14f), 0.2f, new Color(0.95f, 0.8f, 0.65f), 11);
            Bar(root.transform, shoulder, HeadTop + new Vector2(0.14f, 0.16f), 0.07f, riderColor, 10); // arm to the handlebar
            legA = Bar(root.transform, hip, Bracket, 0.08f, new Color(0.2f, 0.25f, 0.5f), 10);
            legB = Bar(root.transform, hip, Bracket, 0.08f, new Color(0.15f, 0.2f, 0.4f), 7);
        }

        // The headlight, at the front of the handlebar.
        Disc(root.transform, HeadTop + new Vector2(0.26f, 0.12f), 0.1f, new Color(1f, 0.92f, 0.35f), 11);
    }

    // Spins the wheels and pedals. step = how far the bike moved this frame.
    void Animate(float step)
    {
        float spin = -step / WheelR * Mathf.Rad2Deg;  // rolling forward = turning clockwise
        if (wheelBack != null) wheelBack.Rotate(0f, 0f, spin);
        if (wheelFront != null) wheelFront.Rotate(0f, 0f, spin);

        if (legA == null) return;
        crank += step * 5f;                           // pedals turn a little faster than the wheels
        Vector2 hip = SeatTop + new Vector2(-0.02f, 0.14f);
        Vector2 footA = Bracket + 0.14f * new Vector2(Mathf.Cos(-crank), Mathf.Sin(-crank));
        Vector2 footB = Bracket - 0.14f * new Vector2(Mathf.Cos(-crank), Mathf.Sin(-crank));
        Place(legA, hip, footA, 0.08f);
        Place(legB, hip, footB, 0.08f);
    }

    Transform MakeWheel(Transform parent, Vector2 pos)
    {
        GameObject w = new GameObject("Wheel");
        w.transform.SetParent(parent, false);
        w.transform.localPosition = new Vector3(pos.x, pos.y, 0f);

        GameObject tire = new GameObject("Tire");
        tire.transform.SetParent(w.transform, false);
        tire.transform.localScale = new Vector3(WheelR * 2f, WheelR * 2f, 1f);
        SpriteRenderer sr = tire.AddComponent<SpriteRenderer>();
        sr.sprite = GetRingSprite();
        sr.color = new Color(0.08f, 0.08f, 0.1f);
        sr.sortingOrder = 8;

        // Spokes, so you can see it spin.
        for (int i = 0; i < 3; i++)
        {
            GameObject sp = new GameObject("Spoke");
            sp.transform.SetParent(w.transform, false);
            sp.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
            sp.transform.localScale = new Vector3(WheelR * 1.8f, 0.02f, 1f);
            SpriteRenderer ss = sp.AddComponent<SpriteRenderer>();
            ss.sprite = GetBoxSprite();
            ss.color = new Color(0.85f, 0.85f, 0.9f);
            ss.sortingOrder = 8;
        }
        return w.transform;
    }

    // A thin box from point a to point b.
    Transform Bar(Transform parent, Vector2 a, Vector2 b, float thickness, Color color, int order)
    {
        GameObject go = new GameObject("BikePart");
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBoxSprite();
        sr.color = color;
        sr.sortingOrder = order;
        Place(go.transform, a, b, thickness);
        return go.transform;
    }

    static void Place(Transform t, Vector2 a, Vector2 b, float thickness)
    {
        Vector2 d = b - a;
        t.localPosition = new Vector3((a.x + b.x) / 2f, (a.y + b.y) / 2f, 0f);
        t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(d.magnitude, thickness, 1f);
    }

    Transform Disc(Transform parent, Vector2 pos, float diameter, Color color, int order)
    {
        GameObject go = new GameObject("BikeDisc");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(diameter, diameter, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDiscSprite();
        sr.color = color;
        sr.sortingOrder = order;
        return go.transform;
    }

    static Sprite GetRingSprite()
    {
        if (ringSprite == null) ringSprite = MakeCircle(64, 0.78f);
        return ringSprite;
    }

    static Sprite discSprite;
    static Sprite GetDiscSprite()
    {
        if (discSprite == null) discSprite = MakeCircle(64, 0f);
        return discSprite;
    }

    // A circle (inner > 0 makes a ring), 1 unit wide, centered.
    static Sprite MakeCircle(int size, float inner)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size / 2f);
                float a = Mathf.Clamp01((1f - d) * size / 2f);                    // soft outer edge
                if (inner > 0f) a *= Mathf.Clamp01((d - inner) * size / 2f);       // soft inner edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
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