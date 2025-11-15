# Пошаговое руководство с картинками (текстовое описание)

## Вариант 1: Автоматическая генерация (самый простой)

### Этап 1: Открытие меню Unity

```
1. Убедись, что проект открыт в Unity
2. Посмотри в строку меню (сверху)
3. Нажми "Dungeon" (новый пункт меню, которого не было раньше)
```

**Ты увидишь выпадающее меню:**
```
├── Setup
│   ├── Create Default Tiles
│   └── Create Test Scene
```

### Этап 2: Создание тайлов

```
1. Нажми "Dungeon" → "Setup" → "Create Default Tiles"
2. Жди пару секунд
3. В консоли появится сообщение:
   [TileSetup] Тайлы созданы в папке Assets/Tiles
```

**Что произойдёт в проекте:**
```
Assets/
└── Tiles/
    ├── FloorSprite.png
    ├── FloorTile.asset
    ├── WallSprite.png
    └── WallTile.asset
```

### Этап 3: Создание тестовой сцены

```
1. Нажми "Dungeon" → "Setup" → "Create Test Scene"
2. В консоли появится:
   [TileSetup] Тестовая сцена создана!
3. В иерархии появится:
   ├── DungeonGrid
   │   └── DungeonTilemap
   └── DungeonGenerator
```

### Этап 4: Запуск!

```
1. Нажми кнопку Play ▶ (сверху в центре)
2. Жди 1-2 секунды
3. На сцене появятся светлые прямоугольники (это комнаты)
4. В консоли увидишь информацию о подземелье
```

**Вот и всё!** Генератор работает.

---

## Вариант 2: Ручная пошаговая настройка

Если по какой-то причине автоматический вариант не сработал.

### Шаг 1: Создание Grid

**Что ты видишь:**
```
Scene (пусто)
  └── MainCamera
```

**Что ты делаешь:**
```
1. Right Click в пустой области иерархии (слева)
2. 2D Object → Tilemap → Rectangular
```

**Результат:**
```
Scene (пусто)
  ├── MainCamera
  ├── Grid
  │   └── Tilemap
  └── (может быть Layer_0 или похоже)
```

**Переименование:**
```
1. Right Click на "Tilemap"
2. Нажми "Rename"
3. Введи "DungeonTilemap"
4. Enter
```

### Шаг 2: Быстрое создание тайлов (вариант автомат)

**Если ты уже создал тайлы через меню - пропусти этот шаг!**

**Если нет - выполни:**
```
1. Dungeon → Setup → Create Default Tiles
2. Жди сообщения в консоли
```

**Если это не работает, сделай вручную:**

```
1. Right Click в Assets → Create → Folder → назови "Tiles"
2. Открой папку Tiles (double click)
3. Right Click → Create → 2D → Tiles → Tile
4. Назови "FloorTile"
5. Повтори и создай "WallTile"

(Визуально они будут невидимыми, но работать будут)
```

### Шаг 3: Создание DungeonConfig

**Где создавать:**
```
1. Убедись, что в Assets папка "Resources" существует
   Если нет: Right Click на Assets → Create → Folder → "Resources"
2. Открой папку Resources (double click)
3. Right Click в пустоте → Create → ScriptableObject → DungeonConfig
4. Назови "DungeonSettings"
```

**Результат:**
```
Assets/
├── Resources/
│   └── DungeonSettings.asset  ← Новый файл
├── Scrpits/
│   └── Generator/
│       └── (все скрипты)
└── Tiles/
    └── (тайлы)
```

**Настройка параметров:**
```
1. Выбери DungeonSettings в Project
2. Посмотри в Inspector (справа)
3. Измени параметры:

   Map Width: 100
   Map Height: 100
   Normal Rooms Count: 15
   Special Rooms Count: 2
   Min Room Size: X=8, Y=8
   Max Room Size: X=20, Y=20
   Min Path To Boss: 5
   Max Path To Boss: 10
   Branch Probability: 0.3
   Use Random Seed: ✓ (галочка включена)
```

### Шаг 4: Создание Generator GameObject

**Где создавать:**
```
1. Right Click в иерархии (в пустой области)
2. Create Empty
3. Переименуй на "DungeonGenerator"
```

**Результат в иерархии:**
```
Scene
  ├── MainCamera
  ├── Grid
  │   └── DungeonTilemap
  └── DungeonGenerator  ← Новый пустой объект
```

**Добавление компонента Generator:**
```
1. Выбери DungeonGenerator
2. В Inspector → Add Component
3. Начни писать "Generator"
4. Нажми на найденный Generator (не GeneratorBrush!)
5. Компонент добавлен
```

**Настройка параметров Generator:**
```
1. Найди в Inspector компонент Generator
2. Настрой поля:

   Generate On Start: ✓ (включена галочка)
   Debug Mode: ✓ (включена галочка)
   Dungeon Config: (пока пусто, настроим дальше)
   Dungeon Renderer: (пока пусто, настроим дальше)
```

### Шаг 5: Назначение DungeonConfig

```
1. Выбери DungeonGenerator (в иерархии)
2. В Inspector найди компонент Generator
3. Найди поле "Dungeon Config"
4. Способ A: Drag & Drop
   - Найди DungeonSettings в Project
   - Перетащи его в поле "Dungeon Config"
5. Способ B: Через кнопку выбора
   - Нажми на маленький кружок рядом с "Dungeon Config"
   - Найди "DungeonSettings"
   - Нажми на нём
```

**Результат:**
```
Generator
  Generate On Start: ✓
  Debug Mode: ✓
  Dungeon Config: DungeonSettings
  Dungeon Renderer: (ещё пусто)
```

### Шаг 6: Добавление DungeonRenderer

```
1. Выбери DungeonTilemap в иерархии
2. В Inspector → Add Component
3. Начни писать "DungeonRenderer"
4. Нажми на найденный компонент
```

**Настройка DungeonRenderer:**
```
1. Найди в Inspector компонент DungeonRenderer
2. Убедись, что поле "Tilemap" автоматически заполнилось
   (если нет - перетащи DungeonTilemap в это поле)
3. В поле "Wall Tile" перетащи WallTile из Tiles папки
4. В поле "Floor Tile" перетащи FloorTile из Tiles папки

(Если тайлов нет - оставь пусто, всё равно будет работать)
```

**Результат:**
```
DungeonRenderer
  Tilemap: DungeonTilemap
  Wall Tile: WallTile
  Floor Tile: FloorTile
```

### Шаг 7: Связь Generator → Renderer

```
1. Выбери DungeonGenerator в иерархии
2. В Inspector найди компонент Generator
3. Найди поле "Dungeon Renderer"
4. Перетащи DungeonTilemap (или выбери через кнопку)
```

**Результат:**
```
Generator
  Generate On Start: ✓
  Debug Mode: ✓
  Dungeon Config: DungeonSettings
  Dungeon Renderer: DungeonTilemap
```

### Шаг 8: Запуск!

```
1. Нажми кнопку Play ▶ (в центре сверху)
2. Жди 1-2 секунды пока компилируется и генерируется
3. На сцене должны появиться светлые прямоугольники
4. В консоли должны появиться логи подземелья
```

**Если что-то не так:**
```
1. Посмотри в консоль (Window → General → Console)
2. Найди красные сообщения об ошибках
3. Исправь ошибку и повтори шаг 8
```

---

## Проверка результата

### На Game View (основное окно):
```
[Светлый прямоугольник] [Светлый прямоугольник]
[Тёмное пространство]
[Светлый прямоугольник]
```

Светлые прямоугольники = комнаты
Тёмное = коридоры (пока не реализовано визуально)

### В Console (внизу):
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

Комнаты:
  [0] Room(Start, pos=(10,10), size=(12x15))
  [1] Room(Normal, pos=(25,30), size=(18x12))
  ...
```

**Если видишь это - УСПЕХ! Генератор работает!**

---

## Экспериментирование

### Измение параметров без перезапуска:

```
1. Выбери DungeonGenerator (во время Play'а)
2. В Inspector нажми кнопку "Regenerate Dungeon"
3. Подземелье пересоздастся с другими параметрами
```

### Измение параметров конфига:

```
1. Stop (Pause проект если был Play)
2. Выбери DungeonSettings в Project
3. Измени параметры в Inspector
4. Нажми Play
5. Подземелье сгенерируется с новыми параметрами
```

---

## Быстрый чеклист

- [ ] Проект открыт в Unity
- [ ] DungeonTilemap создан (в иерархии)
- [ ] Тайлы созданы (FloorTile, WallTile в папке)
- [ ] DungeonSettings создан (в Resources)
- [ ] DungeonGenerator создан (в иерархии)
- [ ] Generator компонент добавлен и настроен
- [ ] DungeonRenderer добавлен на DungeonTilemap
- [ ] Все ссылки назначены (Config, Renderer)
- [ ] Play нажат и видны комнаты
- [ ] В консоли видна информация о подземелье

Если все пункты отмечены - ты готов!
