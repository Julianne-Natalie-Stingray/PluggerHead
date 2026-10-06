using UnityEngine;

/// <summary>
/// 为地面赋予电线极性；PlayerMove 在脚下支撑接触中检查持线匹配，不匹配时死亡。
/// 挂在非 Trigger Collider2D 所在物体或其父物体上。
/// </summary>
[DisallowMultipleComponent]
public class GroundPolarity : MonoBehaviour
{
    [SerializeField]
    [Tooltip("仅持有同极性电线时安全（Live、Neutral 或 Ground）；None 和组合值不允许通行。")]
    private WirePolarity polarity = WirePolarity.Live;

    public WirePolarity Polarity => polarity;

    /// <summary>只有正在持有且单极性完全相同的电线能保护站在此地面上的玩家。</summary>
    public bool CanSupport(Wire wire)
    {
        bool validPolarity = polarity == WirePolarity.Live || polarity == WirePolarity.Neutral ||
            polarity == WirePolarity.Ground;
        return validPolarity && wire && wire.IsHeld && wire.Polarity == polarity;
    }
}
