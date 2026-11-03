using UnityEngine;

// Lets the pupcat strike for real with a left mouse click (or the Y button on a controller).
// Attach this to the Pupcat object, next to PupcatController.
[RequireComponent(typeof(PupcatController))]
public class PupcatAttack : MonoBehaviour
{
    public float reach = 1.5f;      // how far in front the strike reaches
    public float damage = 10f;      // health taken off the enemy per strike
    public float cooldown = 0.4f;   // wait between strikes

    private PupcatController controller;
    private float nextAttackTime;

    // A quick white flash in front of the pupcat so you can see the strike.
    private SpriteRenderer slash;
    private float slashHideTime;
    private static Sprite slashSprite;

    void Awake()
    {
        controller = GetComponent<PupcatController>();
        slash = MakeSlash();
    }

    void Update()
    {
        if (slash.enabled && Time.time >= slashHideTime) slash.enabled = false;

        if (!GameInput.AttackPressed || Time.time < nextAttackTime || controller.IsDodging) return;

        nextAttackTime = Time.time + cooldown;

        // A box just in front of the pupcat. Anything with an Enemy script inside gets hit.
        float forward = 0.5f + reach / 2f;
        Vector2 center = (Vector2)transform.position + Vector2.right * controller.FacingDirection * forward;
        Collider2D[] found = Physics2D.OverlapBoxAll(center, new Vector2(reach, 1.2f), 0f);

        ShowSlash(controller.FacingDirection * forward);
        GameFeel.Play(GameFeel.Sound.Swing);

        foreach (Collider2D c in found)
        {
            Enemy enemy = c.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeHit(damage);
        }
    }

    void ShowSlash(float localX)
    {
        slash.transform.localPosition = new Vector3(localX, 0f, 0f);
        slash.transform.localScale = new Vector3(reach, 1.2f, 1f);
        slash.enabled = true;
        slashHideTime = Time.time + 0.08f;
    }

    SpriteRenderer MakeSlash()
    {
        GameObject go = new GameObject("StrikeFlash");
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSlashSprite();
        sr.color = new Color(1f, 1f, 1f, 0.55f);
        sr.sortingOrder = 7;
        sr.enabled = false;
        return sr;
    }

    // A 1x1 white pixel, centered, that scales into a box.
    static Sprite GetSlashSprite()
    {
        if (slashSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            slashSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
        return slashSprite;
    }
}