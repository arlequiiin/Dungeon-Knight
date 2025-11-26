using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Вспомогательный скрипт для быстрой настройки тайлов из Editor'а
public class TileSetup
{
#if UNITY_EDITOR
    // Создаёт сцену для тестирования с Grid и Tilemap
    [MenuItem("Dungeon/Setup/Create Test Scene")]
    public static void CreateTestScene()
    {
        // Создаём Grid
        var gridGO = new GameObject("DungeonGrid");
        var grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(1, 1, 0);

        // Создаём Tilemap
        var tilemapGO = new GameObject("DungeonTilemap");
        tilemapGO.transform.parent = gridGO.transform;
        var tilemap = tilemapGO.AddComponent<Tilemap>();
        var tilemapRenderer = tilemapGO.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = 0;

        // Создаём Generator
        var generatorGO = new GameObject("DungeonGenerator");
        var generator = generatorGO.AddComponent<Generator>();

        // Создаём DungeonRenderer
        var rendererComponent = tilemapGO.AddComponent<DungeonRenderer>();
        rendererComponent.GetType().GetField("tilemap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(rendererComponent, tilemap);

        // Связываем компоненты
        generator.GetType().GetField("dungeonRenderer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(generator, rendererComponent);

        Debug.Log("[TileSetup] Тестовая сцена создана!");
    }

#endif
}
