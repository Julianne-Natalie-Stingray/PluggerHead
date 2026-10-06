/// <summary>
/// Contract for environment content that exists in the scene and can be taken into the player's inventory.
/// Subsystem: Environment.
/// Who should implement this contract: environment content that can be moved, currently Anchor. It is implemented
/// alongside IEnvironmentInteractable when the same object can also be interacted with, which is why the two
/// contracts are separate: CanPickup answers "may this be moved", CanInteract answers "may this be used here".
/// What this contract grants: a state query (CanPickup) and the ability to be taken.
/// The generic parameter of the original sketch was dropped in favour of the shared InteractionDetails base, so
/// that the facade can hold and dispatch every environment object uniformly.
/// 场景中存在、并可被收进玩家背包的环境内容的契约.
/// Subsystem 归属: Environment.
/// 谁应该实现这个契约: 可被移动的环境内容, 当前是 Anchor. 当同一物体也能被交互时, 它会同时实现 IEnvironmentInteractable,
/// 这正是两个契约分开的原因: CanPickup 回答"能不能被搬动", CanInteract 回答"能不能在这里被使用".
/// 契约赋予了什么特性: 状态查询(CanPickup)与被拾取的能力.
/// 原稿的泛型参数已改为共用的 InteractionDetails 基类, 使门面能统一持有并分派所有环境物体.
/// </summary>
public interface IEnvironmentPickup
{
    /// <summary>
    /// Whether this object may currently be taken. False must make Pickup a no-op.
    /// 当前是否可以被拾取. 为假时 Pickup 必须是无副作用的空操作.
    /// </summary>
    bool CanPickup { get; }

    /// <summary>
    /// Single entry point for taking this object.
    /// Implementation approach: gate on CanPickup, then hand the object's inventory form to whoever asked, and
    /// leave the scene object in a state its owning side can restore on drop.
    /// The trigger (player input, carry animation) belongs to the Player subsystem, not to this contract.
    /// 拾取本物体的单一入口.
    /// 实现思路: 先按 CanPickup 设门, 再把该物体的"背包形态"交给发起方, 并让场景物体进入其拥有方在放下时可恢复的状态.
    /// 触发者(玩家输入, 携带动画)属于 Player 子系统, 不属于本契约.
    /// </summary>
    void Pickup(InteractionDetails details);
}
