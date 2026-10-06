using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// One authored point of a wire: an endpoint or a bend.
/// Subsystem: Environment.
/// Where it lives: as a child of the Wire that owns it, so the wire finds its points through the hierarchy instead
/// of through Inspector wiring.
/// Responsibility: hold its own position, and remember whether the player has engaged it into the wire's path and
/// in which order.
/// Does NOT own: the path itself, its rendering, or whether the circuit is closed. The facade owns that state and
/// recomputes it from every node after each interaction.
/// Lifetime: part of the level object; never created at runtime.
/// Engagement, not movement: the grill sketched a point whose position travels between an anchor and the player.
/// This implementation keeps the point where the level placed it and lets engagement decide whether the wire bends
/// through it, which is the same idea expressed as a state instead of a position.
/// 一条线上由关卡摆好的点: 端点或折点.
/// Subsystem 归属: Environment.
/// 存在位置: 作为拥有它的 Wire 的子物体, 因此线通过层级而不是 Inspector 接线找到自己的点.
/// 职能: 持有自身位置, 并记住玩家是否已把它接入线的路径, 以及接入的先后顺序.
/// 不负责: 路径本身, 路径的渲染, 以及回路是否闭合. 那由门面拥有, 并在每次交互后从所有节点重算.
/// 生命周期: 属于关卡物体; 从不在运行时创建.
/// 是"接入"而不是"移动": grill 里的草图描述了一个位置在锚点与玩家之间往返的点.
/// 本实现让点留在关卡摆放的位置, 由"是否接入"决定线是否在此折向 —— 同一件事, 表达成状态而不是位置.
/// </summary>
[DisallowMultipleComponent]
public class WirePoint : MonoBehaviour, IEnvironmentInteractable
{
    public bool CanInteract => canInteract;
    public bool IsEngaged => isEngaged;
    public int EngagementSequence => engagementSequence;

    public event Action<IEnvironmentInteractable> OnInteracted;

    [SerializeField, BoxGroup("Routing")]
    [Tooltip("False marks a point the level fixes in place: the wire's shape may pass through it, but the player cannot engage or release it.")]
    private bool canInteract = true;

    private bool isEngaged;
    private int engagementSequence;

    private Wire wire;

    internal void ResetRouting()
    {
        isEngaged = false;
        engagementSequence = 0;
    }

    private void Awake()
    {
        wire = GetComponentInParent<Wire>();
    }

    /// <summary>
    /// Single entry point for engaging or releasing this point.
    /// Implementation approach: refuse without side effects when the point is fixed, otherwise flip the state,
    /// take the next order number from the owning wire so the path can be ordered, and raise OnInteracted for the
    /// facade to rebuild the circuit.
    /// 接入或解除本点的单一入口.
    /// 实现思路: 固定点时无副作用拒绝; 否则翻转状态, 从所属线取下一个顺序号以便路径排序, 并触发 OnInteracted 供门面重建回路.
    /// </summary>
    public void Interact(InteractionDetails details)
    {
        if (!CanInteract)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Interaction refused: this wire point is fixed by the level. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        isEngaged = !isEngaged;
        engagementSequence = isEngaged ? NextSequence() : 0;

        OnInteracted?.Invoke(this);
    }

    private int NextSequence()
    {
        if (!wire)
        {
            wire = GetComponentInParent<Wire>();
        }

        return wire ? wire.NextEngagementSequence() : 0;
    }
}
