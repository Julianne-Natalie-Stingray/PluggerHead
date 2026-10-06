using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ordered cells visited by a carried wire. Immediate backtracking retracts only its unpinned tail.
/// 持线时经过的有序格子；原路返回只收回最后一个 Anchor 之后的路径。
/// </summary>
public sealed class TileWirePath
{
    private readonly List<Vector3Int> cells = new();
    private readonly Dictionary<Anchor, int> pins = new();

    public IReadOnlyList<Vector3Int> Cells => cells;

    public void CopyFrom(TileWirePath source)
    {
        cells.Clear();
        cells.AddRange(source.cells);
        pins.Clear();
    }

    public void Reset(Vector3Int start)
    {
        cells.Clear();
        pins.Clear();
        cells.Add(start);
    }

    public void Visit(Vector3Int cell)
    {
        if (cells.Count == 0)
        {
            cells.Add(cell);
            return;
        }

        int last = cells.Count - 1;
        if (cells[last] == cell)
        {
            return;
        }

        int pinned = 0;
        foreach (KeyValuePair<Anchor, int> pin in pins)
        {
            if (pin.Key && pin.Key.IsEngaged)
            {
                pinned = Mathf.Max(pinned, pin.Value);
            }
        }

        if (last > pinned && cells[last - 1] == cell)
        {
            cells.RemoveAt(last);
        }
        else
        {
            cells.Add(cell);
        }
    }

    public void Pin(Anchor anchor)
    {
        if (anchor && cells.Count > 0)
        {
            pins[anchor] = cells.Count - 1;
        }
    }

    public void Unpin(Anchor anchor)
    {
        pins.Remove(anchor);
    }

    public float GetLength(Tilemap tilemap)
    {
        float length = 0f;
        for (int i = 1; i < cells.Count; i++)
        {
            length += Vector3.Distance(tilemap.GetCellCenterWorld(cells[i - 1]),
                tilemap.GetCellCenterWorld(cells[i]));
        }
        return length;
    }

    public void CopyWorldPath(Tilemap tilemap, List<Vector3> destination)
    {
        destination.Clear();
        foreach (Vector3Int cell in cells)
        {
            destination.Add(tilemap.GetCellCenterWorld(cell));
        }
    }
}
