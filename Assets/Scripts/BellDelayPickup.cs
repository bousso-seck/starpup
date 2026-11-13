using UnityEngine;

// Power-up: Bell Delay. A small golden bell floats on the street. Touch it and the school bell timer gets
// extra seconds, with a "+15s" that rises up and fades.
//
// SETUP: GameObject > Create Empty, name it "Bell Delay", add this script, and drag it where you want it
// (on the street, about at the pupcat's chest height). Nothing else is needed: it draws its own bell, adds its
// own trigger and finds the Bell Timer by itself. Copy and paste it (Ctrl+D) to place more of them.
// To use your own art, put a SpriteRenderer on it with your sprite before pressing Play.
public class BellDelayPickup : MonoBehaviour
{
    public float secondsAdded = 15f;   // time given to the bell timer
    public float pickupRadius = 0.8f;  // how close the pupcat has to get
    public float size = 1.2f;          // size of the drawn bell (ignored when you use your own sprite)
    public float bobHeight = 0.25f;    // how far it floats up and down
    public float bobSpeed = 2f;

    private bool taken;
    private bool drawnBell;
    private Vector3 startPos;
    private Vector3 baseScale;
    private static Sprite bellSprite;

    void Awake()
    {
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null) circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = pickupRadius;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            sr.sprite = GetBellSprite();
            transform.localScale = new Vector3(size, size, 1f);
            drawnBell = true;
        }
        sr.sortingOrder = 5;

        startPos = transform.position;
        baseScale = transform.localScale;
    }

    void Update()
    {
        // Float up and down, with a tiny pulse so it catches the eye.
        float t = Time.time * bobSpeed;
        transform.position = startPos + new Vector3(0f, Mathf.Sin(t) * bobHeight, 0f);
        float pulse = 1f + Mathf.Sin(t * 2f) * 0.04f;
        transform.localScale = baseScale * pulse;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (taken) return;
        if (other.GetComponentInParent<PupcatController>() == null) return;

        BellTimer bell = FindFirstObjectByType<BellTimer>();
        if (bell == null)
        {
            Debug.LogWarning("Bell Delay: there is no Bell Timer in this scene.");
            return;
        }

        taken = true;
        bell.remainingTime += secondsAdded;
        GameFeel.Play(GameFeel.Sound.Parry); // a bright ping, same as the level goal
        FloatingText.Spawn(transform.position + Vector3.up * 0.6f, "+" + Mathf.RoundToInt(secondsAdded) + "s");
        Debug.Log("Bell Delay picked up: +" + secondsAdded + "s. Time left: " + Mathf.CeilToInt(bell.remainingTime) + "s");
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    // ---------- the bell, drawn from code ----------

    static Sprite GetBellSprite()
    {
        if (bellSprite != null) return bellSprite;

        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color gold = new Color(1f, 0.82f, 0.2f, 1f);
        Color shade = new Color(0.82f, 0.6f, 0.1f, 1f);
        Color shine = new Color(1f, 0.95f, 0.65f, 1f);

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x - (n - 1) / 2f) / (n / 2f); // -1 to 1 across
                float v = y / (float)n;                  // 0 bottom to 1 top
                Color c = clear;

                // Dome: narrow at the top, wide at the bottom. A rim sits at the very bottom.
                if (v >= 0.14f && v <= 0.85f)
                {
                    float half = (v < 0.2f) ? 0.95f : 0.9f * Mathf.Sqrt(Mathf.Clamp01((0.85f - v) / 0.65f));
                    if (Mathf.Abs(u) < half)
                    {
                        c = gold;
                        if (u > 0.3f) c = shade;                                // shadow on the right
                        if (u < -0.35f && u > -0.55f && v > 0.3f && v < 0.7f) c = shine; // shine on the left
                    }
                }

                // Knob on top.
                float kx = u, ky = (v - 0.9f) * 2f;
                if (kx * kx + ky * ky < 0.07f * 0.07f * 4f) c = gold;

                // Clapper under the rim.
                float cx = u, cy = (v - 0.08f) * 2f;
                if (cx * cx + cy * cy < 0.09f * 0.09f * 4f) c = shade;

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        bellSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
        return bellSprite;
    }

    // ---------- the "+15s" that rises and fades ----------

    class FloatingText : MonoBehaviour
    {
        string text;
        Vector3 world;
        float age;
        const float Life = 1.2f;

        public static void Spawn(Vector3 position, string message)
        {
            GameObject go = new GameObject("FloatingText");
            FloatingText ft = go.AddComponent<FloatingText>();
            ft.text = message;
            ft.world = position;
        }

        void Update()
        {
            age += Time.deltaTime;
            world.y += 1.2f * Time.deltaTime;
            if (age >= Life) Destroy(gameObject);
        }

        void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 s = cam.WorldToScreenPoint(world);
            float alpha = 1f - Mathf.Clamp01((age - 0.6f) / (Life - 0.6f));

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 40;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;

            Rect r = new Rect(s.x - 100f, Screen.height - s.y - 25f, 200f, 50f);
            style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.7f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style); // shadow
            style.normal.textColor = new Color(1f, 0.85f, 0.2f, alpha);
            GUI.Label(r, text, style);
        }
    }
}