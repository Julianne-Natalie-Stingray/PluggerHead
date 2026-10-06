using UnityEngine;

/// <summary>
/// Payload of one environment request, shared by every interactable and pickup.
/// Subsystem: Environment.
/// Where it lives: nowhere. It is created per request by whoever triggers the interaction, and is never serialized.
/// Why a shared base instead of a generic parameter: the facade holds every interactable as
/// IEnvironmentInteractable and must broadcast one request type to all of them; a generic method on the interface
/// could not be dispatched without the caller knowing the concrete type. A derived type exists per interaction
/// that genuinely needs more fields, not per implementer.
/// 一次环境请求的载荷, 由所有可交互物与可拾取物共用.
/// Subsystem 归属: Environment.
/// 存在位置: 无. 它由触发交互的一方按次创建, 且从不被序列化.
/// 为什么用共享基类而不是泛型参数: 门面以 IEnvironmentInteractable 持有所有交互者, 必须向它们广播同一种请求类型;
/// 接口上的泛型方法无法在调用方不知道具体类型时被分派. 只有当某类交互**确实**需要更多字段时才派生新类型, 而不是每个实现者一个.
/// </summary>
public class InteractionDetails
{
    public GameObject Actor { get; }
    public GameObject Target { get; }

    public InteractionDetails(GameObject actor, GameObject target)
    {
        Actor = actor;
        Target = target;
    }
}
