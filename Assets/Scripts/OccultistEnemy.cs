
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class OccultistEnemy : MonoBehaviour
{
    [Header("Spirit Flame")]
    public GameObject spiritFlamePrefab;
    public int maxFlames = 4;
    public float summonInterval = 3f;
    public float detectionRange = 10f;
    public float flameSpeed = 3f;
    public float flameLifetime = 6f;
    public int flameDamage = 1;

    private Enemy enemy;
    private Transform player;
    private float nextSummon;
    private readonly List<GameObject> flames = new();

    void Start()
    {
        enemy = GetComponent<Enemy>();

        PupcatController pupcat =
            FindFirstObjectByType<PupcatController>();

        if (pupcat != null)
            player = pupcat.transform;

        nextSummon = Time.time + 1f;
    }

    void Update()
    {
        flames.RemoveAll(flame => flame == null);

        if (enemy == null || !enemy.enabled ||
            enemy.CurrentState == Enemy.State.Defeated ||
            enemy.CurrentState == Enemy.State.Exhausted)
        {
            ClearFlames();
            enabled = false;
            return;
        }

        if (player == null || spiritFlamePrefab == null)
            return;

        if (Vector2.Distance(transform.position, player.position)
            > detectionRange)
            return;

        if (Time.time < nextSummon || flames.Count >= maxFlames)
            return;

        Vector3 spawnPosition =
            transform.position + new Vector3(0, 1.2f, 0);

        GameObject flame =
            Instantiate(spiritFlamePrefab, spawnPosition,
                Quaternion.identity);

        flame.name = "Occultist Spirit Flame";

        // Keep the original prefab intact.
        OccultistFlame behavior =
            flame.AddComponent<OccultistFlame>();

        behavior.Initialize(
            player, flameSpeed, flameLifetime, flameDamage
        );

        flames.Add(flame);
        nextSummon = Time.time + summonInterval;
    }

    void ClearFlames()
    {
        foreach (GameObject flame in flames)
        {
            if (flame != null)
                Destroy(flame);
        }

        flames.Clear();
    }

    void OnDestroy()
    {
        ClearFlames();
    }
}

public class OccultistFlame : MonoBehaviour
{
    private Transform target;
    private float speed;
    private float lifetime;
    private int damage;
    private float spawnTime;

    public void Initialize(
        Transform player,
        float moveSpeed,
        float lifeSeconds,
        int damagePoints)
    {
        target = player;
        speed = moveSpeed;
        lifetime = lifeSeconds;
        damage = damagePoints;
        spawnTime = Time.time;

        // The flame prefab is a visual effect.
        // Add a small trigger hitbox for gameplay.
        CircleCollider2D hitbox =
            gameObject.AddComponent<CircleCollider2D>();

        hitbox.isTrigger = true;
        hitbox.radius = 0.35f;

        Rigidbody2D body =
            gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0;
    }

    void Update()
    {
        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = target.position;
        destination.z = transform.position.z;

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime
        );
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PupcatHealth health =
            other.GetComponentInParent<PupcatHealth>();

        if (health == null)
            return;

        float direction =
            Mathf.Sign(other.transform.position.x -
                       transform.position.x);

        health.TakeDamage(damage, direction);
        Destroy(gameObject);
    }
}
