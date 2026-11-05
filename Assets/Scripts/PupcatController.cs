using UnityEngine;

// Moves the pupcat, lets it jump, crouch and dodge-roll. (Written for Unity 6.)
// Attach this to the Pupcat object (it needs a Rigidbody2D and a Collider2D).
public class PupcatController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;

    [Header("Crouch (hold C, S or Down)")]
    public float crouchHeight = 0.65f;          // how tall the pupcat is while crouching (1 = normal)
    public float crouchSpeedMultiplier = 0.4f;  // walking speed while crouched

    [Header("Dodge roll")]
    public float dodgeSpeed = 14f;      // how fast the roll is
    public float dodgeDuration = 0.25f; // how long the roll lasts (seconds)
    public float dodgeCooldown = 0.5f;  // wait time after a roll before the next one

    // Other scripts (hearts, enemies) can read these.
    public bool IsDodging { get; private set; }
    public bool IsInvincible { get { return IsDodging; } }
    public bool IsCrouching { get; private set; }
    public bool IsGrounded { get { return isGrounded; } }
    public bool IsKnockedBack { get { return Time.time < knockbackEndTime; } }
    public int FacingDirection { get; private set; } = 1; // 1 = right, -1 = left

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Color normalColor;
    private bool isGrounded;
    private float dodgeEndTime;
    private float nextDodgeTime;
    private float knockbackEndTime;
    private Collider2D body;
    private PupcatParry parry;
    private Vector3 standingScale;
    private bool crouchLocked; // after a jump, crouch stays off until the key is let go
    private bool airDashUsed;  // one dash per jump; landing gives it back
    private bool dashInAir;    // true while the current roll is an air dash
    private float nextTrail;   // when the next little dash puff is allowed

    void Awake()
    {
        body = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        parry = GetComponent<PupcatParry>();
        standingScale = transform.localScale;
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
            // You can jump out of a roll that is on the ground.
            if (GameInput.JumpPressed && isGrounded)
            {
                CancelDodge();
                Jump();
                return;
            }

            // An air dash flies straight sideways. A ground roll keeps its up/down speed.
            rb.linearVelocity = new Vector2(FacingDirection * dodgeSpeed, dashInAir ? 0f : rb.linearVelocity.y);

            // Smaller puffs of the dash dust left behind while rolling.
            if (Time.time >= nextTrail)
            {
                nextTrail = Time.time + 0.1f;
                DashEffect.Play(dashInAir ? transform.position + Vector3.down * 0.4f : Feet(), -FacingDirection, dashInAir ? 0.8f : 0.9f, 0.6f);
            }

            if (Time.time >= dodgeEndTime)
            {
                CancelDodge();
            }
            return;
        }

        if (isGrounded) airDashUsed = false;

        // Crouch: hold the key while standing on the ground.
        if (!GameInput.CrouchHeld) crouchLocked = false;
        bool wantCrouch = GameInput.CrouchHeld && !crouchLocked && (IsCrouching || isGrounded);
        if (wantCrouch != IsCrouching) SetCrouch(wantCrouch);

        // Normal movement: keep the current up/down speed, only change left/right.
        float speed = IsCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed;
        rb.linearVelocity = new Vector2(horizontal * speed, rb.linearVelocity.y);

        // Jump only when Space is pressed AND we're standing on something. Jumping stands you up.
        if (GameInput.JumpPressed && isGrounded)
        {
            Jump();
        }

        // Start a roll with Left Shift (or Circle on a PS4 controller). In the air it is a dash,
        // once per jump.
        if (GameInput.DodgePressed && Time.time >= nextDodgeTime && (isGrounded || !airDashUsed))
        {
            StartDodge();
        }
    }

    void Jump()
    {
        if (IsCrouching) SetCrouch(false);
        crouchLocked = true;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        GameFeel.Play(GameFeel.Sound.Jump);
        GameFeel.Dust(Feet(), 3, 0f);
    }

    void StartDodge()
    {
        if (IsCrouching) SetCrouch(false);
        if (parry != null) parry.CancelGuard(); // a roll cancels the guard

        dashInAir = !isGrounded;
        if (dashInAir) airDashUsed = true;

        IsDodging = true;
        dodgeEndTime = Time.time + dodgeDuration;
        nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;

        GameFeel.Play(dashInAir ? GameFeel.Sound.Dash : GameFeel.Sound.Roll);
        // The dash dust sprite. (If the sprite sheet is missing, the old dust particles are used.)
        Vector3 dustSpot = dashInAir ? transform.position + Vector3.down * 0.4f : Feet();
        bool drewDust = DashEffect.Play(dustSpot, -FacingDirection, dashInAir ? 1.2f : 1.6f, dashInAir ? 0.7f : 1f);
        if (!drewDust && !dashInAir) GameFeel.Dust(Feet(), 4, -FacingDirection);

        // Make the pupcat see-through during the roll, so you can see the invincible window.
        if (sprite != null)
        {
            Color c = normalColor;
            c.a = 0.4f;
            sprite.color = c;
        }
    }

    // Ends the roll right now. The attack and parry scripts use this to cancel out of a roll.
    public void CancelDodge()
    {
        IsDodging = false;
        if (sprite != null) sprite.color = normalColor;
    }

    // Squashes the pupcat down (smaller body, smaller hitbox) or stands back up, keeping the feet planted.
    void SetCrouch(bool on)
    {
        float feet = (body != null) ? body.bounds.min.y : transform.position.y;
        transform.localScale = new Vector3(standingScale.x, standingScale.y * (on ? crouchHeight : 1f), standingScale.z);
        Physics2D.SyncTransforms();
        if (body != null) transform.position += new Vector3(0f, feet - body.bounds.min.y, 0f);
        IsCrouching = on;
    }

    // Called when the pupcat gets hit: shoves it and briefly takes away steering.
    public void Knockback(Vector2 velocity, float duration)
    {
        if (IsDodging) CancelDodge();
        if (IsCrouching) SetCrouch(false);
        knockbackEndTime = Time.time + duration;
        rb.linearVelocity = velocity;
    }

    // A sideways shove that keeps the current up/down speed. Used for strike lunges and parry staggers.
    // xVelocity is in world direction (positive = right).
    public void Shove(float xVelocity, float duration)
    {
        knockbackEndTime = Time.time + duration;
        rb.linearVelocity = new Vector2(xVelocity, rb.linearVelocity.y);
    }

    // Called by Unity when we touch something.
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only count it as ground if the surface is pointing mostly upward.
        if (collision.GetContact(0).normal.y > 0.5f)
        {
            // Landing after a real fall (not just walking over a bump) gets a thud and some dust.
            if (!isGrounded && Mathf.Abs(collision.relativeVelocity.y) > 4f)
            {
                GameFeel.Play(GameFeel.Sound.Land);
                GameFeel.Dust(Feet(), 4, 0f);
            }
            isGrounded = true;
        }
    }

    // The point at the bottom of the pupcat, where dust should appear.
    Vector3 Feet()
    {
        float y = (body != null) ? body.bounds.min.y : transform.position.y - 0.5f;
        return new Vector3(transform.position.x, y, 0f);
    }

    // Called by Unity when we stop touching something.
    void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
}