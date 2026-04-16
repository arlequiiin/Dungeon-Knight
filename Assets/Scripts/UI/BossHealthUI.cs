using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Boss HP bar UI. Finds the boss automatically via MobHealth.IsBoss SyncVar.
/// Shown at the top of the screen when boss is alive.
/// </summary>
public class BossHealthUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject bossBarPanel;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthGhostFill;
    [SerializeField] private TextMeshProUGUI bossNameText;

    private MobHealth bossHealth;
    private bool bound;

    // Ghost bar
    private float ghostHealth = 1f;
    private float ghostDelay;
    private const float GhostDelayTime = 0.5f;
    private const float GhostLerpSpeed = 2f;

    private void Awake()
    {
        if (bossBarPanel != null)
            bossBarPanel.SetActive(false);
    }

    private void Update()
    {
        // Try to find boss if not yet bound
        if (!bound)
        {
            TryFindBoss();
            return;
        }

        // Boss was destroyed
        if (bossHealth == null)
        {
            Hide();
            return;
        }

        // Update fill
        float normalized = bossHealth.HealthNormalized;
        if (healthFill != null)
            healthFill.fillAmount = normalized;

        // Ghost bar
        UpdateGhostBar(normalized);

        // Hide when dead
        if (bossHealth.IsDead)
            Hide();
    }

    private void TryFindBoss()
    {
        // Search all MobHealth in the scene for the one marked as boss
        foreach (var mh in FindObjectsByType<MobHealth>(FindObjectsSortMode.None))
        {
            if (mh.IsBoss && !mh.IsDead)
            {
                Bind(mh);
                return;
            }
        }
    }

    private void Bind(MobHealth boss)
    {
        bossHealth = boss;
        bound = true;

        if (bossBarPanel != null)
            bossBarPanel.SetActive(true);

        // Set boss name from MobData
        var ai = boss.GetComponent<MobAI>();
        if (ai != null && ai.mobData != null && bossNameText != null)
            bossNameText.text = ai.mobData.mobName;

        // Init fill
        float normalized = boss.HealthNormalized;
        if (healthFill != null)
            healthFill.fillAmount = normalized;
        ghostHealth = normalized;
        if (healthGhostFill != null)
            healthGhostFill.fillAmount = normalized;

        // Subscribe to health changes
        boss.onHealthChanged.AddListener(OnBossHealthChanged);
    }

    private void OnBossHealthChanged(float current, float max)
    {
        float normalized = max > 0 ? current / max : 0f;
        if (healthFill != null)
            healthFill.fillAmount = normalized;

        ghostDelay = GhostDelayTime;
    }

    private void UpdateGhostBar(float target)
    {
        if (healthGhostFill == null) return;

        if (ghostHealth > target)
        {
            ghostDelay -= Time.deltaTime;
            if (ghostDelay <= 0f)
                ghostHealth = Mathf.Lerp(ghostHealth, target, GhostLerpSpeed * Time.deltaTime);
        }
        else
        {
            ghostHealth = target;
        }

        healthGhostFill.fillAmount = ghostHealth;
    }

    private void Hide()
    {
        if (bossHealth != null)
            bossHealth.onHealthChanged.RemoveListener(OnBossHealthChanged);

        bound = false;
        bossHealth = null;

        if (bossBarPanel != null)
            bossBarPanel.SetActive(false);
    }
}
