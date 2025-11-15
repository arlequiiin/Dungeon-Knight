# Примеры кода для работы с генератором

## Содержание

1. [Базовая генерация](#базовая-генерация)
2. [Кастомные параметры](#кастомные-параметры)
3. [Работа с комнатами](#работа-с-комнатами)
4. [Поиск специфических комнат](#поиск-специфических-комнат)
5. [Расширение функционала](#расширение-функционала)

---

## Базовая генерация

### Пример 1: Простая генерация с параметрами по умолчанию

```csharp
using UnityEngine;

public class DungeonTest : MonoBehaviour
{
    void Start()
    {
        // Создаём конфиг с параметрами по умолчанию
        var config = new DungeonConfig();

        // Создаём генератор
        var generator = new BSPGenerator(config);

        // Генерируем подземелье
        generator.Generate();

        // Выводим информацию
        Debug.Log($"Создано комнат: {generator.Rooms.Count}");
    }
}
```

### Пример 2: Генерация через компонент Generator

```csharp
using UnityEngine;

public class TestGenerator : MonoBehaviour
{
    [SerializeField] private Generator dungeonGenerator;

    void Start()
    {
        // Генерируем подземелье через компонент
        dungeonGenerator.GenerateDungeon();
    }

    // Можно вызвать из UI или других скриптов
    void RegenerateMap()
    {
        dungeonGenerator.RegenerateDungeon();
    }
}
```

---

## Кастомные параметры

### Пример 1: Изменение размера карты

```csharp
using UnityEngine;

public class CustomMapSize : MonoBehaviour
{
    void GenerateSmallDungeon()
    {
        var config = new DungeonConfig();
        config.mapWidth = 50;      // Маленькая карта
        config.mapHeight = 50;
        config.normalRoomsCount = 5; // Мало комнат

        var generator = new BSPGenerator(config);
        generator.Generate();

        Debug.Log($"Создана маленькая карта: {generator.Rooms.Count} комнат");
    }

    void GenerateLargeDungeon()
    {
        var config = new DungeonConfig();
        config.mapWidth = 200;     // Большая карта
        config.mapHeight = 200;
        config.normalRoomsCount = 50; // Много комнат

        var generator = new BSPGenerator(config);
        generator.Generate();

        Debug.Log($"Создана большая карта: {generator.Rooms.Count} комнат");
    }
}
```

### Пример 2: Разные типы подземелий

```csharp
using UnityEngine;

public class DungeonTypes : MonoBehaviour
{
    DungeonConfig CreateCityDungeon()
    {
        var config = new DungeonConfig();
        config.mapWidth = 150;
        config.mapHeight = 150;
        config.normalRoomsCount = 30;      // Много комнат
        config.specialRoomsCount = 10;     // Много магазинов
        config.minRoomSize = new Vector2Int(10, 10);
        config.maxRoomSize = new Vector2Int(25, 25);
        return config;
    }

    DungeonConfig CreateCaveDungeon()
    {
        var config = new DungeonConfig();
        config.mapWidth = 100;
        config.mapHeight = 100;
        config.normalRoomsCount = 8;       // Мало комнат
        config.specialRoomsCount = 1;
        config.minRoomSize = new Vector2Int(5, 5);
        config.maxRoomSize = new Vector2Int(15, 15);
        return config;
    }

    DungeonConfig CreateTowerDungeon()
    {
        var config = new DungeonConfig();
        config.mapWidth = 80;
        config.mapHeight = 80;
        config.normalRoomsCount = 20;
        config.specialRoomsCount = 3;
        config.branchProbability = 0.1f;  // Мало веток (вертикальная структура)
        return config;
    }
}
```

### Пример 3: Фиксированный seed для тестирования

```csharp
using UnityEngine;

public class FixedSeedDungeon : MonoBehaviour
{
    void GenerateTestMap()
    {
        var config = new DungeonConfig();
        config.useRandomSeed = false;
        config.seed = 42;  // Всегда одна и та же карта

        var generator = new BSPGenerator(config);
        generator.Generate();

        // Теперь каждый запуск будет идентичным
        Debug.Log($"Seed: {config.seed}");
    }

    void GenerateRandomMap()
    {
        var config = new DungeonConfig();
        config.useRandomSeed = true;  // Каждый раз разная карта

        var generator = new BSPGenerator(config);
        generator.Generate();

        Debug.Log($"Seed: {config.seed}"); // Покажет использованный seed
    }
}
```

---

## Работа с комнатами

### Пример 1: Получение информации о каждой комнате

```csharp
using UnityEngine;

public class RoomInfo : MonoBehaviour
{
    void PrintRoomInfo(BSPGenerator generator)
    {
        foreach (var room in generator.Rooms)
        {
            Debug.Log($"=== Комната ===");
            Debug.Log($"  Тип: {room.type}");
            Debug.Log($"  Позиция: {room.bounds.position}");
            Debug.Log($"  Размер: {room.bounds.size}");
            Debug.Log($"  Центр: {room.center}");
            Debug.Log($"  ID узла: {room.nodeId}");
            Debug.Log($"  До босса: {room.distanceToBoss} шагов");
        }
    }

    // Использование
    void Start()
    {
        var config = new DungeonConfig();
        var generator = new BSPGenerator(config);
        generator.Generate();

        PrintRoomInfo(generator);
    }
}
```

### Пример 2: Подсчёт статистики

```csharp
using UnityEngine;

public class DungeonStats : MonoBehaviour
{
    void PrintDungeonStats(BSPGenerator generator)
    {
        var rooms = generator.Rooms;

        // Подсчитаем каждый тип
        int startRooms = rooms.FindAll(r => r.type == RoomType.Start).Count;
        int normalRooms = rooms.FindAll(r => r.type == RoomType.Normal).Count;
        int specialRooms = rooms.FindAll(r => r.type == RoomType.Special).Count;
        int bossRooms = rooms.FindAll(r => r.type == RoomType.Boss).Count;

        Debug.Log($"Статистика подземелья:");
        Debug.Log($"  Всего комнат: {rooms.Count}");
        Debug.Log($"  - Start: {startRooms}");
        Debug.Log($"  - Normal: {normalRooms}");
        Debug.Log($"  - Special: {specialRooms}");
        Debug.Log($"  - Boss: {bossRooms}");

        // Площадь каждого типа
        int normalArea = normalRooms > 0 ? rooms
            .FindAll(r => r.type == RoomType.Normal)
            .Sum(r => r.bounds.width * r.bounds.height) : 0;

        Debug.Log($"Общая площадь Normal комнат: {normalArea} тайлов");
    }
}
```

### Пример 3: Вычисление расстояний между комнатами

```csharp
using UnityEngine;

public class RoomDistance : MonoBehaviour
{
    float GetDistanceBetweenRooms(Room room1, Room room2)
    {
        // Расстояние между центрами комнат
        return Vector2.Distance(room1.center, room2.center);
    }

    void FindClosestRooms(BSPGenerator generator)
    {
        var rooms = generator.Rooms;
        float minDistance = float.MaxValue;
        Room room1 = null, room2 = null;

        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                float distance = GetDistanceBetweenRooms(rooms[i], rooms[j]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    room1 = rooms[i];
                    room2 = rooms[j];
                }
            }
        }

        Debug.Log($"Ближайшие комнаты на расстоянии {minDistance} тайлов");
        Debug.Log($"  Комната 1: {room1.bounds}");
        Debug.Log($"  Комната 2: {room2.bounds}");
    }
}
```

---

## Поиск специфических комнат

### Пример 1: Поиск стартовой и финальной комнаты

```csharp
using UnityEngine;

public class FindSpecialRooms : MonoBehaviour
{
    void FindStartAndBoss(BSPGenerator generator)
    {
        var rooms = generator.Rooms;

        // Стартовая комната (обычно первая)
        var startRoom = rooms.Find(r => r.type == RoomType.Start);
        if (startRoom != null)
        {
            Debug.Log($"Стартовая комната: {startRoom.bounds}");
            // Спавни игрока здесь
        }

        // Комната босса (обычно последняя)
        var bossRoom = rooms.Find(r => r.type == RoomType.Boss);
        if (bossRoom != null)
        {
            Debug.Log($"Комната босса: {bossRoom.bounds}");
            // Спавни босса здесь
        }
    }
}
```

### Пример 2: Получение всех комнат определённого типа

```csharp
using UnityEngine;

public class GetRoomsByType : MonoBehaviour
{
    void GetAllNormalRooms(BSPGenerator generator)
    {
        var normalRooms = generator.Rooms
            .FindAll(r => r.type == RoomType.Normal);

        Debug.Log($"Найдено Normal комнат: {normalRooms.Count}");

        foreach (var room in normalRooms)
        {
            Debug.Log($"  {room.bounds}");
        }
    }

    void GetAllSpecialRooms(BSPGenerator generator)
    {
        var specialRooms = generator.Rooms
            .FindAll(r => r.type == RoomType.Special);

        // Специальные комнаты для магазинов
        foreach (var room in specialRooms)
        {
            SpawnShop(room);
        }
    }

    void SpawnShop(Room room)
    {
        Debug.Log($"Спаун магазина в {room.bounds}");
        // Добавь спаун магазина
    }
}
```

### Пример 3: Случайная комната определённого типа

```csharp
using UnityEngine;

public class RandomRoomSelection : MonoBehaviour
{
    Room GetRandomNormalRoom(BSPGenerator generator)
    {
        var normalRooms = generator.Rooms
            .FindAll(r => r.type == RoomType.Normal);

        if (normalRooms.Count == 0)
            return null;

        int randomIndex = Random.Range(0, normalRooms.Count);
        return normalRooms[randomIndex];
    }

    void SpawnEnemyInRandomRoom(BSPGenerator generator)
    {
        var room = GetRandomNormalRoom(generator);
        if (room != null)
        {
            Vector3 spawnPos = new Vector3(room.center.x, room.center.y, 0);
            Debug.Log($"Враг спаунится в {spawnPos}");
            // Спавни врага
        }
    }
}
```

---

## Расширение функционала

### Пример 1: Добавление системы врагов

```csharp
using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    public void SpawnEnemiesInDungeon(BSPGenerator generator)
    {
        var normalRooms = generator.Rooms
            .FindAll(r => r.type == RoomType.Normal);

        foreach (var room in normalRooms)
        {
            int enemyCount = Random.Range(2, 5);
            for (int i = 0; i < enemyCount; i++)
            {
                SpawnEnemyInRoom(room);
            }
        }
    }

    void SpawnEnemyInRoom(Room room)
    {
        // Случайная позиция в комнате
        int x = Random.Range(room.bounds.x, room.bounds.x + room.bounds.width);
        int y = Random.Range(room.bounds.y, room.bounds.y + room.bounds.height);
        Vector3 spawnPos = new Vector3(x, y, 0);

        // Спавни врага
        Debug.Log($"Враг спаунится в {spawnPos}");
    }
}
```

### Пример 2: Система лута

```csharp
using UnityEngine;

public class LootSpawner : MonoBehaviour
{
    public void SpawnLootInDungeon(BSPGenerator generator)
    {
        var specialRooms = generator.Rooms
            .FindAll(r => r.type == RoomType.Special);

        foreach (var room in specialRooms)
        {
            // Магазины получают сокровища
            SpawnTreasure(room, 5);
        }

        // Обычные комнаты получают случайный лут
        var normalRooms = generator.Rooms
            .FindAll(r => r.type == RoomType.Normal);

        foreach (var room in normalRooms)
        {
            if (Random.value > 0.5f)  // 50% комнат имеют лут
            {
                SpawnLoot(room);
            }
        }
    }

    void SpawnTreasure(Room room, int count)
    {
        Debug.Log($"Сокровище ({count}шт) в {room.bounds}");
    }

    void SpawnLoot(Room room)
    {
        Debug.Log($"Лут в {room.bounds}");
    }
}
```

### Пример 3: Система прохождения

```csharp
using UnityEngine;

public class ProgressionSystem : MonoBehaviour
{
    public class DungeonProgression
    {
        public Room startRoom;
        public Room bossRoom;
        public int totalRooms;
        public float difficulty;
    }

    public DungeonProgression GetDungeonProgression(BSPGenerator generator)
    {
        var rooms = generator.Rooms;

        return new DungeonProgression
        {
            startRoom = rooms.Find(r => r.type == RoomType.Start),
            bossRoom = rooms.Find(r => r.type == RoomType.Boss),
            totalRooms = rooms.Count,
            difficulty = CalculateDifficulty(rooms)
        };
    }

    float CalculateDifficulty(System.Collections.Generic.List<Room> rooms)
    {
        // Сложность зависит от количества комнат
        float difficulty = rooms.Count * 0.1f;

        // И количества Special комнат
        int specialRooms = rooms.FindAll(r => r.type == RoomType.Special).Count;
        difficulty += specialRooms * 0.15f;

        return Mathf.Clamp(difficulty, 1f, 10f);
    }
}
```

---

## Интеграция с UI

### Пример: Отображение информации в UI

```csharp
using UnityEngine;
using UnityEngine.UI;

public class DungeonUI : MonoBehaviour
{
    [SerializeField] private Text dungeonInfoText;
    [SerializeField] private Generator generator;

    void Update()
    {
        if (generator != null)
        {
            UpdateDungeonInfo();
        }
    }

    void UpdateDungeonInfo()
    {
        var rooms = generator.Rooms;

        string info = $"Подземелье\n";
        info += $"Комнат: {rooms.Count}\n";
        info += $"Start: {rooms.FindAll(r => r.type == RoomType.Start).Count}\n";
        info += $"Normal: {rooms.FindAll(r => r.type == RoomType.Normal).Count}\n";
        info += $"Special: {rooms.FindAll(r => r.type == RoomType.Special).Count}\n";
        info += $"Boss: {rooms.FindAll(r => r.type == RoomType.Boss).Count}";

        dungeonInfoText.text = info;
    }
}
```

---

## Советы по использованию

1. **Кэширование результатов:**
   ```csharp
   private List<Room> cachedNormalRooms;

   void CacheRoomsByType(BSPGenerator generator)
   {
       cachedNormalRooms = generator.Rooms
           .FindAll(r => r.type == RoomType.Normal);
   }
   ```

2. **Использование LINQ для поиска:**
   ```csharp
   using System.Linq;

   var largeRooms = generator.Rooms
       .Where(r => r.bounds.area > 100)
       .OrderByDescending(r => r.bounds.area)
       .ToList();
   ```

3. **Отладка с Debug Draw:**
   ```csharp
   void DebugDrawRooms(BSPGenerator generator)
   {
       foreach (var room in generator.Rooms)
       {
           // Рисуем границы комнаты в редакторе
           Debug.DrawLine(
               new Vector3(room.bounds.xMin, room.bounds.yMin),
               new Vector3(room.bounds.xMax, room.bounds.yMax),
               Color.green
           );
       }
   }
   ```

---

**Хочешь больше примеров? Спроси! 🚀**
