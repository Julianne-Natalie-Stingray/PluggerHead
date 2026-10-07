using UnityEngine;

/// <summary>
/// Payload supplied directly to an interactable or pickup; construction does not validate either object.
/// 交互或拾取调用方直接传入的载荷；构造时不校验 Actor 或 Target，具体校验由接收者负责。
/// The environment does not broadcast this payload to all nodes.
/// 环境门面不会向所有节点广播该载荷。
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
