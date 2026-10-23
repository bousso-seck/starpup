using UnityEngine;

// Moves the pupcat left/right and lets it jump.
public class PupcatController : MonoBehaviour
{
    // default move. gravity at 3
    public float moveSpeed = 6f;
    public float jumpForce = 12f;

    private Rigidbody2D rb;
    private bool isGrounded;

    void Awake()
    {
        // Grab the Rigidbody2D on this same object once, at the start.
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // -1 = left (A or left arrow), 0 = nothing, 1 = right (D or right arrow)
        float horizontal = Input.GetAxisRaw("Horizontal");

        // Keep the current up/down speed, only change the left/right speed.
        // (Unity 6 renamed velocity to linearVelocity; this file targets Unity 2022.3.)
        rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);

        // Jump only when the Space bar is pressed AND we're standing on something.
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }
    }

    // Called by Unity when we touch something.
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only count it as ground if the surface is pointing mostly upward.
        if (collision.contacts[0].normal.y > 0.5f)
        {
            isGrounded = true;
        }
    }

    // Called by Unity when we stop touching something.
    void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
}