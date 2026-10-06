using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene switch service used by production gameplay. Editor tests also load scenes directly for isolation.
/// Subsystem: Core (SceneSwitch).
/// Where it lives: on the Core GameObject, reached through CoreFacade.SceneSwitch.
/// Responsibility: validate a switch request against the whitelist and Unity's Build Settings, enforce that only
/// one switch is in flight across service instances, and bracket asynchronous Single loading with the Loading label.
/// Does NOT own: which scene the game should go to next -- that is the caller's decision, passed as an argument,
/// because a "next scene" field on a bus would put a gameplay decision in a configuration asset. It also does
/// not own progress reporting (AsyncOperation already exposes it), transitions or loading screens, what happens
/// to audio already playing, or whether input is blocked; those are gameplay and presentation decisions.
/// Lifetime: created with the Core GameObject and kept alive by CoreFacade's DontDestroyOnLoad, so one instance
/// serves subsequent scenes; scene loading can create duplicate Core instances, removed by CoreFacade.
/// Loading state: entered after loading begins and left by its completion callback, independent of this component lifecycle.
/// This service does not write timeScale or listener pause directly, but Changed subscribers may do so.
/// Paradigms: none. It is a MonoBehaviour service, reached through CoreFacade rather than a Singleton.
/// 生产玩法使用的 Core 场景切换服务; Editor 测试也会直接加载场景以隔离验证.
/// Subsystem 归属: Core (SceneSwitch).
/// 存在位置: Core GameObject 上, 通过 CoreFacade.SceneSwitch 访问.
/// 职能: 依据白名单与 Unity 的 Build Settings 校验请求; 所有服务实例同一时刻只允许一次切换;
/// 异步 Single 加载前后进入和退出 Loading 标签.
/// 不负责: 游戏接下来该去哪个场景 —— 那是调用方的决定, 以参数传入, 因为把"下一个场景"字段放在总线上
/// 等于把玩法决定塞进配置资产. 它也不负责进度上报(AsyncOperation 已经暴露了),
/// 不负责转场或加载界面, 不负责已在播放的音频如何处置, 也不负责是否屏蔽输入; 那些是玩法与表现的决定.
/// 生命周期: 随 Core GameObject 创建, 并由 CoreFacade 的 DontDestroyOnLoad 保活,
/// 因此首个实例服务后续场景; 场景加载可以创建重复 Core, 由 CoreFacade 移除.
/// Loading 状态: 加载开始后进入, 操作完成回调中退出, 不依赖组件保持激活或存活, 不保证与首个可见帧精确同步.
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
    private string switchingDescription;
    private static SceneSwitchManager switchingOwner;

    #region Unity lifecycle

    private void Awake()
    {
        InitializeInternal();
    }

    #endregion

    #region APIs

    /// <summary>
    /// Single entry point for requesting a switch to another scene.
    /// Implementation approach: rejects inactive, disabled, unconfigured or busy services and existing Loading,
    /// validates the mapping and Build Settings, then starts an automatically activated Single load.
    /// Completion tracking survives component destruction. State notification failures are logged; they do not
    /// cancel Unity's load. A synchronous load failure releases the reservation and propagates to the caller.
    /// 请求切换到另一个场景的单一入口.
    /// 实现思路: 拒绝失活、禁用、缺配置、忙碌及已有 Loading 的请求, 再校验映射与构建注册.
    /// 加载默认允许激活; 完成跟踪不依赖组件存活. 状态通知异常记录后继续加载, 不伪装取消;
    /// 同步启动异常释放预留标记后交给调用方处理.
    /// </summary>
    public AsyncOperation RequestSwitch(SceneId requested)
    {
        if (!isActiveAndEnabled || !configs || isSwitching ||
            !ReferenceEquals(switchingOwner, null) || GameStateManager.Current == GameState.Loading)
        {
            return null;
        }

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

        switchingOwner = this;
        isSwitching = true;
        switchingDescription = $"{requested} ({sceneName})";
        AsyncOperation startedOperation;
        try
        {
            startedOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch
        {
            ClearSwitch();
            throw;
        }

        if (startedOperation == null)
        {
            ClearSwitch();
            return null;
        }

        operation = startedOperation;
        try
        {
            GameLog.Info()
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"Switching to {switchingDescription}. "))
                .Write();
            GameStateManager.EnterLoading();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            // Subscribe after entering Loading: even an already-complete operation must exit the state
            // after entry. Unity invokes newly registered handlers for completed operations synchronously.
            // 在进入 Loading 后注册, 避免已完成操作的同步回调先退出、随后再次进入 Loading.
            startedOperation.completed += CompleteSwitch;
        }

        return startedOperation;
    }

    #endregion

    /// <summary>
    /// Completes the actual Unity operation even if its original component was disabled or destroyed.
    /// Keep the reservation during state callbacks to reject reentrant or interleaved requests.
    /// 即使原组件已失活或销毁, 也在 Unity 操作实际完成时清理; 通知期间仍持有预留, 拒绝重入请求.
    /// </summary>
    private void CompleteSwitch(AsyncOperation completedOperation)
    {
        completedOperation.completed -= CompleteSwitch;
        if (!ReferenceEquals(operation, completedOperation))
        {
            return;
        }

        string completedDescription = switchingDescription;
        try
        {
            GameStateManager.ExitLoading();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            ClearSwitch();
        }
        GameLog.Info()
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Switched to {completedDescription}. "))
            .Write();
    }

    private void ClearSwitch()
    {
        operation = null;
        isSwitching = false;
        switchingDescription = null;
        // Unity's destroyed-object equality must not release another owner's reservation.
        if (ReferenceEquals(switchingOwner, this))
        {
            switchingOwner = null;
        }
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
        {
            return;
        }

        GameLog.Error(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.NotAssigned(nameof(configs)))
            .Action(LogAction.DisableComponent)
            .Write();

        enabled = false;
    }

}
