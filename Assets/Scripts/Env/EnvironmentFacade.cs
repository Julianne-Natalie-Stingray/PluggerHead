using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Per-level circuit controller: scans nodes, tracks held wires and sockets, rebuilds paths and evaluates completion.
/// 每关的回路控制器：扫描节点、管理持线与插口、重建路径并判定通关。
/// Place one in each level scene; the component does not enforce scene-wide uniqueness or persist itself across loads.
/// 每个关卡场景应配置一个；组件不强制场景内唯一，也不主动跨场景保留自身。
/// Current is the last instance assigned by Awake or RefreshNodes. Use ForScene for scene-specific lookup.
/// Current 是最近经 Awake 或 RefreshNodes 赋值的实例；按场景查询应使用 ForScene。
/// Player owns range/input checks. Sockets notify requests; routing nodes also maintain their own engagement state.
/// Player 负责范围与输入检查；插口通知请求，绕线节点还维护各自接入状态。
/// </summary>
[DisallowMultipleComponent]
public class EnvironmentFacade : MonoBehaviour
{
    public static EnvironmentFacade Current { get; private set; }

    public Wire HeldWire => heldWire;
    public Wire HeldGroundWire => heldGroundWire;
    /// <summary>Total voltage drop required to complete this level. 通关所需的累计降压量。</summary>
    public float NeededVoltage => neededVoltage;
    /// <summary>Remaining required drop; zero or less satisfies the voltage condition. 剩余所需降压量，可为负。</summary>
    public double CurrentVoltage { get; private set; }

    [SerializeField, Min(0f), Tooltip("通关所需的累计降压量。")]
    private float neededVoltage = 30f;
    [SerializeField, Tooltip("本关 BGM；在 AudioManager 配置中映射到循环、2D、OST 混音组的音频。")]
    private AudioId backgroundMusic = AudioId.DefaultOst;
    public AudioId BackgroundMusic => backgroundMusic;

    public bool IsCircuitClosed => isCircuitClosed;
    public int SwapCount => swapCount;
    public Tilemap RoutingTilemap => routingTilemap;

    [SerializeField, Tooltip("Painted rectangular XY tiles used by sockets, Anchors and wire paths.")]
    private Tilemap routingTilemap;

    /// <summary>
    /// Return the Player death sentinel when the carried wire's routed length exceeds its configured limit.
    /// 实际绕线路径超出长度上限时返回负无穷以触发 Player 死亡; 未超限、未持线或不限长时返回零.
    /// </summary>
    public Vector2 GetResistance(Vector2 playerPosition)
    {
        SamplePlayerPath(playerPosition);
        return ExceedsLength(heldWire) || ExceedsLength(heldGroundWire)
            ? Vector2.negativeInfinity : Vector2.zero;
    }

    private bool ExceedsLength(Wire wire)
    {
        return routingTilemap && wire && wire.IsHeld && wire.MaxLength > 0f &&
            wire.TilePath.GetLength(routingTilemap) > wire.MaxLength;
    }

    /// <summary>
    /// Raised whenever evaluation changes the win condition from false to true.
    /// 每次判定结果由未闭合变为闭合时触发，不是整局仅一次。
    /// </summary>
    public event Action LevelCleared;

    [SerializeField, BoxGroup("Player")]
    [Tooltip("Tag of the player whose tile movement builds the held wire path.")]
    private string playerTag = "Player";

    private readonly List<IEnvironmentInteractable> nodes = new();
    private readonly List<Wire> wires = new();
    private readonly List<Anchor> anchors = new();
    private readonly Dictionary<Component, WirePolarity> occupied = new();
    private readonly List<Vector3> renderBuffer = new();

    private PowerSocket socket;
    private Wire heldWire;
    private Wire heldGroundWire;
    private readonly List<Wire> generatedWires = new();
    private int swapCount;
    private bool isCircuitClosed;
    private Transform playerTransform;
    private Vector3 previousPlayerPosition;
    private Vector3 previousGroundPosition;
    private bool hasPlayerSample;
    private int nextWireRenderOrder;
    private int wireSortingLayerId;
    private PlayerMove musicPlayer;
    private ISoundHandle musicHandle;
    private bool musicStarted;
    private bool musicEnded;
    private bool hasStarted;

    private void OnEnable()
    {
        GameStateManager.Changed += HandleMusicStateChanged;
        ResolvePlayer();
    }

    private void Start()
    {
        hasStarted = true;
        TryStartMusic();
    }

    private void OnDisable()
    {
        GameStateManager.Changed -= HandleMusicStateChanged;
        if (musicPlayer)
        {
            musicPlayer.Died -= EndMusic;
        }
        musicPlayer = null;
        if (GameStateManager.Current == GameState.Loading)
        {
            // Core keeps the playback alive while this scene owner is unloaded.
            // 切场景只释放本关引用，由 Core 保持原播放位置。
            musicHandle = null;
            musicStarted = false;
        }
        else
        {
            StopMusic();
        }
    }

    private void HandleMusicStateChanged(GameState state)
    {
        if (state != GameState.Loading)
        {
            TryStartMusic();
        }
    }

    private void TryStartMusic()
    {
        // Wait for both Unity Start and the scene switch completion. Paused music retains its handle.
        // 等待 Start 与场景切换完成；暂停时保留句柄，不根据 IsPlaying 重新播放。
        if (!Application.isPlaying || !hasStarted || !isActiveAndEnabled || musicStarted || musicEnded ||
            isCircuitClosed || (musicPlayer && musicPlayer.IsDead) ||
            GameStateManager.Current != GameState.Playing || !gameObject.scene.isLoaded)
        {
            return;
        }
        CoreFacade core = CoreFacade.Instance;
        if (!core || !core.Audio)
        {
            return;
        }
        // A rejected request is not retried every frame (missing mapping / exhausted pool).
        musicStarted = true;
        musicHandle = core.Audio.PlayBackgroundMusic(backgroundMusic);
    }

    private void EndMusic()
    {
        musicEnded = true;
        StopMusic();
    }

    private void StopMusic()
    {
        ISoundHandle previous = musicHandle;
        musicHandle = null;
        musicStarted = false;
        previous?.Stop();
    }

    private void Awake()
    {
        Current = this;

        RefreshNodes();
        ResolvePlayer();
        BeginRun();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null)
            {
                nodes[i].OnInteracted -= HandleInteracted;
            }
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    private void LateUpdate()
    {
        TryStartMusic();
        if (playerTransform && playerTransform.gameObject.activeInHierarchy && Time.timeScale > 0f)
        {
            SamplePlayerPath(playerTransform.position);
        }
        RenderWires();
    }

    private void RenderWires()
    {
        if (!routingTilemap)
        {
            return;
        }
        foreach (Wire wire in wires)
        {
            if (wire)
            {
                wire.TilePath.CopyWorldPath(routingTilemap, renderBuffer);
                wire.RenderPath(renderBuffer);
            }
        }
    }

    /// <summary>Resolve a painted tile's center for authored nodes and player placement.
    /// 将节点及玩家放置位置映射到已绘制 tile 的中心。</summary>
    public bool TryGetTilePosition(Vector3 worldPosition, out Vector3 center)
    {
        center = worldPosition;
        if (!routingTilemap || routingTilemap.gameObject.scene != gameObject.scene)
        {
            return false;
        }
        Vector3Int cell = routingTilemap.WorldToCell(worldPosition);
        cell.z = 0;
        if (!routingTilemap.HasTile(cell))
        {
            return false;
        }
        center = routingTilemap.GetCellCenterWorld(cell);
        return true;
    }

    internal bool TryPinAnchor(Anchor anchor, Vector3 actorPosition)
    {
        if (!heldWire || !anchor || anchor.gameObject.scene != gameObject.scene ||
            !TryGetTilePosition(anchor.transform.position, out Vector3 center))
        {
            return false;
        }
        SamplePlayerPath(actorPosition);
        // Routing fixtures pin a visited tile, rather than inventing a shortcut to a nearby node.
        // 锚点只能固定玩家当前所在格，不能远程把线拉到未经过的格子。
        if (PathCell(actorPosition) != PathCell(center))
        {
            return false;
        }
        anchor.transform.position = center;
        heldWire.TilePath.Pin(anchor);
        return true;
    }

    /// <summary>Record movement through rectangular cells, including cells crossed between samples.
    /// 记录矩形网格上的移动，补齐两次采样之间跨过的格子。</summary>
    public void SamplePlayerPath(Vector3 worldPosition)
    {
        if (!routingTilemap ||
            float.IsNaN(worldPosition.x) || float.IsInfinity(worldPosition.x) ||
            float.IsNaN(worldPosition.y) || float.IsInfinity(worldPosition.y) ||
            float.IsNaN(worldPosition.z) || float.IsInfinity(worldPosition.z))
        {
            return;
        }
        if (heldWire && heldWire.IsHeld)
        {
            if (heldWire.TilePath.Cells.Count == 0)
            {
                heldWire.TilePath.Reset(PathCell(heldWire.FixedEndPosition));
            }
            Vector3 from = hasPlayerSample ? previousPlayerPosition : heldWire.FixedEndPosition;
            TraceCells(heldWire.TilePath, from, worldPosition);
        }
        if (heldGroundWire && heldGroundWire.IsHeld)
        {
            TraceCells(heldGroundWire.TilePath, previousGroundPosition, worldPosition);
            previousGroundPosition = worldPosition;
        }
        previousPlayerPosition = worldPosition;
        hasPlayerSample = true;
    }

    private Vector3Int PathCell(Vector3 position)
    {
        Vector3Int cell = routingTilemap.WorldToCell(position);
        cell.z = 0;
        return cell;
    }

    private void TraceCells(TileWirePath path, Vector3 from, Vector3 to)
    {
        Vector3 origin = routingTilemap.CellToWorld(Vector3Int.zero);
        Vector3 axisX = routingTilemap.CellToWorld(Vector3Int.right) - origin;
        Vector3 axisY = routingTilemap.CellToWorld(Vector3Int.up) - origin;
        Matrix4x4 basis = Matrix4x4.identity;
        basis.SetColumn(0, new Vector4(axisX.x, axisX.y, axisX.z, 0f));
        basis.SetColumn(1, new Vector4(axisY.x, axisY.y, axisY.z, 0f));
        Vector3 normal = Vector3.Cross(axisX, axisY).normalized;
        basis.SetColumn(2, new Vector4(normal.x, normal.y, normal.z, 0f));
        Matrix4x4 inverse = basis.inverse;
        Vector3 start = inverse.MultiplyVector(from - origin);
        Vector3 end = inverse.MultiplyVector(to - origin);
        double deltaX = (double)end.x - start.x;
        double deltaY = (double)end.y - start.y;
        Vector3Int cell = routingTilemap.WorldToCell(from);
        Vector3Int target = routingTilemap.WorldToCell(to);
        cell.z = target.z = 0;
        path.Visit(cell);
        int stepX = deltaX >= 0d ? 1 : -1;
        int stepY = deltaY >= 0d ? 1 : -1;
        while (cell != target)
        {
            double tx = cell.x == target.x ? double.PositiveInfinity :
                ((double)cell.x + (stepX > 0 ? 1 : 0) - start.x) / deltaX;
            double ty = cell.y == target.y ? double.PositiveInfinity :
                ((double)cell.y + (stepY > 0 ? 1 : 0) - start.y) / deltaY;
            // Use a reversible tie-break at exact corners so retracing a diagonal retracts the same cells.
            bool xFirst = Math.Abs(tx - ty) <= 1e-12d ? stepX > 0 : tx < ty;
            if (xFirst)
            {
                cell.x += stepX;
            }
            else
            {
                cell.y += stepY;
            }
            path.Visit(cell);
        }
    }

    /// <summary>
    /// Single entry point for rebuilding the scene's node lists.
    /// Implementation approach: one sweep per concrete type, because an interface cannot be searched for directly;
    /// it re-subscribes from scratch so a second call cannot double-subscribe. It also assigns the global Current
    /// reference, because Awake does not run while the Editor drives a run without entering play mode, and the routing
    /// nodes ask that access point which wire an actor carries. Call it again after the level creates nodes at
    /// runtime.
    /// 重建场景节点清单的单一入口.
    /// 实现思路: 每种具体类型扫一次, 因为无法直接查找接口; 它从零重新订阅, 因此重复调用不会造成重复订阅.
    /// 它同时更新全局 Current 引用: 编辑器在未进入播放模式的情况下驱动一局时 Awake 不会执行,
    /// 而绕线节点正是向该访问点询问"交互者携带哪根线". 关卡在运行期新建节点之后, 再次调用它.
    /// </summary>
    public void RefreshNodes()
    {
        Current = this;
        if (!routingTilemap)
        {
            Tilemap[] maps = FindSceneComponents<Tilemap>();
            if (maps.Length == 1)
            {
                routingTilemap = maps[0];
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null)
            {
                nodes[i].OnInteracted -= HandleInteracted;
            }
        }

        nodes.Clear();
        wires.Clear();
        anchors.Clear();
        occupied.Clear();
        socket = null;

        Anchor[] found = FindSceneComponents<Anchor>(true);
        for (int i = 0; i < found.Length; i++)
        {
            anchors.Add(found[i]);
            nodes.Add(found[i]);
        }

        PolaritySocket[] interfaces = FindSceneComponents<PolaritySocket>();
        for (int i = 0; i < interfaces.Length; i++)
        {
            interfaces[i].Initialize();
            nodes.Add(interfaces[i]);
        }

        nodes.AddRange(FindSceneComponents<VoltageReducer>());

        PowerSocket[] sockets = FindSceneComponents<PowerSocket>();
        for (int i = 0; i < sockets.Length; i++)
        {
            nodes.Add(sockets[i]);

            if (socket == null && !sockets[i].IsGroundTerminal && sockets[i].StartingWire != null)
            {
                socket = sockets[i];
            }
        }

        wires.AddRange(FindSceneComponents<Wire>());

        for (int i = 0; i < wires.Count; i++)
        {
            wires[i].Initialize();
        }

        for (int i = 0; i < sockets.Length; i++)
        {
            sockets[i].LinkWires();
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].OnInteracted += HandleInteracted;
            Component component = nodes[i] as Component;
            if (component && TryGetTilePosition(component.transform.position, out Vector3 center))
            {
                component.transform.position = center;
            }
        }

        RebuildOccupancy();

        RenderWires();
    }

    /// <summary>Register a newly placed anchor without resetting circuit state.
    /// 注册新放置的锚点，不重置回路状态。</summary>
    public void RegisterAnchor(Anchor anchor)
    {
        if (!anchor || anchor.gameObject.scene != gameObject.scene || anchors.Contains(anchor))
        {
            return;
        }

        if (TryGetTilePosition(anchor.transform.position, out Vector3 center))
        {
            anchor.transform.position = center;
        }
        anchors.Add(anchor);
        nodes.Add(anchor);
        anchor.OnInteracted += HandleInteracted;
        RenderWires();
    }

    /// <summary>Remove a reclaimed anchor and rebuild paths immediately.
    /// 收回锚点后立即移除节点并重建路径。</summary>
    public void UnregisterAnchor(Anchor anchor)
    {
        if (!anchors.Remove(anchor))
        {
            return;
        }

        foreach (Wire wire in wires)
        {
            if (wire)
            {
                wire.TilePath.Unpin(anchor);
            }
        }
        nodes.Remove(anchor);
        anchor.OnInteracted -= HandleInteracted;
        RenderWires();
    }

    /// <summary>
    /// Single entry point for asking which wire a given actor is carrying.
    /// Implementation approach: a static lookup, because a routing node is asked to act on the wire the actor
    /// holds. The actor determines the scene; invalid actors or cross-scene targets return null.
    /// 询问某个交互者当前携带哪根线的单一入口.
    /// 实现思路: 静态查询, 因为绕线节点需要作用于该交互者手中的线, 而没有别的途径触达拥有该事实的枢纽.
    /// 交互者无效、目标跨场景或当前未携带线时返回 null.
    /// </summary>
    public static Wire HeldWireOf(InteractionDetails details)
    {
        if (details == null || !details.Actor ||
            (details.Target && details.Target.scene != details.Actor.scene))
        {
            return null;
        }

        EnvironmentFacade environment = ForScene(details.Actor.scene);
        return environment && environment.heldWire && environment.heldWire.IsHeld
            ? environment.heldWire
            : null;
    }

    /// <summary>Resolve only the environment belonging to this scene; never borrow another level's circuit.
    /// 仅查询指定场景的环境, 不借用另一个关卡的回路.</summary>
    public static EnvironmentFacade ForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        if (Current && Current.gameObject.scene == scene)
        {
            return Current;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            EnvironmentFacade environment = root.GetComponentInChildren<EnvironmentFacade>();
            if (environment != null)
            {
                return environment;
            }
        }

        return null;
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
        bool wasClosed = isCircuitClosed;
        isCircuitClosed = false;
        RebuildOccupancy();
        bool valid = IsVoltageValid(neededVoltage);
        int socketCount = 0;
        foreach (IEnvironmentInteractable node in nodes)
        {
            Component component = node as Component;
            if (!component)
            {
                valid = false;
                continue;
            }
            WirePolarity required;
            if (node is PolaritySocket target)
            {
                valid &= target.IsConfigurationValid;
                required = target.Accepted;
            }
            else if (node is PowerSocket outlet)
            {
                required = outlet.IsGroundTerminal ? WirePolarity.Ground :
                    WirePolarity.Live | WirePolarity.Neutral;
            }
            else
            {
                continue;
            }
            socketCount++;
            valid &= occupied.TryGetValue(component, out WirePolarity taken) &&
                (taken & required) == required;
        }
        CurrentVoltage = neededVoltage;
        var connectedReducers = new HashSet<VoltageReducer>();
        foreach (Wire wire in wires)
        {
            if (!wire)
            {
                continue;
            }
            foreach (VoltageReducer reducer in wire.ConnectedReducers)
            {
                if (reducer && nodes.Contains(reducer) && connectedReducers.Add(reducer))
                {
                    valid &= reducer.IsConfigurationValid;
                    CurrentVoltage -= reducer.VoltageDrop;
                }
            }
        }
        isCircuitClosed = valid && socketCount > 0 && CurrentVoltage <= 0d;

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(
                $"###ASSERT circuit closed={isCircuitClosed} swaps={swapCount}. "))
            .Write();

        if (isCircuitClosed && !wasClosed)
        {
            EndMusic();
            LevelCleared?.Invoke();
        }

        return isCircuitClosed;
    }

    private static bool IsVoltageValid(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    private void RebuildOccupancy()
    {
        occupied.Clear();
        foreach (Wire wire in wires)
        {
            if (!wire)
            {
                continue;
            }
            MarkEndpoint(wire.CircuitStart, wire.Polarity);
            MarkEndpoint(wire.PlugTarget, wire.Polarity);
            foreach (PolaritySocket target in wire.ConnectedInterfaces)
            {
                if (target && nodes.Contains(target))
                {
                    MarkOccupied(target, wire.Polarity);
                }
            }
        }
    }

    private void MarkEndpoint(Transform endpoint, WirePolarity polarity)
    {
        if (!endpoint)
        {
            return;
        }
        Component target = endpoint.GetComponent<PolaritySocket>();
        if (!target)
        {
            target = endpoint.GetComponent<PowerSocket>();
        }
        if (target && nodes.Contains((IEnvironmentInteractable)target))
        {
            MarkOccupied(target, polarity);
        }
    }

    private void MarkOccupied(Component target, WirePolarity polarity)
    {
        occupied.TryGetValue(target, out WirePolarity taken);
        occupied[target] = taken | polarity;
    }

    /// <summary>
    /// Single entry point for reacting to one node's interaction.
    /// Implementation approach: routing nodes have updated engagement while sockets only announce requests; this applies the circuit
    /// consequences, then rebuilds the drawn paths and re-evaluates.
    /// 响应某个节点交互的单一入口.
    /// 实现思路: 绕线节点已更新接入状态，插口仅通知请求；此处施加回路后果, 然后重建绘制路径并重新判定.
    /// </summary>
    private void HandleInteracted(IEnvironmentInteractable node)
    {
        if (playerTransform)
        {
            SamplePlayerPath(playerTransform.position);
        }

        switch (node)
        {
            case PolaritySocket liveInterface:
                ApplyPlug(liveInterface);
                break;
            case PowerSocket outlet:
                ApplyOutlet(outlet);
                break;
            case VoltageReducer reducer:
                if (heldWire && heldWire.IsHeld && routingTilemap && reducer.CanInteract)
                {
                    TraceToNode(heldWire, reducer.transform);
                    heldWire.ConnectReducer(reducer);
                }
                break;
        }

        RenderWires();
        EvaluateCircuit();
    }

    /// <summary>Each polarity endpoint has one connection, including its outgoing wire.
    /// 每个极性锚点仅容纳一条连接，出线同样占用；禁止接回当前线的起点插座。</summary>
    public bool CanConnectSocket(Component target)
    {
        if (!target || target.gameObject.scene != gameObject.scene || !routingTilemap ||
            !(target is IEnvironmentInteractable node) || !nodes.Contains(node) ||
            (target is PolaritySocket configured && !configured.IsConfigurationValid))
        {
            return false;
        }
        bool ground = target is PolaritySocket terminal ? terminal.Accepted == WirePolarity.Ground :
            target is PowerSocket outlet && outlet.IsGroundTerminal;
        Wire wire = ground ? heldGroundWire : heldWire;
        if (ground && !wire && !heldWire && (!socket || !socket.StartingWire))
        {
            return false;
        }
        if (!ground && (!wire || !wire.IsHeld))
        {
            return false;
        }
        if (wire && wire.CircuitStart == target.transform)
        {
            return false;
        }
        RebuildOccupancy();
        WirePolarity polarity = ground ? WirePolarity.Ground : wire.Polarity;
        return !occupied.TryGetValue(target, out WirePolarity taken) || (taken & polarity) == 0;
    }

    private void ApplyPlug(PolaritySocket target)
    {
        if (!target.CanInteract || !CanConnectSocket(target))
        {
            return;
        }
        if (target.Accepted == WirePolarity.Ground)
        {
            ApplyGround(target.transform);
            return;
        }
        ApplyPoweredSocket(target, false);
    }

    private void ApplyPoweredSocket(Component target, bool closesCircuit)
    {
        Wire previous = heldWire;
        WirePolarity opposite = previous.Polarity == WirePolarity.Live ? WirePolarity.Neutral : WirePolarity.Live;
        occupied.TryGetValue(target, out WirePolarity taken);
        TraceToNode(previous, target.transform);
        if (target is PolaritySocket incoming)
        {
            previous.ConnectInterface(incoming);
        }
        previous.PlugInto(target.transform, closesCircuit);
        heldWire = null;
        // An occupied outgoing endpoint cannot supply another wire (e.g. the starting outlet).
        // 异极端口已有出线时仅完成接入，不在已占用锚点再次出线。
        if ((taken & opposite) != 0)
        {
            return;
        }
        Wire next = NextPoweredWire(previous);
        next.TilePath.CopyFrom(previous.TilePath);
        HoldWire(next, previous);
        next.TilePath.CommitConnection();
        if (target is PolaritySocket outgoing)
        {
            next.ConnectInterface(outgoing);
        }
        swapCount++;
    }

    private void TraceToNode(Wire wire, Transform target)
    {
        Vector3 from = wire == heldGroundWire ? previousGroundPosition :
            hasPlayerSample ? previousPlayerPosition : wire.FixedEndPosition;
        TraceCells(wire.TilePath, from, target.position);
        wire.TilePath.CommitConnection();
        if (wire == heldGroundWire)
        {
            previousGroundPosition = target.position;
        }
        else
        {
            previousPlayerPosition = target.position;
            hasPlayerSample = true;
        }
    }

    private Wire NextPoweredWire(Wire previous)
    {
        WirePolarity opposite = previous.Polarity == WirePolarity.Live ? WirePolarity.Neutral : WirePolarity.Live;
        foreach (Wire candidate in wires)
        {
            if (candidate && candidate != previous && candidate.Polarity == opposite &&
                !candidate.IsHeld && !candidate.CircuitStart && !candidate.PlugTarget)
            {
                return candidate;
            }
        }
        return CreateWire(opposite, previous, previous.PlugTarget);
    }

    private Wire CreateWire(WirePolarity polarity, Wire template, Transform origin)
    {
        var root = new GameObject($"{polarity} Wire");
        root.transform.SetParent(transform, false);
        root.transform.position = origin.position;
        Wire wire = root.AddComponent<Wire>();
        wire.ConfigureContinuation(template, polarity);
        wire.SetSocket(socket);
        generatedWires.Add(wire);
        wires.Add(wire);
        return wire;
    }

    private void ApplyGround(Transform target)
    {
        if (heldGroundWire)
        {
            TraceToNode(heldGroundWire, target);
            heldGroundWire.PlugInto(target, false);
            heldGroundWire = null;
        }
        else
        {
            Wire template = heldWire ? heldWire : socket.StartingWire;
            heldGroundWire = CreateWire(WirePolarity.Ground, template, target);
            heldGroundWire.BeginConnection(target, null);
            heldGroundWire.TilePath.Reset(PathCell(target.position));
            heldGroundWire.SetRenderOrder(wireSortingLayerId, nextWireRenderOrder++);
            heldGroundWire.Hold();
            previousGroundPosition = target.position;
        }
    }

    private void ApplyOutlet(PowerSocket outlet)
    {
        if (!outlet.CanInteract || !CanConnectSocket(outlet))
        {
            return;
        }
        if (outlet.IsGroundTerminal)
        {
            ApplyGround(outlet.transform);
            return;
        }
        ApplyPoweredSocket(outlet, true);
    }

    private void ResolvePlayer()
    {
        GameObject player = FindTaggedObject(playerTag);
        playerTransform = player ? player.transform : null;
        if (musicPlayer)
        {
            musicPlayer.Died -= EndMusic;
        }
        musicPlayer = player ? player.GetComponent<PlayerMove>() : null;
        if (musicPlayer && isActiveAndEnabled)
        {
            musicPlayer.Died += EndMusic;
        }
    }

    private void BeginRun()
    {
        StopMusic();
        musicEnded = false;
        nextWireRenderOrder = 0;
        hasPlayerSample = false;
        swapCount = 0;
        isCircuitClosed = false;
        heldWire = null;
        heldGroundWire = null;
        foreach (Wire generated in generatedWires)
        {
            if (!generated)
            {
                continue;
            }
            wires.Remove(generated);
            generated.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(generated.gameObject);
            }
            else
            {
                DestroyImmediate(generated.gameObject);
            }
        }
        generatedWires.Clear();
        occupied.Clear();

        for (int i = 0; i < wires.Count; i++)
        {
            if (wires[i])
            {
                wires[i].PlugInto(null, false);
                wires[i].BeginConnection(null, null);
                if (routingTilemap)
                {
                    wires[i].TilePath.Reset(PathCell(wires[i].FixedEndPosition));
                }
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is Anchor anchor)
            {
                anchor.ResetRouting();
            }
        }

        if (!routingTilemap || routingTilemap.gameObject.scene != gameObject.scene ||
            routingTilemap.cellLayout != GridLayout.CellLayout.Rectangle ||
            routingTilemap.cellSwizzle != GridLayout.CellSwizzle.XYZ)
        {
            Debug.LogWarning("Environment requires a rectangular XY routing Tilemap in its own scene.", this);
            return;
        }

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
            HoldWire(heldWire);

            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"Run started carrying {heldWire.Polarity} from {socket.name}. "))
                .Write();
        }

        if (playerTransform)
        {
            SamplePlayerPath(playerTransform.position);
        }
        RenderWires();
        EvaluateCircuit();
    }

    private void HoldWire(Wire wire, Wire previous = null)
    {
        heldWire = wire;
        wire.BeginConnection(previous ? previous.PlugTarget : wire.Socket.transform, previous);
        if (nextWireRenderOrder == 0)
        {
            wireSortingLayerId = wire.GetComponent<LineRenderer>().sortingLayerID;
        }
        // All wires in a run share one layer; later handovers draw above earlier ones.
        // 关卡内按实际出线顺序排序，不依赖预制体层级、实例 ID 或路径包围盒。
        wire.SetRenderOrder(wireSortingLayerId, nextWireRenderOrder++);
        wire.Hold();
    }

    private string Describe(Wire wire)
    {
        if (wire == null)
        {
            return "absent";
        }

        if (wire.IsClosed)
        {
            return wire.Socket ? $"closed at {wire.Socket.name}" : "closed";
        }

        if (wire.PlugTarget != null)
        {
            return $"parked at {wire.PlugTarget.name}";
        }

        return wire.IsHeld ? "carried" : "loose";
    }

    private GameObject FindTaggedObject(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        try
        {
            Transform[] transforms = FindSceneComponents<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].CompareTag(tag))
                {
                    return transforms[i].gameObject;
                }
            }

            return null;
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

    // A level owns only its scene's circuit, including when scenes are loaded additively.
    // 关卡仅管理自身场景的回路, 避免叠加加载时接入其他关卡节点.
    private T[] FindSceneComponents<T>(bool includeInactive = false) where T : Component
    {
        List<T> result = new();
        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T[] components = roots[i].GetComponentsInChildren<T>(includeInactive);
            for (int j = 0; j < components.Length; j++)
            {
                if (includeInactive || components[j].gameObject.activeInHierarchy)
                {
                    result.Add(components[j]);
                }
            }
        }

        return result.ToArray();
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < wires.Count; i++)
        {
            Wire wire = wires[i];
            if (!wire)
            {
                continue;
            }

            Gizmos.color = ColorFor(wire.Polarity);

            if (!routingTilemap)
            {
                continue;
            }
            wire.TilePath.CopyWorldPath(routingTilemap, renderBuffer);

            for (int p = 1; p < renderBuffer.Count; p++)
            {
                Gizmos.DrawLine(renderBuffer[p - 1], renderBuffer[p]);
            }

            if (renderBuffer.Count > 0)
            {
                Gizmos.DrawWireSphere(renderBuffer[renderBuffer.Count - 1], 0.12f);
            }
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
    [Tooltip("Node to drive manually without player input; debug actions modify the current scene state.")]
    private Transform debugTarget;

    /// <summary>
    /// Single entry point for driving one interaction by hand.
    /// Implementation approach: resolves the node on the debug target, builds the request with the tagged player
    /// as the actor, and calls the same entry point as Player. This allows testing circuit transitions without player input.
    /// 手动驱动一次交互的单一入口.
    /// 实现思路: 在调试目标上解析出节点, 以带 Tag 的玩家为交互者构造请求, 并调用与 Player 相同的入口.
    /// 可在无需玩家输入的情况下检查回路状态变化.
    /// </summary>
    [Button("Simulate interact with debug target")]
    private void DebugSimulateInteract()
    {
        IEnvironmentInteractable node = ResolveDebugNode();
        if (node == null)
        {
            return;
        }

        GameObject actor = FindTaggedObject(playerTag);
        if (!actor)
        {
            actor = gameObject;
        }

        InteractionDetails details = new InteractionDetails(actor, debugTarget.gameObject);

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Simulating interact on {debugTarget.name}. "))
            .Write();

        node.Interact(details);
    }

    /// <summary>Reclaim the debug anchor using the same entry point as Player.
    /// 使用 Player 相同入口收回调试锚点。</summary>
    [Button("Reclaim debug anchor")]
    private void DebugReclaimAnchor()
    {
        Anchor anchor = debugTarget ? debugTarget.GetComponentInParent<Anchor>() : null;
        if (anchor)
        {
            anchor.TryReclaim(new InteractionDetails(DebugActor(), anchor.gameObject));
        }
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
        {
            return polaritySocket;
        }

        PowerSocket powerSocket = debugTarget.GetComponentInParent<PowerSocket>();
        if (powerSocket)
        {
            return powerSocket;
        }

        VoltageReducer reducer = debugTarget.GetComponentInParent<VoltageReducer>();
        if (reducer)
        {
            return reducer;
        }

        Anchor anchor = debugTarget.GetComponentInParent<Anchor>();
        if (anchor)
        {
            return anchor;
        }

        return null;
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
        ResolvePlayer();
        RenderWires();
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
        RefreshNodes();
        ResolvePlayer();
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
            DebugInteractAt(anchor);
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
                DebugInteractAt(anchor);
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
            DebugInteractAt(dual);
            ReportStep("4 swapping the carried wire at a dual interface",
                heldWire != null && heldWire != carriedBefore && swapCount == 1 &&
                carriedBefore != null && carriedBefore.PlugTarget == dual.transform,
                $"was={polarityBefore} now={(heldWire ? heldWire.Polarity.ToString() : "none")} swaps={swapCount}");
        }

        bool cleared = isCircuitClosed;
        Action onCleared = () => cleared = true;
        LevelCleared += onCleared;
        try
        {
            // Complete every authored socket, including ground sockets, through the normal interaction path.
            // 通过正常交互补齐全部插座与降压器，而不是假定回原插座必定获胜。
            var remaining = new List<IEnvironmentInteractable>(nodes);
            remaining.Sort((left, right) => AcceptanceOrder(left).CompareTo(AcceptanceOrder(right)));
            foreach (IEnvironmentInteractable node in remaining)
            {
                if ((node is PolaritySocket || node is PowerSocket || node is VoltageReducer) && node.CanInteract)
                {
                    DebugInteractAt((MonoBehaviour)node);
                }
            }
            ReportStep("5 the carried wire or completed endpoint is consistent",
                !heldWire || (heldWire.IsHeld && (heldWire.Polarity == WirePolarity.Live || heldWire.Polarity == WirePolarity.Neutral)),
                $"held={Describe(heldWire)} remainingVoltage={CurrentVoltage}");
            ReportStep("6 configured sample meets the success conditions",
                IsCircuitClosed && cleared,
                $"complete={IsCircuitClosed} remainingVoltage={CurrentVoltage} needed={neededVoltage}; check socket configuration and available voltage drops if this fails");
        }
        finally
        {
            LevelCleared -= onCleared;
        }

        GameLog.Info(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(failedSteps == 0
                ? $"###ACCEPTANCE PASS ({acceptedSteps} passed, {skippedSteps} skipped). "
                : $"###ACCEPTANCE FAIL ({failedSteps} failed, {acceptedSteps} passed, {skippedSteps} skipped). "))
            .Write();
    }

    // Scripted acceptance moves the actor through the same tiles before driving each node.
    // 脚本化验收先移动角色并采样到目标格，再驱动节点；不模拟输入或物理移动。
    private int AcceptanceOrder(IEnvironmentInteractable node)
    {
        return node is VoltageReducer ? 0 : ReferenceEquals(node, socket) ? 2 : 1;
    }

    private void DebugInteractAt(MonoBehaviour target)
    {
        GameObject actor = DebugActor();
        actor.transform.position = target.transform.position;
        Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
        if (body)
        {
            body.position = target.transform.position;
            body.velocity = Vector2.zero;
        }
        SamplePlayerPath(actor.transform.position);
        ((IEnvironmentInteractable)target).Interact(new InteractionDetails(actor, target.gameObject));
    }

    private void ReportStep(string step, bool passed, string detail)
    {
        if (passed)
        {
            acceptedSteps++;
        }
        else
        {
            failedSteps++;
        }

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
            {
                return anchors[i];
            }
        }

        return null;
    }

    private PolaritySocket FindDualInterface()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is PolaritySocket candidate && candidate.CanInteract && candidate.IsDual)
            {
                return candidate;
            }
        }

        return null;
    }


#endregion
}
