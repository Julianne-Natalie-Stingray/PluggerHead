using UnityEngine;

/// <summary>
/// Shared service access point on the authored Core GameObject; it does not create Core automatically.
/// Awake keeps the first live instance, caches local services, and calls DontDestroyOnLoad.
/// Later instances destroy their own GameObject. Surviving references remain valid across scene switches;
/// references to destroyed scene content must be reacquired. Other components' Awake order is not guaranteed.
/// RequireComponent declares component dependencies, not their configuration or readiness.
/// 场景中预先配置的 Core 服务入口, 不自动创建 Core. Awake 保留首个存活实例、缓存同物体服务并标记持久.
/// 重复实例销毁自己的 GameObject. 指向持久对象的引用可跨场景保留, 被销毁的场景对象需要重新获取.
/// 不保证其他组件的 Awake 顺序; RequireComponent 声明组件依赖, 不保证其配置完整或已经初始化.
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
    /// Current Core access point. Assigned during Awake before service caching; it is not a readiness signal.
    /// 当前 Core 入口, 在 Awake 中缓存服务之前赋值, 不代表各服务已经就绪.
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
    /// Cache services on this GameObject. Service asset references still require serialized configuration.
    /// 缓存同物体服务组件; 各服务使用的资源引用仍需通过序列化配置.
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
