using System.Collections;
using UnityEngine;

// "Game feel" helpers: hit-stop, camera shake, dust puffs and placeholder sounds.
// Nothing to attach: it creates itself when you press Play.
// Other scripts call:
//   GameFeel.HitStop(seconds)        a tiny freeze when something hits
//   GameFeel.Shake(strength, secs)   camera shake
//   GameFeel.Dust(position, count)   little dust puffs
//   GameFeel.Play(GameFeel.Sound.X)  a placeholder sound, made from code (no audio files needed)
// The sounds are simple beeps and thumps so the fight has audio feedback until real sounds exist.
// Needs an Audio Listener in the scene (the Main Camera has one by default).
public class GameFeel : MonoBehaviour
{
    static GameFeel instance;

    // The camera adds this to its position every frame (see CameraFollow).
    public static Vector3 ShakeOffset { get; private set; }

    public enum Sound { Swing, Hit, Parry, Guard, Hurt, Jump, Land, Roll, Defeat }

    private const float MasterVolume = 0.7f; // turn all the placeholder sounds up or down here

    private float shakeStrength;
    private float shakeDuration = 0.01f;
    private float shakeEnd;
    private float freezeEnd;
    private bool frozen;

    private AudioSource audioSource;
    private AudioClip[] clips;
    private Sprite dustSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("GameFeel");
        instance = go.AddComponent<GameFeel>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // MakeClip(length, start pitch, end pitch, how noisy 0-1, how fast it fades, volume)
        clips = new AudioClip[System.Enum.GetValues(typeof(Sound)).Length];
        clips[(int)Sound.Swing]  = MakeClip(0.10f, 300f, 120f, 0.9f, 25f, 0.35f);  // whoosh
        clips[(int)Sound.Hit]    = MakeClip(0.15f, 160f, 60f, 0.5f, 20f, 0.7f);    // thump
        clips[(int)Sound.Parry]  = MakeClip(0.25f, 900f, 1400f, 0.05f, 12f, 0.45f); // bright ping
        clips[(int)Sound.Guard]  = MakeClip(0.06f, 600f, 700f, 0f, 40f, 0.2f);      // tiny tick
        clips[(int)Sound.Hurt]   = MakeClip(0.25f, 220f, 90f, 0.3f, 10f, 0.7f);    // ouch
        clips[(int)Sound.Jump]   = MakeClip(0.12f, 250f, 500f, 0.05f, 14f, 0.3f);   // hop
        clips[(int)Sound.Land]   = MakeClip(0.10f, 90f, 50f, 0.4f, 28f, 0.45f);     // soft thud
        clips[(int)Sound.Roll]   = MakeClip(0.20f, 200f, 200f, 0.95f, 9f, 0.3f);    // scuff
        clips[(int)Sound.Defeat] = MakeClip(0.40f, 200f, 40f, 0.4f, 6f, 0.7f);     // sinking

        // A 1x1 white pixel, centered, used for the dust puffs.
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        dustSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    // Builds a short sound from a sine wave (the pitch slides from start to end) mixed with noise.
    AudioClip MakeClip(float seconds, float freqStart, float freqEnd, float noise, float decay, float volume)
    {
        const int rate = 44100;
        int count = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;      // 0 to 1 through the sound
            float time = (float)i / rate;    // seconds
            float freq = Mathf.Lerp(freqStart, freqEnd, t);
            phase += 2f * Mathf.PI * freq / rate;

            float tone = Mathf.Sin(phase);
            float hiss = Random.value * 2f - 1f;
            float fade = Mathf.Exp(-decay * time);
            data[i] = (tone * (1f - noise) + hiss * noise) * fade * volume;
        }

        AudioClip clip = AudioClip.Create("fx", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Plays one of the placeholder sounds.
    public static void Play(Sound sound)
    {
        if (instance == null || instance.audioSource == null) return;
        instance.audioSource.PlayOneShot(instance.clips[(int)sound], MasterVolume);
    }

    // Freezes the whole game for a split second, so a hit feels heavy.
    public static void HitStop(float seconds)
    {
        if (instance == null) return;
        instance.StartFreeze(seconds);
    }

    // Shakes the camera. strength is in world units (0.1 is a small bump), then it fades out.
    public static void Shake(float strength, float seconds)
    {
        if (instance == null) return;
        instance.shakeStrength = Mathf.Max(instance.shakeStrength * 0.5f, strength);
        instance.shakeDuration = Mathf.Max(seconds, 0.01f);
        instance.shakeEnd = Time.unscaledTime + seconds;
    }

    // Kicks up a few small dust puffs at a position (usually the pupcat's feet).
    // direction pushes them sideways: 1 = right, -1 = left, 0 = just up.
    public static void Dust(Vector3 position, int count, float direction)
    {
        if (instance == null) return;
        for (int i = 0; i < count; i++)
        {
            instance.StartCoroutine(instance.PuffRoutine(position, direction));
        }
    }

    IEnumerator PuffRoutine(Vector3 position, float direction)
    {
        GameObject go = new GameObject("Dust");
        go.transform.position = position + new Vector3(Random.Range(-0.3f, 0.3f), 0.05f, 0f);

        float size = Random.Range(0.12f, 0.24f);
        go.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dustSprite;
        sr.sortingOrder = 4;

        Color color = new Color(0.85f, 0.8f, 0.7f, 0.8f);
        Vector2 velocity = new Vector2(Random.Range(-1f, 1f) + direction * Random.Range(0.5f, 2f), Random.Range(0.4f, 1.4f));
        float life = Random.Range(0.25f, 0.45f);
        float age = 0f;

        while (age < life)
        {
            if (go == null) yield break; // the scene was restarted while this puff was alive
            age += Time.deltaTime;
            float k = age / life;
            go.transform.position += (Vector3)(velocity * Time.deltaTime);
            go.transform.localScale = new Vector3(size * (1f + k), size * (1f + k), 1f);
            color.a = 0.8f * (1f - k);
            sr.color = color;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    void StartFreeze(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        if (frozen && end <= freezeEnd) return; // already frozen for longer
        freezeEnd = end;
        if (!frozen) StartCoroutine(FreezeRoutine());
    }

    IEnumerator FreezeRoutine()
    {
        frozen = true;
        Time.timeScale = 0f;
        while (Time.unscaledTime < freezeEnd) yield return null;
        Time.timeScale = 1f;
        frozen = false;
    }

    void Update()
    {
        float remaining = shakeEnd - Time.unscaledTime;
        if (remaining > 0f)
        {
            float fade = remaining / shakeDuration;
            Vector2 jitter = Random.insideUnitCircle * shakeStrength * fade;
            ShakeOffset = new Vector3(jitter.x, jitter.y, 0f);
        }
        else
        {
            ShakeOffset = Vector3.zero;
            shakeStrength = 0f;
        }
    }
}



