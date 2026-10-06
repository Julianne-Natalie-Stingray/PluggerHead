using System;
using UnityEngine;

/// <summary>
/// Inventory form of a picked up Anchor.
/// Subsystem: Environment.
/// Where it lives: nowhere in the scene. It is created when an anchor is stowed and is meant to be carried by an
/// inventory; the scene object itself is kept and reused rather than rebuilt.
/// Responsibility: answer the two stack questions of IPickupInstance and keep the binding back to the scene
/// anchor that this instance came from.
/// Does NOT own: the carrying, the drop, or the placement. Those belong to whoever holds the instance.
/// Lifetime: created on pickup, discarded on drop.
/// Data overview: the stack ceiling and count are serializable payload; the source anchor is a runtime binding and
/// is deliberately not serialized, because a scene object cannot be persisted as JSON and the persistence format
/// belongs to the inventory side that is not settled yet.
/// 被拾起的 Anchor 在背包中的形态.
/// Subsystem 归属: Environment.
/// 存在位置: 场景中无处. 它在锚点被收起时创建, 预期由背包持有; 场景物体本身被保留复用, 而不是重建.
/// 职能: 回答 IPickupInstance 的两个堆叠问题, 并保留指回"该实例来自哪个场景锚点"的绑定.
/// 不负责: 携带, 放下, 放置. 这些归持有该实例的一方.
/// 生命周期: 拾取时创建, 放下时丢弃.
/// 数据概览: 堆叠上限与数量是可序列化载荷; 源锚点是运行期绑定, 刻意不序列化 ——
/// 场景物体无法以 JSON 形式持久化, 而持久化格式属于尚未定案的背包侧.
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

    [SerializeField] private int maxStackAmount = 1;
    [SerializeField] private int currentStackAmount = 1;

    public AnchorInstance(Anchor source)
    {
        Source = source;
    }

    // TODO: Persistence and stack mutation are not implemented. Neither a parameterless constructor nor a way to
    // change CurrentStackAmount exists, so this instance cannot yet survive a save/load cycle or merge two pickups.
    // Not implemented because the inventory model that would decide both is owned by the Player side and is still
    // open; guessing it here would fix a format that side has not chosen.
    // TODO: 持久化与堆叠变更未实现. 既没有无参构造函数, 也没有修改 CurrentStackAmount 的途径, 因此该实例尚不能跨存读档存活,
    // 也不能合并两次拾取. 未实现原因: 决定这两件事的背包模型归 Player 侧且仍未定案, 在这里猜会把一个它尚未选择的格式固定下来.
}
