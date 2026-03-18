using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns fading ghost sprites behind the player during dodge.
/// Attach to the player prefab. Works only on the local client (visual only).
/// </summary>
public class DodgeGhostEffect : MonoBehaviour
{
    [Header("Ghost Settings")]
    public int ghostCount = 3;
    public float ghostInterval = 0.06f;
    public float ghostFadeDuration = 0.25f;
    public Color ghostColor = new Color(0.5f, 0.8f, 1f, 0.6f);

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SpawnGhosts(float dodgeDuration)
    {
        StartCoroutine(GhostRoutine(dodgeDuration));
    }

    private IEnumerator GhostRoutine(float dodgeDuration)
    {
        int count = Mathf.Max(1, ghostCount);
        float interval = dodgeDuration / count;

        for (int i = 0; i < count; i++)
        {
            SpawnOneGhost();
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnOneGhost()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        var ghostObj = new GameObject("DodgeGhost");
        ghostObj.transform.position = transform.position;
        ghostObj.transform.rotation = transform.rotation;
        ghostObj.transform.localScale = transform.localScale;

        var ghostSr = ghostObj.AddComponent<SpriteRenderer>();
        ghostSr.sprite = spriteRenderer.sprite;
        ghostSr.flipX = spriteRenderer.flipX;
        ghostSr.flipY = spriteRenderer.flipY;
        ghostSr.color = ghostColor;
        ghostSr.sortingLayerID = spriteRenderer.sortingLayerID;
        ghostSr.sortingOrder = spriteRenderer.sortingOrder - 1;
        ghostSr.material = spriteRenderer.material;

        StartCoroutine(FadeAndDestroy(ghostSr, ghostFadeDuration));
    }

    private IEnumerator FadeAndDestroy(SpriteRenderer sr, float duration)
    {
        float startAlpha = sr.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            var c = sr.color;
            c.a = Mathf.Lerp(startAlpha, 0f, t);
            sr.color = c;
            yield return null;
        }

        Destroy(sr.gameObject);
    }
}
