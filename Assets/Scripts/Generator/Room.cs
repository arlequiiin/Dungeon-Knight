using UnityEngine;

// Представляет отдельную комнату в подземелье
public class Room
{
    public RoomType type;
    public RectInt bounds; // координаты и размер комнаты в пиксель-координатах
    public Vector2 center => bounds.center;

    // Для графа
    public int nodeId = -1; // айди узла в графе комнат
    public int distanceToBoss = int.MaxValue; // расстояние до комнаты босса

    public Room(RoomType type, RectInt bounds, int nodeId = -1)
    {
        this.type = type;
        this.bounds = bounds;
        this.nodeId = nodeId;
    }

    public override string ToString()
    {
        return $"Room({type}, pos=({bounds.x},{bounds.y}), size=({bounds.width}x{bounds.height}))";
    }
}
