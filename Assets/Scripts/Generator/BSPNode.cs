using UnityEngine;

/// <summary>
/// Узел Binary Space Partition дерева
/// </summary>
public class BSPNode
{
    public RectInt rect;
    public BSPNode left;
    public BSPNode right;
    public Room room; // null для внутренних узлов, заполнено для листьев

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
