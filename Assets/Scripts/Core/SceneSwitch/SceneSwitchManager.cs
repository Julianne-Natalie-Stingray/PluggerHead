using System.Collections;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene switch service used by production gameplay. Editor tests also load scenes directly for isolation.
/// Subsystem: Core (SceneSwitch).
/// Where it lives: on the Core GameObject, reached through CoreFacade.SceneSwitch.
/// Responsibility: validate a switch request against the whitelist and Unity's Build Settings, enforce that only
/// one switch is in flight per instance, and bracket asynchronous Single loading with the Loading label.
/// Does NOT own: which scene the game should go to next -- that is the caller's decision, passed as an argument,
/// because a "next scene" field on a bus would put a gameplay decision in a configuration asset. It also does
/// not own progress reporting (AsyncOperation already exposes it), transitions or loading screens, what happens
/// to audio already playing, or whether input is blocked; those are gameplay and presentation decisions.
/// Lifetime: created with the Core GameObject and kept alive by CoreFacade's DontDestroyOnLoad, so one instance
/// serves subsequent scenes; scene loading can create duplicate Core instances, removed by CoreFacade.
/// Loading state: entered after loading begins and left after the coroutine observes operation.isDone.
/// This service does not write timeScale or listener pause directly, but Changed subscribers may do so.
/// Paradigms: none. It is a MonoBehaviour service, reached through CoreFacade rather than a Singleton.
/// 生产玩法使用的 Core 场景切换服务; Editor 测试也会直接加载场景以隔离验证.
/// Subsystem 归属: Core (SceneSwitch).
/// 存在位置: Core GameObject 上, 通过 CoreFacade.SceneSwitch 访问.
/// 职能: 依据白名单与 Unity 的 Build Settings 校验请求; 单个实例同一时刻只允许一次切换;
/// 异步 Single 加载前后进入和退出 Loading 标签.
/// 不负责: 游戏接下来该去哪个场景 —— 那是调用方的决定, 以参数传入, 因为把"下一个场景"字段放在总线上
/// 等于把玩法决定塞进配置资产. 它也不负责进度上报(AsyncOperation 已经暴露了),
/// 不负责转场或加载界面, 不负责已在播放的音频如何处置, 也不负责是否屏蔽输入; 那些是玩法与表现的决定.
/// 生命周期: 随 Core GameObject 创建, 并由 CoreFacade 的 DontDestroyOnLoad 保活,
/// 因此首个实例服务后续场景; 场景加载可以创建重复 Core, 由 CoreFacade 移除.
/// Loading 状态: 加载开始后进入, 协程观察到 operation.isDone 后退出, 不保证与首个可见帧精确同步.
/// 本服务不直接写 timeScale 或监听器暂停, 但 Changed 订阅者仍可能改变它们.
/// 使用范式: 无. 它是 MonoBehaviour 服务, 通过 CoreFacade 而非单例访问.
/// </summary>
[DisallowMultipleComponent]
public class SceneSwitchManager : MonoBehaviour
{
    #region APIs

    /// <summary>
    /// Whether a switch is currently in flight. Callers may check this to avoid a refused request, but
    /// RequestSwitch enforces it regardless, so checking is a convenience rather than a requirement.
    /// 当前是否有一次切换在途. 调用方可以检查它以避免请求被拒, 但 RequestSwitch 无论如何都会强制执行该约束,
    /// 因此检查只是便利, 不是必需.
    /// </summary>
    public bool IsSwitching => isSwitching;

    #endregion

    [SerializeField, Expandable] private SceneSwitchConfigs configs;

    private AsyncOperation operation;
    private bool isSwitching;

    #region Unity lifecycle

    private void Awake()
    {
        InitializeInternal();
    }

    #endregion

    #region APIs

    /// <summary>
    /// Single entry point for requesting a switch to another scene.
    /// Implementation approach: refuses without starting anything when the whitelist is missing, when a switch
    /// is already in flight, when the key has no mapping, or when the mapped scene is not registered for
    /// loading. Only after all four checks pass does it start the load. Returning null on refusal keeps the
    /// caller's refusal handling to a null check. Missing configuration and busy refusals are silent here;
    /// missing mappings and missing build entries log errors. Exceptions from loading are not caught.
    /// 请求切换到另一个场景的单一入口.
    /// 实现思路: 在白名单缺失, 已有切换在途, 键无映射, 或映射到的场景未注册可加载时, 不启动任何东西直接拒绝.
    /// 四项检查全部通过后才开始加载. 被拒时返回 null, 使调用方的失败处理收敛为一次 null 判断;
    /// 此处缺配置或忙碌时静默返回, 无映射或无构建项时记录错误; 未捕获加载调用抛出的异常.
    /// </summary>
    public AsyncOperation RequestSwitch(SceneId requested)
    {
        if (!configs || isSwitching)
            return null;

        if (!configs.TryGetSceneName(requested, out string sceneName))
        {
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.CannotFind(nameof(requested), nameof(configs)))
                .Action(LogAction.Ignore)
                .Write();

            return null;
        }

        if (SceneUtility.GetBuildIndexByScenePath(sceneName) < 0)
        {
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"{sceneName} is not registered in Build Settings. "))
                .Action(LogAction.Ignore)
                .Write();

            return null;
        }

        operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        isSwitching = true;

        StartCoroutine(RunSwitch(requested, sceneName));

        return operation;
    }

    #endregion

    /// <summary>
    /// Single entry point for driving one switch from start to finish.
    /// Implementation approach: holds activation until progress reaches 0.9, then releases it and waits for
    /// isDone before leaving Loading and clearing local bookkeeping. This is not synchronized to rendering.
    /// There is no timeout or finally cleanup if the coroutine is interrupted or a state callback throws.
    /// 驱动一次切换从开始到结束的单一入口.
    /// 实现思路: progress 达到 0.9 后放行激活, 等 isDone 才退出 Loading 并清理本地标记;
    /// 不与渲染帧同步. 协程中断或状态回调抛异常时没有超时或 finally 清理.
    /// </summary>
    private IEnumerator RunSwitch(SceneId requested, string sceneName)
    {
        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Switching to {requested} ({sceneName}). "))
            .Write();

        operation.allowSceneActivation = false;

        GameStateManager.EnterLoading();

        while (operation.progress < ProgressBeforeActivation)
            yield return null;

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        GameStateManager.ExitLoading();

        isSwitching = false;
        operation = null;

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Switched to {requested} ({sceneName}). "))
            .Write();
    }

    /// <summary>
    /// Single entry point for resolving this component's own dependencies.
    /// Implementation approach: validates the whitelist here and disables the component when it is absent, so a
    /// missing assignment becomes one clear error plus a component that refuses requests, rather than a null
    /// reference on the first request attempt. Unlike AudioManager, nothing on this component needs Awake to be
    /// finished elsewhere, so the validation stays in Awake.
    /// 解析本组件自身依赖的单一入口.
    /// 实现思路: 在此校验白名单, 缺失时禁用本组件, 使一次漏赋值成为"一条明确错误 + 一个拒绝请求的组件",
    /// 而不是首次请求时的空引用. 与 AudioManager 不同, 本组件没有任何东西需要别处先完成 Awake,
    /// 因此校验留在 Awake.
    /// </summary>
    private void InitializeInternal()
    {
        if (configs)
            return;

        GameLog.Error(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.NotAssigned(nameof(configs)))
            .Action(LogAction.DisableComponent)
            .Write();

        enabled = false;
    }

    private const float ProgressBeforeActivation = 0.9f;
}
