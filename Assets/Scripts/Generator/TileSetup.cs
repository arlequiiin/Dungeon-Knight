using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Вспомогательный скрипт для быстрой настройки тайлов из Editor'а
/// </summary>
public class TileSetup
{
#if UNITY_EDITOR
    /// <summary>
    /// Создаёт простые тайлы белого и серого цвета для тестирования
    /// </summary>
    [MenuItem("Dungeon/Setup/Create Default Tiles")]
    public static void CreateDefaultTiles()
    {
        string tilePath = "Assets/Tiles";

        // Проверяем наличие папки
        if (!AssetDatabase.IsValidFolder(tilePath))
        {
            AssetDatabase.CreateFolder("Assets", "Tiles");
        }

        // Создаём белый спрайт для пола
        var floorSprite = CreateSimpleSprite("FloorSprite", Color.white, tilePath);

        // Создаём серый спрайт для стен
        var wallSprite = CreateSimpleSprite("WallSprite", new Color(0.5f, 0.5f, 0.5f), tilePath);

        // Создаём тайлы
        CreateTile("FloorTile", floorSprite, tilePath);
        CreateTile("WallTile", wallSprite, tilePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TileSetup] Тайлы созданы в папке " + tilePath);
    }

    /// <summary>
    /// Создаёт простой спрайт из одного цвета
    /// </summary>
    private static Sprite CreateSimpleSprite(string name, Color color, string path)
    {
        // Создаём текстуру 16x16
        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);

        // Заполняем цветом
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();

        // Сохраняем текстуру
        byte[] pngData = texture.EncodeToPNG();
        string texturePath = path + "/" + name + ".png";
        System.IO.File.WriteAllBytes(texturePath, pngData);
        Object.DestroyImmediate(texture);

        // Импортируем и настраиваем текстуру
        AssetDatabase.ImportAsset(texturePath);
        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16; // 16x16 спрайт = 1 юнит
        importer.filterMode = FilterMode.Point; // Pixel art style
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        // Загружаем спрайт
        return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
    }

    /// <summary>
    /// Создаёт Tile asset
    /// </summary>
    private static void CreateTile(string name, Sprite sprite, string path)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.name = name;

        string tilePath = path + "/" + name + ".asset";
        AssetDatabase.CreateAsset(tile, tilePath);
    }

    /// <summary>
    /// Создаёт сцену для тестирования с Grid и Tilemap
    /// </summary>
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
