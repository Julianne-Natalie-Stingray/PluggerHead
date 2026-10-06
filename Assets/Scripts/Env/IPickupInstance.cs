using UnityEngine;

/// <summary>
/// The form one pickup takes while it is held by an inventory.
/// Subsystem: Environment.
/// Who should implement this contract: the data type describing one inventory stack.
/// What this contract grants: the stack ceiling and the current count, so an inventory can decide whether a new
/// pickup merges into an existing stack or starts a new one.
/// The inventory owns membership and placement; TryDrop restores the source object and consumes this instance.
/// 某个可拾取物被背包持有时所取的形态.
/// Subsystem 归属: Environment.
/// 谁应该实现这个契约: 描述一个背包堆叠的数据类型.
/// 契约赋予了什么特性: 堆叠上限与当前数量, 使背包能判断新的拾取是并入已有堆叠还是新建一个.
/// 背包拥有成员关系及放置决策; TryDrop 恢复来源物体并消耗本实例.
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
