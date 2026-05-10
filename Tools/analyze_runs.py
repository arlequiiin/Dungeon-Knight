"""
Парсер логов Analytics.cs (NDJSON формат).

Использование:
    python analyze_runs.py <папка_с_логами>

По умолчанию ищет логи в стандартном persistentDataPath Unity на Windows:
    %userprofile%/AppData/LocalLow/<company>/<game>/Analytics/

Выводит сводку: винрейт по биому, смерти по комнатам, топ-убийц, длина забегов.
Не требует pandas — стандартная библиотека Python 3.
"""
import json
import os
import sys
from collections import Counter, defaultdict
from pathlib import Path


def load_runs(folder: Path):
    runs = []
    for path in sorted(folder.glob("run_*.ndjson")):
        events = []
        with open(path, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    events.append(json.loads(line))
                except json.JSONDecodeError:
                    pass
        if events:
            runs.append({"file": path.name, "events": events})
    return runs


def summarize(runs):
    total_runs = len(runs)
    if total_runs == 0:
        print("Нет логов для анализа.")
        return

    completed = [r for r in runs if any(e["event"] == "run_end" for e in r["events"])]
    abandoned = total_runs - len(completed)

    by_level = defaultdict(lambda: {"victory": 0, "defeat": 0})
    durations = []
    deaths_by_room = Counter()
    rooms_cleared = Counter()
    mobs_killed = Counter()
    bosses_killed = Counter()
    downs_by_hero = Counter()
    runs_by_level = Counter()

    for r in runs:
        events = r["events"]
        start = next((e for e in events if e["event"] == "run_start"), None)
        end = next((e for e in events if e["event"] == "run_end"), None)
        level = start.get("level", "?") if start else "?"
        runs_by_level[level] += 1

        if end:
            by_level[level][end.get("result", "?")] += 1
            durations.append(end.get("t", 0))

        for e in events:
            if e["event"] == "room_clear":
                rooms_cleared[e.get("type", "?")] += 1
            elif e["event"] == "mob_killed":
                if e.get("boss"):
                    bosses_killed[e.get("mob", "?")] += 1
                else:
                    mobs_killed[e.get("mob", "?")] += 1
            elif e["event"] == "player_downed":
                downs_by_hero[e.get("hero", "?")] += 1

        # последняя комната перед run_end (или последняя комната вообще, если abandoned)
        last_room = None
        for e in events:
            if e["event"] == "room_enter":
                last_room = e.get("room")
        if last_room is not None and (end is None or end.get("result") == "defeat"):
            deaths_by_room[last_room] += 1

    # ── Печать ──
    print(f"\n=== Сводка по {total_runs} забегам ===")
    print(f"Завершено: {len(completed)}, брошено/незакрыто: {abandoned}")

    if durations:
        avg = sum(durations) / len(durations)
        print(f"Средняя длина забега: {avg:.0f} сек ({avg/60:.1f} мин)")

    print("\n--- Винрейт по уровням ---")
    for level, stats in sorted(by_level.items()):
        total = stats["victory"] + stats["defeat"]
        wr = stats["victory"] / total * 100 if total else 0
        print(f"  {level:30s}  {stats['victory']:3d}W / {stats['defeat']:3d}L  ({wr:.0f}% WR, n={total})")

    print("\n--- Где умирают (последняя комната перед поражением) ---")
    for room, count in deaths_by_room.most_common(10):
        print(f"  Комната {room}: {count} смертей")

    print("\n--- Зачищено комнат по типу ---")
    for rt, count in rooms_cleared.most_common():
        print(f"  {rt}: {count}")

    print("\n--- Убито мобов (топ 15) ---")
    for mob, count in mobs_killed.most_common(15):
        print(f"  {mob}: {count}")

    if bosses_killed:
        print("\n--- Убито боссов ---")
        for boss, count in bosses_killed.most_common():
            print(f"  {boss}: {count}")

    print("\n--- Падений по героям ---")
    for hero, count in downs_by_hero.most_common():
        print(f"  {hero}: {count}")


def default_log_folder() -> Path:
    if sys.platform == "win32":
        appdata = os.environ.get("USERPROFILE", "")
        candidate = Path(appdata) / "AppData" / "LocalLow"
        # ищем папку игры — в Unity это <CompanyName>/<ProductName>/Analytics
        for company in candidate.glob("*"):
            for game in company.glob("*"):
                analytics = game / "Analytics"
                if analytics.is_dir() and any(analytics.glob("run_*.ndjson")):
                    return analytics
    return Path(".")


def main():
    if len(sys.argv) > 1:
        folder = Path(sys.argv[1])
    else:
        folder = default_log_folder()
        print(f"[i] Авто-папка: {folder}")

    if not folder.is_dir():
        print(f"Папка не найдена: {folder}")
        sys.exit(1)

    runs = load_runs(folder)
    summarize(runs)


if __name__ == "__main__":
    main()
