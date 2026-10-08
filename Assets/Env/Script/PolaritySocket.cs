using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// A live interface: the place a wire must match to be plugged in, and the dual interface where the player swaps
/// which wire they carry.
/// Subsystem: Environment.
/// Where it lives: on the scene object that represents the interface, next to a Collider2D so the Player's range
/// detection can find it.
/// Responsibility: declare which polarities it accepts, and announce that an interaction was requested here.
/// Does NOT own: which wire is held, whether the plug is legal right now, or which polarity slot is already taken.
/// Occupancy and the swap are circuit state, so the facade derives them from the circuit it already tracks: one
/// owner for that state instead of two that can disagree.
/// Lifetime: normally authored in a level; refresh the environment after runtime creation.
/// 带电接口: 线必须电性对应才能插入的地方, 也是玩家**换线**的双性接口.
/// Subsystem 归属: Environment.
/// 存在位置: 代表该接口的场景物体上, 与一个 Collider2D 同处, 以便 Player 的范围检测能找到它.
/// 职能: 声明它接受哪些电性, 并广播"此处收到了交互请求".
/// 不负责: 哪根线被持有, 此刻这次插入是否合法, 以及某个极性槽位是否已被占用.
/// 占用与换线属于回路状态, 因此由门面从它已持有的回路推导: 这份状态只有一个拥有者, 而不是两个可能互相矛盾的拥有者.
/// 生命周期: 通常随关卡预设；运行时创建后需刷新环境节点。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PolaritySocket : MonoBehaviour, IEnvironmentInteractable
{
    public bool CanInteract => canInteract && isActiveAndEnabled && IsConfigurationValid &&
        EnvironmentFacade.ForScene(gameObject.scene) is EnvironmentFacade environment && environment.CanConnectSocket(this);
    [HideInInspector]
    public WirePolarity Accepted => accepted;
    public bool IsConfigurationValid => accepted == (WirePolarity.Live | WirePolarity.Neutral) ||
        accepted == WirePolarity.Ground;

    /// <summary>
    /// Whether this interface carries both live and neutral, which is what makes it a swap point.
    /// 该接口是否同时含火与零, 即它是否是换线点.
    /// </summary>
    public bool IsDual
        => (accepted & WirePolarity.Live) != 0 && (accepted & WirePolarity.Neutral) != 0;

    public event Action<IEnvironmentInteractable> OnInteracted;

    [SerializeField, BoxGroup("Polarity")]
    [Tooltip("Which wire polarities this interface accepts. Live | Neutral makes it the dual interface where the player swaps wires.")]
    private WirePolarity accepted = WirePolarity.Live | WirePolarity.Neutral;

    [SerializeField, BoxGroup("Polarity")]
    [Tooltip("Whether any wire may be plugged in here at all.")]
    private bool canInteract = true;

    [SerializeField, BoxGroup("Polarity")]
    private SocketFireEffect firePrefab;

    [SerializeField, BoxGroup("Polarity")] private Transform liveAnchor;
    [SerializeField, BoxGroup("Polarity")] private Transform neutralAnchor;

    public float randomRadius;
    public int randomFireNumber;
    
    /// <summary>Visual endpoint only; circuit occupancy still belongs to the socket.
    /// 仅用于外观接线，电路占用仍以插座为单位。</summary>
    public Transform GetWireAnchor(WirePolarity polarity)
    {
        if (!IsDual)
        {
            return null;
        }
        return polarity == WirePolarity.Live ? liveAnchor :
            polarity == WirePolarity.Neutral ? neutralAnchor : null;
    }

    private bool initializationErrorReported;

    /// <summary>Play one Fire burst after the environment accepts a dual socket interaction.
    /// 环境确认双极插座交互成功后，在插座位置播放一次 Fire。</summary>
    internal void PlayInteractionEffect()
    {
        if (!Application.isPlaying || !firePrefab)
        {
            return;
        }

        for (int i = 0; i < randomFireNumber; i++)
        {
            var random = UnityEngine.Random.insideUnitCircle;
            SocketFireEffect effect = Instantiate(firePrefab, transform.position + (Vector3)random * randomRadius,
                Quaternion.identity, transform);
            SpriteRenderer socketRenderer = GetComponent<SpriteRenderer>();
            if (socketRenderer)
            {
                SpriteRenderer effectRenderer = effect.GetComponent<SpriteRenderer>();
                effectRenderer.sortingLayerID = socketRenderer.sortingLayerID;
                effectRenderer.sortingOrder = socketRenderer.sortingOrder + 1;
            }
        }
    }

    private void Awake()
    {
        Initialize();
    }

    /// <summary>Only a dual socket or a ground socket may initialize.
    /// 仅允许双极插座或纯地线插座初始化。</summary>
    public bool Initialize()
    {
        if (IsConfigurationValid)
        {
            initializationErrorReported = false;
            return true;
        }
        enabled = false;
        if (!initializationErrorReported)
        {
            initializationErrorReported = true;
            Debug.LogError($"PolaritySocket '{name}' cannot initialize: expected exactly Live | Neutral or Ground.", this);
        }
        return false;
    }

    /// <summary>
    /// Single entry point for asking whether this interface can take a wire of the given polarity.
    /// 询问该接口能否接受给定电性的线的单一入口.
    /// </summary>
    public bool Accepts(WirePolarity polarity)
        => IsConfigurationValid && polarity != WirePolarity.None && (accepted & polarity) != 0;

    /// <summary>
    /// Single entry point for a plug request at this interface.
    /// Implementation approach: gate on CanInteract, then announce the request. Plugging is a circuit change, and
    /// the circuit has exactly one owner, so the facade applies it and can refuse it with a reason.
    /// 本接口收到一次插入请求的单一入口.
    /// 实现思路: 按 CanInteract 设门, 然后广播该请求. 插入是一次回路变更, 而回路只有一个拥有者,
    /// 因此由门面施加它, 并能给出拒绝的理由.
    /// </summary>
    public void Interact(InteractionDetails details)
    {
        if (!CanInteract)
        {
            GameLog.Info(this)
                .Subsystem("Environment")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Interaction refused: this interface does not accept plugs. "))
                .Action(LogAction.Return)
                .Write();

            return;
        }

        OnInteracted?.Invoke(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (IsConfigurationValid)
        {
            return;
        }

        GameLog.Warning(this)
            .Subsystem("Environment")
            .Name(LogName.Class)
            .Issue(LogIssue.Invalid(nameof(accepted)))
            .Action(LogAction.Specify("Expected exactly Live | Neutral or Ground. Initialization will be refused. "))
            .Write();
    }
#endif
}
