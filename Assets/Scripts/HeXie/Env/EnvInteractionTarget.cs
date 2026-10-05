using UnityEngine;

/// <summary>
/// 示例目标分类：挂到物品或机关根对象，根对象或子对象需有 Collider2D。
/// 分类和可交互条件仅供 Player 示例查询使用，最终 Env 数据模型由 Jill 决定。
/// </summary>
[DisallowMultipleComponent]
public class EnvInteractionTarget : MonoBehaviour
{
    public enum OperationType
    {
        PickUp,
        Interact
    }

    [SerializeField] private OperationType operation = OperationType.Interact;

    public OperationType Operation => operation;
}
