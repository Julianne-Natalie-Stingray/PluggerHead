using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Electrical wire storing polarity, endpoints, plug state and its independent visited tile path.
/// 电线保存极性、端点、插入状态及独立的格子路径。
/// EnvironmentFacade samples player cells; rendering, collision and length share those cell centers.
/// EnvironmentFacade 采样玩家格子；渲染、碰撞及长度共用格心，继承段由原线绘制。
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
    public Vector3 FixedEndPosition => socket ? socket.PlugPosition : transform.position;
    public float MaxLength => maxLength;
    public EdgeCollider2D PathCollider => pathCollider;
    public Transform CircuitStart { get; private set; }
    public Wire PreviousWire { get; private set; }
    public IReadOnlyList<PolaritySocket> ConnectedInterfaces => connectedInterfaces;
    private readonly List<PolaritySocket> connectedInterfaces = new();
    public IReadOnlyList<VoltageReducer> ConnectedReducers => connectedReducers;
    private readonly List<VoltageReducer> connectedReducers = new();

    [SerializeField, BoxGroup("Wire")]
    [Tooltip("Electrical property of this wire. It must match the polarity of every interface it is plugged into.")]
    private WirePolarity polarity = WirePolarity.Live;

    [SerializeField, Min(0f), BoxGroup("Wire")]
    [Tooltip("Maximum routed length of the carried wire. Exceeding it kills the player; zero leaves its length unrestricted.")]
    private float maxLength;

    private LineRenderer line;
    private EdgeCollider2D pathCollider;
    private readonly List<Vector2> colliderPoints = new();
    private readonly List<Vector2> previousColliderPoints = new();
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

    /// <summary>Electrical origin differs from the copied visual/path prefix after a handover.
    /// 换线后的电气起点是交接接口；复制的历史路径不代表另一条电气连接。</summary>
    internal void BeginConnection(Transform start, Wire previous)
    {
        CircuitStart = start;
        PreviousWire = previous;
        connectedInterfaces.Clear();
        connectedReducers.Clear();
    }

    internal void ConfigureContinuation(Wire source, WirePolarity value)
    {
        polarity = value;
        maxLength = source ? source.MaxLength : 0f;
        Initialize();
        if (source)
        {
            LineRenderer original = source.GetComponent<LineRenderer>();
            line.sharedMaterial = original.sharedMaterial;
            line.textureMode = original.textureMode;
            line.textureScale = original.textureScale;
            line.widthCurve = original.widthCurve;
            line.widthMultiplier = original.widthMultiplier;
            line.numCornerVertices = original.numCornerVertices;
            line.numCapVertices = original.numCapVertices;
        }
        Color color = value == WirePolarity.Live ? Color.red :
            value == WirePolarity.Neutral ? Color.blue : Color.green;
        line.startColor = line.endColor = color;
    }

    internal void ConnectReducer(VoltageReducer target)
    {
        if (!connectedReducers.Contains(target))
        {
            connectedReducers.Add(target);
        }
        TilePath.CommitConnection();
    }

    internal void ConnectInterface(PolaritySocket target)
    {
        if (!connectedInterfaces.Contains(target))
        {
            connectedInterfaces.Add(target);
        }
        TilePath.CommitConnection();
    }

    /// <summary>Use the run's handover order, independent of object creation and renderer bounds.</summary>
    public void SetRenderOrder(int sortingLayerId, int order)
    {
        Initialize();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
    }

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
    /// Records the endpoint and legacy return flag; Environment checks socket occupancy and voltage.
    /// 状态"本线自由端已插入某处"的单一入口.
    /// 仅记录插入点与回插标志；环境检查插座锚点接线与电压。
    /// </summary>
    public void PlugInto(Transform target, bool closesCircuit)
    {
        isHeld = false;
        plugTarget = target;
        isClosed = closesCircuit;
    }

    /// <summary>
    /// Single entry point for drawing the polyline the facade computed.
    /// Reuses the buffer while point count stays unchanged; count changes allocate a new array.
    /// Only the new suffix is drawn: inherited edges remain visible on the previous wire.
    /// Collision still follows the entire logical path.
    /// 绘制门面算出的折线的单一入口.
    /// 实现思路: 复用缓冲区，只绘制本线新增尾段，复制段由原线保留原色；碰撞仍使用完整路径。
    /// </summary>
    public void RenderPath(IReadOnlyList<Vector3> positions)
    {
        if (!line || positions == null)
        {
            return;
        }

        int firstPoint = Mathf.Min(TilePath.InheritedEdgeCount, positions.Count);
        int visibleCount = positions.Count - firstPoint;
        if (buffer.Length != visibleCount)
        {
            buffer = new Vector3[visibleCount];
        }

        for (int i = 0; i < visibleCount; i++)
        {
            buffer[i] = positions[firstPoint + i];
        }

        line.positionCount = visibleCount;
        line.useWorldSpace = true;
        line.SetPositions(buffer);
        UpdatePathCollider(positions);
    }

    // The collider includes inherited edges as well as the visible suffix, converted to local coordinates.
    // 碰撞体保留完整逻辑路径（含继承段），仅在路径变化时更新物理形状；不产生实体碰撞。
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
