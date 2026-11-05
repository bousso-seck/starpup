using System.Collections;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

// The spirit flame that leaves a defeated classmate. It appears in the middle of the body, stays there
// for 1.5 seconds, then slowly drifts away (sideways first, then up) off the screen and removes itself.
// Nothing to attach by hand: Enemy calls SpiritRise.Spawn(...) when someone is defeated.
//
// If the Enemy Data has a Spirit Flame Prefab, that prefab is used. Its particles are set to loop and
// to follow the flame through the world, and are drawn in front of the level, so the flame always lasts
// for the whole flight. If the prefab has nothing visible in it, or there is no prefab, a simple
// glowing flame is drawn from code instead.
//
// TESTING: in the Editor (or a development build), press T to spawn a flame above the pupcat.
// The Console says what was spawned, so you can see if the prefab is the problem.
public class SpiritRise : MonoBehaviour
{
    public float growTime = 3f;     // seconds to grow from small to its normal size (the scale it has on the prefab)
    public float startSize = 0.01f;    // how small it starts: 0.1 = a tenth of its normal size
    public float holdTime = 1.8f;     // seconds the flame stays in the middle of the body before it leaves
    public float driftSpeed = 1.5f;   // how fast it drifts at first (units per second). Small = slow.
    public float driftSpeedUp = 0.6f; // how much faster it gets each second, so it does leave the screen

    private float sideDir = 1f;      // 1 = flies off to the right, -1 = to the left
    private float age;
    private bool placeholder;        // true when the flame is the code-drawn one (it gets a trail and grows in)
    private bool isTester;           // the hidden object that listens for the T key
    private bool checkedStatus;      // the one-time Console check has been printed
    private Vector3 baseScale;
    private float nextPuff;
    private static Sprite softSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateTester()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameObject go = new GameObject("SpiritTester");
        go.AddComponent<SpiritRise>().isTester = true;
        DontDestroyOnLoad(go);
#endif
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, float sideDirection)
    {
        position.z = 0f;
        GameObject go;
        bool usePlaceholder = false;

        if (prefab != null)
        {
            go = Instantiate(prefab, position, Quaternion.identity);
            go.SetActive(true); // the prefab may be saved switched off
            PrepareParticles(go);

            // A prefab with nothing to draw would be invisible, so give it a glow as well.
            if (go.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Debug.LogWarning("The Spirit Flame prefab '" + prefab.name + "' has nothing visible in it. Using the built-in flame too.");
                AddGlow(go.transform);
                usePlaceholder = true;
            }
        }
        else
        {
            go = MakePlaceholder(position);
            usePlaceholder = true;
        }

        SpiritRise rise = go.GetComponent<SpiritRise>();
        if (rise == null) rise = go.AddComponent<SpiritRise>();
        rise.sideDir = sideDirection >= 0f ? 1f : -1f;
        rise.placeholder = usePlaceholder;

        // Remember the normal size, then start small and grow (see Update).
        rise.baseScale = go.transform.localScale;
        go.transform.localScale = rise.baseScale * rise.startSize;
        return go;
    }

    // Makes sure the prefab's particles play for the whole flight, follow the flame as it moves,
    // and are drawn in front of the level.
    static void PrepareParticles(GameObject go)
    {
        int systems = 0;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.None;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; // so growing the flame grows its particles too

            // "Lifetime by Emitter Speed" shortens particle lifetimes while the flame moves, which can make it vanish.
            ParticleSystem.LifetimeByEmitterSpeedModule bySpeed = ps.lifetimeByEmitterSpeed;
            bySpeed.enabled = false;

            ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.enabled = true;
                pr.sortingOrder = Mathf.Max(pr.sortingOrder, 20);
            }

            ps.Clear();
            ps.Play();
            systems++;
        }

        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingOrder = Mathf.Max(sr.sortingOrder, 20);
        }

        Debug.Log("Spirit flame spawned: '" + go.name + "' at " + go.transform.position + ", "
                  + systems + " particle system(s), " + go.GetComponentsInChildren<Renderer>(true).Length + " renderer(s).");
    }

    void Update()
    {
        if (isTester)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame)
            {
                Enemy e = FindFirstObjectByType<Enemy>();
                PupcatController p = FindFirstObjectByType<PupcatController>();
                GameObject prefab = (e != null && e.data != null) ? e.data.spiritFlamePrefab : null;
                if (p != null) Spawn(prefab, p.transform.position, p.FacingDirection);
            }
#endif
            return;
        }

        age += Time.deltaTime;

        // One check shortly after spawning, so the Console says whether the particles are really there and on screen.
        if (!placeholder && !checkedStatus && age > 0.4f)
        {
            checkedStatus = true;
            string report = "Spirit flame check: ";
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                report += "[" + ps.name + " particles=" + ps.particleCount + " playing=" + ps.isPlaying + "] ";
            }
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                report += "[renderer visibleToCamera=" + r.isVisible + " size=" + r.bounds.size + "] ";
            }
            Debug.Log(report);
        }

        // Grow from small to normal size (fast at first, then settling). Only the code-drawn flame also flickers.
        float k = Mathf.Clamp01(age / Mathf.Max(0.01f, growTime));
        float eased = 1f - (1f - k) * (1f - k);
        float flicker = placeholder ? 1f + Mathf.Sin(age * 22f) * 0.08f : 1f;
        transform.localScale = new Vector3(baseScale.x, baseScale.y * flicker, baseScale.z) * Mathf.Lerp(startSize, 1f, eased);

        // Stay put in the middle of the body for holdTime, then drift away slowly: sideways first, curving up.
        if (age >= holdTime)
        {
            float t = age - holdTime;
            float bend = Mathf.Clamp01(t / 2.5f);
            Vector3 dir = new Vector3(sideDir * Mathf.Lerp(1f, 0.5f, bend), Mathf.Lerp(0.25f, 1f, bend), 0f).normalized;
            transform.position += dir * (driftSpeed * (1f + t * driftSpeedUp)) * Time.deltaTime;

            if (OffScreen() || age > 25f) Destroy(gameObject);
        }

        if (placeholder && Time.time >= nextPuff)
        {
            nextPuff = Time.time + 0.05f;
            StartCoroutine(Puff(transform.position));
        }
    }

    bool OffScreen()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(transform.position);
        return v.y > 1.15f || v.x < -0.15f || v.x > 1.15f;
    }

    // A little fading glow left behind the code-drawn flame.
    IEnumerator Puff(Vector3 position)
    {
        GameObject go = new GameObject("SpiritTrail");
        go.transform.position = position;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSoftSprite();
        sr.sortingOrder = 19;

        float size = 0.3f;
        float life = 0.4f;
        float t = 0f;
        while (t < life && go != null)
        {
            t += Time.deltaTime;
            float k = t / life;
            go.transform.position += Vector3.up * 0.4f * Time.deltaTime;
            go.transform.localScale = new Vector3(size * (1f - k * 0.5f), size * (1f - k * 0.5f), 1f);
            sr.color = new Color(0.6f, 0.9f, 1f, 0.5f * (1f - k));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    static GameObject MakePlaceholder(Vector3 position)
    {
        GameObject go = new GameObject("SpiritFlame");
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.6f, 0.85f, 1f);
        AddGlow(go.transform);
        Debug.Log("Spirit flame spawned: the built-in flame at " + position);
        return go;
    }

    // The code-drawn glow: a soft round blob.
    static void AddGlow(Transform parent)
    {
        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(parent, false);
        SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
        sr.sprite = GetSoftSprite();
        sr.color = new Color(0.7f, 0.95f, 1f, 0.95f);
        sr.sortingOrder = 20;
        glow.transform.localScale = Vector3.one * 1.2f;
    }

    // A soft round glow, drawn from code.
    static Sprite GetSoftSprite()
    {
        if (softSprite == null)
        {
            const int size = 32;
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
            softSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return softSprite;
    }
}