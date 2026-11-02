using UnityEngine;

// Moves the pupcat, lets it jump, and lets it dodge-roll. (Written for Unity 6.)
// Attach this to the Pupcat object (it needs a Rigidbody2D and a Collider2D).
public class PupcatController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;

    [Header("Dodge roll")]
    public float dodgeSpeed = 14f;      // how fast the roll is
    public float dodgeDuration = 0.25f; // how long the roll lasts (seconds)
    public float dodgeCooldown = 0.5f;  // wait time after a roll before the next one

    // Other scripts (hearts, enemies) can read these later.
    public bool IsDodging { get; private set; }
    public bool IsInvincible { get { return IsDodging; } }
    public int FacingDirection { get; private set; } = 1; // 1 = right, -1 = left

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Color normalColor;
    private bool isGrounded;
    private float dodgeEndTime;
    private float nextDodgeTime;
    private float knockbackEndTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) normalColor = sprite.color;
    }

    void Update()
    {
        // -1 = left (A or left arrow), 0 = nothing, 1 = right (D or right arrow)
        float horizontal = GameInput.Move;

        // Remember which way we last moved, so the roll goes that way.
        if (horizontal > 0f) FacingDirection = 1;
        else if (horizontal < 0f) FacingDirection = -1;

        // While being knocked back, let the shove play out. Steering is locked for a moment.
        if (Time.time < knockbackEndTime) return;

        // While rolling, the roll takes over. Steering and jumping are locked.
        if (IsDodging)
        {
            rb.linearVelocity = new Vector2(FacingDirection * dodgeSpeed, rb.linearVelocity.y);

            if (Time.time >= dodgeEndTime)
            {
                EndDodge();
            }
            return;
        }

        // Normal movement: keep the current up/down speed, only change left/right.
        rb.linearVelocity = new Vector2(horizontal * moveSpeed, rb.linearVelocity.y);

        // Jump only when Space is pressed AND we're standing on something.
        if (GameInput.JumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // Start a roll with Left Shift (or the B button on an Xbox-style controller).
        if (GameInput.DodgePressed && isGrounded && Time.time >= nextDodgeTime)
        {
            StartDodge();
        }
    }

    void StartDodge()
    {
        IsDodging = true;
        dodgeEndTime = Time.time + dodgeDuration;
        nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;

        // Make the pupcat see-through during the roll, so you can see the invincible window.
        if (sprite != null)
        {
            Color c = normalColor;
            c.a = 0.4f;
            sprite.color = c;
        }
    }

    void EndDodge()
    {
        IsDodging = false;
        if (sprite != null) sprite.color = normalColor;
    }

    // Called when the pupcat gets hit: shoves it and briefly takes away steering.
    public void Knockback(Vector2 velocity, float duration)
    {
        if (IsDodging) EndDodge();
        knockbackEndTime = Time.time + duration;
        rb.linearVelocity = velocity;
    }

    // Called by Unity when we touch something.
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only count it as ground if the surface is pointing mostly upward.
        if (collision.GetContact(0).normal.y > 0.5f)
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