using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Electrical wire storing polarity, endpoints, plug state and its independent visited tile path.
/// 电线保存极性、端点、插入状态及独立的格子路径。
/// EnvironmentFacade samples player cells; rendering, collision and length share those cell centers.
/// EnvironmentFacade 采样玩家格子；渲染、碰撞及长度共用这些格子的中心。
/// The polyline uses a query-only trigger collider, with no rope simulation. Authored or runtime-created wires need Initialize.
/// 折线使用查询用 Trigger 碰撞体，不模拟绳索；预设或运行时创建的线均需 Initialize。
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
    public TileWirePath TilePath { get; } = new TileWirePath();
    public Vector3 FixedEndPosition => socket ? socket.PlugPosition : fixedEnd ? fixedEnd.position : transform.position;
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

    private void Awake()
        => Initialize();

    /// <summary>
    /// Single entry point for caching this wire's rendering and collision components.
    /// Implementation approach: called from Awake, and again by the facade's sweep, so that a run driven from the
    /// Editor without entering play mode still has its LineRenderer and collider. It is idempotent.
    /// 缓存本线渲染及碰撞组件的单一入口.
    /// 实现思路: 由 Awake 调用, 也由门面的扫描再次调用, 使"不进入播放模式、直接在编辑器里驱动"的一局依然有
    /// LineRenderer 与碰撞体. 它是幂等的.
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

    }

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
    /// Implementation approach: records where, and whether that place completes the wire; a wire plugged into a
    /// dual interface counts as terminated in the facade; this method itself only records the supplied values.
    /// 状态"本线自由端已插入某处"的单一入口.
    /// 实现思路: 仅记录传入的插入点与闭合标志；门面把双电性接口视为终止，不是任意接口都算终止。
    /// </summary>
    public void PlugInto(Transform target, bool closesCircuit)
    {
        isHeld = false;
        plugTarget = target;
        isClosed = closesCircuit;
    }

    /// <summary>
    /// Single entry point for drawing the polyline the facade computed.
    /// Implementation approach: reuses the buffer while point count stays unchanged; count changes allocate a new array. The
    /// carried end can move each frame without changing the point count.
    /// 绘制门面算出的折线的单一入口.
    /// 实现思路: 点数相同时复用缓冲区，点数变化时重新分配；自由端逐帧移动不必改变点数。
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
