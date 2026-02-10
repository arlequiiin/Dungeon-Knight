using UnityEngine;
using UnityEngine.Tilemaps;

// Визуализация сгенерированного подземелья на Tilemap
public class DungeonRenderer : MonoBehaviour
{
    [Header("Tilemap слои")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap lavaTilemap;

    [Header("Тайлы")]
    [SerializeField] private TileBase groundTile;   // Rule Tile для земли
    [SerializeField] private TileBase lavaTile;     // Тайл лавы

    private void Start()
    {
        if (groundTilemap == null)
            Debug.LogError("Ground Tilemap не назначен!");

        if (lavaTilemap == null)
            Debug.LogError("Lava Tilemap не назначен!");

        if (groundTile == null)
            Debug.LogWarning("Ground Tile (Rule Tile) не назначен!");

        if (lavaTile == null)
            Debug.LogWarning("Lava Tile не назначен!");
    }

    // Нарисовать подземелье на карте
    public void RenderDungeon(BSPGenerator generator, DungeonConfig config)
    {
        // Очищаем тайлмапы
        groundTilemap.ClearAllTiles();
        lavaTilemap.ClearAllTiles();

        // Заполняем всю область лавой
        FillWithLava(config.mapWidth, config.mapHeight);

        // Рисуем коридоры
        foreach (var corridor in generator.Corridors)
        {
            RenderCorridor(corridor);
        }

        // Рисуем комнаты поверх лавы
        foreach (var room in generator.Rooms)
        {
            RenderRoom(room);
        }

        Debug.Log($"Отрисовано: лава {config.mapWidth}x{config.mapHeight}, комнат: {generator.Rooms.Count}, коридоров: {generator.Corridors.Count}");
    }

    // Заполняет всю карту лавой
    private void FillWithLava(int width, int height)
    {
        if (lavaTile == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                lavaTilemap.SetTile(tilePos, lavaTile);
            }
        }
    }

    // Рисует один коридор
    private void RenderCorridor(Corridor corridor)
    {
        if (groundTile == null) return;

        foreach (var tile in corridor.tiles)
        {
            Vector3Int tilePos = new Vector3Int(tile.x, tile.y, 0);
            groundTilemap.SetTile(tilePos, groundTile);
        }
    }

    // Рисует одну комнату
    private void RenderRoom(Room room)
    {
        if (groundTile == null) return;

        for (int x = room.bounds.x; x < room.bounds.x + room.bounds.width; x++)
        {
            for (int y = room.bounds.y; y < room.bounds.y + room.bounds.height; y++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                groundTilemap.SetTile(tilePos, groundTile);
            }
        }
    }

    // Отладочная отрисовка
    public void DebugRenderRooms(BSPGenerator generator, DungeonConfig config)
    {
        RenderDungeon(generator, config);
    }
}
