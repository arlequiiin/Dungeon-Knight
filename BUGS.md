# Dungeon Knight - Known Bugs


1. Камера не следует за игроком в лобби
- **Файл:** `Assets/Scripts/PlayerController.cs` -> `OnStartLocalPlayer()`
- **Суть:** `SetTarget()` вызывается в `OnStartLocalPlayer()`, который срабатывает один раз. При смене сцены камера на новой сцене не привязывается к игроку.
- **Исправление:** привязывать камеру при каждой смене сцены или в `OnEnable()`.