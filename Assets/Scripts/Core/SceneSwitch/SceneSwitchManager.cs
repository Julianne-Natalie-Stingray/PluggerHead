using System.Collections;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene switcher of the Core subsystem. It is the only entry point for loading another scene.
/// Subsystem: Core (SceneSwitch).
/// Where it lives: on the Core GameObject, reached through CoreFacade.SceneSwitch.
/// Responsibility: validate a switch request against the whitelist and Unity's Build Settings, enforce that only
/// one switch is in flight, own the Loading state for exactly as long as the new scene is not yet shown, and
/// load the scene.
/// Does NOT own: which scene the game should go to next -- that is the caller's decision, passed as an argument,
/// because a "next scene" field on a bus would put a gameplay decision in a configuration asset. It also does
/// not own progress reporting (AsyncOperation already exposes it), transitions or loading screens, what happens
/// to audio already playing, or whether input is blocked; those are gameplay and presentation decisions.
/// Lifetime: created with the Core GameObject and kept alive by CoreFacade's DontDestroyOnLoad, so one instance
/// serves every scene for the whole session. It is never created at runtime.
/// Loading state: entered once the load has begun and left as soon as the new scene is activated, which is the
/// window in which no new scene is visible yet. It applies no mechanism -- game time and audio are untouched --
/// because a load is a transition, not a suspension.
/// Paradigms: none. It is a MonoBehaviour service, reached through CoreFacade rather than a Singleton.
/// 场景切换器, 属于 Core 子系统. 它是加载另一个场景的唯一入口.
/// Subsystem 归属: Core (SceneSwitch).
/// 存在位置: Core GameObject 上, 通过 CoreFacade.SceneSwitch 访问.
/// 职能: 依据白名单与 Unity 的 Build Settings 校验切换请求; 强制同一时刻只有一次切换在途;
/// 在"新场景尚未显示"的整段时间内拥有 Loading 状态; 并加载场景.
/// 不负责: 游戏接下来该去哪个场景 —— 那是调用方的决定, 以参数传入, 因为把"下一个场景"字段放在总线上
/// 等于把玩法决定塞进配置资产. 它也不负责进度上报(AsyncOperation 已经暴露了),
/// 不负责转场或加载界面, 不负责已在播放的音频如何处置, 也不负责是否屏蔽输入; 那些是玩法与表现的决定.
/// 生命周期: 随 Core GameObject 创建, 并由 CoreFacade 的 DontDestroyOnLoad 保活,
/// 因此一个实例服务整场会话中的每个场景. 运行时从不创建它.
/// Loading 状态: 在加载开始后进入, 在新场景被激活的瞬间离开 —— 那正是"新场景尚不可见"的窗口.
/// 它不施加任何机制: 游戏时间与音频都不动, 因为加载是一次过渡而不是一次挂起.
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
    /// caller's failure handling to a single null check, and every refusal states its own reason in the log
    /// rather than silently doing nothing.
    /// 请求切换到另一个场景的单一入口.
    /// 实现思路: 在白名单缺失, 已有切换在途, 键无映射, 或映射到的场景未注册可加载时, 不启动任何东西直接拒绝.
    /// 四项检查全部通过后才开始加载. 被拒时返回 null, 使调用方的失败处理收敛为一次 null 判断;
    /// 且每次拒绝都在日志中写明各自的原因, 而不是静默地什么都不做.
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
    /// Implementation approach: holds activation back until the load has all but finished, so that the Loading
    /// state covers every frame in which the old scene is on screen or the new one is not, then releases
    /// activation and ends Loading once the new scene is active. Activation is deferred rather than automatic
    /// because Unity activates as soon as it can, which would otherwise leave Loading set for frames after the
    /// new scene had already appeared.
    /// 驱动一次切换从开始到结束的单一入口.
    /// 实现思路: 把激活压住直到加载基本完成, 使 Loading 覆盖"旧场景还在屏幕上、或新场景尚未出现"的每一帧;
    /// 随后放行激活, 并在新场景激活后结束 Loading. 之所以推迟激活, 是因为 Unity 一旦能激活就会立刻激活,
    /// 否则 Loading 会滞后到新场景已经显示之后的若干帧.
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
