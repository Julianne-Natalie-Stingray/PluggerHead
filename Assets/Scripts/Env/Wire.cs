using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// One electrical wire: a fixed end at its outlet, a free end the player carries, and the points it bends through.
/// Subsystem: Environment.
/// Where it lives: in the level. Its points are its children, so the path's authored candidates are found through
/// the hierarchy and need no Inspector wiring.
/// Responsibility: hold the wire's polarity, its fixed end, and the state of its free end (held by a player, or
/// plugged somewhere), and render the polyline the facade hands it.
/// Does NOT own: which wire the player holds, the order of engaged points, the polarity rules, or the win check.
/// Those are circuit state and live in EnvironmentFacade, which is the only place that holds them.
/// Lifetime: part of the level object; never created at runtime.
/// Geometry: straight segments with a query-only trigger collider, no rope simulation or animation. The path is the outlet, then every engaged
/// point in engagement order, then the free end, which is why the wire is a polyline rather than a simulated rope.
/// 一根电线: 固定端在插座孔, 自由端由玩家携带, 中间是它折向经过的点.
/// Subsystem 归属: Environment.
/// 存在位置: 关卡中. 它的点是它的子物体, 因此路径的候选点通过层级找到, 无需 Inspector 接线.
/// 职能: 持有线的电性, 固定端, 以及自由端的状态(被某个玩家持有, 或已插在某处), 并渲染门面交给它的折线.
/// 不负责: 玩家持有哪根线, 已接入点的顺序, 电性规则, 以及通关判定.
/// 那些是回路状态, 位于 EnvironmentFacade —— 唯一持有它们的地方.
/// 生命周期: 属于关卡物体; 从不在运行时创建.
/// 几何: 直线段附带查询用 Trigger 碰撞体, 不做绳索物理或动画. 路径 = 插座孔 -> 按接入顺序排列的所有已接入点 -> 自由端,
/// 因此线是折线而不是被模拟的绳子.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class Wire : MonoBehaviour
{
    public WirePolarity Polarity => polarity;
    public bool IsHeld => isHeld;
    public bool IsClosed => isClosed;
    public PowerSocket Socket => socket;
    public Transform PlugTarget => plugTarget;
    public IReadOnlyList<WirePoint> Points => points;
    public Vector3 FixedEndPosition => fixedEnd ? fixedEnd.position : transform.position;
    public float MaxLength => maxLength;
    public float PullStrength => pullStrength;
    public EdgeCollider2D PathCollider => pathCollider;

    /// <summary>
    /// Where this wire's free end currently is: the carrying player's attach point while held, otherwise the place
    /// it is plugged into, otherwise the wire's own transform.
    /// 本线自由端当前所在: 被持有时是携带者的挂点, 否则是它插入的位置, 再否则是线自身的变换.
    /// </summary>
    public Vector3 FreeEndPosition
    {
        get
        {
            if (isHeld)
            {
                return attachPoint ? attachPoint.position : transform.position;
            }

            return plugTarget ? plugTarget.position : transform.position;
        }
    }

    [SerializeField, BoxGroup("Wire")]
    [Tooltip("Electrical property of this wire. It must match the polarity of every interface it is plugged into.")]
    private WirePolarity polarity = WirePolarity.Live;

    [SerializeField, BoxGroup("Wire")]
    [Tooltip("Where the wire leaves its outlet. Left empty, the wire's own transform is used.")]
    private Transform fixedEnd;

    [SerializeField, Min(0f), BoxGroup("Wire")]
    [Tooltip("Maximum routed length of the carried wire. Exceeding it kills the player; zero leaves its length unrestricted.")]
    private float maxLength;

    [SerializeField, Min(0f), BoxGroup("Wire")]
    [Tooltip("Legacy pull-force setting retained for asset compatibility. Exceeding Max Length now kills the player regardless of this value.")]
    private float pullStrength = 10f;

    private readonly List<WirePoint> points = new();

    private LineRenderer line;
    private EdgeCollider2D pathCollider;
    private readonly List<Vector2> colliderPoints = new();
    private readonly List<Vector2> previousColliderPoints = new();
    private Transform attachPoint;
    private PowerSocket socket;
    private Transform plugTarget;
    private Vector3[] buffer = new Vector3[0];
    private bool isHeld;
    private bool isClosed;
    private int engagementCounter;

    private void Awake()
        => Initialize();

    /// <summary>
    /// Single entry point for caching this wire's own components and collecting its points.
    /// Implementation approach: called from Awake, and again by the facade's sweep, so that a run driven from the
    /// Editor without entering play mode still has its LineRenderer and its points. It is idempotent.
    /// 缓存本线自身组件并收集其点的单一入口.
    /// 实现思路: 由 Awake 调用, 也由门面的扫描再次调用, 使"不进入播放模式、直接在编辑器里驱动"的一局依然有
    /// LineRenderer 与它的点. 它是幂等的.
    /// </summary>
    public void Initialize()
    {
        if (!line)
        {
            line = GetComponent<LineRenderer>();
        }

        if (!pathCollider)
        {
            pathCollider = GetComponent<EdgeCollider2D>();
            if (!pathCollider)
            {
                pathCollider = gameObject.AddComponent<EdgeCollider2D>();
            }
            pathCollider.isTrigger = true;
            pathCollider.edgeRadius = 0f;
            pathCollider.enabled = false;
        }

        points.Clear();
        points.AddRange(GetComponentsInChildren<WirePoint>());
    }

    /// <summary>
    /// Single entry point for taking the next routing order number.
    /// Implementation approach: a monotonic counter per wire, because the path order is the order in which the
    /// player engaged the points, and timestamps cannot express two engagements in the same frame.
    /// 取下一个绕线顺序号的单一入口.
    /// 实现思路: 每根线一个单调计数器, 因为路径顺序就是玩家接入各点的先后, 而时间戳无法表达同一帧内的两次接入.
    /// </summary>
    public int NextEngagementSequence()
        => ++engagementCounter;

    public void SetSocket(PowerSocket owner)
        => socket = owner;

    /// <summary>
    /// Hand this wire the Transform its free end should follow while it is held.
    /// 把"被持有时自由端应跟随的 Transform"交给本线的单一入口.
    /// </summary>
    public void SetAttachPoint(Transform point)
        => attachPoint = point;

    /// <summary>
    /// Single entry point for the state "a player carries this wire's free end".
    /// 状态"某玩家携带本线的自由端"的单一入口.
    /// </summary>
    public void Hold()
    {
        isHeld = true;
        isClosed = false;
        plugTarget = null;
    }

    /// <summary>
    /// Single entry point for the state "this wire's free end is plugged into something".
    /// Implementation approach: records where, and whether that place completes the wire; a wire plugged into an
    /// interface is terminated there, a wire plugged back into its outlet is closed.
    /// 状态"本线自由端已插入某处"的单一入口.
    /// 实现思路: 记录插入点以及该处是否使本线闭环; 插入带电接口即为在该处终止, 插回插座孔即为闭合.
    /// </summary>
    public void PlugInto(Transform target, bool closesCircuit)
    {
        isHeld = false;
        plugTarget = target;
        isClosed = closesCircuit;
    }

    /// <summary>
    /// Single entry point for drawing the polyline the facade computed.
    /// Implementation approach: reuses one buffer, because this runs every frame while the carried end follows the
    /// player, and a per-frame array allocation would be pure waste.
    /// 绘制门面算出的折线的单一入口.
    /// 实现思路: 复用同一个缓冲区, 因为被携带的一端跟随时每帧都会执行, 每帧分配数组纯属浪费.
    /// </summary>
    public void RenderPath(IReadOnlyList<Vector3> positions)
    {
        if (!line || positions == null)
        {
            return;
        }

        if (buffer.Length != positions.Count)
        {
            buffer = new Vector3[positions.Count];
        }

        for (int i = 0; i < positions.Count; i++)
        {
            buffer[i] = positions[i];
        }

        line.positionCount = positions.Count;
        line.useWorldSpace = true;
        line.SetPositions(buffer);
        UpdatePathCollider(positions);
    }

    // The collider follows the same world-space polyline, converted to local coordinates.
    // 碰撞体与渲染共用路径，仅在路径变化时更新物理形状；不产生实体碰撞。
    private void UpdatePathCollider(IReadOnlyList<Vector3> positions)
    {
        colliderPoints.Clear();
        for (int i = 0; i < positions.Count; i++)
        {
            // Collider points are translated by offset before the Transform is applied.
            // 碰撞体先叠加 offset 再应用 Transform，反向换算时需抵消该偏移。
            Vector2 point = (Vector2)transform.InverseTransformPoint(positions[i]) - pathCollider.offset;
            if (colliderPoints.Count == 0 ||
                (colliderPoints[colliderPoints.Count - 1] - point).sqrMagnitude > 0.00000001f)
            {
                colliderPoints.Add(point);
            }
        }

        bool changed = colliderPoints.Count != previousColliderPoints.Count;
        for (int i = 0; !changed && i < colliderPoints.Count; i++)
        {
            changed = colliderPoints[i] != previousColliderPoints[i];
        }
        if (changed && colliderPoints.Count >= 2)
        {
            pathCollider.SetPoints(colliderPoints);
        }
        pathCollider.enabled = colliderPoints.Count >= 2;
        if (changed)
        {
            previousColliderPoints.Clear();
            previousColliderPoints.AddRange(colliderPoints);
        }
    }
}
