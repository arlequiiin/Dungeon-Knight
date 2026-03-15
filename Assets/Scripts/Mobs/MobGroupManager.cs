using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Менеджер групповой координации мобов в комнате.
/// Два слота атаки: один спереди игрока, один сзади.
/// Остальные мобы кружат вокруг, ожидая своей очереди.
/// </summary>
public class MobGroupManager : MonoBehaviour
{
    [Header("Позиционирование")]
    public float circleRadius = 2.5f;
    public float circleSpeed = 1.5f;

    private readonly List<MobAI> aliveMobs = new();

    // Два слота: спереди и сзади игрока
    private MobAI frontSlot;
    private MobAI backSlot;

    public void Register(MobAI mob)
    {
        if (!aliveMobs.Contains(mob))
            aliveMobs.Add(mob);
    }

    public void Unregister(MobAI mob)
    {
        aliveMobs.Remove(mob);
        if (frontSlot == mob) frontSlot = null;
        if (backSlot == mob) backSlot = null;
    }

    /// <summary>
    /// Запрашивает слот атаки. Моб назначается на ближайшую к нему сторону (спереди/сзади).
    /// </summary>
    public bool RequestAttackSlot(MobAI mob, Transform target)
    {
        // Уже имеет слот
        if (frontSlot == mob || backSlot == mob) return true;

        // Оба заняты
        if (frontSlot != null && backSlot != null) return false;

        // Определяем, с какой стороны моб относительно игрока
        bool mobIsRight = mob.transform.position.x >= target.position.x;

        // Определяем куда смотрит игрок (flipX = true → смотрит влево)
        var playerSprite = target.GetComponent<SpriteRenderer>();
        bool playerLooksRight = playerSprite == null || !playerSprite.flipX;

        // "Спереди" = та сторона куда смотрит игрок
        bool mobIsFront = (playerLooksRight && mobIsRight) || (!playerLooksRight && !mobIsRight);

        if (mobIsFront)
        {
            if (frontSlot == null) { frontSlot = mob; return true; }
            if (backSlot == null)  { backSlot = mob; return true; }
        }
        else
        {
            if (backSlot == null)  { backSlot = mob; return true; }
            if (frontSlot == null) { frontSlot = mob; return true; }
        }

        return false;
    }

    public void ReleaseAttackSlot(MobAI mob)
    {
        if (frontSlot == mob) frontSlot = null;
        if (backSlot == mob) backSlot = null;
    }

    public bool HasFreeSlot => frontSlot == null || backSlot == null;

    /// <summary>
    /// Позиция для кружения. Мобы без слота равномерно распределяются по окружности.
    /// </summary>
    public Vector2 GetCirclePosition(MobAI mob, Vector2 targetPos)
    {
        int waitingIndex = 0;
        int waitingCount = 0;

        for (int i = 0; i < aliveMobs.Count; i++)
        {
            var m = aliveMobs[i];
            if (m == frontSlot || m == backSlot) continue;
            if (m == mob) waitingIndex = waitingCount;
            waitingCount++;
        }

        if (waitingCount == 0) return targetPos;

        float angleStep = 360f / waitingCount;
        float angle = (angleStep * waitingIndex + Time.time * circleSpeed * 30f) * Mathf.Deg2Rad;

        return targetPos + new Vector2(
            Mathf.Cos(angle) * circleRadius,
            Mathf.Sin(angle) * circleRadius
        );
    }
}
