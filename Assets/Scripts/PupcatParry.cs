using UnityEngine;

// The parry: right-click (or press the X button on a controller) to raise a short guard window.
// If an enemy's swing lands while the guard is up, it is parried instead of hurting you.
// Miss the timing and you are stuck in a short cooldown, so mashing is not free.
// Attach this to the Pupcat, next to PupcatController.
[RequireComponent(typeof(PupcatController))]
public class PupcatParry : MonoBehaviour
{
    public float parryWindow = 0.3f; // how long the guard stays up (seconds)
    public float cooldown = 0.6f;    // wait before you can parry again, hit or miss

    public bool IsParrying { get { return Time.time < parryEndTime; } }

    private PupcatController controller;
    private PupcatHealth health;
    private SpriteRenderer shield;
    private float parryEndTime;
    private float nextParryTime;
    private static Sprite shieldSprite;

    void Awake()
    {
        controller = GetComponent<PupcatController>();
        health = GetComponent<PupcatHealth>();
        shield = MakeShield();
    }

    void Update()
    {
        if (health != null && health.IsDefeated)
        {
            shield.enabled = false;
            return;
        }

        if (GameInput.ParryPressed && Time.time >= nextParryTime && !controller.IsDodging)
        {
            parryEndTime = Time.time + parryWindow;
            nextParryTime = Time.time + cooldown;
            GameFeel.Play(GameFeel.Sound.Guard);
        }

        // A cyan box around the pupcat shows the guard is up.
        shield.enabled = IsParrying;
    }

    SpriteRenderer MakeShield()
    {
        GameObject go = new GameObject("ParryShield");
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(1.4f, 1.4f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetShieldSprite();
        sr.color = new Color(0.3f, 0.9f, 1f, 0.4f);
        sr.sortingOrder = 6;
        sr.enabled = false;
        return sr;
    }

    // A 1x1 white pixel, centered, that scales into a box.
    static Sprite GetShieldSprite()
    {
        if (shieldSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            shieldSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
        return shieldSprite;
    }
}