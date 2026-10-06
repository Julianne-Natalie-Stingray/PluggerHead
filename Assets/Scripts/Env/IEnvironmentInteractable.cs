using System;

/// <summary>
/// Contract for a mechanism in the scene that the player interacts with and never picks up: an outlet, a live
/// interface, a routing point, a fixed anchor.
/// Subsystem: Environment.
/// Who should implement this contract: environment content owned by this Subsystem. A pickup implements
/// IEnvironmentPickup as well when it can also be moved; the two contracts describe different questions and are
/// deliberately not merged.
/// What this contract grants: a state query (CanInteract), the ability to be interacted with (Interact), and the
/// notification that an interaction happened (OnInteracted).
/// CanInteract is a state check for whoever wants it, not the enforcement point: an implementation must gate
/// itself, and Interact must be a side-effect-free no-op while CanInteract is false, so a caller that forgets to
/// check cannot corrupt state.
/// 场景中玩家可交互、但永不被捡起的机关的契约: 插座孔, 带电接口, 绕线点, 固定锚点.
/// Subsystem 归属: Environment.
/// 谁应该实现这个契约: 本 Subsystem 拥有的环境内容. 若某物同时可被移动, 它另外实现 IEnvironmentPickup;
/// 两个契约回答的是不同问题, 因此刻意不合并.
/// 契约赋予了什么特性: 状态查询(CanInteract), 被交互的能力(Interact), 以及"发生过一次交互"的通知(OnInteracted).
/// CanInteract 是给需要的人用的状态查询, **不是**强制点: 实现必须自己设门, 且 CanInteract 为假时 Interact 必须是无副作用空操作,
/// 这样漏查的调用方也不会破坏状态.
/// </summary>
public interface IEnvironmentInteractable
{
    /// <summary>
    /// Whether an interaction would currently do anything. False must make Interact a no-op.
    /// 当前交互是否会真正生效. 为假时 Interact 必须是无副作用的空操作.
    /// </summary>
    bool CanInteract { get; }

    /// <summary>
    /// Raised after an interaction actually took effect. The facade subscribes to rebuild the circuit from every
    /// node instead of relying on the payload, so the argument is only the interactable itself.
    /// 交互**确实生效之后**触发. 门面订阅它以便从所有节点重建回路, 而不是依赖载荷, 因此参数只有交互者本身.
    /// </summary>
    event Action<IEnvironmentInteractable> OnInteracted;

    /// <summary>
    /// Single entry point for interacting with this mechanism.
    /// Implementation approach: gate on CanInteract first, then apply the effect, then raise OnInteracted. An
    /// implementation that cannot serve the request logs it and returns without side effects.
    /// 与本机关交互的单一入口.
    /// 实现思路: 先按 CanInteract 设门, 再施加效果, 最后触发 OnInteracted. 无法服务该请求的实现应记录日志并**无副作用**返回.
    /// </summary>
    void Interact(InteractionDetails details);
}
