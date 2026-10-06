using UnityEngine;

/// <summary>
/// Details of one interaction with a routing point.
/// Subsystem: Environment.
/// It deliberately adds no field today: the point decides its own direction from its own state, and the base
/// payload already carries the actor. The type exists so call sites read as what they are, and so a future field
/// has an obvious home instead of widening the base payload for every interactable.
/// 与一个绕线点交互一次所需的细节.
/// Subsystem 归属: Environment.
/// 它今天刻意不加任何字段: 点的方向由它自身状态决定, 而基类载荷已带有交互者.
/// 该类型存在的意义是让调用点自解释, 并给将来的字段一个显然的落点, 而不是为所有交互者扩大基类载荷.
/// </summary>
public sealed class WirePointDetails : InteractionDetails
{
    public WirePointDetails(GameObject actor, GameObject target)
        : base(actor, target)
    {
    }
}
