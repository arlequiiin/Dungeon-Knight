# Dungeon Knight - Known Bugs

## Critical

### [BUG-001] Templar shield не снижает урон
- **Файл:** `Assets/Scripts/Hero/HeroStats.cs` -> `TakeDamage()`
- **Суть:** `TakeDamage` проверяет только `KnightAbility.DamageMultiplier`, но не `TemplarAbility`. Щит Темплара активируется визуально, но урон не снижается.
- **Исправление:** добавить проверку `TemplarAbility.DamageMultiplier` в `TakeDamage()` аналогично Knight.

### [BUG-002] Projectiles не сетевые (Archer/Wizard)
- **Файл:** `Assets/Scripts/Hero/Abilities/ArcherAbility.cs`, `WizardAbility.cs`
- **Суть:** `Attack1()` вызывает `Instantiate` локально на клиенте, а не через `[Command]` на сервере. Снаряды не являются серверными Mirror-объектами. Урон работает только если хост стреляет.
- **Исправление:** перенести спавн снарядов в `[Command]` и использовать `NetworkServer.Spawn()`.

## Medium

### [BUG-003] Energy система не подключена
- **Файл:** `Assets/Scripts/Hero/HeroStats.cs`, все `*Ability.cs`
- **Суть:** `SpendEnergy()` / `RestoreEnergy()` реализованы, но ни один герой не вызывает их. Abilities и Dodge бесконечны.
- **Исправление:** добавить `SpendEnergy()` в каждый ability и dodge.

### [BUG-004] ~~MobHealth жёстко привязан к SkeletonAI~~ ИСПРАВЛЕНО
- Заменено на `GetComponent<MobAI>()` — работает с любыми наследниками.

### [BUG-006] Камера не следует за игроком в лобби
- **Файл:** `Assets/Scripts/PlayerController.cs` -> `OnStartLocalPlayer()`
- **Суть:** `SetTarget()` вызывается в `OnStartLocalPlayer()`, который срабатывает один раз. При смене сцены камера на новой сцене не привязывается к игроку.
- **Исправление:** привязывать камеру при каждой смене сцены или в `OnEnable()`.

## Low

### [BUG-005] Wizard abilities — заглушки
- **Файл:** `Assets/Scripts/Hero/Abilities/WizardAbility.cs`
- **Суть:** `OnAbility1()` и `OnAbility2()` содержат только `// TODO`. Анимации запускаются, но эффекта нет.
