
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GuitarMinigame : MonoBehaviour
{
    [Header("Music")]
    public AudioClip guitarSong;
    public float bpm = 120f;
    public float songOffset = 0f;
    public float noteInterval = 0.8f;
    public float duration = 30f;

    [Header("Gameplay")]
    public float noteSpeed = 4f;
    public float hitWindow = 0.7f;

    private const float spawnY = 5f;
    private const float hitY = -3f;

    private AudioSource audioSource;
    private double songStartDSP;
    private float nextBeatTime;

    private int hits;
    private int misses;
    private bool finished;

    private readonly Color[] colors =
    {
        Color.red,
        Color.green,
        Color.blue
    };

    private readonly string[] labels =
    {
        "A / ○",
        "S / ×",
        "D / □"
    };

    private readonly List<Note> notes = new List<Note>();

    private class Note
    {
        public int lane;
        public float hitTime;
        public GameObject obj;
    }

    private readonly float[] flashUntil = new float[3];
    private readonly string[] feedback = new string[3];
    private readonly float[] feedbackUntil = new float[3];

    private GUIStyle titleStyle;
    private GUIStyle feedbackStyle;
    private GUIStyle buttonStyle;

    private float SongTime
    {
        get
        {
            return (float)(AudioSettings.dspTime - songStartDSP);
        }
    }

    void Start()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            cam = cameraObj.AddComponent<Camera>();
        }

        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color(0.08f, 0.06f, 0.15f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        for (int i = 0; i < 3; i++)
        {
            CreateRect(
                "Lane " + i,
                new Vector3((i - 1) * 2f, 0, 1),
                new Vector3(1.7f, 10f, 1),
                new Color(0.17f, 0.15f, 0.25f)
            );

            CreateRect(
                "Hit Zone " + i,
                new Vector3((i - 1) * 2f, hitY, 0),
                new Vector3(1.5f, 0.3f, 1),
                colors[i]
            );
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.7f;

        if (guitarSong != null)
            audioSource.clip = guitarSong;
        else
            guitarSong = audioSource.clip;

        if (guitarSong != null)
            duration = guitarSong.length;

        songStartDSP = AudioSettings.dspTime + 0.2;

        if (guitarSong != null)
            audioSource.PlayScheduled(songStartDSP);
        else
            Debug.LogWarning("Guitar song is missing!");

        noteSpeed = Mathf.Max(0.1f, noteSpeed);
        bpm = Mathf.Max(1f, bpm);

        float travelTime = (spawnY - hitY) / noteSpeed;
        float beatLength = 60f / bpm;

        nextBeatTime = songOffset;

        while (nextBeatTime < travelTime)
            nextBeatTime += beatLength;
    }

    GameObject CreateRect(
        string name,
        Vector3 position,
        Vector3 scale,
        Color color)
    {
        GameObject obj =
            GameObject.CreatePrimitive(PrimitiveType.Quad);

        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = color;
            obj.GetComponent<Renderer>().material = mat;
        }

        return obj;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;

        if ((keyboard != null &&
             keyboard.escapeKey.wasPressedThisFrame) ||
            (pad != null &&
             pad.startButton.wasPressedThisFrame))
        {
            if (audioSource != null)
                audioSource.Stop();

            GuitarCombatTrigger.FinishCombat(false, false);
            return;
        }

        if (finished)
            return;

        float time = SongTime;
        float beatLength = 60f / bpm;
        float travelTime = (spawnY - hitY) / noteSpeed;

        while (nextBeatTime - travelTime <= time &&
               nextBeatTime <= duration - hitWindow)
        {
            int lane = Random.Range(0, 3);
            SpawnNote(lane, nextBeatTime);
            nextBeatTime += beatLength;
        }

        for (int i = notes.Count - 1; i >= 0; i--)
        {
            Note note = notes[i];

            float remaining = note.hitTime - time;
            float y = hitY + remaining * noteSpeed;

            note.obj.transform.position =
                new Vector3(
                    (note.lane - 1) * 2f,
                    y,
                    -1f
                );

            if (time > note.hitTime + hitWindow / noteSpeed)
            {
                ShowFeedback(note.lane, "MISS");
                misses++;
                Destroy(note.obj);
                notes.RemoveAt(i);
            }
        }

        if ((keyboard != null &&
             keyboard.aKey.wasPressedThisFrame) ||
            (pad != null &&
             pad.buttonEast.wasPressedThisFrame))
        {
            HitNote(0);
        }

        if ((keyboard != null &&
             keyboard.sKey.wasPressedThisFrame) ||
            (pad != null &&
             pad.buttonSouth.wasPressedThisFrame))
        {
            HitNote(1);
        }

        if ((keyboard != null &&
             keyboard.dKey.wasPressedThisFrame) ||
            (pad != null &&
             pad.buttonWest.wasPressedThisFrame))
        {
            HitNote(2);
        }

        if (time >= duration + 0.3f)
        {
            finished = true;
            misses += notes.Count;

            foreach (Note note in notes)
                Destroy(note.obj);

            notes.Clear();

            if (audioSource != null)
                audioSource.Stop();
        }
    }

    void SpawnNote(int lane, float hitTime)
    {
        GameObject obj = CreateRect(
            "Guitar Note",
            new Vector3(
                (lane - 1) * 2f,
                spawnY,
                -1f
            ),
            new Vector3(1.2f, 0.4f, 1),
            colors[lane]
        );

        notes.Add(new Note
        {
            lane = lane,
            hitTime = hitTime,
            obj = obj
        });
    }

    void HitNote(int lane)
    {
        Note closest = null;
        float bestDistance = hitWindow / noteSpeed;

        foreach (Note note in notes)
        {
            if (note.lane != lane)
                continue;

            float distance =
                Mathf.Abs(note.hitTime - SongTime);

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                closest = note;
            }
        }

        if (closest != null)
        {
            hits++;

            Destroy(closest.obj);
            notes.Remove(closest);

            ShowFeedback(lane, "HIT!");
        }
        else
        {
            misses++;
            ShowFeedback(lane, "MISS");
        }
    }

    void ShowFeedback(int lane, string message)
    {
        feedback[lane] = message;
        feedbackUntil[lane] =
            Time.unscaledTime + 0.4f;

        flashUntil[lane] =
            Time.unscaledTime + 0.18f;
    }

    void OnGUI()
    {
        // GUI styles MUST be initialized inside OnGUI.
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 25;
            titleStyle.alignment =
                TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = Color.white;

            feedbackStyle = new GUIStyle(titleStyle);
            feedbackStyle.fontSize = 22;
            feedbackStyle.fontStyle = FontStyle.Bold;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 18;
        }

        float center = Screen.width / 2f;

        GUI.Label(
            new Rect(center - 250, 10, 500, 40),
            "STARPUP GUITAR BATTLE",
            titleStyle
        );

        GUI.Label(
            new Rect(center - 250, 50, 500, 40),
            "Hits: " + hits +
            "    Misses: " + misses,
            titleStyle
        );

        float laneSpacing = Screen.width * 0.18f;

        for (int lane = 0; lane < 3; lane++)
        {
            float x =
                center + (lane - 1) * laneSpacing;

            if (Time.unscaledTime < flashUntil[lane])
            {
                Color oldColor = GUI.color;
                GUI.color = colors[lane];

                GUI.Box(
                    new Rect(
                        x - 80,
                        Screen.height - 95,
                        160,
                        65
                    ),
                    ""
                );

                GUI.color = oldColor;
            }

            GUI.Label(
                new Rect(
                    x - 85,
                    Screen.height - 80,
                    170,
                    45
                ),
                labels[lane],
                titleStyle
            );

            if (Time.unscaledTime <
                feedbackUntil[lane])
            {
                GUI.Label(
                    new Rect(
                        x - 90,
                        Screen.height - 140,
                        180,
                        40
                    ),
                    feedback[lane],
                    feedbackStyle
                );
            }
        }

        if (!finished)
            return;

        int total = hits + misses;

        float accuracy =
            total > 0 ? (float)hits / total : 0f;

        bool clean = accuracy >= 0.70f;

        string result = clean
            ? "CLEAN RUN! 6 Hearts Restored!"
            : "SLOPPY RUN! Classmate Freed!";

        GUI.Box(
            new Rect(
                center - 230,
                Screen.height / 2f - 90,
                460,
                180
            ),
            result + "\nAccuracy: " +
            Mathf.RoundToInt(accuracy * 100) + "%"
        );

        if (GUI.Button(
            new Rect(
                center - 100,
                Screen.height / 2f + 20,
                200,
                45
            ),
            "Return to Level",
            buttonStyle))
        {
            GuitarCombatTrigger.FinishCombat(
                true,
                clean
            );
        }
    }
}
