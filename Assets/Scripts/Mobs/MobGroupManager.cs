using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Менеджер групповой координации мобов в комнате.
/// Два слота атаки: один слева от игрока, один справа.
/// Остальные мобы кружат вокруг, ожидая своей очереди.
/// Распределяет цели между мобами, чтобы не все бежали к одному игроку.
/// </summary>
public class MobGroupManager : MonoBehaviour
{
    [Header("Позиционирование")]
    public float circleRadius = 2.5f;
    public float circleSpeed = 1.5f;

    private readonly List<MobAI> aliveMobs = new();

    // Распределение целей: сколько мобов преследуют каждого игрока
    private readonly Dictionary<Transform, int> targetCounts = new();

    // Два слота: слева и справа от игрока
    private MobAI leftSlot;
    private MobAI rightSlot;

    public void Register(MobAI mob)
    {
        if (!aliveMobs.Contains(mob))
            aliveMobs.Add(mob);
    }

    public void Unregister(MobAI mob)
    {
        aliveMobs.Remove(mob);
        if (leftSlot == mob) leftSlot = null;
        if (rightSlot == mob) rightSlot = null;
    }

    /// <summary>
    /// Запрашивает слот атаки. Моб назначается на ближайшую сторону (лево/право от игрока).
    /// </summary>
    public bool RequestAttackSlot(MobAI mob, Transform target)
    {
        // Уже имеет слот
        if (leftSlot == mob || rightSlot == mob) return true;

        // Оба заняты
        if (leftSlot != null && rightSlot != null) return false;

        // Определяем, с какой стороны моб относительно игрока
        bool mobIsRight = mob.transform.position.x >= target.position.x;

        if (mobIsRight)
        {
            if (rightSlot == null) { rightSlot = mob; return true; }
            if (leftSlot == null)  { leftSlot = mob; return true; }
        }
        else
        {
            if (leftSlot == null)  { leftSlot = mob; return true; }
            if (rightSlot == null) { rightSlot = mob; return true; }
        }

        return false;
    }

    public void ReleaseAttackSlot(MobAI mob)
    {
        if (leftSlot == mob) leftSlot = null;
        if (rightSlot == mob) rightSlot = null;
    }

    public bool HasFreeSlot => leftSlot == null || rightSlot == null;

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
            if (m == leftSlot || m == rightSlot) continue;
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

    /// <summary>
    /// Назначает цель мобу с учётом распределения нагрузки по игрокам.
    /// Предпочитает ближайшего игрока, но если к нему уже бежит слишком много мобов,
    /// переключается на менее загруженного.
    /// </summary>
    public Transform AssignTarget(MobAI mob, float detectionRange)
    {
        // Собираем живых игроков в зоне видимости
        var candidates = new List<(Transform player, float dist)>();
        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            var heroStats = identity.GetComponent<HeroStats>();
            if (heroStats == null || heroStats.IsDead || heroStats.IsDowned) continue;

            float d = Vector2.Distance(mob.transform.position, identity.transform.position);
            if (d < detectionRange)
                candidates.Add((identity.transform, d));
        }

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0].player;

        // Сортируем по дистанции
        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        // Идеальное количество мобов на одного игрока
        int maxPerTarget = Mathf.Max(1, Mathf.CeilToInt((float)aliveMobs.Count / candidates.Count));

        // Выбираем ближайшего игрока, у которого ещё не превышен лимит
        foreach (var (player, dist) in candidates)
        {
            targetCounts.TryGetValue(player, out int count);
            if (count < maxPerTarget)
                return player;
        }

        // Все перегружены — берём ближайшего
        return candidates[0].player;
    }

    /// <summary>
    /// Моб начал преследовать цель — обновляем счётчик.
    /// </summary>
    public void NotifyTargetChanged(MobAI mob, Transform oldTarget, Transform newTarget)
    {
        if (oldTarget != null && targetCounts.ContainsKey(oldTarget))
        {
            targetCounts[oldTarget]--;
            if (targetCounts[oldTarget] <= 0)
                targetCounts.Remove(oldTarget);
        }

        if (newTarget != null)
        {
            targetCounts.TryGetValue(newTarget, out int count);
            targetCounts[newTarget] = count + 1;
        }
    }
}
