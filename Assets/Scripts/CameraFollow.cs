using UnityEngine;

// Makes the camera follow the pupcat smoothly, and shake when something hits (see GameFeel).
// Attach this to the Main Camera, then drag the Pupcat into the Target slot.
public class CameraFollow : MonoBehaviour
{
    // The object the camera follows (drag the Pupcat here in the Inspector).
    public Transform target;

    // How far from the pupcat the camera sits.
    // Z must stay at -10 for a 2D camera, or you'll see nothing.
    public Vector3 offset = new Vector3(0f, 1.5f, -10f);

    // Smaller = snappier camera, larger = lazier camera.
    public float smoothTime = 0.15f;

    // Stops the camera at the ends of the level so it never shows empty space past them.
    // Tick Use Limits, then set the left and right edges of the level.
    public bool useLimits = false;
    public float minX = -10f;
    public float maxX = 130f;

    private Vector3 velocity = Vector3.zero;
    private Vector3 basePosition;   // where the camera would be without any shake
    private bool started;

    // LateUpdate runs after all movement for the frame, which avoids camera jitter.
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = target.position + offset;

        if (useLimits)
        {
            Camera cam = GetComponent<Camera>();
            float half = (cam != null) ? cam.orthographicSize * cam.aspect : 9f; // half the screen width
            float lo = minX + half;
            float hi = Mathf.Max(lo, maxX - half);
            goal.x = Mathf.Clamp(goal.x, lo, hi);
        }

        // On the very first frame the camera jumps straight to where it belongs (no gliding in from wherever
        // it was left in the editor). After that it follows smoothly.
        if (!started)
        {
            basePosition = goal;
            velocity = Vector3.zero;
            started = true;
        }

        basePosition = Vector3.SmoothDamp(basePosition, goal, ref velocity, smoothTime);
        transform.position = basePosition + GameFeel.ShakeOffset;
    }
}