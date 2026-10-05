using System;
using UnityEngine;

/// <summary>
/// Owner of the global game state, and the only thing that may change it combined with its consequences.
/// Subsystem: GameState.
/// Where it lives: nowhere. It is a static class, so it has no GameObject and cannot be found in the
/// Hierarchy, and it needs no bootstrap: Current starts as Playing, which is the correct value for a session
/// that has not frozen or started loading anything.
/// Responsibility: hold Current, accept Freeze and Resume requests, and apply the two mechanisms a state
/// change implies -- game time and listener pause. Raising Changed is the whole of its outward signal, so
/// components decide for themselves what a state means to them.
/// Does NOT own: which component should react, what a reaction should be, or which systems other than time
/// and the audio listener are affected. It holds no reference to any subsystem, including AudioManager.
/// Lifetime: created once per play session and never destroyed. Restoring state on exit is the session's
/// responsibility, not this class's.
/// Idempotence: repeated Freeze or repeated Resume are no-ops. Freeze records the time scale in force when it
/// is entered and restores exactly that value, and it must not overwrite the record while already frozen --
/// otherwise a slow-motion game would either lose its scale permanently or restore to zero.
/// Paradigms: static accessor. A Singleton Component was rejected because the state must be correct before the
/// first scene object awakes, and a Component would need a hand-placed object to guarantee that.
/// 全局游戏状态的拥有者, 也是唯一可以改变它并同时施加其后果的地方.
/// Subsystem 归属: GameState.
/// 存在位置: 无. 它是静态类, 因此没有 GameObject, 也无法在 Hierarchy 中找到; 它也不需要自举:
/// Current 初始即 Playing, 而这正是一个尚未冻结、也未开始加载任何东西的会话所应处的值.
/// 职能: 持有 Current, 接受 Freeze 与 Resume 请求, 并施加状态变化所隐含的两个机制 —— 游戏时间与监听器暂停.
/// 触发 Changed 是它对外的全部信号, 因此组件自行决定某个状态对它们意味着什么.
/// 不负责: 哪个组件应当响应, 响应应当是什么, 或除时间与音频监听器之外还有哪些系统受影响.
/// 它不持有任何 Subsystem 的引用, 包括 AudioManager.
/// 生命周期: 每次运行创建一次, 从不销毁. 退出时恢复状态是会话的责任, 不是本类的.
/// 幂等: 重复 Freeze 或重复 Resume 都是空操作. Freeze 会记录进入时所生效的时间缩放, 并在解冻时精确还原该值;
/// 且它不得在已处于 Freezed 时覆盖该记录 —— 否则慢动作游戏要么永久丢失其缩放, 要么被还原成零.
/// 使用范式: 静态访问器. 已否决 Singleton Component, 因为状态必须在第一个场景对象 Awake 之前就是正确的,
/// 而 Component 要做到这一点就需要一个手动放置的物体.
/// </summary>
public static class GameStateManager
{
    /// <summary>
    /// The state the game is currently in. Readable at any time; it is correct from before the first scene
    /// loads, so no reader has to wait for readiness.
    /// 游戏当前所处的状态. 任意时刻可读; 它在第一个场景加载之前就是正确的, 因此读者无需等待就绪.
    /// </summary>
    public static GameState Current { get; private set; }

    /// <summary>
    /// Raised after Current has changed, never before, so a handler that reads Current sees the value that
    /// belongs to this notification.
    /// 在 Current 改变之后触发, 绝不在之前, 因此读取 Current 的处理函数看到的正是本次通知所属的值.
    /// </summary>
    public static event Action<GameState> Changed;

    private static float timeScaleBeforeFreeze = 1f;
    private static GameState stateBeforeLoading = GameState.Playing;

    /// <summary>
    /// Single entry point for suspending play.
    /// Implementation approach: refuses to act when already frozen, which is what keeps the recorded time
    /// scale from being overwritten with zero. It records the current time scale, stops game time, pauses the
    /// audio listener, and only then announces the state.
    /// 挂起游玩的单一入口.
    /// 实现思路: 已处于冻结时拒绝动作, 这正是防止已记录的时间缩放被零覆盖的关键.
    /// 它记录当前时间缩放, 停止游戏时间, 暂停音频监听器, 然后才宣布状态.
    /// </summary>
    public static void Freeze()
    {
        if (Current == GameState.Freezed)
        {
            GameLog.Info()
                .Subsystem("GameState")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Freeze was requested while already Freezed; ignored. "))
                .Action(LogAction.Ignore)
                .Write();

            return;
        }

        timeScaleBeforeFreeze = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        Apply(GameState.Freezed);
    }

    /// <summary>
    /// Single entry point for resuming play.
    /// Implementation approach: restores the time scale that was in force when Freeze was entered rather than
    /// assuming one, then unpauses the listener and announces the state. Like Freeze, it is idempotent.
    /// 恢复游玩的单一入口.
    /// 实现思路: 还原进入 Freeze 时所生效的时间缩放, 而不是假定某个值; 随后解除监听器暂停并宣布状态.
    /// 与 Freeze 一样, 它是幂等的.
    /// </summary>
    public static void Resume()
    {
        if (Current == GameState.Playing)
        {
            GameLog.Info()
                .Subsystem("GameState")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Resume was requested while already Playing; ignored. "))
                .Action(LogAction.Ignore)
                .Write();

            return;
        }

        Time.timeScale = timeScaleBeforeFreeze;
        AudioListener.pause = false;

        Apply(GameState.Playing);
    }

    /// <summary>
    /// Single entry point for declaring that a scene switch has begun.
    /// Implementation approach: records whatever state was in force first, so the state can be restored rather
    /// than assumed. It applies no mechanism: a load is a transition, not a suspension, so game time and audio
    /// are left alone. Entering it twice is a no-op, which keeps a second record from overwriting the first and
    /// silently losing what the game was doing before the first load started.
    /// 宣布场景切换已开始的单一入口.
    /// 实现思路: 先记录当时生效的状态, 以便之后**还原**而不是假定. 它不施加任何机制:
    /// 加载是一次过渡而非一次挂起, 因此游戏时间与音频都不动. 重复进入是空操作,
    /// 这防止第二次记录覆盖第一次, 从而静默丢失第一次加载开始前游戏在做什么.
    /// </summary>
    public static void EnterLoading()
    {
        if (Current == GameState.Loading)
            return;

        stateBeforeLoading = Current;

        Apply(GameState.Loading);
    }

    /// <summary>
    /// Single entry point for declaring that a scene switch has finished.
    /// Implementation approach: restores the state recorded by EnterLoading instead of assuming Playing, because
    /// a switch may have been requested while the game was frozen; forcing Playing would silently unfreeze a
    /// game that nobody asked to unfreeze. It is a no-op when no switch is in flight.
    /// 宣布场景切换已结束的单一入口.
    /// 实现思路: 还原 EnterLoading 记录的状态, 而不是假定 Playing, 因为切换可能在游戏已冻结时被请求;
    /// 强制回到 Playing 会静默解冻一个没人要求解冻的游戏. 当前没有切换在进行时, 它是空操作.
    /// </summary>
    public static void ExitLoading()
    {
        if (Current != GameState.Loading)
            return;

        Apply(stateBeforeLoading);
    }

    /// <summary>
    /// Single entry point for applying a state change and announcing it.
    /// Implementation approach: assigns first and raises last, in that order, because handlers read Current;
    /// announcing before assigning would make every handler observe the previous state.
    /// 应用状态变化并宣布它的单一入口.
    /// 实现思路: 先赋值, 后触发, 顺序不可颠倒, 因为处理函数会读取 Current;
    /// 若先宣布再赋值, 每个处理函数观察到的都会是上一个状态.
    /// </summary>
    private static void Apply(GameState state)
    {
        Current = state;

        GameLog.Info()
            .Subsystem("GameState")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Game state is now {state}. "))
            .Write();

        Changed?.Invoke(state);
    }
}
