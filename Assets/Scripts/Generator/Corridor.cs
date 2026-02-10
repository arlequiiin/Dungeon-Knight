using UnityEngine;
using System.Collections.Generic;

// Коридор между двумя комнатами
public class Corridor
{
    public Room roomA;
    public Room roomB;
    public HashSet<Vector2Int> tiles = new HashSet<Vector2Int>();

    public Corridor(Room a, Room b)
    {
        roomA = a;
        roomB = b;
    }
}
