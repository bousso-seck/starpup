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

    void Awake()
    {
        controller = GetComponent<PupcatController>();
    }

    void Update()
    {
        if (!GameInput.AttackPressed || Time.time < nextAttackTime || controller.IsDodging) return;

        nextAttackTime = Time.time + cooldown;

        // A box just in front of the pupcat. Anything with an Enemy script inside gets hit.
        Vector2 center = (Vector2)transform.position + Vector2.right * controller.FacingDirection * (0.5f + reach / 2f);
        Collider2D[] found = Physics2D.OverlapBoxAll(center, new Vector2(reach, 1.2f), 0f);

        foreach (Collider2D c in found)
        {
            Enemy enemy = c.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeHit(damage);
        }
    }
}