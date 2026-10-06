using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// An authored routing candidate under a Wire; only engaged points enter the routed path.
/// Wire 子层级中的预设绕线候选点；只有已接入的点进入路径。
/// Interact toggles engagement and obtains an order number from its cached parent Wire; it ignores the payload.
/// Interact 翻转接入状态，接入时向缓存的父 Wire 取得顺序号；不读取请求载荷。
/// This component does not move the Transform. Runtime creation is possible but node lists need refreshing.
/// 本组件不移动 Transform；允许运行时创建，但需刷新节点清单。
/// </summary>
[DisallowMultipleComponent]
public class WirePoint : MonoBehaviour, IEnvironmentInteractable
{
    public bool CanInteract => canInteract;
    public bool IsEngaged => isEngaged;
    public int EngagementSequence => engagementSequence;

    public event Action<IEnvironmentInteractable> OnInteracted;

    [SerializeField, BoxGroup("Routing")]
    [Tooltip("Allows toggling engagement. False does not automatically add this point to the wire path.")]
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
    /// Implementation approach: refuse without side effects when interaction is disabled, otherwise flip the state,
    /// take the next order number from the owning wire so the path can be ordered, and raise OnInteracted for the
    /// facade to rebuild the circuit.
    /// 接入或解除本点的单一入口.
    /// 实现思路: 禁用交互时不改变状态; 否则翻转状态, 从所属线取下一个顺序号以便路径排序, 并触发 OnInteracted 供门面重建回路.
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
