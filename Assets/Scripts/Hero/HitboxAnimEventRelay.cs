using UnityEngine;

/// <summary>
/// Прокси для Animation Events.
/// Висит на префабе Player постоянно, пробрасывает вызовы в HeroAbility.
/// В анимации: Add Event → Function = "EnableHitbox" или "DisableHitbox".
/// </summary>
public class HitboxAnimEventRelay : MonoBehaviour
{
    private HeroAbility ability;

    public void EnableHitbox()
    {
        if (ability == null)
            ability = GetComponent<HeroAbility>();
        if (ability != null)
            ability.EnableHitbox();
    }

    public void DisableHitbox()
    {
        if (ability == null)
            ability = GetComponent<HeroAbility>();
        if (ability != null)
            ability.DisableHitbox();
    }
}
