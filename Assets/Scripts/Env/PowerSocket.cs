using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Scene outlet owning a configured list of wires and announcing plug requests to EnvironmentFacade.
/// 场景插座，维护配置的电线列表，并向 EnvironmentFacade 通知插入请求。
/// LinkWires assigns ownership; owned wires use this socket tile as their fixed end.
/// LinkWires 设置归属；所属电线以本插座格子为固定端，不创建玩家。
/// Interact checks canInteract only; Actor, Target and component enable state are not validated here.
/// Interact 仅检查 canInteract，不校验 Actor、Target 或组件启用状态。
/// IsGroundTerminal is retained configuration with no current gameplay consumer.
/// IsGroundTerminal 是保留配置，当前玩法没有读取或执行该开关。
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
    /// The wire the level starts the run with: the first valid reference in the configured list. Null when none exists.
    /// 关卡开局时玩家手中那根线: 配置列表中的首个有效引用. 没有有效引用时为 null.
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
    [Tooltip("Reserved ground-terminal flag; currently not consumed by circuit logic.")]
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
