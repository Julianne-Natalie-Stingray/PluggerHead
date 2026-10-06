using System;
using UnityEngine;

/// <summary>
/// Inventory form of a picked up Anchor.
/// Subsystem: Environment.
/// Where it lives: nowhere in the scene. It is created when an anchor is stowed and is meant to be carried by an
/// inventory; the scene object itself is kept and reused rather than rebuilt.
/// Responsibility: answer the two stack questions of IPickupInstance and keep the binding back to the scene
/// anchor that this instance came from.
/// The inventory decides when and where to drop; this instance restores its source and prevents reuse.
/// Lifetime: created on pickup, discarded on drop.
/// Data overview: the stack ceiling and count are serializable payload; the source anchor is a runtime binding and
/// is deliberately not serialized, because this inventory stores runtime scene bindings and does not implement save/load.
/// 被拾起的 Anchor 在背包中的形态.
/// Subsystem 归属: Environment.
/// 存在位置: 场景中无处. 它在锚点被收起时创建, 预期由背包持有; 场景物体本身被保留复用, 而不是重建.
/// 职能: 回答 IPickupInstance 的两个堆叠问题, 并保留指回"该实例来自哪个场景锚点"的绑定.
/// 背包决定何时及在哪里放下; 本实例负责恢复来源物体并防止重复使用.
/// 生命周期: 拾取时创建, 放下时丢弃.
/// 数据概览: 堆叠上限与数量是可序列化载荷; 源锚点是运行期绑定, 刻意不序列化 ——
/// 当前背包保存运行期场景绑定, 尚不支持存读档.
/// </summary>
[Serializable]
public sealed class AnchorInstance : IPickupInstance
{
    public int MaxStackAmount => maxStackAmount;
    public int CurrentStackAmount => currentStackAmount;

    /// <summary>
    /// The scene anchor this instance came from; null once that object is gone.
    /// 本实例来源的那个场景锚点; 该物体消失后为 null.
    /// </summary>
    public Anchor Source { get; private set; }
    public GameObject SourceObject => Source ? Source.gameObject : null;

    [SerializeField] private int maxStackAmount = 1;
    [SerializeField] private int currentStackAmount = 1;

    public AnchorInstance(Anchor source)
    {
        Source = source;
    }

    public bool TryDrop(Vector3 position)
    {
        if (!Source || !Source.Restore(position))
        {
            return false;
        }

        Source = null;
        currentStackAmount = 0;
        return true;
    }

    // Instances are runtime bindings; save/load and stack merging are not supported.
    // 实例仅保存运行期场景绑定; 不支持存读档及堆叠合并.
}
