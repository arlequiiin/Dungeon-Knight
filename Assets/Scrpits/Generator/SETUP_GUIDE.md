# Руководство по настройке процедурной генерации подземелья

## Шаг 1: Подготовка сцены

### 1.1 Создание Grid (сетка для тайлов)

1. В иерархии сцены → Right Click → 2D Object → Tilemap → Rectangular
   - Это создаст GameObject с Grid и Tilemap

2. Переименуй новый GameObject на "DungeonTilemap"

3. В Inspector найди компонент Grid и убедись, что:
   - Cell Size: (1, 1) или (16, 16) в зависимости от размера пиксел-артов
   - Grid Type: Rectangle

### 1.2 Создание Tileset (набор плиток)

Тебе нужны как минимум 2 текстуры (или 1, если использовать одну для всего):
- **Пол** (для комнат)
- **Стена** (для границ) - опционально

Если у тебя есть текстуры:
1. Перенеси их в папку `Assets/Tiles/`
2. Выбери текстуру в проекте
3. Inspector → Sprite Mode: Multiple (если спрайтлист)
4. Нажми Slice для нарезки спрайтов
5. Создай Tile Assets:
   - Right Click в папке → 2D → Tiles → Tile
   - Назови его "FloorTile"
   - Drag текстуру спрайта в поле Sprite
   - Повтори для WallTile

### 1.3 Если текстур нет (для тестирования)

1. Создай простую текстуру белого цвета (16x16 пикселей)
   - Right Click в папке Assets → Create → Render Texture или просто импортируй
2. Используй одну текстуру для обоих типов плиток (они будут выглядеть одинаково)

---

## Шаг 2: Настройка Generator

### 2.1 Создание DungeonConfig (параметры)

1. Right Click в папке `Assets/Resources/` (создай если нет)
2. Create → Scriptable Object → DungeonConfig
3. Назови его "DungeonSettings"

4. Выбери созданный файл в Inspector и установи параметры:

```
Map Width: 100
Map Height: 100
Normal Rooms Count: 15
Special Rooms Count: 2-3
Min Room Size: (8, 8)
Max Room Size: (20, 20)
Min Path To Boss: 5
Max Path To Boss: 10
Branch Probability: 0.3
Use Random Seed: ✓ (галочка включена)
```

### 2.2 Создание Generator GameObject

1. Right Click в иерархии → Create Empty
2. Назови его "DungeonGenerator"
3. Add Component → Generator

4. В Inspector компонента Generator установи:
   - Generate On Start: ✓ (галочка включена)
   - Debug Mode: ✓ (включи для тестирования, потом можно выключить)

### 2.3 Присвоение ссылок

В компоненте Generator назначь:

- **Dungeon Config**: перетащи созданный DungeonSettings
- **Dungeon Renderer**: (см. шаг 3)

---

## Шаг 3: Настройка DungeonRenderer

### 3.1 Добавление компонента

1. Выбери GameObject "DungeonTilemap" в иерархии
2. Add Component → DungeonRenderer

### 3.2 Присвоение Tilemap и плиток

В Inspector компонента DungeonRenderer установи:

- **Tilemap**: (автоматически найдется, если нет - перетащи сам)
- **Wall Tile**: перетащи созданный WallTile
- **Floor Tile**: перетащи созданный FloorTile

Если плиток нет - можно оставить пусто для тестирования.

---

## Шаг 4: Связывание компонентов

### 4.1 Связь Generator → Renderer

1. Выбери GameObject "DungeonGenerator"
2. В компоненте Generator → поле "Dungeon Renderer"
3. Перетащи GameObject "DungeonTilemap" (или выбери его через точку)

---

## Шаг 5: Запуск и тестирование

1. Нажми Play в Unity
2. В консоли должны появиться логи:
   ```
   [DungeonGenerator] Seed: 12345
   [DungeonGenerator] Начало BSP разбиения...
   [DungeonGenerator] Создание комнат из листьев BSP...
   [DungeonGenerator] Построение графа комнат...
   [DungeonGenerator] Генерация завершена. Всего комнат: 35
   [DungeonRenderer] Debug-отрисовано 35 комнат

   === ИНФОРМАЦИЯ О ПОДЗЕМЕЛЬЕ ===
   Всего комнат: 35
     - Стартовых: 1
     - Обычных: 32
     - Специальных: 2
     - Боссов: 1
   ```

3. На Tilemap должны отобразиться прямоугольники комнат

---

## Шаг 6: Регенерация (для тестирования)

Если хочешь пересоздать подземелье без перезагрузки:

1. Выбери GameObject "DungeonGenerator"
2. В Inspector найди компонент Generator
3. Нажми на кнопку "Regenerate Dungeon" (должна появиться внизу)

Или вызови из кода:
```csharp
Generator gen = GetComponent<Generator>();
gen.GenerateDungeon();
```

---

## Что дальше

### Отключение Debug Mode

Когда всё работает:
1. Выбери "DungeonGenerator"
2. В компоненте Generator отключи Debug Mode
3. Теперь будут рисоваться только комнаты без логов каждой в консоли

### Просмотр структуры

Если включить Gizmos в Game View, можно будет видеть размеры комнат:

1. Window → 2D → Tile Palette (если нужна палитра)
2. Scene → Right Click → Gizmos (включить визуализацию)

### Создание коридоров (следующий этап)

Когда комнаты генерируются корректно, можно добавить коридоры между ними.

---

## Решение проблем

### Проблема: Комнаты не видны
- Проверь, что Tilemap выбран и видим на сцене
- Убедись, что Floor Tile назначен
- Проверь, что камера смотрит на нужное место (Canvas)

### Проблема: Ошибка "DungeonRenderer не найден"
- Убедись, что DungeonRenderer добавлен на GameObject с Tilemap
- В Generator назначь Dungeon Renderer вручную

### Проблема: Все комнаты очень маленькие или больших нет
- Измени Max Room Size в DungeonConfig
- Увеличь Map Width/Height

### Проблема: Слишком мало или много комнат
- Измени Normal Rooms Count в DungeonConfig

---

## Структура файлов

```
Assets/
├── Scrpits/
│   └── Generator/
│       ├── Generator.cs                (главный компонент)
│       ├── DungeonConfig.cs           (параметры)
│       ├── DungeonRenderer.cs         (визуализация)
│       ├── BSPGenerator.cs            (генератор)
│       ├── Room.cs                    (класс комнаты)
│       ├── RoomType.cs                (типы комнат)
│       ├── BSPNode.cs                 (узел дерева)
│       └── SETUP_GUIDE.md             (этот файл)
├── Resources/
│   └── DungeonSettings.asset          (конфиг подземелья)
└── Tiles/
    ├── FloorTile.asset                (плитка пола)
    └── WallTile.asset                 (плитка стены)
```
