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
    public bool CanInteract => canInteract;
    public bool CanPickup => canPickup;
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
    /// Implementation approach: gate on CanPickup, then report that the carry loop does not exist yet. The pickup
    /// itself is a Player-side loop (take, carry, drop, snap) that this Subsystem does not own.
    /// TODO: The carry loop is not implemented in this delivery. Deferred because it needs the Player side's carry
    /// and drop entry points, which do not exist yet; implementing half of it here would fix a shape that side has
    /// not chosen. Nothing is destroyed or moved until that entry point exists.
    /// 拾取本固定物的单一入口.
    /// 实现思路: 按 CanPickup 设门, 然后报告"携带回路尚不存在". 拾取本身是一条 Player 侧的回路(拿起, 携带, 放下, 吸附),
    /// 不属于本 Subsystem.
    /// TODO: 本次交付未实现携带回路. 暂缓原因: 它需要 Player 侧的拿起与放下入口, 而那两个入口尚不存在; 在这里实现一半会把
    /// 对方尚未选择的形态固定下来. 在那之前不会销毁或移动任何东西.
    /// </summary>
    public void Pickup(InteractionDetails details)
    {
        if (!CanPickup)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Pickup refused: this anchor is fixed by the level. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        GameLog.Warning(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify("Pickup accepted but not carried: the carry loop belongs to the Player side. "))
            .Action(LogAction.Specify("See Assets/Scripts/Jill/Env/README.md for the open interface. "))
            .Write();
    }
}
