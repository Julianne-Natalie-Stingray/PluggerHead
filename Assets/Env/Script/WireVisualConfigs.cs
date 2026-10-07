using UnityEngine;

/// <summary>
/// Shared material choices for the three electrical wire types.
/// 三种电线共用的外观配置；贴图和颜色由材质资产管理，不在运行时染色。
/// </summary>
[CreateAssetMenu(fileName = "WireVisualConfigs", menuName = "PluggerHead/Environment/Wire Visual Configs")]
public sealed class WireVisualConfigs : ScriptableObject
{
    [SerializeField] private Material liveMaterial;
    [SerializeField] private Material neutralMaterial;
    [SerializeField] private Material groundMaterial;

    public Material GetMaterial(WirePolarity polarity)
    {
        switch (polarity)
        {
            case WirePolarity.Live:
                return liveMaterial;
            case WirePolarity.Neutral:
                return neutralMaterial;
            case WirePolarity.Ground:
                return groundMaterial;
            default:
                return null;
        }
    }
}
