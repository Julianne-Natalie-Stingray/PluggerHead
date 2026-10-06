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
    public Vector3 FixedEndPosition => socket ? socket.PlugPosition : transform.position;
    public float MaxLength => maxLength;
    public EdgeCollider2D PathCollider => pathCollider;

    [SerializeField, BoxGroup("Wire")]
    [Tooltip("Electrical property of this wire. It must match the polarity of every interface it is plugged into.")]
    private WirePolarity polarity = WirePolarity.Live;

    [SerializeField, Min(0f), BoxGroup("Wire")]
    [Tooltip("Maximum routed length of the carried wire. Exceeding it kills the player; zero leaves its length unrestricted.")]
    private float maxLength;

    [SerializeField, BoxGroup("Wire")]
    [Tooltip("Color for portions inside tiles currently visited more than once, by this wire or another wire.")]
    private Color overlapColor = new Color(1f, 0.2f, 0.75f, 1f);

    [SerializeField, HideInInspector] private List<LineRenderer> overlapLines = new();
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
    public void RenderPath(IReadOnlyList<Vector3> positions, IReadOnlyDictionary<Vector3Int, int> tileUseCounts)
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
        RenderOverlaps(positions, tileUseCounts);
    }

    // Each overlay covers only the half-edges belonging to a repeated tile. Pool renderers so
    // arbitrary long alternating paths do not depend on Gradient's limited number of keys.
    // 覆盖重复格子内的半段电线；复用渲染器，不受 Gradient 关键点数量上限影响。
    private void RenderOverlaps(IReadOnlyList<Vector3> positions, IReadOnlyDictionary<Vector3Int, int> tileUseCounts)
    {
        int used = 0;
        if (positions.Count >= 2)
        {
            for (int i = 0; i < TilePath.Cells.Count; i++)
            {
                if (!tileUseCounts.TryGetValue(TilePath.Cells[i], out int count) || count < 2)
                {
                    continue;
                }
                if (used == overlapLines.Count)
                {
                    overlapLines.Add(null);
                }
                LineRenderer renderer = overlapLines[used];
                if (!renderer)
                {
                    GameObject overlay = new GameObject("Repeated Tile " + used);
                    overlay.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    overlay.layer = gameObject.layer;
                    overlay.transform.SetParent(transform, false);
                    renderer = overlay.AddComponent<LineRenderer>();
                    overlapLines[used] = renderer;
                }
                used++;
                renderer.enabled = line.enabled;
                renderer.useWorldSpace = true;
                renderer.sharedMaterial = line.sharedMaterial;
                renderer.widthMultiplier = line.widthMultiplier;
                renderer.widthCurve = line.widthCurve;
                renderer.startColor = overlapColor;
                renderer.endColor = overlapColor;
                renderer.sortingLayerID = line.sortingLayerID;
                renderer.sortingOrder = line.sortingOrder + 1;
                renderer.alignment = line.alignment;
                renderer.numCapVertices = line.numCapVertices;
                renderer.numCornerVertices = line.numCornerVertices;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                int points = 1 + (i > 0 ? 1 : 0) + (i + 1 < positions.Count ? 1 : 0);
                renderer.positionCount = points;
                int index = 0;
                if (i > 0)
                {
                    renderer.SetPosition(index++, Vector3.Lerp(positions[i - 1], positions[i], 0.5f));
                }
                renderer.SetPosition(index++, positions[i]);
                if (i + 1 < positions.Count)
                {
                    renderer.SetPosition(index, Vector3.Lerp(positions[i], positions[i + 1], 0.5f));
                }
            }
        }
        for (int i = used; i < overlapLines.Count; i++)
        {
            if (overlapLines[i])
            {
                overlapLines[i].enabled = false;
                overlapLines[i].positionCount = 0;
            }
        }
    }

    private void OnDestroy()
    {
        foreach (LineRenderer renderer in overlapLines)
        {
            if (!renderer)
            {
                continue;
            }
            if (Application.isPlaying)
            {
                Destroy(renderer.gameObject);
            }
            else
            {
                DestroyImmediate(renderer.gameObject);
            }
        }
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
