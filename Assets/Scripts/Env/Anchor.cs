using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// A fixture the wire can be routed around, and optionally a resource the player can move.
/// Subsystem: Environment.
/// Where it lives: in the level, as the parent of nothing in particular; wires reference it through the facade's
/// node sweep rather than through the hierarchy.
/// Responsibility: remember whether the wire currently held by a player is routed around it, and expose whether it
/// may be moved at all.
/// Does NOT own: which wire is held, how the polyline is computed, or the win check.
/// Lifetime: part of the level object; never created at runtime.
/// Two independent questions: CanInteract answers "may the wire be routed around this fixture", CanPickup answers
/// "may this fixture itself be taken away". The original sketch wrote the second one as if it were the first.
/// 线可以绕过其折向的固定物, 同时也可能是玩家可以搬动的资源.
/// Subsystem 归属: Environment.
/// 存在位置: 关卡中, 不作任何东西的父物体; 线经由门面的节点扫描引用它, 而不是经由层级.
/// 职能: 记住"当前被玩家持有的那根线是否绕过它", 并暴露它是否可以被搬动.
/// 不负责: 哪根线被持有, 折线如何计算, 以及通关判定.
/// 生命周期: 属于关卡物体; 从不在运行时创建.
/// 两个互相独立的问题: CanInteract 回答"线能否绕过这个固定物", CanPickup 回答"这个固定物本身能否被拿走".
/// 原稿把后者写成了前者的样子.
/// </summary>
[DisallowMultipleComponent]
public class Anchor : MonoBehaviour, IEnvironmentInteractable, IEnvironmentPickup
{
    public bool CanInteract => canInteract && !isStowed;
    public bool CanPickup => canPickup && !isStowed;
    public bool IsEngaged => engagedBy != null;
    public int EngagementSequence => engagementSequence;

    /// <summary>
    /// The wire currently routed around this fixture, or null.
    /// 当前绕过本固定物的那根线, 若无则为 null.
    /// </summary>
    public Wire EngagedBy => engagedBy;

    public event Action<IEnvironmentInteractable> OnInteracted;

    [SerializeField, BoxGroup("Routing")]
    [Tooltip("Whether the player may route a wire around this fixture. False marks a fixture the level fixes in place.")]
    private bool canInteract = true;

    [SerializeField, BoxGroup("Pickup")]
    [Tooltip("Whether this fixture may be picked up and moved. False marks an immovable anchor.")]
    private bool canPickup = false;

    private Wire engagedBy;
    private int engagementSequence;
    private bool isStowed;

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
        if (!CanInteract)
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

    /// <summary>
    /// Single entry point for taking this fixture.
    /// Gate on CanPickup, detach this anchor from its wire, and return its carried instance to the caller.
    /// The scene object is preserved while inactive; Player owns inventory membership and the drop position.
    /// 拾取本固定物的单一入口. 按 CanPickup 设门, 解除绕线并返回携带实例.
    /// 场景物体隐藏但保留, Player 负责背包归属及放置坐标.
    /// </summary>
    public IPickupInstance Pickup(InteractionDetails details)
    {
        if (!CanPickup || details == null || !details.Actor)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Pickup refused: the anchor is unavailable or the actor is missing. "))
                .Action(LogAction.Return)
                .Write();

            return null;
        }

        isStowed = true;
        engagedBy = null;
        engagementSequence = 0;
        OnInteracted?.Invoke(this);
        gameObject.SetActive(false);
        return new AnchorInstance(this);
    }

    /// <summary>Restore the same scene anchor without replacing its identity or subscriptions.
    /// 放回原场景锚点, 保留物体身份与事件订阅.</summary>
    public bool Restore(Vector3 position)
    {
        if (!isStowed || !IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z))
        {
            return false;
        }

        transform.position = position;
        isStowed = false;
        gameObject.SetActive(true);
        OnInteracted?.Invoke(this);
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
