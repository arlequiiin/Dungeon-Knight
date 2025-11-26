using UnityEngine;
using UnityEngine.Tilemaps;

// Визуализация сгенерированного подземелья на Tilemap
public class DungeonRenderer : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase floorTile;

    private void Start()
    {
        if (tilemap == null)
        {
            Debug.LogError("Tilemap не назначен!");
            return;
        }

        if (floorTile == null)
        {
            Debug.LogWarning("floorTile не назначен!");
        }
    }

    // Нарисовать подземелье на карте
    public void RenderDungeon(BSPGenerator generator)
    {
        // Очищаем тайлмап
        tilemap.ClearAllTiles();

        var config = generator.Rooms.Count > 0 ?
            new DungeonConfig() : null;

        // Заполняем стены (всю карту)
        // В будущем заполнить только край карты

        // Рисуем комнаты
        foreach (var room in generator.Rooms)
        {
            RenderRoom(room);
        }

        Debug.Log("Комнаты отрисованы");
    }

    // Рисует одну комнату
    private void RenderRoom(Room room)
    {
        // Рисуем пол в комнате
        for (int x = room.bounds.x; x < room.bounds.x + room.bounds.width; x++)
        {
            for (int y = room.bounds.y; y < room.bounds.y + room.bounds.height; y++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);

                // Выбираем тайл в зависимости от типа комнаты
                TileBase tile = GetTileForRoom(room);
                tilemap.SetTile(tilePos, tile);
            }
        }

        // Рисуем контур комнаты 
        RenderRoomBorder(room);
    }

    // Рисует контур комнаты
    private void RenderRoomBorder(Room room)
    {
        // Верхняя и нижняя граница
        for (int x = room.bounds.x - 1; x <= room.bounds.x + room.bounds.width; x++)
        {
            // Верх
            Vector3Int topPos = new Vector3Int(x, room.bounds.y + room.bounds.height, 0);
            if (tilemap.GetTile(topPos) == null && wallTile != null)
                tilemap.SetTile(topPos, wallTile);

            // Низ
            Vector3Int bottomPos = new Vector3Int(x, room.bounds.y - 1, 0);
            if (tilemap.GetTile(bottomPos) == null && wallTile != null)
                tilemap.SetTile(bottomPos, wallTile);
        }

        // Левая и правая граница
        for (int y = room.bounds.y; y < room.bounds.y + room.bounds.height; y++)
        {
            // Слева
            Vector3Int leftPos = new Vector3Int(room.bounds.x - 1, y, 0);
            if (tilemap.GetTile(leftPos) == null && wallTile != null)
                tilemap.SetTile(leftPos, wallTile);

            // Справа
            Vector3Int rightPos = new Vector3Int(room.bounds.x + room.bounds.width, y, 0);
            if (tilemap.GetTile(rightPos) == null && wallTile != null)
                tilemap.SetTile(rightPos, wallTile);
        }
    }

    // тайл для типа комнаты
    private TileBase GetTileForRoom(Room room)
    {
        // в будущем можно добавить разные цвета
        if (floorTile != null)
            return floorTile;

        return null;
    }

    // Нарисовать комнату для отладки
    public void DebugRenderRooms(BSPGenerator generator)
    {
        tilemap.ClearAllTiles();

        foreach (var room in generator.Rooms)
        {
            // Рисуем все тайлы комнаты
            for (int x = room.bounds.x; x < room.bounds.x + room.bounds.width; x++)
            {
                for (int y = room.bounds.y; y < room.bounds.y + room.bounds.height; y++)
                {
                    Vector3Int tilePos = new Vector3Int(x, y, 0);
                    tilemap.SetTile(tilePos, floorTile);
                }
            }

            // Рисуем стены вокруг комнаты
            RenderRoomBorder(room);
        }

        Debug.Log($"Отрисовано {generator.Rooms.Count} комнат");
    }
}
