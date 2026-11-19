using UnityEngine;

// Hazard: rolling tires bounce across a lane from right to left.
// The object's position is where the tires start. Roll through them or jump over them.
// Select it in the Scene view to see the lane and wake-up zone.
public class BouncingTires : MonoBehaviour
{
    [Header("Path")]
    public float direction = -1f;     // -1 = the tires roll left, 1 = to the right
    public float distance = 24f;      // how far each tire travels from this object
    public Vector2 tireSize = new Vector2(1f, 1f);

    [Header("Tires")]
    public int tireCount = 4;
    public float tireSpacing = 3f;    // distance between tires in the stream
    public float bounceHeight = 1.4f;
    public float bounceDistance = 5f; // distance traveled per bounce
    public Color tireColor = new Color(0.08f, 0.08f, 0.1f);
    public Color rimColor = new Color(0.55f, 0.55f, 0.6f);

    [Header("Timing")]
    public float triggerRange = 8f;   // wakes up when the pupcat nears the lane's midpoint
    public float warningTime = 1f;
    public float speed = 12f;
    public float repeatDelay = 0f;    // pause before the tires can roll again. 0 = only once.

    [Header("Damage")]
    public int damage = 1;            // in points. 1 = half a heart.

    private enum Phase { Idle, Warning, Rolling, Cooldown, Done }
    private Phase phase = Phase.Idle;

    private PupcatController player;
    private PupcatHealth playerHealth;
    private Collider2D playerBody;
    private SpriteRenderer warning;
    private Transform[] tires;
    private float[] tireProgress;
    private bool[] tireActive;
    private bool[] hitThisTire;
    private float phaseEnd;
    private float elapsed;
    private int nextTire;
    private int activeTires;

    private static Sprite boxSprite;
    private static Sprite tireSprite;
    private static Sprite rimSprite;
    private static Sprite hubSprite;

    private float Dir { get { return direction >= 0f ? 1f : -1f; } }
    private float CenterX { get { return transform.position.x + Dir * distance / 2f; } }
    private float TireY { get { return transform.position.y + tireSize.y / 2f; } }

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

        warning = MakeBox("TireWarning", 3, new Color(1f, 0.55f, 0.1f, 0.3f));
        warning.transform.localPosition = new Vector3(Dir * distance / 2f, tireSize.y / 2f, 0f);
        warning.transform.localScale = new Vector3(distance, tireSize.y, 1f);
        warning.enabled = false;

        int count = Mathf.Max(1, tireCount);
        tires = new Transform[count];
        tireProgress = new float[count];
        tireActive = new bool[count];
        hitThisTire = new bool[count];
        for (int i = 0; i < count; i++)
        {
            tires[i] = BuildTire();
            tires[i].gameObject.SetActive(false);
        }
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.Idle:
                if (Mathf.Abs(player.transform.position.x - CenterX) <= triggerRange) StartWarning();
                break;

            case Phase.Warning:
                float pulse = 0.15f + 0.3f * Mathf.PingPong(Time.time * 4f, 1f);
                warning.color = new Color(1f, 0.55f, 0.1f, pulse);
                if (Time.time >= phaseEnd) StartRolling();
                break;

            case Phase.Rolling:
                UpdateTires();
                if (nextTire >= tires.Length && activeTires == 0) EndRolling();
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
        GameFeel.Play(GameFeel.Sound.BikeBell);
    }

    void StartRolling()
    {
        phase = Phase.Rolling;
        elapsed = 0f;
        nextTire = 0;
        activeTires = 0;
        warning.enabled = false;
        SpawnTire(nextTire++);
        GameFeel.Play(GameFeel.Sound.BikeRide);
    }

    void UpdateTires()
    {
        float deltaTime = Time.deltaTime;
        elapsed += deltaTime;
        float step = Mathf.Max(0f, speed) * deltaTime;
        float spawnInterval = Mathf.Max(0.1f, tireSpacing) / Mathf.Max(0.01f, speed);

        while (nextTire < tires.Length && elapsed >= nextTire * spawnInterval)
            SpawnTire(nextTire++);

        for (int i = 0; i < tires.Length; i++)
        {
            if (!tireActive[i]) continue;

            tireProgress[i] += step;
            if (tireProgress[i] >= distance)
            {
                tireActive[i] = false;
                tires[i].gameObject.SetActive(false);
                activeTires--;
                continue;
            }

            float bouncePeriod = Mathf.Max(0.1f, bounceDistance);
            float bounce = Mathf.Abs(Mathf.Sin(tireProgress[i] * Mathf.PI / bouncePeriod)) * bounceHeight;
            float x = transform.position.x + Dir * tireProgress[i];
            tires[i].position = new Vector3(x, TireY + bounce, transform.position.z);
            tires[i].Rotate(0f, 0f, -Dir * step / Mathf.Max(0.01f, tireSize.x / 2f) * Mathf.Rad2Deg);
            CheckHit(i);
        }
    }

    void SpawnTire(int index)
    {
        tireProgress[index] = 0f;
        hitThisTire[index] = false;
        tireActive[index] = true;
        activeTires++;
        tires[index].position = new Vector3(transform.position.x, TireY, transform.position.z);
        tires[index].rotation = Quaternion.identity;
        tires[index].gameObject.SetActive(true);
    }

    void EndRolling()
    {
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

    void CheckHit(int index)
    {
        if (hitThisTire[index] || playerBody == null || player.IsInvincible) return;

        Bounds p = playerBody.bounds;
        Vector3 c = tires[index].position;
        float halfW = tireSize.x / 2f;
        float halfH = tireSize.y / 2f;
        bool overlap = p.max.x > c.x - halfW && p.min.x < c.x + halfW
                    && p.max.y > c.y - halfH && p.min.y < c.y + halfH;

        if (overlap)
        {
            hitThisTire[index] = true;
            if (playerHealth != null) playerHealth.TakeDamage(damage, Dir);
            Debug.Log("Bouncing tire HIT the pupcat for " + damage + " point(s).");
        }
    }

    Transform BuildTire()
    {
        GameObject root = new GameObject("Tire");
        root.transform.SetParent(transform, false);

        AddDisc(root.transform, tireSize.x, tireColor, GetTireSprite(), 8);

        GameObject rim = new GameObject("Rim");
        rim.transform.SetParent(root.transform, false);
        rim.transform.localScale = new Vector3(tireSize.x * 0.72f, tireSize.x * 0.72f, 1f);
        SpriteRenderer rimRenderer = rim.AddComponent<SpriteRenderer>();
        rimRenderer.sprite = GetRimSprite();
        rimRenderer.color = rimColor;
        rimRenderer.sortingOrder = 9;

        for (int i = 0; i < 3; i++)
        {
            GameObject spoke = new GameObject("Spoke");
            spoke.transform.SetParent(root.transform, false);
            spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
            spoke.transform.localScale = new Vector3(tireSize.x * 0.68f, tireSize.x * 0.035f, 1f);
            SpriteRenderer spokeRenderer = spoke.AddComponent<SpriteRenderer>();
            spokeRenderer.sprite = GetBoxSprite();
            spokeRenderer.color = rimColor;
            spokeRenderer.sortingOrder = 10;
        }

        AddDisc(root.transform, tireSize.x * 0.16f, rimColor, GetHubSprite(), 11);
        return root.transform;
    }

    void AddDisc(Transform parent, float diameter, Color color, Sprite sprite, int order)
    {
        GameObject go = new GameObject("Wheel");
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(diameter, diameter, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
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

    static Sprite GetTireSprite()
    {
        if (tireSprite == null) tireSprite = MakeCircle(64, 0.58f);
        return tireSprite;
    }

    static Sprite GetRimSprite()
    {
        if (rimSprite == null) rimSprite = MakeCircle(64, 0.82f);
        return rimSprite;
    }

    static Sprite GetHubSprite()
    {
        if (hubSprite == null) hubSprite = MakeCircle(64, 0f);
        return hubSprite;
    }

    static Sprite MakeCircle(int size, float inner)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distanceFromCenter = Mathf.Sqrt(dx * dx + dy * dy) / (size / 2f);
                float alpha = Mathf.Clamp01((1f - distanceFromCenter) * size / 2f);
                if (inner > 0f) alpha *= Mathf.Clamp01((distanceFromCenter - inner) * size / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void OnDrawGizmosSelected()
    {
        float dir = direction >= 0f ? 1f : -1f;
        float centerX = transform.position.x + dir * distance / 2f;
        float centerY = transform.position.y + tireSize.y / 2f;

        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(centerX, centerY, 0f), new Vector3(distance, tireSize.y, 0f));

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(centerX, centerY, 0f), new Vector3(triggerRange * 2f, 6f, 0f));
    }
}
