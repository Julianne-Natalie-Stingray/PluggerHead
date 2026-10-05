using System;
using UnityEngine;

/// <summary>
/// 环境交互的统一入口，通过请求事件将玩家操作交给具体实现。
/// 当前接口和数据类型仅用于示例，最终数据设计与业务实现由 Jill 决定。
/// 拾取会将道具引用加入玩家背包；使用及交互只发出请求，不表示效果已经执行。
/// </summary>
public class EnvFacade : MonoBehaviour
{
    /// <summary>道具引用成功加入背包后发出；场景表现由订阅方实现。</summary>
    public event Action<PickUpItemData> PickUpItemRequested;

    /// <summary>使用物品请求；实际消耗及效果由订阅方实现。</summary>
    public event Action<UseItemData> UseItemRequested;

    /// <summary>交互请求；实际交互条件及行为由订阅方实现。</summary>
    public event Action<InteractionData> InteractionRequested;

    /// <summary>
    /// 将物品原引用加入发起者的背包，成功返回 true；重复拾取返回 false。
    /// 示例不隐藏或销毁场景道具，距离、所有权等业务条件由 Jill 后续定义。
    /// </summary>
    public bool PickUpItem(PickUpItemData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        PlayerInventory inventory = GetInventory(data.Actor);
        if (inventory == null || !inventory.TryAddItem(data.Item))
        {
            return false;
        }

        PickUpItemRequested?.Invoke(data);
        return true;
    }

    /// <summary>
    /// 请求使用背包中持有的物品。目标可以为空，例如使用作用于自身的物品。
    /// 返回 true 仅表示已向订阅方发出请求；不自动移除或消耗物品。
    /// </summary>
    public bool UseItem(UseItemData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        PlayerInventory inventory = GetInventory(data.Actor);
        if (inventory == null || !inventory.Contains(data.Item) || UseItemRequested == null)
        {
            return false;
        }

        UseItemRequested.Invoke(data);
        return true;
    }

    private PlayerInventory GetInventory(GameObject actor)
    {
        if (actor == null || !actor.TryGetComponent(out PlayerInventory inventory))
        {
            return null;
        }

        return inventory.Environment == this ? inventory : null;
    }

    /// <summary>
    /// 请求与环境目标交互，例如开门或操作机关。
    /// </summary>
    public void Interact(InteractionData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        InteractionRequested?.Invoke(data);
    }
}

/// <summary>
/// 捡起物品的示例数据。暂以场景对象表示物品，最终表示方式由 Jill 决定。
/// Actor 上需要挂载 PlayerInventory，并将其 Environment 指向处理请求的 Env。
/// 物品是否满足距离等拾取条件，由具体业务实现检查。
/// </summary>
[Serializable]
public sealed class PickUpItemData
{
    [Tooltip("发起拾取的对象，例如玩家。")]
    public GameObject Actor;

    [Tooltip("准备捡起的场景物品。")]
    public GameObject Item;
}

/// <summary>
/// 使用物品的示例数据。物品引用、目标及消耗规则的最终设计由 Jill 决定。
/// Item 必须是发起者背包中保存的原始对象引用。
/// </summary>
[Serializable]
public sealed class UseItemData
{
    [Tooltip("使用物品的对象，例如玩家。")]
    public GameObject Actor;

    [Tooltip("准备使用的物品；当前仅为示例对象引用。")]
    public GameObject Item;

    [Tooltip("物品的作用目标；无指定目标时可以为空。")]
    public GameObject Target;
}

/// <summary>
/// 环境交互的示例数据。目标类型和交互规则的最终设计由 Jill 决定。
/// </summary>
[Serializable]
public sealed class InteractionData
{
    [Tooltip("发起交互的对象，例如玩家。")]
    public GameObject Actor;

    [Tooltip("交互目标，例如门或机关。")]
    public GameObject Target;
}
