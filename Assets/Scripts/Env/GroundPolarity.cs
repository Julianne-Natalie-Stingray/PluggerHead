using UnityEngine;

/// <summary>
/// 为地面赋予电线极性；PlayerMove 在脚下支撑接触中检查并处理异极死亡。
/// 挂在非 Trigger Collider2D 所在物体或其父物体上。
/// </summary>
[DisallowMultipleComponent]
public class GroundPolarity : MonoBehaviour
{
    [SerializeField]
    [Tooltip("地面电性。Live 与 Neutral 相反；None 和 Ground 不产生异极死亡。")]
    private WirePolarity polarity = WirePolarity.Live;

    public WirePolarity Polarity => polarity;

    /// <summary>火线与零线互为相反极性；组合值只要含有相反电性即构成危险。</summary>
    public bool IsOppositeTo(WirePolarity wirePolarity)
    {
        return ((polarity & WirePolarity.Live) != 0 && (wirePolarity & WirePolarity.Neutral) != 0) ||
            ((polarity & WirePolarity.Neutral) != 0 && (wirePolarity & WirePolarity.Live) != 0);
    }
}
