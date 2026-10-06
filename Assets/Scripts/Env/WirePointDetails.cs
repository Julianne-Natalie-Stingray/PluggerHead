using UnityEngine;

/// <summary>
/// Marker payload used by routing-point callers; it adds no fields to InteractionDetails.
/// 绕线点调用方使用的载荷类型，不增加字段。
/// WirePoint currently toggles its own state without reading this payload or requiring this derived type.
/// WirePoint 当前不读取载荷，也不要求该派生类型，只按自身状态翻转接入状态。
/// </summary>
public sealed class WirePointDetails : InteractionDetails
{
    public WirePointDetails(GameObject actor, GameObject target)
        : base(actor, target)
    {
    }
}
