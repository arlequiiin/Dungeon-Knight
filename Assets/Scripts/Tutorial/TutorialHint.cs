using UnityEngine;

/// <summary>
/// Одна туториал-подсказка. id уникален и используется как ключ показа.
/// Создавать через Assets → Create → Dungeon Knight → Tutorial Hint.
/// </summary>
[CreateAssetMenu(fileName = "TutorialHint", menuName = "Dungeon Knight/Tutorial Hint")]
public class TutorialHint : ScriptableObject
{
    [Tooltip("Уникальный id подсказки (например \"movement\", \"first_damage\", \"boss\"). " +
             "Этот же id шлётся по сети через ShowHintMessage.")]
    public string id;

    [TextArea(2, 4)]
    [Tooltip("Текст подсказки. Поддерживает Rich Text (например <b>WASD</b>).")]
    public string text;

    [Tooltip("Сколько секунд держать подсказку на экране.")]
    [Range(2f, 10f)] public float duration = 4.5f;

    [Tooltip("Иконка слева от текста (опционально).")]
    public Sprite icon;
}
