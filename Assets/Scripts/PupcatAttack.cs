using UnityEngine;

// The pupcat's strikes. Left click (or Square on a PS4 controller):
//   Tap, tap, tap      a 3-hit combo. Each hit is a little stronger; the 3rd is a finisher.
//   Hold, then release  a charged heavy strike (a glow shows when it is ready).
//   Crouch + tap        a quick low strike (2/3 of a normal strike's damage).
//   Tap while jumping   an air strike.
//   Tap while rolling   a dash strike (cancels the roll).
// A strike pressed just before the last one ends is remembered and fires right after.
// Attach this to the Pupcat object, next to PupcatController.
[RequireComponent(typeof(PupcatController))]
public class PupcatAttack : MonoBehaviour
{
    [Header("Strike")]
    public float reach = 1.5f;      // how far in front a normal strike reaches
    public float damage = 10f;      // health taken off the enemy by a normal strike. Everything below is a multiple of this.

    [Header("Click combo (tap again in time)")]
    public float[] comboMultipliers = { 1f, 1.2f, 1.8f }; // damage of hit 1, 2, 3. The last one is the finisher.
    public float hitGap = 0.28f;          // wait after hit 1 and 2 before the next can start
    public float finisherRecovery = 0.55f; // wait after the finisher
    public float comboWindow = 0.6f;      // how long after a hit you have to start the next one
    public float bufferTime = 0.2f;       // a click this early before recovery ends is remembered

    [Header("Hold to charge")]
    public float chargeTime = 0.6f;       // how long to hold for a heavy strike
    public float heavyMultiplier = 2.2f;
    public float heavyReach = 1.9f;
    public float heavyRecovery = 0.7f;

    [Header("Crouch strike")]
    public float lowMultiplier = 0.67f;   // 2/3 of a normal strike
    public float lowRecovery = 0.3f;

    [Header("Air strike and dash strike")]
    public float airMultiplier = 1f;
    public float airRecovery = 0.35f;
    public float dashMultiplier = 1.3f;
    public float dashReach = 2f;
    public float dashLunge = 10f;         // forward speed of the dash strike
    public float dashRecovery = 0.4f;

    private PupcatController controller;
    private PupcatParry parry;
    private float nextAttackTime;

    // Combo memory.
    private int comboIndex;
    private float comboExpire;

    // Hold and buffer memory.
    private bool holding;
    private float holdStart;
    private bool chargeReady;
    private bool chargeSoundPlayed;   // the "loading" sound has started for this hold
    private bool queued;
    private float queuedHeld;
    private float queuedUntil;

    // A quick flash in front of the pupcat so you can see the strike, and a glow while charging.
    private SpriteRenderer slash;
    private SpriteRenderer glow;
    private float slashHideTime;
    private static Sprite boxSprite;

    void Awake()
    {
        controller = GetComponent<PupcatController>();
        parry = GetComponent<PupcatParry>();
        slash = MakeBox("StrikeFlash", 7, new Color(1f, 1f, 1f, 0.55f));
        glow = MakeBox("ChargeGlow", 5, new Color(1f, 0.85f, 0.3f, 0f));
        glow.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
    }

    void OnDisable()
    {
        holding = false;
        queued = false;
        GameFeel.StopHeld();
        if (glow != null) glow.enabled = false;
        if (slash != null) slash.enabled = false;
    }

    void Update()
    {
        if (slash.enabled && Time.time >= slashHideTime) slash.enabled = false;

        // Getting hit, or raising the guard, cancels a charge.
        if (holding && (controller.IsKnockedBack || (parry != null && parry.IsParrying)))
        {
            holding = false;
            glow.enabled = false;
            GameFeel.StopHeld();
        }

        if (GameInput.AttackPressed)
        {
            holding = true;
            holdStart = Time.time;
            chargeReady = false;
            chargeSoundPlayed = false;
        }

        if (holding)
        {
            float held = Time.time - holdStart;
            ShowCharge(held);

            // The strike happens when the button is let go: a quick tap is a normal strike,
            // a long hold is the heavy strike.
            if (!GameInput.AttackHeld)
            {
                holding = false;
                glow.enabled = false;
                GameFeel.StopHeld(); // the loading sound ends the moment the button is let go
                Request(held);
            }
        }

        // A click that came a hair early waits here and fires the moment recovery ends.
        if (queued)
        {
            if (Time.time > queuedUntil)
            {
                queued = false;
            }
            else if (Time.time >= nextAttackTime)
            {
                queued = false;
                Resolve(queuedHeld);
            }
        }
    }

    void Request(float held)
    {
        if (Time.time >= nextAttackTime)
        {
            Resolve(held);
        }
        else if (nextAttackTime - Time.time <= bufferTime)
        {
            queued = true;
            queuedHeld = held;
            queuedUntil = Time.time + bufferTime;
        }
    }

    // Picks which strike to do from what the pupcat is doing right now.
    void Resolve(float held)
    {
        if (controller.IsDodging) DashStrike();
        else if (!controller.IsGrounded) AirStrike();
        else if (controller.IsCrouching) LowStrike();
        else if (held >= chargeTime) HeavyStrike();
        else ComboStrike();
    }

    void ComboStrike()
    {
        if (Time.time > comboExpire) comboIndex = 0; // too slow: the combo starts over

        int last = Mathf.Max(0, comboMultipliers.Length - 1);
        int i = Mathf.Clamp(comboIndex, 0, last);
        bool finisher = (i == last) && last > 0;

        // Each hit of the combo has its own pair of sounds: one for hitting air, one for hitting an enemy.
        GameFeel.Sound miss, hit;
        if (finisher) { miss = GameFeel.Sound.FinisherMiss; hit = GameFeel.Sound.FinisherHit; }
        else if (i == 0) { miss = GameFeel.Sound.Combo1Miss; hit = GameFeel.Sound.Combo1Hit; }
        else { miss = GameFeel.Sound.Combo2Miss; hit = GameFeel.Sound.Combo2Hit; }

        controller.Shove(controller.FacingDirection * (finisher ? 3f : 1.5f), 0.08f); // a little step forward
        Strike(comboMultipliers[i], reach, 1.2f, 0f,
               finisher ? finisherRecovery : hitGap,
               finisher ? 2f : 1f,
               miss, hit,
               finisher ? new Color(1f, 0.75f, 0.3f, 0.7f) : new Color(1f, 1f, 1f, 0.55f));

        comboIndex = finisher ? 0 : i + 1;
        comboExpire = nextAttackTime + comboWindow;
    }

    void HeavyStrike()
    {
        controller.Shove(controller.FacingDirection * 4f, 0.12f);
        Strike(heavyMultiplier, heavyReach, 1.4f, 0f, heavyRecovery, 2f,
               GameFeel.Sound.HeavyMiss, GameFeel.Sound.HeavyHit, new Color(1f, 0.75f, 0.3f, 0.7f));
        comboIndex = 0;
    }

    void LowStrike()
    {
        Strike(lowMultiplier, reach, 0.8f, -0.1f, lowRecovery, 1f,
               GameFeel.Sound.CrouchMiss, GameFeel.Sound.CrouchHit, new Color(0.7f, 0.9f, 1f, 0.55f));
    }

    void AirStrike()
    {
        Strike(airMultiplier, reach, 1.6f, -0.2f, airRecovery, 1f,
               GameFeel.Sound.AirMiss, GameFeel.Sound.AirHit, new Color(1f, 1f, 1f, 0.55f));
    }

    void DashStrike()
    {
        controller.CancelDodge();
        controller.Shove(controller.FacingDirection * dashLunge, 0.12f);
        Strike(dashMultiplier, dashReach, 1.2f, 0f, dashRecovery, 1.3f,
               GameFeel.Sound.DashStrikeMiss, GameFeel.Sound.DashStrikeHit, new Color(1f, 1f, 1f, 0.7f));
    }

    // One strike: a box in front of the pupcat. Every enemy inside it takes the hit.
    // power scales the shove, the freeze and the shake that the hit causes (1 = normal).
    void Strike(float multiplier, float strikeReach, float height, float yOffset,
                float recovery, float power, GameFeel.Sound missSound, GameFeel.Sound hitSound, Color flash)
    {
        nextAttackTime = Time.time + recovery;

        float forward = 0.5f + strikeReach / 2f;
        Vector2 center = (Vector2)transform.position + new Vector2(controller.FacingDirection * forward, yOffset);
        Collider2D[] found = Physics2D.OverlapBoxAll(center, new Vector2(strikeReach, height), 0f);

        ShowSlash(center, new Vector2(strikeReach, height), flash);

        // Does the strike actually reach an enemy that can still be hit?
        bool connected = false;
        foreach (Collider2D c in found)
        {
            Enemy e = c.GetComponent<Enemy>();
            if (e != null && e.CurrentState != Enemy.State.Defeated && e.CurrentState != Enemy.State.Exhausted) connected = true;
        }

        // Cyan projectiles are knocked back at the thrower by any strike.
        if (Projectile.TryStrike(center, new Vector2(strikeReach, height), hitSound)) connected = true;

        // One sound for hitting air. When the strike lands, the enemy plays the hit sound instead.
        if (!connected) GameFeel.Play(missSound);

        float amount = damage * multiplier;
        foreach (Collider2D c in found)
        {
            Enemy enemy = c.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeHit(amount, power, hitSound);
        }
    }

    // While the button is held, a yellow glow grows. It pings and pulses when the heavy strike is ready.
    void ShowCharge(float held)
    {
        if (held < 0.12f) return;

        glow.enabled = true;

        // The "loading" sound starts once the button has been held a moment (a quick tap never makes it).
        if (!chargeSoundPlayed)
        {
            chargeSoundPlayed = true;
            GameFeel.PlayHeld(GameFeel.Sound.HeavyCharge);
        }

        float k = Mathf.Clamp01(held / chargeTime);
        float alpha = Mathf.Lerp(0.1f, 0.4f, k);

        if (k >= 1f)
        {
            if (!chargeReady)
            {
                chargeReady = true;
                GameFeel.Play(GameFeel.Sound.Guard);
            }
            alpha = 0.55f + Mathf.Sin(Time.time * 30f) * 0.15f;
        }

        Color c = glow.color;
        c.a = alpha;
        glow.color = c;
    }

    void ShowSlash(Vector2 center, Vector2 size, Color color)
    {
        // Set in world units, so it still looks right while the pupcat is squashed down to crouch.
        Vector3 s = transform.lossyScale;
        slash.transform.position = new Vector3(center.x, center.y, 0f);
        slash.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, Mathf.Abs(s.x)),
                                                 size.y / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f);
        slash.color = color;
        slash.enabled = true;
        slashHideTime = Time.time + 0.08f;
    }

    SpriteRenderer MakeBox(string boxName, int order, Color color)
    {
        GameObject go = new GameObject(boxName);
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBoxSprite();
        sr.color = color;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    // A 1x1 white pixel, centered, that scales into a box.
    static Sprite GetBoxSprite()
    {
        if (boxSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            boxSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
        return boxSprite;
    }
}