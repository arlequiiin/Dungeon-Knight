using UnityEngine;

/// <summary>
/// Dynamically sets sortingOrder based on Y position every frame.
/// Lower Y = higher sortingOrder = drawn on top (closer to camera).
/// Attach to any GameObject with a SpriteRenderer (players, mobs, decorations).
/// </summary>
public class SortByY : MonoBehaviour
{
    /// <summary>
    /// Precision multiplier. Higher = finer sorting granularity.
    /// 100 means 0.01 unit Y difference is enough to change order.
    /// </summary>
    private const int Precision = 100;

    /// <summary>
    /// Base offset added to sortingOrder. Use to shift entire categories
    /// (e.g., decorations vs characters) if needed.
    /// </summary>
    [Tooltip("Base offset so sprites always render above tilemaps (sortingOrder 0)")]
    public int offset = 10000;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = -(int)(transform.position.y * Precision) + offset;
    }
}
