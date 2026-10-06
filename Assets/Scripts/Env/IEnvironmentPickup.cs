/// <summary>
/// Contract for scene items that PlayerInventory can pick up through InteractionDetails.
/// PlayerInventory 使用 InteractionDetails 拾取场景道具的契约；Anchor 不实现此接口。
/// The project currently supplies consumers but no concrete pickup implementation.
/// 当前项目提供调用方，尚无具体道具实现；环境门面不负责统一分派拾取。
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
    /// <returns>The carried instance on success; null when pickup is refused. 成功时返回携带实例, 拒绝时返回 null.</returns>
    IPickupInstance Pickup(InteractionDetails details);
}
