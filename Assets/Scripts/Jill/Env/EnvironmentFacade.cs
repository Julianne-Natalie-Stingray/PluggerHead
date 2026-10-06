using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// The environment hub of one level: it owns the circuit state, applies interactions, and decides whether the
/// level is cleared.
/// Subsystem: Environment.
/// Where it lives: on the persistent SceneRoot object of the level.
/// Responsibility: sweep the scene for nodes, hold the circuit (which wire the player carries, what is plugged
/// where, how many swaps happened), apply one interaction at a time, rebuild every wire's polyline, render them,
/// and evaluate the win condition.
/// Does NOT own: the range check that decides which node the player is aiming at (Player side), the pickup and
/// carry loop (Player side), or the node's own flags, which they toggle themselves before notifying this hub.
/// Lifetime: one per level, created and destroyed with the scene. It deliberately does not survive a scene switch:
/// the only thing that does is CoreFacade.
/// Paradigms: a per-scene access point (Current), because a routing node has to ask "which wire does this actor
/// carry" and only this hub can answer that. It is the same kind of exception CoreFacade documents, kept as small
/// as possible: one lookup, no service locator.
/// 一个关卡的环境枢纽: 它拥有回路状态, 施加交互, 并判定关卡是否通关.
/// Subsystem 归属: Environment.
/// 存在位置: 关卡常驻 SceneRoot 物体上.
/// 职能: 扫描场景中的节点, 持有回路(玩家携带哪根线, 什么插在哪里, 换过几次线), 一次施加一个交互,
/// 重建每根线的折线并渲染, 以及判定通关.
/// 不负责: 决定玩家瞄准哪个节点的范围检测(归 Player), 拾取与携带回路(归 Player), 以及节点自身的旗标 ——
/// 它们在通知本枢纽之前已自行翻转.
/// 生命周期: 每关一个, 随场景创建与销毁. 它刻意不跨场景存活: 唯一跨场景的是 CoreFacade.
/// 使用范式: 每场景一个访问点(Current), 因为绕线节点必须问"这个交互者携带哪根线", 而只有本枢纽能回答.
/// 它与 CoreFacade 记录的是同一类例外, 并尽可能小: 一次查询, 不是服务定位器.
/// </summary>
[DisallowMultipleComponent]
public class EnvironmentFacade : MonoBehaviour
{
    public static EnvironmentFacade Current { get; private set; }

    public Wire HeldWire => heldWire;
    public bool IsCircuitClosed => isCircuitClosed;
    public int SwapCount => swapCount;

    /// <summary>
    /// Raised the first time the win condition becomes true during a run.
    /// 本次运行中通关条件第一次成立时触发.
    /// </summary>
    public event Action LevelCleared;

    [SerializeField, BoxGroup("Player")]
    [Tooltip("Tag of the player object, used to check that the attach point really belongs to the player.")]
    private string playerTag = "Player";

    [SerializeField, BoxGroup("Player")]
    [Tooltip("Tag of the Transform the carried wire's free end follows. It must sit under the tagged player.")]
    private string attachPointTag = "WireAttach";

    [SerializeField, BoxGroup("Win condition")]
    [Tooltip("Whether this level also contains a broken ground line that must be connected.")]
    private bool requireGround;

    [SerializeField, BoxGroup("Win condition")]
    [Tooltip("How many times the player may switch which wire they carry. One swap is the normal run.")]
    private int maxSwaps = 1;

    private readonly List<IEnvironmentInteractable> nodes = new();
    private readonly List<Wire> wires = new();
    private readonly List<Anchor> anchors = new();
    private readonly Dictionary<PolaritySocket, WirePolarity> occupied = new();
    private readonly Dictionary<Wire, List<Vector3>> waypoints = new();
    private readonly List<Vector3> renderBuffer = new();
    private readonly List<Vector3> orderedBuffer = new();
    private readonly List<int> sequenceBuffer = new();

    private Transform attachPoint;
    private PowerSocket socket;
    private Wire heldWire;
    private int swapCount;
    private bool isCircuitClosed;
    private bool reportedMissingAttachPoint;

    private void Awake()
    {
        Current = this;

        RefreshNodes();
        ResolveAttachPoint();
        BeginRun();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null)
                nodes[i].OnInteracted -= HandleInteracted;
        }

        if (Current == this)
            Current = null;
    }

    /// <summary>
    /// Single entry point for the per-frame drawing of every wire.
    /// Implementation approach: the free end follows the carrying player, so the polyline is rebuilt every frame
    /// from the cached waypoint order rather than recomputed from scratch; the waypoint order only changes when an
    /// interaction does.
    /// 每根线每帧绘制的单一入口.
    /// 实现思路: 自由端跟随携带者, 因此折线每帧由缓存的绕线顺序重建, 而不是从头重算; 绕线顺序只在发生交互时改变.
    /// </summary>
    private void LateUpdate()
    {
        for (int i = 0; i < wires.Count; i++)
        {
            Wire wire = wires[i];
            if (!wire)
                continue;

            renderBuffer.Clear();
            renderBuffer.Add(wire.FixedEndPosition);

            if (waypoints.TryGetValue(wire, out List<Vector3> cached))
                renderBuffer.AddRange(cached);

            renderBuffer.Add(wire.FreeEndPosition);
            wire.RenderPath(renderBuffer);
        }
    }

    /// <summary>
    /// Single entry point for rebuilding the scene's node lists.
    /// Implementation approach: one sweep per concrete type, because an interface cannot be searched for directly;
    /// it re-subscribes from scratch so a second call cannot double-subscribe. It also claims the per-scene access
    /// point, because Awake does not run while the Editor drives a run without entering play mode, and the routing
    /// nodes ask that access point which wire an actor carries. Call it again after the level creates nodes at
    /// runtime.
    /// 重建场景节点清单的单一入口.
    /// 实现思路: 每种具体类型扫一次, 因为无法直接查找接口; 它从零重新订阅, 因此重复调用不会造成重复订阅.
    /// 它同时声明本场景的访问点: 编辑器在未进入播放模式的情况下驱动一局时 Awake 不会执行,
    /// 而绕线节点正是向该访问点询问"交互者携带哪根线". 关卡在运行期新建节点之后, 再次调用它.
    /// </summary>
    public void RefreshNodes()
    {
        Current = this;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null)
                nodes[i].OnInteracted -= HandleInteracted;
        }

        nodes.Clear();
        wires.Clear();
        anchors.Clear();
        waypoints.Clear();
        occupied.Clear();

        WirePoint[] points = FindObjectsByType<WirePoint>(FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
            nodes.Add(points[i]);

        Anchor[] found = FindObjectsByType<Anchor>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            anchors.Add(found[i]);
            nodes.Add(found[i]);
        }

        PolaritySocket[] interfaces = FindObjectsByType<PolaritySocket>(FindObjectsSortMode.None);
        for (int i = 0; i < interfaces.Length; i++)
            nodes.Add(interfaces[i]);

        PowerSocket[] sockets = FindObjectsByType<PowerSocket>(FindObjectsSortMode.None);
        for (int i = 0; i < sockets.Length; i++)
        {
            nodes.Add(sockets[i]);

            if (socket == null && sockets[i].StartingWire != null)
                socket = sockets[i];
        }

        wires.AddRange(FindObjectsByType<Wire>(FindObjectsSortMode.None));

        for (int i = 0; i < wires.Count; i++)
            wires[i].Initialize();

        for (int i = 0; i < sockets.Length; i++)
            sockets[i].LinkWires();

        for (int i = 0; i < nodes.Count; i++)
            nodes[i].OnInteracted += HandleInteracted;

        for (int i = 0; i < wires.Count; i++)
        {
            if (attachPoint)
                wires[i].SetAttachPoint(attachPoint);
        }
    }

    /// <summary>
    /// Single entry point for asking which wire a given actor is carrying.
    /// Implementation approach: a static lookup, because a routing node is asked to act on the wire the actor
    /// holds and has no other way to reach the hub that owns that fact. It answers null outside play mode.
    /// 询问某个交互者当前携带哪根线的单一入口.
    /// 实现思路: 静态查询, 因为绕线节点需要作用于该交互者手中的线, 而没有别的途径触达拥有该事实的枢纽.
    /// 播放模式之外返回 null.
    /// </summary>
    public static Wire HeldWireOf(InteractionDetails details)
    {
        if (!Current || details == null)
            return null;

        return Current.heldWire;
    }

    /// <summary>
    /// Single entry point for evaluating the win condition on demand.
    /// Implementation approach: recompute from the live circuit and print one greppable assertion line, so a run
    /// leaves positive evidence of what was checked instead of only evidence of what failed.
    /// 按需判定通关的单一入口.
    /// 实现思路: 从当前回路重算, 并打印一行可 grep 的断言, 使一次运行留下"检查了什么"的正面证据, 而不只是失败证据.
    /// </summary>
    public bool EvaluateCircuit()
    {
        Wire live = FindWire(WirePolarity.Live);
        Wire neutral = FindWire(WirePolarity.Neutral);
        Wire ground = FindWire(WirePolarity.Ground);

        bool polarityOk = true;
        for (int i = 0; i < wires.Count; i++)
        {
            Wire wire = wires[i];
            if (wire.PlugTarget == null)
                continue;

            PolaritySocket target = wire.PlugTarget.GetComponent<PolaritySocket>();
            if (target && !target.Accepts(wire.Polarity))
                polarityOk = false;
        }

        bool withinSwapBudget = swapCount <= maxSwaps;
        bool terminated = IsTerminated(live) && IsTerminated(neutral);
        bool groundOk = !requireGround || IsTerminated(ground);

        bool wasClosed = isCircuitClosed;
        isCircuitClosed = polarityOk && withinSwapBudget && terminated && groundOk;

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(
                $"###ASSERT circuit closed={isCircuitClosed} polarity={polarityOk} terminated={terminated} " +
                $"ground={(requireGround ? groundOk.ToString() : "not-required")} swaps={swapCount}/{maxSwaps} " +
                $"live={Describe(live)} neutral={Describe(neutral)}. "))
            .Write();

        if (isCircuitClosed && !wasClosed)
            LevelCleared?.Invoke();

        return isCircuitClosed;
    }

    /// <summary>
    /// Single entry point for reacting to one node's interaction.
    /// Implementation approach: the node has already toggled its own flag, so this applies only the circuit
    /// consequences, then rebuilds the drawn paths and re-evaluates. Everything the circuit owns happens here, in
    /// one place, which is what lets the nodes stay dumb.
    /// 响应某个节点交互的单一入口.
    /// 实现思路: 节点已自行翻转旗标, 因此这里只施加回路的后果, 然后重建绘制路径并重新判定.
    /// 回路拥有的一切都发生在此一处, 这正是节点得以保持"愚蠢"的原因.
    /// </summary>
    private void HandleInteracted(IEnvironmentInteractable node)
    {
        switch (node)
        {
            case PolaritySocket liveInterface:
                ApplyPlug(liveInterface);
                break;
            case PowerSocket outlet:
                ApplyClose(outlet);
                break;
        }

        RebuildWaypoints();
        EvaluateCircuit();
    }

    private void ApplyPlug(PolaritySocket target)
    {
        if (heldWire == null)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Plug refused: no wire is currently carried. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        if (!target.Accepts(heldWire.Polarity))
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid($"{nameof(PolaritySocket)} accepts {target.Accepted}, wire is {heldWire.Polarity}"))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        if (occupied.TryGetValue(target, out WirePolarity taken) && (taken & heldWire.Polarity) != 0)
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"Plug refused: {target.name} already carries {heldWire.Polarity}. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        occupied[target] = taken | heldWire.Polarity;
        heldWire.PlugInto(target.transform, false);

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Plugged {heldWire.Polarity} into {target.name}. "))
            .Write();

        if (target.IsDual)
            SwapToOtherWire();
    }

    private void SwapToOtherWire()
    {
        PowerSocket owner = heldWire ? heldWire.Socket : null;
        if (owner == null)
            return;

        IReadOnlyList<Wire> candidates = owner.Wires;
        for (int i = 0; i < candidates.Count; i++)
        {
            Wire candidate = candidates[i];
            if (!candidate || candidate == heldWire || candidate.IsClosed)
                continue;

            heldWire = candidate;
            heldWire.Hold();
            swapCount++;

            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify(
                    $"Swapped wire: now carrying {heldWire.Polarity} (swap {swapCount} of {maxSwaps}). "))
                .Write();

            if (swapCount > maxSwaps)
            {
                GameLog.Warning(this)
                    .Subsystem("Environment")
                    .Name(LogName.Class)
                    .Issue(LogIssue.Specify(
                        $"Swap budget of {maxSwaps} exceeded; the level can no longer be cleared in this run. "))
                    .Action(LogAction.Ignore)
                    .Write();
            }

            return;
        }

        GameLog.Warning(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify("Swap requested but this outlet has no other open wire. "))
            .Action(LogAction.Ignore)
            .Write();
    }

    private void ApplyClose(PowerSocket outlet)
    {
        if (heldWire == null)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Plug refused: no wire is currently carried. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        if (heldWire.Socket != outlet)
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"Plug refused: {heldWire.name} does not belong to {outlet.name}. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        heldWire.PlugInto(outlet.transform, true);

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Closed {heldWire.Polarity} back into {outlet.name}. "))
            .Write();

        IReadOnlyList<Wire> candidates = outlet.Wires;
        for (int i = 0; i < candidates.Count; i++)
        {
            Wire candidate = candidates[i];
            if (!candidate || candidate == heldWire || candidate.IsClosed || candidate.PlugTarget != null)
                continue;

            heldWire = candidate;
            heldWire.Hold();

            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify(
                    $"Handed over at the outlet: now carrying {heldWire.Polarity} (not counted as a swap). "))
                .Write();

            return;
        }
    }

    private void RebuildWaypoints()
    {
        waypoints.Clear();

        for (int i = 0; i < wires.Count; i++)
        {
            Wire wire = wires[i];
            if (!wire)
                continue;

            orderedBuffer.Clear();
            sequenceBuffer.Clear();

            IReadOnlyList<WirePoint> points = wire.Points;
            for (int p = 0; p < points.Count; p++)
            {
                WirePoint point = points[p];
                if (point && point.IsEngaged)
                    InsertOrdered(point.EngagementSequence, point.transform.position);
            }

            for (int a = 0; a < anchors.Count; a++)
            {
                Anchor anchor = anchors[a];
                if (anchor && anchor.EngagedBy == wire)
                    InsertOrdered(anchor.EngagementSequence, anchor.transform.position);
            }

            waypoints[wire] = new List<Vector3>(orderedBuffer);
        }
    }

    private void InsertOrdered(int sequence, Vector3 position)
    {
        int index = 0;
        while (index < sequenceBuffer.Count && sequenceBuffer[index] <= sequence)
            index++;

        sequenceBuffer.Insert(index, sequence);
        orderedBuffer.Insert(index, position);
    }

    private void ResolveAttachPoint()
    {
        GameObject point = FindTaggedObject(attachPointTag);
        attachPoint = point ? point.transform : null;

        if (attachPoint)
        {
            for (int i = 0; i < wires.Count; i++)
                wires[i].SetAttachPoint(attachPoint);
        }
        else if (!reportedMissingAttachPoint)
        {
            reportedMissingAttachPoint = true;

            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.CannotFind($"tag {attachPointTag}", "scene"))
                .Action(LogAction.Specify("The carried wire will not follow a hand until that tag exists. "))
                .Write();
        }

        GameObject player = FindTaggedObject(playerTag);
        if (player && attachPoint && !HasTaggedAncestor(attachPoint, playerTag))
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify(
                    $"The object tagged {attachPointTag} ({attachPoint.name}) has no ancestor tagged {playerTag} " +
                    $"(the player found was {player.name}). "))
                .Action(LogAction.Specify("Check the hierarchy: the attach point must belong to the player. "))
                .Write();
        }
    }

    private void BeginRun()
    {
        swapCount = 0;
        isCircuitClosed = false;

        if (socket == null)
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.CannotFind(nameof(PowerSocket), nameof(EnvironmentFacade)))
                .Action(LogAction.Specify("No outlet with wires was found; the run cannot start. "))
                .Write();

            return;
        }

        heldWire = socket.StartingWire;
        if (heldWire)
        {
            heldWire.Hold();

            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"Run started carrying {heldWire.Polarity} from {socket.name}. "))
                .Write();
        }

        RebuildWaypoints();
        EvaluateCircuit();
    }

    private Wire FindWire(WirePolarity polarity)
    {
        for (int i = 0; i < wires.Count; i++)
        {
            if (wires[i] && wires[i].Polarity == polarity)
                return wires[i];
        }

        return null;
    }

    private bool IsTerminated(Wire wire)
    {
        if (wire == null)
            return false;

        if (wire.IsClosed)
            return true;

        if (wire.PlugTarget == null)
            return false;

        PolaritySocket target = wire.PlugTarget.GetComponent<PolaritySocket>();
        return target && target.IsDual;
    }

    private string Describe(Wire wire)
    {
        if (wire == null)
            return "absent";

        if (wire.IsClosed)
            return wire.Socket ? $"closed at {wire.Socket.name}" : "closed";

        if (wire.PlugTarget != null)
            return $"parked at {wire.PlugTarget.name}";

        return wire.IsHeld ? "carried" : "loose";
    }

    private GameObject FindTaggedObject(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        try
        {
            return GameObject.FindWithTag(tag);
        }
        catch (UnityException)
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.CannotFind($"tag {tag}", "Tag Manager"))
                .Action(LogAction.Specify("Add that tag in Project Settings > Tags and Layers. "))
                .Write();

            return null;
        }
    }

    private bool HasTaggedAncestor(Transform point, string tag)
    {
        Transform current = point;
        while (current)
        {
            if (current.CompareTag(tag))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < wires.Count; i++)
        {
            Wire wire = wires[i];
            if (!wire)
                continue;

            Gizmos.color = ColorFor(wire.Polarity);

            renderBuffer.Clear();
            renderBuffer.Add(wire.FixedEndPosition);

            if (waypoints.TryGetValue(wire, out List<Vector3> cached))
                renderBuffer.AddRange(cached);

            renderBuffer.Add(wire.FreeEndPosition);

            for (int p = 1; p < renderBuffer.Count; p++)
                Gizmos.DrawLine(renderBuffer[p - 1], renderBuffer[p]);

            Gizmos.DrawWireSphere(wire.FreeEndPosition, 0.12f);
        }
    }

    private Color ColorFor(WirePolarity polarity)
    {
        switch (polarity)
        {
            case WirePolarity.Live:
                return Color.red;
            case WirePolarity.Neutral:
                return Color.blue;
            case WirePolarity.Ground:
                return Color.green;
            default:
                return Color.gray;
        }
    }

#region Debug

    [SerializeField, BoxGroup("Debug")]
    [Tooltip("Node to drive by hand while the Player side is not wired to this subsystem yet.")]
    private Transform debugTarget;

    /// <summary>
    /// Single entry point for driving one interaction by hand.
    /// Implementation approach: resolves the node on the debug target, builds the request with the tagged player
    /// as the actor, and calls exactly the entry point the Player side will call. It exists because nothing in the
    /// scene triggers an interaction until that wiring lands, which would make acceptance impossible.
    /// 手动驱动一次交互的单一入口.
    /// 实现思路: 在调试目标上解析出节点, 以带 Tag 的玩家为交互者构造请求, 并调用 Player 侧将来会调用的同一入口.
    /// 它存在的原因: 在那条接线落地之前, 场景里没有任何东西会触发交互, 验收将无法进行.
    /// </summary>
    [Button("Simulate interact with debug target")]
    private void DebugSimulateInteract()
    {
        IEnvironmentInteractable node = ResolveDebugNode();
        if (node == null)
            return;

        GameObject actor = FindTaggedObject(playerTag);
        if (!actor)
            actor = gameObject;

        InteractionDetails details = node is WirePoint
            ? new WirePointDetails(actor, debugTarget.gameObject)
            : new InteractionDetails(actor, debugTarget.gameObject);

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Simulating interact on {debugTarget.name}. "))
            .Write();

        node.Interact(details);
    }

    /// <summary>
    /// Single entry point for driving one pickup by hand, for the same reason as the interaction above.
    /// 手动驱动一次拾取的单一入口, 原因同上.
    /// </summary>
    [Button("Simulate pickup of debug target")]
    private void DebugSimulatePickup()
    {
        if (!debugTarget)
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(debugTarget)))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        Anchor anchor = debugTarget.GetComponentInParent<Anchor>();
        if (!anchor)
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.CannotFind(nameof(Anchor), nameof(debugTarget)))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        GameObject actor = FindTaggedObject(playerTag);
        if (!actor)
            actor = gameObject;

        anchor.Pickup(new InteractionDetails(actor, debugTarget.gameObject));
    }

    private IEnvironmentInteractable ResolveDebugNode()
    {
        if (!debugTarget)
        {
            GameLog.Warning(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(debugTarget)))
                .Action(LogAction.Return)
                .Write();

            return null;
        }

        PolaritySocket polaritySocket = debugTarget.GetComponentInParent<PolaritySocket>();
        if (polaritySocket)
            return polaritySocket;

        PowerSocket powerSocket = debugTarget.GetComponentInParent<PowerSocket>();
        if (powerSocket)
            return powerSocket;

        Anchor anchor = debugTarget.GetComponentInParent<Anchor>();
        if (anchor)
            return anchor;

        return debugTarget.GetComponentInParent<WirePoint>();
    }

    /// <summary>
    /// Single entry point for re-sweeping and re-evaluating by hand.
    /// Implementation approach: it exists because the level's nodes are assembled in the Editor, and an assertion
    /// line is the only way to see the circuit's state without walking the hierarchy.
    /// 手动重新扫描并判定的单一入口.
    /// 实现思路: 关卡的节点是在编辑器中装配的, 而断言行是无需逐个查看层级即可看到回路状态的唯一途径, 因此需要它.
    /// </summary>
    [Button("Refresh nodes and evaluate")]
    private void DebugRefreshAndEvaluate()
    {
        RefreshNodes();
        ResolveAttachPoint();
        RebuildWaypoints();
        EvaluateCircuit();
    }

    private int acceptedSteps;
    private int failedSteps;
    private int skippedSteps;

    /// <summary>
    /// Single entry point for restarting the run without restarting the game.
    /// Implementation approach: releases every wire, re-sweeps the scene and starts again from the outlet's first
    /// wire. It exists so that acceptance can be repeated inside one session.
    /// 不重启游戏而重开一局的单一入口.
    /// 实现思路: 释放所有线, 重新扫描场景, 并再次从插座孔的第一根线开局. 它存在的原因: 使验收能在同一次会话里重复.
    /// </summary>
    [Button("Restart run")]
    private void DebugRestartRun()
    {
        for (int i = 0; i < wires.Count; i++)
        {
            if (wires[i])
                wires[i].PlugInto(null, false);
        }

        RefreshNodes();
        ResolveAttachPoint();
        BeginRun();
    }

    /// <summary>
    /// Single entry point for the scripted acceptance run.
    /// Implementation approach: restart the run, drive the state machine through the level's own nodes, compare
    /// each step with what the design says should happen, and print one verdict line per step plus a final verdict.
    /// It exists because reading an assertion line and judging it by hand is exactly the step a human gets wrong;
    /// the machine should do the judging.
    /// 脚本化验收的单一入口.
    /// 实现思路: 重开一局, 用关卡自己的节点驱动状态机, 逐步与设计应有的结果比对, 每步打印一行判定, 最后打印总结局.
    /// 它存在的原因: "读一行断言再自己判断"正是人会做错的那一步; 判断应该由机器来做.
    /// </summary>
    [Button("Run acceptance")]
    private void DebugRunAcceptance()
    {
        acceptedSteps = 0;
        failedSteps = 0;
        skippedSteps = 0;

        DebugRestartRun();

        Wire first = socket ? socket.StartingWire : null;
        ReportStep("1 the run carries the outlet's first wire",
            first != null && heldWire == first && !IsCircuitClosed,
            $"wires={wires.Count} held={Describe(heldWire)} closed={IsCircuitClosed}");

        Anchor anchor = FindRoutingAnchor();
        if (anchor == null)
        {
            SkipStep("2 routing a wire around an anchor", "no anchor with CanInteract found");
        }
        else
        {
            Wire carried = heldWire;
            anchor.Interact(new InteractionDetails(DebugActor(), anchor.gameObject));
            bool routed = anchor.IsEngaged && anchor.EngagedBy == carried;
            ReportStep("2 routing a wire around an anchor",
                routed,
                $"engaged={anchor.IsEngaged} routed={(anchor.EngagedBy ? anchor.EngagedBy.Polarity.ToString() : "none")}");

            if (!routed)
            {
                SkipStep("3 releasing the routing again", "step 2 did not route anything");
            }
            else
            {
                anchor.Interact(new InteractionDetails(DebugActor(), anchor.gameObject));
                ReportStep("3 releasing the routing again", !anchor.IsEngaged, $"engaged={anchor.IsEngaged}");
            }
        }

        PolaritySocket dual = FindDualInterface();
        if (dual == null)
        {
            SkipStep("4 swapping the carried wire at a dual interface", "no dual interface found");
        }
        else
        {
            Wire carriedBefore = heldWire;
            WirePolarity polarityBefore = carriedBefore ? carriedBefore.Polarity : WirePolarity.None;
            dual.Interact(new InteractionDetails(DebugActor(), dual.gameObject));
            ReportStep("4 swapping the carried wire at a dual interface",
                heldWire != null && heldWire != carriedBefore && swapCount == 1 &&
                carriedBefore != null && carriedBefore.PlugTarget == dual.transform,
                $"was={polarityBefore} now={(heldWire ? heldWire.Polarity.ToString() : "none")} swaps={swapCount}");
        }

        PolaritySocket mismatched = FindMismatchingInterface();
        if (mismatched == null)
        {
            SkipStep("5 refusing a mismatched polarity", "no interface rejects the carried polarity");
        }
        else
        {
            Transform plugTargetBefore = heldWire ? heldWire.PlugTarget : null;
            mismatched.Interact(new InteractionDetails(DebugActor(), mismatched.gameObject));
            bool unchanged = heldWire != null && heldWire.PlugTarget == plugTargetBefore;
            ReportStep("5 refusing a mismatched polarity", unchanged, $"state unchanged={unchanged}");
        }

        bool cleared = false;
        Action onCleared = () => cleared = true;
        LevelCleared += onCleared;

        if (socket == null)
        {
            SkipStep("6 closing the carried wire clears the level", "no outlet found");
        }
        else
        {
            socket.Interact(new InteractionDetails(DebugActor(), socket.gameObject));
            ReportStep("6 closing the carried wire clears the level",
                IsCircuitClosed && cleared,
                $"closed={IsCircuitClosed} LevelCleared fired={cleared}");
        }

        LevelCleared -= onCleared;

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(failedSteps == 0
                ? $"###ACCEPTANCE PASS ({acceptedSteps} passed, {skippedSteps} skipped). "
                : $"###ACCEPTANCE FAIL ({failedSteps} failed, {acceptedSteps} passed, {skippedSteps} skipped). "))
            .Write();
    }

    private void ReportStep(string step, bool passed, string detail)
    {
        if (passed)
            acceptedSteps++;
        else
            failedSteps++;

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"###ACCEPT STEP {step}: {(passed ? "PASS" : "FAIL")} - {detail}. "))
            .Write();
    }

    private void SkipStep(string step, string reason)
    {
        skippedSteps++;

        GameLog.Warning(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"###ACCEPT STEP {step}: SKIPPED - {reason}. "))
            .Write();
    }

    private GameObject DebugActor()
    {
        GameObject actor = FindTaggedObject(playerTag);
        return actor ? actor : gameObject;
    }

    private Anchor FindRoutingAnchor()
    {
        for (int i = 0; i < anchors.Count; i++)
        {
            if (anchors[i] && anchors[i].CanInteract)
                return anchors[i];
        }

        return null;
    }

    private PolaritySocket FindDualInterface()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is PolaritySocket candidate && candidate.CanInteract && candidate.IsDual)
                return candidate;
        }

        return null;
    }

    private PolaritySocket FindMismatchingInterface()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is PolaritySocket candidate && candidate.CanInteract && heldWire != null &&
                !candidate.Accepts(heldWire.Polarity))
                return candidate;
        }

        return null;
    }

#endregion
}
