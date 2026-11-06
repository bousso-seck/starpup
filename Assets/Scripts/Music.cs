using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays background music, fading in and out. Nothing to attach by hand: it makes itself, like GameFeel does.
//   Music.Play(clip)   fades a track in (and loops it)
//   Music.Stop()       fades it out
//
// PLACEHOLDER: Music.Placeholder() makes a short boss-style loop from code, so there is something to
// hear until you have a real track. To use a real track, either drag it into the "Boss Music" slot on the
// BossArena, or save it as Assets/Resources/Music/BossTheme.wav (.mp3 and .ogg work too).
// The music stops by itself when a scene loads (for example when the level restarts).
public class Music : MonoBehaviour
{
    public static float Volume = 0.5f;   // how loud the music is (0 to 1)

    private static Music instance;
    private static AudioClip placeholder;

    private AudioSource source;
    private Coroutine fade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        Make();
    }

    static Music Make()
    {
        if (instance != null) return instance;
        GameObject go = new GameObject("Music");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Music>();
        return instance;
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (fade != null) StopCoroutine(fade);
        fade = null;
        source.Stop();
    }

    public static void Play(AudioClip clip, float fadeIn = 0.8f)
    {
        if (clip == null) return;
        Music m = Make();
        if (m.source.isPlaying && m.source.clip == clip) return;
        if (m.fade != null) m.StopCoroutine(m.fade);
        m.source.clip = clip;
        m.source.volume = 0f;
        m.source.Play();
        m.fade = m.StartCoroutine(m.FadeTo(Volume, fadeIn, false));
    }

    public static void Stop(float fadeOut = 1.2f)
    {
        Music m = Make();
        if (!m.source.isPlaying) return;
        if (m.fade != null) m.StopCoroutine(m.fade);
        m.fade = m.StartCoroutine(m.FadeTo(0f, fadeOut, true));
    }

    IEnumerator FadeTo(float target, float seconds, bool stopAtEnd)
    {
        float start = source.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / Mathf.Max(0.01f, seconds)));
            yield return null;
        }
        source.volume = target;
        if (stopAtEnd) source.Stop();
        fade = null;
    }

    // A tense little loop made from code: a pulsing kick, a bass line and a fast arpeggio (Am, F, G, E).
    public static AudioClip Placeholder()
    {
        if (placeholder != null) return placeholder;

        const int rate = 22050;
        float beat = 60f / 140f;
        float eighth = beat / 2f;
        int eighths = 4 * 8;                       // four bars
        int total = Mathf.RoundToInt(eighths * eighth * rate);
        float[] data = new float[total];

        float[][] chords =
        {
            new[] { 220.00f, 261.63f, 329.63f },   // A  C  E
            new[] { 174.61f, 220.00f, 261.63f },   // F  A  C
            new[] { 196.00f, 246.94f, 293.66f },   // G  B  D
            new[] { 164.81f, 207.65f, 246.94f },   // E  G# B
        };
        float[] bass = { 110.00f, 87.31f, 98.00f, 82.41f };
        int[] pattern = { 0, 1, 2, 1, 0, 1, 2, 1 };

        for (int e = 0; e < eighths; e++)
        {
            int bar = e / 8;
            int step = e % 8;
            int start = Mathf.RoundToInt(e * eighth * rate);
            int len = Mathf.RoundToInt(eighth * rate);

            // The arpeggio: a short plucked square wave.
            float f = chords[bar][pattern[step]];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)rate;
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                data[(start + i) % total] += sq * Mathf.Exp(-t * 9f) * 0.07f;
            }

            if (step % 2 == 0)
            {
                // The bass: a saw wave on every beat.
                float bf = bass[bar];
                for (int i = 0; i < len * 2; i++)
                {
                    float t = i / (float)rate;
                    float saw = 2f * (t * bf - Mathf.Floor(t * bf + 0.5f));
                    data[(start + i) % total] += saw * Mathf.Exp(-t * 3f) * 0.12f;
                }

                // The kick drum: a quick falling thump.
                int kickLen = Mathf.RoundToInt(0.15f * rate);
                for (int i = 0; i < kickLen; i++)
                {
                    float t = i / (float)rate;
                    float phase = 2f * Mathf.PI * (45f * t + 110f * (1f - Mathf.Exp(-18f * t)) / 18f);
                    data[(start + i) % total] += Mathf.Sin(phase) * Mathf.Exp(-t * 20f) * 0.3f;
                }
            }
        }

        for (int i = 0; i < total; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

        placeholder = AudioClip.Create("BossMusicPlaceholder", total, 1, rate, false);
        placeholder.SetData(data, 0);
        return placeholder;
    }
}