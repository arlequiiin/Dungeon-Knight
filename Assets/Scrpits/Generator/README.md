# Процедурная генерация подземелья для 2D Roguelike

## 📖 Документация

### Для быстрого старта:
- **[QUICK_START.md](../../QUICK_START.md)** ← Начни отсюда! Самый быстрый способ настройки

### Для пошагового руководства:
- **[STEP_BY_STEP.md](./STEP_BY_STEP.md)** ← Детальное описание каждого шага с примерами

### Для полного понимания:
- **[SETUP_GUIDE.md](./SETUP_GUIDE.md)** ← Полное руководство с решением проблем

---

## 🎯 Что это?

Это система процедурной генерации подземелий для 2D roguelike игры на Unity.

**Как она работает:**

1. **BSP разбиение** - пространство рекурсивно делится на прямоугольники
2. **Создание комнат** - в каждом разделе создаётся комната случайного размера
3. **Граф связей** - комнаты соединяются логически (основа для коридоров)
4. **Распределение типов** - комнаты получают типы (старт, обычная, специальная, босс)
5. **Визуализация** - всё отрисовывается на Unity Tilemap

**Результат:**
- Полностью процедурная карта
- Гарантированно без пересечений комнат
- Легко расширяется новыми типами комнат
- Поддерживает кастомные параметры генерации

---

## 📁 Структура файлов

```
Generator/
├── README.md                    ← Этот файл
├── SETUP_GUIDE.md              ← Полное руководство
├── STEP_BY_STEP.md             ← Пошаговое руководство
│
├── Generator.cs                ← ГЛАВНЫЙ компонент
│                                 Запускает генерацию,
│                                 управляет визуализацией
│
├── DungeonConfig.cs            ← Параметры генерации
│                                 mapWidth, mapHeight,
│                                 normalRoomsCount, etc
│
├── BSPGenerator.cs             ← Основной алгоритм
│                                 BSP разбиение
│                                 Создание комнат
│                                 Построение графа
│
├── BSPNode.cs                  ← Узел дерева BSP
│                                 Содержит rect и комнату
│
├── Room.cs                     ← Структура комнаты
│                                 type, bounds, coordinates
│
├── RoomType.cs                 ← Enum типов
│                                 Start, Normal, Special, Boss
│
├── DungeonRenderer.cs          ← Визуализация на Tilemap
│                                 Рисует комнаты
│                                 Рисует границы
│
└── TileSetup.cs                ← Вспомогательный скрипт
                                  Создаёт тайлы и сцену автоматом
```

---

## 🚀 Быстрый старт (3 шага)

### Вариант 1: Автоматический (рекомендуется)

```
1. Меню → Dungeon → Setup → Create Default Tiles
2. Меню → Dungeon → Setup → Create Test Scene
3. Нажми Play
```

**Готово!** Подземелье сгенерировано и видно на сцене.

### Вариант 2: Ручной (если первый не сработал)

Смотри [STEP_BY_STEP.md](./STEP_BY_STEP.md) - там все шаги описаны с картинками.

---

## ⚙️ Основные компоненты

### Generator.cs
```csharp
// Главный MonoBehaviour на сцене
public class Generator : MonoBehaviour
{
    // Запускает генерацию
    public void GenerateDungeon() { ... }

    // Перегенерирует (для тестирования)
    public void RegenerateDungeon() { ... }
}
```

**Использование:**
```csharp
var generator = GetComponent<Generator>();
generator.GenerateDungeon(); // Генерирует новое подземелье
```

### DungeonConfig.cs
```csharp
// Параметры генерации (ScriptableObject)
public class DungeonConfig
{
    public int mapWidth = 100;
    public int mapHeight = 100;
    public int normalRoomsCount = 15;
    public int specialRoomsCount = 2;
    public float branchProbability = 0.3f;
    // ... и т.д.
}
```

**Создание:**
```
Right Click → Create → ScriptableObject → DungeonConfig
```

### BSPGenerator.cs
```csharp
// Основной алгоритм генерации
public class BSPGenerator
{
    // Главный метод генерации
    public void Generate() { ... }

    // Получить список комнат
    public List<Room> Rooms => rooms;
}
```

**Использование:**
```csharp
var generator = new BSPGenerator(config);
generator.Generate();
var rooms = generator.Rooms; // Список всех комнат
```

### DungeonRenderer.cs
```csharp
// Визуализация на Tilemap
public class DungeonRenderer : MonoBehaviour
{
    // Рисует подземелье
    public void RenderDungeon(BSPGenerator generator) { ... }

    // Debug рисование
    public void DebugRenderRooms(BSPGenerator generator) { ... }
}
```

---

## 🎮 Примеры использования

### Пример 1: Базовая генерация

```csharp
// В компоненте Generator (уже реализовано)
public void GenerateDungeon()
{
    var config = new DungeonConfig();
    var generator = new BSPGenerator(config);
    generator.Generate();

    dungeonRenderer.RenderDungeon(generator);
}
```

### Пример 2: Кастомные параметры

```csharp
var config = ScriptableObject.CreateInstance<DungeonConfig>();
config.mapWidth = 200;
config.mapHeight = 150;
config.normalRoomsCount = 30;
config.specialRoomsCount = 5;

var generator = new BSPGenerator(config);
generator.Generate();

// Обработать комнаты
foreach (var room in generator.Rooms)
{
    Debug.Log($"Комната: {room.type} в позиции {room.bounds}");
}
```

### Пример 3: Поиск комнаты по типу

```csharp
var generator = new BSPGenerator(config);
generator.Generate();

// Найти стартовую комнату
var startRoom = generator.Rooms.Find(r => r.type == RoomType.Start);

// Найти комнату босса
var bossRoom = generator.Rooms.Find(r => r.type == RoomType.Boss);

// Получить случайную обычную комнату
var normalRoom = generator.GetRandomRoom(RoomType.Normal);
```

---

## 🔧 Параметры DungeonConfig

| Параметр | Значение | Описание |
|----------|----------|---------|
| `mapWidth` | 100 | Ширина карты в тайлах |
| `mapHeight` | 100 | Высота карты в тайлах |
| `normalRoomsCount` | 15 | Кол-во обычных комнат |
| `specialRoomsCount` | 2 | Кол-во специальных комнат |
| `minRoomSize` | (8, 8) | Минимальный размер комнаты |
| `maxRoomSize` | (20, 20) | Максимальный размер комнаты |
| `minPathToBoss` | 5 | Минимальное расстояние до босса |
| `maxPathToBoss` | 10 | Максимальное расстояние до босса |
| `branchProbability` | 0.3 | Вероятность боковых веток (0-1) |
| `useRandomSeed` | true | Использовать случайный seed |
| `seed` | 0 | Фиксированный seed |

---

## 🐛 Типы комнат

```csharp
public enum RoomType
{
    Start = 0,      // Стартовая комната (игрок начинает здесь)
    Normal = 1,     // Обычная комната (враги, лут)
    Special = 2,    // Специальная (магазины, сокровища)
    Boss = 3        // Комната босса (финальный враг)
}
```

**Распределение в генераторе:**
- Комната [0] → RoomType.Start
- Комнаты [1...n-2] → RoomType.Normal или RoomType.Special
- Комната [n-1] → RoomType.Boss

---

## 🌟 Структура Room

```csharp
public class Room
{
    public RoomType type;           // Тип комнаты
    public RectInt bounds;          // Позиция и размер (x, y, width, height)
    public Vector2 center;          // Центр комнаты
    public int nodeId;              // ID узла в графе
    public int distanceToBoss;      // Расстояние до босса
}
```

**Пример:**
```csharp
var room = generator.Rooms[0];

Debug.Log($"Тип: {room.type}");                     // Start
Debug.Log($"Позиция: {room.bounds.position}");     // (10, 15)
Debug.Log($"Размер: {room.bounds.size}");          // (12, 18)
Debug.Log($"Центр: {room.center}");                // (16, 24)
Debug.Log($"До босса: {room.distanceToBoss}");     // 0
```

---

## 🎨 Визуализация

### Debug Mode

Когда включён Debug Mode в Generator:
- Все комнаты рисуются одним цветом (белым)
- Выводится много логов в консоль
- Видна полная структура подземелья

**Когда использовать:** При разработке и тестировании

### Normal Mode

Когда отключён Debug Mode:
- Комнаты рисуются с учётом типа
- Минимум логов в консоль
- Оптимизированная визуализация

**Когда использовать:** На финальных сборках

---

## 📊 Алгоритм BSP

1. **Инициализация:** Создаём корневой узел со всей картой
2. **Рекурсивное разбиение:**
   - Если размер < минимума → стоп
   - Случайно выбираем горизонтальное или вертикальное разбиение
   - Случайно выбираем позицию разбиения
   - Рекурсивно разбиваем оба подпространства
3. **Создание комнат:** В каждом листе BSP создаём комнату
4. **Соединение:** Соединяем соседние комнаты логически

**Сложность:** O(n log n), где n - площадь карты

---

## 🚀 Расширение (для будущего)

### 1. Коридоры (следующий шаг)

```csharp
// В BSPGenerator нужно добавить:
public List<Corridor> Corridors => corridors;

// Коридор соединит две комнаты
public class Corridor
{
    public Room from;
    public Room to;
    public List<Vector2Int> path; // Путь коридора
}
```

### 2. Враги (спауны)

```csharp
public class Room
{
    public List<EnemySpawn> enemySpawns; // Список врагов
    public int difficulty; // Сложность комнаты
}
```

### 3. Предметы (лут)

```csharp
public class Room
{
    public List<LootSpawn> lootSpawns; // Сундуки, лут
}
```

### 4. Визуальные префабы

```csharp
public class Room
{
    public GameObject prefab; // Визуальная комната
    public GameObject InstantiateRoom(Vector3 position);
}
```

---

## 💡 Советы по использованию

1. **Фиксированный seed для тестирования:**
   ```csharp
   config.useRandomSeed = false;
   config.seed = 12345; // Всегда одна и та же карта
   ```

2. **Экспериментирование с параметрами:**
   ```csharp
   // Мало больших комнат
   config.maxRoomSize = new Vector2Int(10, 10);

   // Много маленьких комнат
   config.normalRoomsCount = 50;
   ```

3. **Получение информации о подземелье:**
   ```csharp
   var startRoom = generator.Rooms.Find(r => r.type == RoomType.Start);
   var bossRoom = generator.Rooms.Find(r => r.type == RoomType.Boss);
   var totalArea = generator.Rooms.Sum(r => r.bounds.width * r.bounds.height);
   ```

---

## 🔍 Отладка

### Вывод информации в консоль

Когда `debugMode = true` в Generator:

```
[DungeonGenerator] Seed: 12345
[DungeonGenerator] Начало BSP разбиения...
[DungeonGenerator] Создание комнат из листьев BSP...
[DungeonGenerator] Построение графа комнат...
[DungeonGenerator] Генерация завершена. Всего комнат: 35

=== ИНФОРМАЦИЯ О ПОДЗЕМЕЛЬЕ ===
Всего комнат: 35
  - Стартовых: 1
  - Обычных: 32
  - Специальных: 2
  - Боссов: 1

Комнаты:
  [0] Room(Start, pos=(10,10), size=(12x15))
  [1] Room(Normal, pos=(25,30), size=(18x12))
  ...
```

### Проверка отдельной комнаты

```csharp
Debug.Log(generator.Rooms[0].ToString());
// Room(Start, pos=(10,10), size=(12x15))
```

---

## ✅ Чеклист перед началом

- [ ] Скрипты находятся в папке Generator
- [ ] Grid создан на сцене
- [ ] Tilemap создан и виден
- [ ] Тайлы созданы (FloorTile, WallTile)
- [ ] DungeonConfig создан и настроен
- [ ] Generator GameObject создан
- [ ] Generator и DungeonRenderer компоненты добавлены
- [ ] Все ссылки назначены
- [ ] Play работает без ошибок
- [ ] Комнаты видны на сцене

**Если всё зелёно - готов к разработке!**

---

## 📞 Помощь

Если возникли проблемы:

1. **Проверь консоль** на красные сообщения об ошибках
2. **Посмотри [STEP_BY_STEP.md](./STEP_BY_STEP.md)** для пошагового разбора
3. **Используй [SETUP_GUIDE.md](./SETUP_GUIDE.md)** для решения проблем
4. **Убедись, что все файлы импортированы** (нет красных значков в Project)

---

## 📝 Лицензия

Используй свободно в своих проектах!

**Создано для:** 2D Roguelike игры на Unity
**Язык:** C#
**Версия:** 1.0 (минимальная версия)
