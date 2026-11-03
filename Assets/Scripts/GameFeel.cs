using System.Collections;
using UnityEngine;

// "Game feel" helpers: a tiny freeze when something hits (hit-stop) and camera shake.
// Nothing to attach: it creates itself when you press Play.
// Other scripts call GameFeel.HitStop(seconds) and GameFeel.Shake(strength, seconds).
public class GameFeel : MonoBehaviour
{
    static GameFeel instance;

    // The camera adds this to its position every frame (see CameraFollow).
    public static Vector3 ShakeOffset { get; private set; }

    private float shakeStrength;
    private float shakeDuration = 0.01f;
    private float shakeEnd;
    private float freezeEnd;
    private bool frozen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("GameFeel");
        instance = go.AddComponent<GameFeel>();
        DontDestroyOnLoad(go);
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