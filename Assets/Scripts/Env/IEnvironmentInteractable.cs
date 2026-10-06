using System;

/// <summary>
/// Contract for a scene mechanism. Pickup capability is a separate, optional contract.
/// 场景机关交互契约；同一对象可另行实现拾取契约。
/// Implementations gate Interact on CanInteract. Notification does not guarantee a successful circuit transition.
/// 实现应在 Interact 中检查 CanInteract；事件通知不保证插线或通关成功。
/// </summary>
public interface IEnvironmentInteractable
{
    /// <summary>
    /// Whether requests are accepted by this node. False must prevent interaction state changes.
    /// 节点当前是否接受请求；为假时 Interact 不应改变交互状态。
    /// </summary>
    bool CanInteract { get; }

    /// <summary>
    /// Raised after the node processes a request; sockets may still be refused by the facade. The facade reads each
    /// node instead of relying on the payload, so the argument is only the interactable itself.
    /// 节点处理请求后触发；插口请求仍可能被门面拒绝。门面读取节点状态，事件参数只有节点本身。
    /// </summary>
    event Action<IEnvironmentInteractable> OnInteracted;

    /// <summary>
    /// Single entry point for interacting with this mechanism.
    /// Implementation approach: gate on CanInteract first, then apply the effect, then raise OnInteracted. An
    /// implementation that cannot serve the request returns without changing interaction state; logging is optional.
    /// 与本机关交互的单一入口.
    /// 实现思路: 先按 CanInteract 设门, 再施加效果, 最后触发 OnInteracted. 无法服务请求时不改变交互状态并返回；日志由实现决定。
    /// </summary>
    void Interact(InteractionDetails details);
}
