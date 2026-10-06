/// <summary>
/// The form one pickup takes while it is held by an inventory.
/// Subsystem: Environment.
/// Who should implement this contract: the data type describing one inventory stack, currently AnchorInstance.
/// What this contract grants: the stack ceiling and the current count, so an inventory can decide whether a new
/// pickup merges into an existing stack or starts a new one.
/// Mutation is deliberately absent: an inventory changes the count, and the inventory model is not settled yet, so
/// this contract only answers questions. A mutating member is added once the owning inventory side decides its
/// shape, rather than being guessed here.
/// 某个可拾取物被背包持有时所取的形态.
/// Subsystem 归属: Environment.
/// 谁应该实现这个契约: 描述一个背包堆叠的数据类型, 当前是 AnchorInstance.
/// 契约赋予了什么特性: 堆叠上限与当前数量, 使背包能判断新的拾取是并入已有堆叠还是新建一个.
/// 变更接口刻意缺席: 改数量的是背包, 而背包模型尚未定案, 因此本契约只回答问题.
/// 变更成员等拥有它的背包侧定下形态后再加, 而不是在这里猜.
/// </summary>
public interface IPickupInstance
{
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
