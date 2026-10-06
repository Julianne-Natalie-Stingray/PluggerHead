using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// The outlet: where the wires' fixed ends live, where the player enters the level, and where the circuit closes.
/// Subsystem: Environment.
/// Where it lives: on the scene object that represents the outlet, next to a Collider2D so the Player's range
/// detection can find it.
/// Responsibility: own the wires that leave it, and announce that a plug request arrived here.
/// Does NOT own: which wire is held, whether a wire is already closed, or the win check. Those are circuit state
/// and live in EnvironmentFacade.
/// Lifetime: part of the level object; never created at runtime.
/// The ground wire of a broken ground line may terminate here as well, which is why that is a per-outlet flag
/// rather than a separate kind of object.
/// 插座孔: 两根线固定端所在, 玩家由此进入关卡, 也是回路闭合处.
/// Subsystem 归属: Environment.
/// 存在位置: 代表插座孔的场景物体上, 与一个 Collider2D 同处, 以便 Player 的范围检测能找到它.
/// 职能: 拥有从它引出的线, 并广播"此处收到一次插入请求".
/// 不负责: 哪根线被持有, 某根线是否已闭合, 以及通关判定. 那些是回路状态, 位于 EnvironmentFacade.
/// 生命周期: 属于关卡物体; 从不在运行时创建.
/// 破裂地线的地线也可以在此终止, 因此这是一个"每个插座孔各自"的开关, 而不是另一种物体.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PowerSocket : MonoBehaviour, IEnvironmentInteractable
{
    public bool CanInteract => canInteract;
    public bool IsGroundTerminal => isGroundTerminal;
    public IReadOnlyList<Wire> Wires => wires;
    public Vector3 PlugPosition => transform.position;

    /// <summary>
    /// The wire the level starts the run with: the first one wired here. Null when the outlet has no wires.
    /// 关卡开局时玩家手中那根线: 此处接线的第一根. 插座孔没有线时为 null.
    /// </summary>
    public Wire StartingWire
    {
        get
        {
            for (int i = 0; i < wires.Count; i++)
            {
                if (wires[i])
                {
                    return wires[i];
                }
            }

            return null;
        }
    }

    public event Action<IEnvironmentInteractable> OnInteracted;

    [SerializeField, BoxGroup("Socket")]
    [Tooltip("Whether a wire may be plugged back in here at all.")]
    private bool canInteract = true;

    [SerializeField, BoxGroup("Socket")]
    [Tooltip("Whether this outlet may also terminate the ground wire of a broken ground line.")]
    private bool isGroundTerminal = false;

    [SerializeField, BoxGroup("Socket")]
    [Tooltip("The wires that leave this outlet. Each one's fixed end is here; their free ends are carried one at a time.")]
    private List<Wire> wires = new();

    private void Awake()
        => LinkWires();

    /// <summary>
    /// Single entry point for handing this outlet to its wires.
    /// Implementation approach: called from Awake, and again by the facade's sweep, because a run driven from the
    /// Editor without entering play mode would otherwise leave every wire without an outlet, and closing a wire
    /// would be refused for belonging to nobody. It is idempotent.
    /// 把本插座孔交给它的线的单一入口.
    /// 实现思路: 由 Awake 调用, 也由门面的扫描再次调用 —— 否则"不进入播放模式、直接在编辑器里驱动"的一局里,
    /// 每根线都没有插座孔, 闭合一根线会因"不属于任何插座孔"而被拒绝. 它是幂等的.
    /// </summary>
    public void LinkWires()
    {
        for (int i = 0; i < wires.Count; i++)
        {
            if (wires[i])
            {
                wires[i].SetSocket(this);
            }
        }
    }

    /// <summary>
    /// Single entry point for a plug request at this outlet.
    /// Implementation approach: gate on CanInteract, then announce the request; the facade applies it, because
    /// closing a wire changes the circuit and the circuit has exactly one owner.
    /// 本插座孔收到一次插入请求的单一入口.
    /// 实现思路: 按 CanInteract 设门, 然后广播该请求; 由门面施加它, 因为闭合一根线会改变回路, 而回路只有一个拥有者.
    /// </summary>
    public void Interact(InteractionDetails details)
    {
        if (!CanInteract)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Interaction refused: this outlet does not accept plugs. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        OnInteracted?.Invoke(this);
    }
}
