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

    [SerializeField, BoxGroup("Wire")]
    [Tooltip("Selects a shared material by wire polarity; textures and colors belong to these material assets.")]
    private WireVisualConfigs visualConfigs;

    private LineRenderer line;
    private EdgeCollider2D pathCollider;
    private readonly List<Vector2> colliderPoints = new();
    private readonly List<Vector2> previousColliderPoints = new();
    private PowerSocket socket;
    private Transform plugTarget;
    private Vector3[] buffer = new Vector3[0];
    private bool isHeld;
    private bool isClosed;
    private LineRenderer startConnectionLine;
    private LineRenderer endConnectionLine;

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

        if (visualConfigs)
        {
            // An empty slot clears stale material instead of displaying another polarity's appearance.
            // 空槽清除旧材质，避免显示其他种类的外观；未绑定配置时兼容已有渲染器。
            line.sharedMaterial = visualConfigs.GetMaterial(polarity);
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
        HideConnectionLines();
    }

    internal void ConfigureContinuation(Wire source, WirePolarity value)
    {
        polarity = value;
        maxLength = source ? source.MaxLength : 0f;
        visualConfigs = source ? source.visualConfigs : null;
        Initialize();
        if (source)
        {
            LineRenderer original = source.GetComponent<LineRenderer>();
            line.textureMode = original.textureMode;
            line.textureScale = original.textureScale;
            line.widthCurve = original.widthCurve;
            line.widthMultiplier = original.widthMultiplier;
            line.numCornerVertices = original.numCornerVertices;
            line.numCapVertices = original.numCapVertices;
        }
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
        if (endConnectionLine)
        {
            endConnectionLine.enabled = false;
        }
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
    /// Draw the new suffix, replacing its socket-adjacent edges with terminal lines; inherited edges stay on the previous wire.
    /// Collision still follows the entire logical path.
    /// 绘制门面算出的折线的单一入口.
    /// 实现思路: 复用缓冲区，新增尾段的插座邻边改由接头线绘制；复制段由原线保留原色，碰撞仍使用完整路径。
    /// </summary>
    public void RenderPath(IReadOnlyList<Vector3> positions)
    {
        if (!line || positions == null)
        {
            return;
        }

        int firstPoint = Mathf.Min(TilePath.InheritedEdgeCount, positions.Count);
        int lastPoint = positions.Count - 1;
        Transform startAnchor = GetSocketAnchor(CircuitStart);
        Transform endAnchor = GetSocketAnchor(plugTarget);
        int drawFirst = firstPoint;
        int drawLast = lastPoint;
        HideConnectionLines();
        if (firstPoint <= lastPoint)
        {
            if (startAnchor)
            {
                drawFirst = Mathf.Min(firstPoint + 1, lastPoint);
                RenderSocketConnection(ref startConnectionLine, CircuitStart, startAnchor,
                    positions[drawFirst], "StartConnection");
            }
            if (endAnchor)
            {
                drawLast = Mathf.Max(firstPoint, lastPoint - 1);
                RenderSocketConnection(ref endConnectionLine, plugTarget, endAnchor,
                    positions[drawLast], "EndConnection");
            }
            // With just one edge, replacing both ends leaves no interior vertex: join the anchors directly.
            // 仅一条边且两端均接插座时，没有中间顶点，直接连接两个锚点，避免交叉或断线。
            if (startAnchor && endAnchor && lastPoint - firstPoint == 1)
            {
                startConnectionLine.SetPosition(0, endAnchor.position);
                PromoteConnectionAboveSocket(startConnectionLine, plugTarget);
                endConnectionLine.enabled = false;
            }
        }

        int visibleCount = Mathf.Max(0, drawLast - drawFirst + 1);
        if (buffer.Length != visibleCount)
        {
            buffer = new Vector3[visibleCount];
        }
        for (int i = 0; i < visibleCount; i++)
        {
            buffer[i] = positions[drawFirst + i];
        }
        line.positionCount = visibleCount;
        line.useWorldSpace = true;
        line.SetPositions(buffer);
        UpdatePathCollider(positions);
    }

    private Transform GetSocketAnchor(Transform endpoint)
    {
        if (!isActiveAndEnabled || !line.enabled || !endpoint || endpoint.gameObject.scene != gameObject.scene)
        {
            return null;
        }
        Transform anchor = null;
        if (endpoint.TryGetComponent(out PowerSocket outlet))
        {
            anchor = outlet.GetWireAnchor(polarity);
        }
        else if (endpoint.TryGetComponent(out PolaritySocket target))
        {
            anchor = target.GetWireAnchor(polarity);
        }
        return anchor && anchor.gameObject.scene == gameObject.scene ? anchor : null;
    }

    // The terminal lines replace the omitted first/last visible edge without changing collision or length.
    // 接头线替换本线可见尾段的首/末边；路径点顺序、继承段、碰撞与线长不变。
    private void RenderSocketConnection(ref LineRenderer connection, Transform endpoint,
        Transform anchor, Vector3 pathPoint, string objectName)
    {
        if (!connection)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            connection = root.AddComponent<LineRenderer>();
            connection.useWorldSpace = true;
            connection.positionCount = 2;
        }

        connection.gameObject.layer = gameObject.layer;
        connection.sharedMaterial = line.sharedMaterial;
        connection.widthCurve = line.widthCurve;
        connection.widthMultiplier = line.widthMultiplier;
        connection.colorGradient = line.colorGradient;
        connection.textureMode = line.textureMode;
        connection.textureScale = line.textureScale;
        connection.alignment = line.alignment;
        connection.numCapVertices = line.numCapVertices;
        connection.numCornerVertices = line.numCornerVertices;
        connection.sortingLayerID = line.sortingLayerID;
        connection.sortingOrder = line.sortingOrder;
        PromoteConnectionAboveSocket(connection, endpoint);
        connection.SetPosition(0, pathPoint);
        connection.SetPosition(1, anchor.position);
        connection.enabled = true;
    }

    // Keep the replacement terminal edge visible over socket artwork, preserving the main wire's layer.
    // 接头替换边显示在插座图片前方；主线路保持原排序。
    private static void PromoteConnectionAboveSocket(LineRenderer connection, Transform endpoint)
    {
        SpriteRenderer socketRenderer = endpoint.GetComponent<SpriteRenderer>();
        if (socketRenderer)
        {
            int connectionLayer = SortingLayer.GetLayerValueFromID(connection.sortingLayerID);
            int socketLayer = SortingLayer.GetLayerValueFromID(socketRenderer.sortingLayerID);
            if (socketLayer >= connectionLayer)
            {
                connection.sortingOrder = socketLayer == connectionLayer
                    ? Mathf.Max(connection.sortingOrder, socketRenderer.sortingOrder + 1)
                    : socketRenderer.sortingOrder + 1;
                connection.sortingLayerID = socketRenderer.sortingLayerID;
            }
        }
    }

    private void HideConnectionLines()
    {
        if (startConnectionLine)
        {
            startConnectionLine.enabled = false;
        }
        if (endConnectionLine)
        {
            endConnectionLine.enabled = false;
        }
    }

    private void OnDisable()
    {
        HideConnectionLines();
    }

    private void OnDestroy()
    {
        DestroyConnectionLine(startConnectionLine);
        DestroyConnectionLine(endConnectionLine);
    }

    private static void DestroyConnectionLine(LineRenderer connection)
    {
        if (!connection)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(connection.gameObject);
        }
        else
        {
            DestroyImmediate(connection.gameObject);
        }
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
