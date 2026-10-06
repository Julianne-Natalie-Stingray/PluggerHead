using UnityEngine;

/// <summary>
/// A carried scene object's inventory representation, including source, drop behavior and stack metadata.
/// 携带物的背包表示，包含来源物体、放下行为及堆叠元数据。
/// PlayerInventory removes membership after successful TryDrop; it currently does not merge stacks.
/// PlayerInventory 在 TryDrop 成功后移除条目；当前不使用数量字段进行堆叠合并。
/// </summary>
public interface IPickupInstance
{
    /// <summary>The preserved scene object. 被保留的场景物体.</summary>
    GameObject SourceObject { get; }

    /// <summary>Restore the carried object at a world position; failure preserves the item.
    /// 在世界坐标处放回携带物; 失败时保留物品.</summary>
    bool TryDrop(Vector3 position);

    /// <summary>
    /// Stack ceiling of this instance; 1 means it never stacks.
    /// 本实例的堆叠上限; 1 表示不可堆叠.
    /// </summary>
    int MaxStackAmount { get; }

    /// <summary>
    /// How many are currently in this stack.
    /// 当前堆叠数量.
    /// </summary>
    int CurrentStackAmount { get; }
}
