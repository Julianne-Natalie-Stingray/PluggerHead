using UnityEngine;

/// <summary>
/// The single entry point of the Core subsystem.
/// Subsystem: Core.
/// Where it lives: on the Core GameObject, which is a prefab instance inside the starting scene.
/// Responsibility: be the one access point that survives a scene switch, cache the Core services, and mark
/// itself persistent.
/// Does NOT own: any service's behaviour. It caches and exposes; every mechanism belongs to the service it
/// exposes.
/// Lifetime: created with the Core GameObject and made persistent in Awake, so it outlives every scene switch.
/// A second CoreFacade destroys its own GameObject and reports it, because only the first one is the access
/// point the rest of the game resolved against.
/// Paradigms: Singleton. This is a deliberate, single exception to "use the Singleton pattern sparingly": a
/// scene switch destroys every scene-local reference, so something that belongs to no scene has to be reachable
/// without one. Alternative shapes were rejected -- a service locator adds a registry for exactly one consumer,
/// and a static facade would still have to hold an instance, which hides the singleton instead of removing it.
/// Core 子系统的单一入口.
/// Subsystem 归属: Core.
/// 存在位置: Core GameObject 上, 而它是起始场景内的一个预制体实例.
/// 职能: 作为唯一能熬过场景切换的访问点; 缓存 Core 的各项服务; 标记自身为持久.
/// 不负责: 任何服务的行为. 它只做缓存与暴露; 每个机制都属于它所暴露的那个服务.
/// 生命周期: 随 Core GameObject 创建, 并在 Awake 中变为持久, 因此比每次场景切换都长寿.
/// 第二个 CoreFacade 会销毁自己的 GameObject 并报告该事实, 因为只有第一个才是游戏其余部分据以解析的访问点.
/// 使用范式: 单例. 这是对"少用单例范式"的一次刻意且唯一的破例: 场景切换会销毁所有场景内引用,
/// 因此某个不属于任何场景的东西必须能在没有场景的情况下被访问到.
/// 已否决的其他形态: 服务定位器会为**恰好一个**消费者引入一张注册表;
/// 而静态门面仍然必须持有实例, 那只是把单例藏起来, 而不是消除它.
/// </summary>
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(AudioManager))]
[RequireComponent(typeof(TimerRunner))]
[RequireComponent(typeof(SceneSwitchManager))]
[DisallowMultipleComponent]
public class CoreFacade : MonoBehaviour
{
    #region APIs

    /// <summary>
    /// The one access point that outlives a scene switch. Null before the first CoreFacade has awoken.
    /// 唯一能熬过场景切换的访问点. 在第一个 CoreFacade 完成 Awake 之前为 null.
    /// </summary>
    public static CoreFacade Instance { get; private set; }

    public InputManager Input => input;
    public AudioManager Audio => audios;
    public SceneSwitchManager SceneSwitch => sceneSwitch;

    #endregion

    private InputManager input;
    private AudioManager audios;
    private SceneSwitchManager sceneSwitch;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("A second Core was found; its GameObject is being destroyed. "))
                .Action(LogAction.Ignore)
                .Write();

            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeInternal();
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Single entry point for resolving this component's own dependencies.
    /// Implementation approach: resolves the services by GetComponent and caches them, so nothing needs
    /// Inspector wiring, and a missing service is caught by the RequireComponent attributes rather than at
    /// first use.
    /// 解析本组件自身依赖的单一入口.
    /// 实现思路: 用 GetComponent 解析各项服务并缓存, 因此无需 Inspector 接线,
    /// 而缺失的服务由 RequireComponent 属性拦截, 不会拖到首次使用时才暴露.
    /// </summary>
    private void InitializeInternal()
    {
        if (!input)
            input = GetComponent<InputManager>();

        if (!audios)
            audios = GetComponent<AudioManager>();

        if (!sceneSwitch)
            sceneSwitch = GetComponent<SceneSwitchManager>();
    }
}
