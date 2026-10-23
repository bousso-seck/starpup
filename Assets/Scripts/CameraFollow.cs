using UnityEngine;

// Makes the camera follow the pupcat smoothly.
public class CameraFollow : MonoBehaviour
{
    // The object the camera follows (drag the Pupcat here in the Inspector).
    public Transform target;

    // How far from the pupcat the camera sits.
    // Z must stay at -10 for a 2D camera, or you'll see nothing.
    public Vector3 offset = new Vector3(0f, 1.5f, -10f);

    // Smaller = snappier camera, larger = lazier camera.
    public float smoothTime = 0.15f;

    private Vector3 velocity = Vector3.zero;

    // LateUpdate runs after all movement for the frame, which avoids camera jitter.
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}