using UnityEditor.Callbacks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpiritFlame : MonoBehaviour
{
    public GameObject flameParticle;
    public GameObject flameDeathEffect;

    private bool hitPlayer;

    public Transform target; // the transform components of the target (player)
    [Header("Stats")]
    public float travelSpeed = 5f; // the speed at which the flame travels

    public float rotateSpeed = 200f; // how fast the flame can rotate to target

    public float lifetime; // how long the flame lasts (not implemented yet)
    public int damage = 1; // how much damage it deals on contact with player

    public Collider2D flameCollider;
    private Rigidbody2D rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
       
    }

    // Update is called once per frame
    void FixedUpdate()
    { // calculates direction by getting target's position (player) and
      // subtracting by flame's position
        Vector2 direction = (Vector2)target.position - rb.position; 

        direction.Normalize(); //makes sure direction is 1


        // calculates the rotate amount by getting the cross product of direction
        // and the transform.up on the z axis 
        float rotateAmount = Vector3.Cross(direction, transform.up).z;

        this.flameParticle.transform.rotation = Quaternion.identity;

        // changes the angular velocity of the flame to the player by minus
        // rotate amount times rotateSpeed
        rb.angularVelocity = -rotateAmount * rotateSpeed;

        // travels towards the player based on travel speed
        rb.linearVelocity = transform.up * travelSpeed;

        if(lifetime > 0)
        {
            lifetime -= Time.deltaTime;
        }
        else
        {
            flameDespawn();
        }
    }

    void flameDespawn()
    {
        Debug.Log("Spirit Flame Destroyed");
        Instantiate(flameDeathEffect, transform.position, transform.rotation);
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hitPlayer) return;

        PupcatController player = other.GetComponentInParent<PupcatController>();
        if (player == null) return;

        hitPlayer = true;

        if (!player.IsInvincible)
        {
            PupcatHealth health = player.GetComponent<PupcatHealth>();
            if (health != null)
            {
                float pushDirection =
                    player.transform.position.x >= transform.position.x ? 1f : -1f;
                health.TakeDamage(damage, pushDirection);
            }
        }

        flameDespawn();
    }
}
