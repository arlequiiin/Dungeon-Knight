# Мобы и боссы по биомам

Список основан на содержимом `Assets/Sprites/<Biome>/Mobs/`. Для каждого моба указан
предполагаемый архетип и AI-класс (`MeleeMobAI` / `RangedMobAI` / собственный
для боссов).

Архетипы:
- **Обычный** — базовые статы, 1-2 атаки.
- **Дальний** — снаряд, держит дистанцию, отступает при сближении.
- **Бронированный** — `canBeInterrupted=false`, ↑maxPoise, ↑HP.
- **Даггер / тяжёлый** — ↓скорость, ↑damage, длинный recovery.
- **Элитный** — ↑↑HP, ↑↑poise, несколько атак с весами.
- **Босс** — отдельный AI-класс с фазами/призывами.

---

## Undead Crypt (готов)

| Моб | Архетип | AI-класс | Статус |
|---|---|---|---|
| SkeletonWarrior | Обычный | `MeleeMobAI` | ✅ |
| SkeletonGreatsword | Тяжёлый (3 атаки) | `MeleeMobAI` | ✅ |
| ArmoredSkeleton | Бронированный | `MeleeMobAI` | ✅ |
| SkeletonArcher | Дальний | `RangedMobAI` | ✅ |
| **SkeletonOverlord** | **Босс** | `SkeletonOverlordAI` | ✅ |

---

## Cursed Wilds (TODO)

| Моб | Архетип | AI-класс | Статус |
|---|---|---|---|
| Slime | Обычный (слабый, рой) | `MeleeMobAI` | ⬜ |
| Werewolf | Обычный (быстрый) | `MeleeMobAI` | ⬜ |
| DarkWarrior | Бронированный | `MeleeMobAI` | ⬜ |
| Werebear | Элитный (тяжёлый) | `MeleeMobAI` | ⬜ |
| **DarkRider** | **Босс** | `DarkRiderAI` (создать) | ⬜ |

Замечания:
- Slime — кандидат на «рой»: маленький HP, низкий урон, спавнить пачками.
- Werewolf — высокая `moveSpeed`, низкий `attackCooldown`, но хрупкий.
- DarkWarrior — `canBeInterrupted=false`, средний урон, средний HP.
- Werebear — большой HP/poise, медленный, тяжёлый удар.
- DarkRider — босс на коне; возможные механики: рывок-чардж через комнату, призыв
  волков-миньонов, фаза при 50% HP.

---

## Orc Camp (TODO)

| Моб | Архетип | AI-класс | Статус |
|---|---|---|---|
| OrcGrunt | Обычный | `MeleeMobAI` | ⬜ |
| ArmoredOrc | Бронированный | `MeleeMobAI` | ⬜ |
| OrcElite | Элитный (3 атаки) | `MeleeMobAI` | ⬜ |
| **OrcWarlordRider** | **Босс** | `OrcWarlordRiderAI` (создать) | ⬜ |

Замечания:
- В Orc Camp **нет дальнего юнита** — биом полностью ближний бой. Если хочется
  разнообразия — добавить шамана/лучника в спрайтах позже.
- OrcGrunt = аналог SkeletonWarrior в новом биоме.
- OrcElite — кандидат на 3 атаки с разными весами (как Greatsword).
- OrcWarlordRider — босс на варге; возможные механики: чардж + ground slam,
  призыв OrcGrunt'ов, ярость при низком HP.

---

## Что нужно создать на каждый новый моб

1. **Префаб** в `Assets/Prefabs/Mobs/<Biome>/`: спрайт + Animator + `MeleeMobAI` или
   `RangedMobAI` + `MobHealth` + `NavMeshAgent` + `WeaponHitbox` (или `projectilePrefab`).
2. **MobData** ассет (`Create → Dungeon Knight → Mob Data`): статы, attackDamages,
   attackTriggers, флаги (`usesAttackSlot`, `canBeInterrupted`).
3. **Регистрация в NetworkManager** как spawnable prefab (для синхронизации Mirror).

## Что нужно на каждый биом

1. **MobSpawnTable_<Biome>** — веса всех мобов биома.
2. **LevelConfig_<Biome>_<Difficulty>** — собирает `GridWalkConfig` + `MobSpawnTable`
   + `bossPrefab` + сложность.
3. **Боссы** — отдельный AI-класс на наследнике `MobAI` (фазы/призывы уникальны).
