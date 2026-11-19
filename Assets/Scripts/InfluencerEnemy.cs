using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class InfluencerEnemy : MonoBehaviour
{
    [Header("When it fights")]
    public float noticeRange = 7f;
    public float moveSpeed = 4f;
    public float firstAttackDelay = 0.5f;

    [Header("The cone attack")]
    public float coneRange = 6f;
    [Range(1f, 179f)] public float coneAngle = 55f;
    public float attackHeight = 1f;
    public float windUpTime = 0.8f;
    public float recoverTime = 1.4f;
    public int damage = 1;
    public float blindDuration = 2f;

    private enum Phase { Waiting, WindUp, Recover, Done }
    private Phase phase = Phase.Waiting;

    private Enemy enemy;
    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private PupcatController player;
    private PupcatHealth playerHealth;
    private Collider2D attackZone;
    private MeshRenderer coneRenderer;
    private Mesh coneMesh;
    private Material coneMaterial;
    private Collider2D[] overlapResults = new Collider2D[8];
    private ContactFilter2D overlapFilter;
    private float phaseEnd;
    private float attackVisualEndTime;
    private float facing = 1f;

    void Start()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        player = FindFirstObjectByType<PupcatController>();

        if (player == null)
        {
            Debug.LogError("No PupcatController found in the scene.", this);
            enabled = false;
            return;
        }

        playerHealth = player.GetComponent<PupcatHealth>();
        if (playerHealth == null)
        {
            Debug.LogError("The PupcatController has no PupcatHealth component.", player);
            enabled = false;
            return;
        }

        enemy.brainPaused = true;
        CreateAttackZone();
    }

    void Update()
    {
        if (enemy.CurrentState == Enemy.State.Defeated || enemy.CurrentState == Enemy.State.Exhausted)
        {
            phase = Phase.Done;
            enemy.brainPaused = false;
            if (attackZone != null) attackZone.enabled = false;
            if (coneRenderer != null) coneRenderer.enabled = false;
            return;
        }
        if (phase == Phase.Done) return;
        if (phase != Phase.WindUp && coneRenderer != null && Time.time >= attackVisualEndTime)
            coneRenderer.enabled = false;

        float dx = player.transform.position.x - transform.position.x;
        float distance = Mathf.Abs(dx);
        if (distance > 0.01f) facing = Mathf.Sign(dx);
        if (sprite != null) sprite.flipX = facing < 0f;

        switch (phase)
        {
            case Phase.Waiting:
                Stand();
                if (distance <= noticeRange)
                {
                    enemy.Wake();
                    phase = Phase.Recover;
                    phaseEnd = Time.time + firstAttackDelay;
                }
                break;

            case Phase.WindUp:
                Stand();
                PositionAttackZone();
                if (coneRenderer != null && coneMaterial != null)
                {
                    float flash = Mathf.PingPong(Time.time * 10f, 1f);
                    coneMaterial.color = Color.Lerp(new Color(1f, 0.1f, 0.65f, 0.18f),
                        new Color(1f, 0.1f, 0.65f, 0.42f), flash);
                }
                if (sprite != null)
                {
                    float flash = Mathf.PingPong(Time.time * 10f, 1f);
                    sprite.color = Color.Lerp(enemy.data.bodyColor, Color.white, 0.3f + 0.5f * flash);
                }
                if (Time.time >= phaseEnd) Attack();
                break;

            case Phase.Recover:
                if (distance > coneRange * 0.8f)
                    rb.linearVelocity = new Vector2(facing * moveSpeed, rb.linearVelocity.y);
                else
                    Stand();

                if (Time.time >= phaseEnd) StartWindUp();
                break;
        }
    }

    void StartWindUp()
    {
        phase = Phase.WindUp;
        phaseEnd = Time.time + windUpTime;
        PositionAttackZone();
        if (coneRenderer != null) coneRenderer.enabled = true;
        if (sprite != null) sprite.color = enemy.data.bodyColor;
        GameFeel.Play(GameFeel.Sound.EnemyWindup);
    }

    void Attack()
    {
        phase = Phase.Recover;
        phaseEnd = Time.time + recoverTime;

        PositionAttackZone();
        attackZone.enabled = true;
        Physics2D.SyncTransforms();

        if (!player.IsInvincible)
        {
            int count = attackZone.Overlap(overlapFilter, overlapResults);
            for (int i = 0; i < count; i++)
            {
                if (overlapResults[i] == null ||
                    overlapResults[i].GetComponentInParent<PupcatController>() != player) continue;

                playerHealth.TakeDamageAndBlind(damage, facing, blindDuration);
                break;
            }
        }

        attackZone.enabled = false;
        if (coneRenderer != null && coneMaterial != null)
        {
            coneMaterial.color = new Color(1f, 0.05f, 0.3f, 0.5f);
            coneRenderer.enabled = true;
            attackVisualEndTime = Time.time + 0.18f;
        }
        if (sprite != null) sprite.color = enemy.data.bodyColor;
        GameFeel.Play(GameFeel.Sound.Flashbang);
    }

    void CreateAttackZone()
    {
        GameObject zone = new GameObject("InfluencerConeHitbox");
        zone.layer = gameObject.layer;
        zone.transform.SetParent(transform, false);

        PolygonCollider2D polygon = zone.AddComponent<PolygonCollider2D>();
        polygon.isTrigger = true;
        polygon.enabled = false;

        MeshFilter meshFilter = zone.AddComponent<MeshFilter>();
        coneRenderer = zone.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("Could not find the Sprites/Default shader for the influencer cone visual.", this);
            coneRenderer.enabled = false;
        }
        else
        {
            coneMaterial = new Material(shader);
            coneMaterial.color = new Color(1f, 0.1f, 0.65f, 0.25f);
            coneRenderer.sharedMaterial = coneMaterial;
            coneRenderer.sortingLayerID = sprite != null ? sprite.sortingLayerID : 0;
            coneRenderer.sortingOrder = sprite != null ? sprite.sortingOrder + 1 : 0;
            coneRenderer.enabled = false;
        }

        const int segments = 16;
        Vector2[] points = new Vector2[segments + 2];
        points[0] = Vector2.zero;
        Vector3[] vertices = new Vector3[points.Length];
        vertices[0] = Vector3.zero;
        float halfAngle = coneAngle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments) * Mathf.Deg2Rad;
            points[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * coneRange;
            vertices[i + 1] = points[i + 1];
        }
        polygon.SetPath(0, points);
        attackZone = polygon;

        int[] triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }
        coneMesh = new Mesh { name = "InfluencerConeVisual" };
        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateBounds();
        meshFilter.sharedMesh = coneMesh;

        overlapFilter = new ContactFilter2D { useTriggers = true };
    }

    void PositionAttackZone()
    {
        attackZone.transform.position = transform.position + Vector3.up * attackHeight;
        attackZone.transform.rotation = Quaternion.Euler(0f, 0f, facing < 0f ? 180f : 0f);
    }

    void OnDestroy()
    {
        if (coneMesh != null) Destroy(coneMesh);
        if (coneMaterial != null) Destroy(coneMaterial);
    }

    void Stand()
    {
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, 0f, 40f * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * attackHeight;
        float direction = Application.isPlaying ? facing : (GetComponent<SpriteRenderer>() != null && GetComponent<SpriteRenderer>().flipX ? -1f : 1f);
        float halfAngle = coneAngle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, 0f, direction < 0f ? 180f - halfAngle : halfAngle) * Vector3.right;
        Vector3 right = Quaternion.Euler(0f, 0f, direction < 0f ? 180f + halfAngle : -halfAngle) * Vector3.right;
        Gizmos.color = new Color(1f, 0.25f, 0.8f, 0.9f);
        Gizmos.DrawLine(origin, origin + left * coneRange);
        Gizmos.DrawLine(origin, origin + right * coneRange);
        Gizmos.DrawWireSphere(origin + Vector3.right * direction * coneRange, 0.08f);
    }
}
