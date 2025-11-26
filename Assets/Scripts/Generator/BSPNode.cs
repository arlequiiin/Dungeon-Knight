using UnityEngine;

public class BSPNode
{
    public RectInt rect;
    public BSPNode left;
    public BSPNode right;
    public Room room; 

    public bool IsLeaf => left == null && right == null;

    public BSPNode(RectInt rect)
    {
        this.rect = rect;
    }

    public override string ToString()
    {
        return $"BSPNode(rect={rect}, isLeaf={IsLeaf})";
    }
}
