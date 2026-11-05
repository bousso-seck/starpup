using System.Collections.Generic;
using UnityEngine;

// The dash dust, drawn from the hand-made 4-frame sprite sheet DashDust.png.
// It replaces the dust particles when the pupcat rolls or dashes.
//
// SETUP: put DashDust.png in the folder Assets/Resources/Fx (make the folders if they don't exist).
// In its Inspector set: Texture Type = Sprite (2D and UI), Compression = None, Filter Mode = Bilinear,
// then Apply. Nothing else: this script creates itself when you press Play.
//
// The 4 frames sit side by side in the sheet, in order. The art shows the dust with its tall end next to the
// pupcat and its tail trailing away behind. If it ever looks backwards, tick "Flip Art" below on the object
// called DashEffect in the Hierarchy while the game runs, or change flipArt to true in this script.
//
// Other scripts call:  DashEffect.Play(position, behindDirection, size, alpha)
//   position         where the dust starts (the pupcat's feet)
//   behindDirection  which way the tail trails: -FacingDirection
//   size             width of the puff in world units (1.6 is a good start)
//   alpha            1 = solid, 0.5 = see-through
// Play returns false if the sheet is missing, so the caller can fall back to the old dust.
public class DashEffect : MonoBehaviour
{
    public bool flipArt = true;         // tick if the dust looks backwards
    public float startGap = 0.5f;       // how far behind the pupcat's middle the dust starts. Bigger = further back.
    public float lifeTime = 0.36f;     // seconds for the 4 frames to play
    public float driftSpeed = 1.2f;     // how fast the puff slides back while it plays

    const int FrameCount = 4;
    static DashEffect instance;

    class Puff
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float age;
        public float alpha;
        public float back;
    }

    private readonly List<Puff> puffs = new List<Puff>();
    private Sprite[] frames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("DashEffect");
        instance = go.AddComponent<DashEffect>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        Texture2D tex = Resources.Load<Texture2D>("Fx/DashDust");
        if (tex == null)
        {
            Debug.LogWarning("DashEffect: could not find Assets/Resources/Fx/DashDust.png. The old dust particles are used instead.");
            return;
        }

        // Cut the sheet into its 4 frames. One frame is 1 world unit wide at scale 1.
        int w = tex.width / FrameCount;
        frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            // The pivot sits near the bottom-right, so that spot lands on the pupcat's feet.
            frames[i] = Sprite.Create(tex, new Rect(i * w, 0f, w, tex.height), new Vector2(0.96f, 0.08f), w);
        }
    }

    public static bool Play(Vector3 position, float behindDirection, float size, float alpha)
    {
        if (instance == null || instance.frames == null) return false;

        float back = behindDirection >= 0f ? 1f : -1f;
        position.z = 0f;
        position.x += back * instance.startGap; // start behind the pupcat, not on top of it

        GameObject go = new GameObject("DashDust");
        go.transform.position = position;
        go.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = instance.frames[0];
        sr.sortingOrder = 1;
        // The art has its tail pointing left. Flip it when the tail should trail to the right.
        sr.flipX = (back > 0f) != instance.flipArt;
        sr.color = new Color(1f, 1f, 1f, alpha);

        Puff p = new Puff();
        p.go = go;
        p.sr = sr;
        p.alpha = alpha;
        p.back = back;
        instance.puffs.Add(p);
        return true;
    }

    void Update()
    {
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            Puff p = puffs[i];
            if (p.go == null) { puffs.RemoveAt(i); continue; } // the scene was restarted

            p.age += Time.deltaTime;
            float k = Mathf.Clamp01(p.age / lifeTime);

            p.sr.sprite = frames[Mathf.Min(FrameCount - 1, (int)(k * FrameCount))];
            p.go.transform.position += new Vector3(p.back * driftSpeed * Time.deltaTime, 0f, 0f);

            // Fade out over the last third.
            float fade = (k < 0.66f) ? 1f : 1f - (k - 0.66f) / 0.34f;
            p.sr.color = new Color(1f, 1f, 1f, p.alpha * fade);

            if (k >= 1f)
            {
                Destroy(p.go);
                puffs.RemoveAt(i);
            }
        }
    }
}