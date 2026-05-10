# Analytics — система сбора метрик забегов

Полный пакет: логгер + хуки + парсер.

## Что собирается

| Событие | Когда | Поля |
|---|---|---|
| `run_start` | Старт забега (генерация подземелья) | level, difficulty, players, seed |
| `room_enter` | Игрок вошёл в комнату | room, type |
| `room_clear` | Комната зачищена | room, type, duration |
| `mob_killed` | Моб убит | mob (mobName из MobData), boss (true/false) |
| `player_downed` | Игрок упал | hero |
| `run_end` | Забег закончен | result (victory/defeat), players |

Каждое событие — строка NDJSON с timestamp `t` (секунды от старта забега).

## Файлы

- **[Analytics.cs](Assets/Scripts/Analytics/Analytics.cs)** — статический логгер.
- **[analyze_runs.py](Tools/analyze_runs.py)** — Python-парсер, без зависимостей.

## Где появятся логи

`%USERPROFILE%/AppData/LocalLow/<CompanyName>/<ProductName>/Analytics/run_<timestamp>.ndjson`

Имя CompanyName/ProductName — из Project Settings. При старте забега в Console печатается полный путь.

## Как тестировать

1. Запусти Build → пройди забег → проиграй или выиграй.
2. В папке Analytics появится `run_20261110_143022_4521.ndjson`.
3. Открой — увидишь NDJSON, по строке на событие.
4. Запусти парсер:

```bash
python Tools/analyze_runs.py
```

Без аргументов он сам найдёт папку. Можно явно: `python analyze_runs.py "C:/.../Analytics/"`.

## Пример вывода парсера

```
=== Сводка по 12 забегам ===
Завершено: 10, брошено/незакрыто: 2
Средняя длина забега: 480 сек (8.0 мин)

--- Винрейт по уровням ---
  LevelUndeadCrypt              7W /   3L  (70% WR, n=10)

--- Где умирают (последняя комната перед поражением) ---
  Комната 7: 2 смертей
  Комната 4: 1 смертей

--- Убито боссов ---
  Skeleton Overlord: 7

--- Падений по героям ---
  Knight: 3
  Wizard: 2
```

## Раздача тестерам

1. Дай билд + просьбу: «после игр прислать архив папки `LocalLow/.../<Game>/Analytics/`».
2. Тебе пришлют zip с .ndjson файлами.
3. Распакуй в одну папку → запусти парсер с этой папкой.

## Что добавить позже

Если захочешь больше детализации (без переписывания):

- В `WeaponHitbox.OnTriggerEnter2D` — событие `damage_dealt` (источник/цель/dmg) → можно строить DPS-таблицы по героям.
- В `HeroAbility.OnAbility1Started` — `ability_used` → реальная частота использования способностей.
- При выборе героя в лобби — `hero_picked` → pick rate.
- На `ChestInteractor` выборе награды — `reward_chosen` → какие награды берут чаще.

Каждое — 1 строка `Analytics.Event(...)` в нужном месте. Парсер парсит любые поля — расширения не сломают существующий вывод.
