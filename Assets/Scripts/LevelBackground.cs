using System.Collections.Generic;
using UnityEngine;

// Builds the Level 1 background from your art. Nothing else to set up by hand:
//   Sky     the 1920x1080 clouds. It stays behind the camera and drifts very slowly, so the sky never runs out.
//   Chunks  the four 3840x1080 street pieces, laid side by side from left to right (the playable area).
// Put this on an empty object called "LevelBackground" and drag the sprites into the slots.
//
// The background shows in the Scene view and Game view WITHOUT pressing Play. Click LevelBackground and change
// Left Edge X or Bottom Y in the Inspector, and the street moves right away. These numbers are saved in the scene.
// The pictures themselves are made by this script and are not saved in the scene, so do not put your own objects
// inside LevelBackground: everything inside it is removed and rebuilt.
[ExecuteAlways]
public class LevelBackground : MonoBehaviour
{
    [Header("Art (drag the sprites in)")]
    public Sprite sky;
    public Sprite[] chunks;            // in order: chunk 1 is the start, the last one is the end of the level

    [Header("Where the street goes")]
    public float leftEdgeX = -9.6f;    // x of the left edge of chunk 1
    public float bottomY = -5.4f;      // y of the bottom edge of the art. Raise it to lift the street, lower it to sink it.

    [Header("Sky")]
    public float skyScale = 1.15f;     // a little bigger than the screen, so there is room for it to drift
    public float skyDrift = 0.12f;     // how fast the clouds drift (units per second). 0 = still.
    public Vector2 skyOffset = Vector2.zero; // moves the sky from the middle of the screen. X = left/right, Y = up/down.

    [Header("Draw order (lower = further back)")]
    public int skyOrder = -100;
    public int chunkOrder = -50;

    // The world edges of the level, so a camera script can stop there if it wants to.
    public float LeftEdge { get; private set; }
    public float RightEdge { get; private set; }

    private readonly List<Transform> chunkObjects = new List<Transform>();
    private readonly List<Sprite> chunkSprites = new List<Sprite>();
    private float laidLeft = float.NaN;
    private float laidBottom = float.NaN;
    private Vector3 laidOrigin = new Vector3(float.NaN, float.NaN, 0f);

    private Transform skyObject;
    private SpriteRenderer skyRenderer;
    private float skyTime;

    void OnEnable()
    {
        Rebuild();
    }

    void OnDisable()
    {
        Clear();
    }

#if UNITY_EDITOR
    // Runs whenever a value is changed in the Inspector. The rebuild waits a moment, because
    // Unity does not allow objects to be created or removed from inside OnValidate.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Rebuild();
        };
    }
#endif

    void Rebuild()
    {
        Clear();
        Build();
    }

    // Removes everything this script made.
    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        chunkObjects.Clear();
        chunkSprites.Clear();
        skyObject = null;
        skyRenderer = null;
        laidLeft = float.NaN;
        laidBottom = float.NaN;
        laidOrigin = new Vector3(float.NaN, float.NaN, 0f);
    }

    GameObject MakeChild(string childName)
    {
        GameObject go = new GameObject(childName);
        go.hideFlags = HideFlags.DontSave; // made by this script, so it is not stored in the scene file
        go.transform.SetParent(transform, false);
        return go;
    }

    void Build()
    {
        // The street chunks.
        if (chunks != null)
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] == null)
                {
                    if (Application.isPlaying) Debug.LogWarning("LevelBackground: chunk " + (i + 1) + " is empty, so it was skipped.");
                    continue;
                }

                GameObject go = MakeChild("Chunk " + (i + 1));
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = chunks[i];
                sr.sortingOrder = chunkOrder;

                chunkObjects.Add(go.transform);
                chunkSprites.Add(chunks[i]);
            }
        }
        Layout();

        // The sky.
        if (sky != null)
        {
            GameObject go = MakeChild("Sky");
            skyObject = go.transform;
            skyRenderer = go.AddComponent<SpriteRenderer>();
            skyRenderer.sprite = sky;
            skyRenderer.sortingOrder = skyOrder;
            UpdateSky(0f);
        }
        else if (Application.isPlaying)
        {
            Debug.LogWarning("LevelBackground: no Sky sprite was given.");
        }

        if (Application.isPlaying)
        {
            Debug.Log("LevelBackground: " + chunkObjects.Count + " chunk(s) from x " + LeftEdge + " to " + RightEdge
                      + ". The street's bottom edge is at y " + bottomY + ".");
        }
    }

    // Puts the chunks side by side, starting at Left Edge X, with their bottoms at Bottom Y.
    void Layout()
    {
        // The street is placed relative to this object, so you can also just drag LevelBackground
        // with the Move tool. Its Position is saved with the scene.
        Vector3 origin = transform.position;
        float cursor = origin.x + leftEdgeX;
        for (int i = 0; i < chunkObjects.Count; i++)
        {
            Bounds b = chunkSprites[i].bounds; // size in world units (3840 px at 100 pixels per unit = 38.4)
            // Works whatever the sprite's pivot is: the left edge lands on "cursor", the bottom on bottomY.
            chunkObjects[i].position = new Vector3(cursor - b.min.x, origin.y + bottomY - b.min.y, 0f);
            cursor += b.size.x;
        }
        LeftEdge = origin.x + leftEdgeX;
        RightEdge = cursor;
        laidLeft = leftEdgeX;
        laidBottom = bottomY;
        laidOrigin = origin;
    }

    // Re-lays the street if a number or this object's position changed.
    void CheckLayout()
    {
        if (!Mathf.Approximately(laidLeft, leftEdgeX) || !Mathf.Approximately(laidBottom, bottomY)
            || (transform.position - laidOrigin).sqrMagnitude > 0.000001f)
        {
            Layout();
        }
    }

    void LateUpdate()
    {
        CheckLayout();
        UpdateSky(Application.isPlaying ? Time.deltaTime : 0f);
    }

    // Runs every time the Scene view redraws, so the street follows while you drag.
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            CheckLayout();
            UpdateSky(0f);
        }
    }

    void UpdateSky(float deltaTime)
    {
        if (skyObject == null || sky == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;
        Vector2 art = sky.bounds.size;

        // Big enough to always cover the screen, even on a wider monitor.
        float scale = Mathf.Max(skyScale, camW / art.x * 1.02f, camH / art.y * 1.02f);
        skyObject.localScale = new Vector3(scale, scale, 1f);

        // Drift left and right slowly, only as far as the extra size allows.
        float room = Mathf.Max(0f, (art.x * scale - camW) / 2f * 0.9f);
        skyTime += deltaTime * skyDrift;
        float offset = room > 0f ? (Mathf.PingPong(skyTime, room * 2f) - room) : 0f;

        Vector3 c = cam.transform.position;
        skyObject.position = new Vector3(c.x + offset + skyOffset.x, c.y + skyOffset.y, 0f);
    }
}