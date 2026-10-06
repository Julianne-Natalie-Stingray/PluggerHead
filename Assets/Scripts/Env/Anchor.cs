using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// A freely placed routing fixture, never an inventory item.
/// 无限放置、收回的绕线锚点，不是背包道具。
/// Remembers its wire and routing order; Environment owns the circuit and polyline.
/// 只记录绕线归属与顺序；回路及路径由 Environment 管理。
/// Lifetime: authored in a level or created by Player, destroyed on reclaim.
/// 生命周期：关卡预设或 Player 创建，收回时销毁。
/// </summary>
[DisallowMultipleComponent]
public class Anchor : MonoBehaviour, IEnvironmentInteractable
{
    public bool CanInteract => canInteract && CanReclaim;
    public bool CanReclaim => isActiveAndEnabled && !isReclaimed && !cornerOwner;
    public bool IsEngaged => engagedBy != null;
    public int EngagementSequence => engagementSequence;

    /// <summary>
    /// The wire currently routed around this fixture, or null.
    /// 当前绕过本固定物的那根线, 若无则为 null.
    /// </summary>
    public Wire EngagedBy => engagedBy;

    public event Action<IEnvironmentInteractable> OnInteracted;

    [SerializeField, BoxGroup("Routing")]
    [Tooltip("Whether the player may route a wire around this fixture. Reclaim remains available independently of routing.")]
    private bool canInteract = true;

    private Wire engagedBy;
    private int engagementSequence;
    private bool isReclaimed;
    private Corner cornerOwner;

    /// <summary>Attach an automatic routing node owned exclusively by a scene corner.
    /// Corner 自动节点仅由所属拐角管理，不参与 J/K 手动收回。</summary>
    internal void HookAtCorner(Corner owner, Wire wire)
    {
        cornerOwner = owner;
        engagedBy = wire;
        engagementSequence = wire.NextEngagementSequence();
    }

    internal void ReleaseFromCorner()
    {
        isReclaimed = true;
        ResetRouting();
        EnvironmentFacade.ForScene(gameObject.scene)?.UnregisterAnchor(this);
        gameObject.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    internal void ResetRouting()
    {
        engagedBy = null;
        engagementSequence = 0;
    }

    /// <summary>
    /// Single entry point for routing the wire around this fixture, or releasing it.
    /// Implementation approach: refuse without side effects when routing is disabled, otherwise toggle which wire
    /// is routed here, take the next order number, and raise OnInteracted. An Anchor belongs to at most one wire
    /// at a time, because the player carries one wire at a time.
    /// 让线绕过本固定物或解除绕线的单一入口.
    /// 实现思路: 禁止绕线时无副作用拒绝; 否则切换"哪根线在这里绕线", 取下一个顺序号, 并触发 OnInteracted.
    /// 一个 Anchor 同时最多属于一根线, 因为玩家一次只携带一根线.
    /// </summary>
    public void Interact(InteractionDetails details)
    {
        if (!CanInteract || !IsValidActor(details))
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Interaction refused: this anchor does not accept routing. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        if (engagedBy != null)
        {
            engagedBy = null;
            engagementSequence = 0;
            OnInteracted?.Invoke(this);
            return;
        }

        Wire held = EnvironmentFacade.HeldWireOf(details);
        if (held == null)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Routing refused: the actor holds no wire. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        engagedBy = held;
        engagementSequence = held.NextEngagementSequence();
        OnInteracted?.Invoke(this);
    }

    /// <summary>Detach and remove this anchor without creating an inventory item.
    /// 解除绕线并收回锚点，不创建背包实例。</summary>
    public bool TryReclaim(InteractionDetails details)
    {
        if (!CanReclaim || !IsValidActor(details))
        {
            return false;
        }

        isReclaimed = true;
        ResetRouting();
        EnvironmentFacade.ForScene(gameObject.scene)?.UnregisterAnchor(this);
        gameObject.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
        return true;
    }

    private bool IsValidActor(InteractionDetails details)
    {
        return details != null && details.Actor && details.Actor.scene == gameObject.scene &&
            (!details.Target || details.Target == gameObject);
    }

    private void OnDestroy()
    {
        EnvironmentFacade.ForScene(gameObject.scene)?.UnregisterAnchor(this);
    }
}
