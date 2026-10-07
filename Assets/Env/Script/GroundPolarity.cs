using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 为地面赋予电线极性；PlayerMove 在脚下支撑接触中检查持线匹配，不匹配时死亡。
/// 挂在非 Trigger Collider2D 所在物体或其父物体上。
/// </summary>
[DisallowMultipleComponent]
public class GroundPolarity : MonoBehaviour
{
    [SerializeField, Dropdown(nameof(AllowedPolarities))]
    [Tooltip("地面只允许 Live 或 Neutral；其他值属于配置错误。")]
    private WirePolarity polarity = WirePolarity.Live;

    private WirePolarity[] AllowedPolarities => new[] { WirePolarity.Live, WirePolarity.Neutral };

    public WirePolarity Polarity
    {
        get
        {
            ValidatePolarity();
            return polarity;
        }
    }

    private void Awake()
    {
        ValidatePolarity();
    }

    private void OnValidate()
    {
        ValidatePolarity();
    }

    private void ValidatePolarity()
    {
        if (polarity != WirePolarity.Live && polarity != WirePolarity.Neutral)
        {
            throw new InvalidOperationException($"GroundPolarity requires Live or Neutral; received {polarity} ({(int)polarity}).");
        }
    }

    /// <summary>只有正在持有且单极性完全相同的电线能保护站在此地面上的玩家。</summary>
    public bool CanSupport(Wire wire)
    {
        ValidatePolarity();
        return wire && wire.IsHeld && wire.Polarity == polarity;
    }
}
