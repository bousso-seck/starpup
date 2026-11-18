using UnityEngine;

// A puddle of wet concrete traps the pupcat until enough jump presses break them free.
// Place this object at floor level; its width and trigger area are set by Area Size.
[RequireComponent(typeof(BoxCollider2D))]
public class WetConcrete : MonoBehaviour
{
    [Header("Patch")]
    public Vector2 areaSize = new Vector2(4f, 0.3f);
    public Color concreteColor = new Color(0.25f, 0.32f, 0.34f, 0.85f);
    public Color escapeColor = new Color(0.65f, 0.85f, 0.85f, 0.95f);

    [Header("Escape")]
    public int jumpPressesToEscape = 6;

    private BoxCollider2D area;
    private SpriteRenderer patch;
    private PupcatController trappedPlayer;
    private PupcatController escapedPlayer;

    private static Sprite puddleSprite;

    void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = areaSize;
        area.offset = new Vector2(0f, areaSize.y / 2f);

        GameObject visual = new GameObject("WetConcreteVisual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0f, areaSize.y / 2f, 0f);
        visual.transform.localScale = new Vector3(areaSize.x, areaSize.y * 1.8f, 1f);

        patch = visual.AddComponent<SpriteRenderer>();
        patch.sprite = GetPuddleSprite();
        patch.color = concreteColor;
        patch.sortingOrder = 2;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryTrap(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryTrap(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PupcatController player = other.GetComponent<PupcatController>();
        if (player == null) player = other.GetComponentInParent<PupcatController>();
        if (player == escapedPlayer) escapedPlayer = null;
    }

    void OnDisable()
    {
        if (trappedPlayer != null) trappedPlayer.ReleaseFromWetConcrete(this);
        trappedPlayer = null;
        escapedPlayer = null;
    }

    void TryTrap(Collider2D other)
    {
        PupcatController player = other.GetComponent<PupcatController>();
        if (player == null) player = other.GetComponentInParent<PupcatController>();
        if (player == null || player == escapedPlayer || player.IsStuckInWetConcrete) return;

        trappedPlayer = player;
        player.TrapInWetConcrete(this, jumpPressesToEscape);
    }

    public void SetEscapeProgress(float progress)
    {
        if (patch != null) patch.color = Color.Lerp(concreteColor, escapeColor, progress);
    }

    public void PlayerEscaped(PupcatController player)
    {
        if (trappedPlayer == player) trappedPlayer = null;
        escapedPlayer = player;
        SetEscapeProgress(0f);
    }

    static Sprite GetPuddleSprite()
    {
        if (puddleSprite == null)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / (size / 2f);
                    float dy = (y - center) / (size / 2f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - distance) * size / 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            puddleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return puddleSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.75f, 0.8f, 0.9f);
        Gizmos.DrawWireCube(
            transform.position + new Vector3(0f, areaSize.y / 2f, 0f),
            new Vector3(areaSize.x, areaSize.y, 0f));
    }
}
