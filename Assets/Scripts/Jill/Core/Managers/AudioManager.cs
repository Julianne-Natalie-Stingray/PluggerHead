using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Audio bus of the Core subsystem. It owns the emitter pool, the active registry, and the two concurrency
/// limits, and it is the only entry point for starting a sound.
/// Subsystem: Core (Audio).
/// Where it lives: on the Core GameObject, required by CoreFacade and reached through CoreFacade.Audio.
/// Responsibility: resolve an AudioId to its clip data, enforce the per-clip and global instance limits by
/// preempting the oldest eligible sound, own the emitter pool and the registry, and refuse a request with a
/// log entry when it cannot be served.
/// Does NOT own: the live-effect parameters of a request (AudioBuilder carries them), the static
/// characteristics of a clip (AudioClipData owns them), the playback itself (AudioEmitter), or the start
/// order bookkeeping (AudioRegistry).
/// Lifetime: created with the Core GameObject. The pool is built in InitializeInternal, together with the
/// registry, and merely prewarmed in Start; emitters live until this manager is destroyed.
/// Limits model: two independent limits. The global cap protects the total voice count and comes from
/// AudioManagerConfigs.MaxSoundInstance; the per-clip cap expresses design intent and comes from
/// AudioClipData.MaxInstances. Neither is the pool size: maxPoolSize bounds only how many emitters are kept
/// for reuse, so it must be at least as large as the global cap, otherwise surplus emitters are destroyed
/// instead of recycled. A looping sound is never preempted, because it is usually music or ambience and
/// should not be cut off by ordinary sound effects. When a limit is reached and every candidate is looping,
/// the request is refused rather than allowed to exceed the limit.
/// Paradigms: none. It is a plain MonoBehaviour service, reached through CoreFacade rather than a Singleton.
/// Core 子系统的音频总线. 它拥有 emitter 池, 活跃注册表与两级并发上限, 并且是启动声音的唯一入口.
/// Subsystem 归属: Core (Audio).
/// 存在位置: Core GameObject 上, 由 CoreFacade 要求, 并通过 CoreFacade.Audio 访问.
/// 职能: 把 AudioId 解析为 clip 数据; 通过抢占最旧且可抢占的声音执行每 clip 与全局两级实例上限;
/// 拥有 emitter 池与注册表; 无法服务某请求时记录日志并拒绝.
/// 不负责: 请求的实时效果参数(由 AudioBuilder 承载), clip 的静态特征(归 AudioClipData),
/// 播放本身(归 AudioEmitter), 以及开始顺序记账(归 AudioRegistry).
/// 生命周期: 随 Core GameObject 创建. 池与注册表同在 InitializeInternal 中构建, Start 只做预热;
/// emitter 存活到本管理器销毁.
/// 上限模型: 两个互相独立的上限. 全局上限保护总声部数, 来自 AudioManagerConfigs.MaxSoundInstance;
/// 每 clip 上限表达设计意图, 来自 AudioClipData.MaxInstances. 两者都不是池大小:
/// maxPoolSize 只界定保留多少 emitter 用于复用, 因此它必须不小于全局上限,
/// 否则多余的 emitter 会被销毁而不是回收. 循环音永不被抢占, 因为它通常是音乐或环境音,
/// 不应被普通音效打断; 当某条上限触顶且候选全是循环音时, 请求被**拒绝**, 而不是被允许突破上限.
/// 使用范式: 无. 它是普通 MonoBehaviour 服务, 通过 CoreFacade 而非单例访问.
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public AudioRegistry Registry => registry;

    [SerializeField] private AudioEmitter emitterPrefab;
    [SerializeField] private Transform emitterRoot;
    [SerializeField, Expandable] private AudioManagerConfigs configs;

    private GameObjectPool<AudioEmitter> emitterPool;
    private AudioRegistry registry;

    private const float MinVolume = 0f;
    private const float MaxVolume = 1f;
    private const float MinPitch = 0.1f;
    private const float MaxPitch = 3f;
    private const float MinDecibels = -80f;
    private const float MinLinearVolume = 0.0001f;
    private const float DecibelsPerDecade = 20f;

    private void Awake()
    {
        InitializeInternal();
    }

    /// <summary>
    /// Single entry point for warming the pool.
    /// Implementation approach: the pool itself is built in InitializeInternal so that a playback requested
    /// from another component's Start already works; only the prewarm is deferred to here, because prewarming
    /// instantiates objects and is not needed for correctness.
    /// 预热对象池的单一入口.
    /// 实现思路: 池本身在 InitializeInternal 中构建, 以便其他组件的 Start 里发起的播放已经可用;
    /// 只有预热放到这里, 因为预热会实例化对象, 且并非正确性所需.
    /// </summary>
    private void Start()
    {
        emitterPool.Prewarm(configs.PrewarmAmount);
    }

    /// <summary>
    /// Single entry point for subscribing to the global state.
    /// Implementation approach: subscribes here rather than at initialization, because a whole-lifetime
    /// subscription belongs in OnEnable/OnDisable. Audio is the first subsystem to react to a game state, and
    /// it does so without GameState knowing anything about audio.
    /// 订阅全局状态的单一入口.
    /// 实现思路: 在此订阅而不是在初始化时, 因为贯穿整个生命周期的订阅属于 OnEnable/OnDisable.
    /// 音频是第一个响应游戏状态的 Subsystem, 且 GameState 对此一无所知.
    /// </summary>
    private void OnEnable()
    {
        GameStateManager.Changed += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameStateManager.Changed -= HandleGameStateChanged;
    }

    private void OnDestroy()
    {
        emitterPool?.Clear();
    }

    /// <summary>
    /// Single entry point for obtaining a request token.
    /// Implementation approach: hands out a fresh value whose parameters are independent of every other
    /// request, so no caller can inherit state from an earlier playback.
    /// 获取请求凭证的单一入口.
    /// 实现思路: 发放一个全新值, 其参数独立于其他任何请求, 因此没有调用方能继承更早播放的状态.
    /// </summary>
    public AudioBuilder CreateBuilder()
        => new AudioBuilder(this);

    /// <summary>
    /// Single entry point for starting a sound. Called by AudioBuilder.Play, not by gameplay code.
    /// Implementation approach: refuses the request while the pool does not exist, which covers the case where
    /// initialization failed, for example over an unassigned configs, leaving no pool at all. It then resolves the
    /// clip, applies the frozen-entry gate, applies both instance limits by preempting the oldest eligible sound,
    /// reserves an emitter, registers it, applies the freeze decision and the live-effect values, and starts it.
    /// Returns null and logs when the request is refused, rather than silently dropping it.
    /// Two freeze questions meet here, and they are not independent in the frozen case.
    /// Whether a sound is kept once a freeze begins is SurviveFreeze: the clip supplies it as a static initial and
    /// the builder may override it per call, and it has nothing to do with whether the clip loops.
    /// Whether a request arriving during a freeze is served at all is the gate, and the gate consults SurviveFreeze:
    /// entry is granted only when the request both permits it and would actually be kept. A playback the freeze will
    /// silence must not take a pooled emitter, because while frozen its completion check cannot advance and the
    /// emitter would stay checked out, invisible in the hierarchy, producing nothing. So a freeze-time request that
    /// is audible is one that both allowed entry and survives; `WithAllowWhileFrozen(true)` alone is therefore not
    /// enough to hear anything while frozen.
    /// 启动声音的单一入口. 由 AudioBuilder.Play 调用, 不由玩法代码调用.
    /// 实现思路: 池尚不存在时直接拒绝 —— 覆盖初始化失败的情况, 例如 configs 未赋值, 于是根本没有池.
    /// 之后解析 clip, 应用"冻结期入口"这道门, 通过抢占最旧且可抢占的声音执行两级实例上限,
    /// 预定 emitter, 注册它, 应用冻结决定与实时效果值, 然后启动. 请求被拒绝时返回 null 并记录日志, 而不是静默丢弃.
    /// 此处涉及两个冻结问题, 而在冻结情形下它们**并非互相独立**.
    /// "一旦开始冻结, 某个音是否被保留"是 SurviveFreeze: clip 提供静态初值, Builder 可以按次覆盖,
    /// 且它与 clip 是否循环毫无关系.
    /// "冻结期间到达的请求是否被服务"是那道门, 而**门会参考 SurviveFreeze**:
    /// 只有既允许进入、又确实会被保留的请求才被放行. 一个无论如何都会被冻结静音的播放不应占用池化 emitter ——
    /// 因为冻结期间它的结束判定无法推进, emitter 会一直处于被占用状态, 在 Hierarchy 中不可见, 也不产生任何声音.
    /// 因此冻结期真能听到的请求 = 允许进入 且 熬过冻结; 单用 `WithAllowWhileFrozen(true)` 并不足以在冻结期出声.
    /// </summary>
    public ISoundHandle Play(
        AudioId requested,
        float volume,
        float pitch,
        Vector3? position,
        Transform followTarget,
        bool? surviveFreeze,
        bool? allowWhileFrozen,
        float? fadeIn = null,
        float? fadeOut = null)
    {
        if (!enabled || !configs || emitterPool == null)
            return null;

        AudioClipData data = ResolveClip(requested);

        if (!data)
            return null;

        bool willSurviveFreeze = surviveFreeze ?? data.DefaultSurviveFreeze;

        if (!IsFrozenEntryAllowed(requested, allowWhileFrozen, willSurviveFreeze))
            return null;

        if (!TryApplyInstanceLimits(requested, data.MaxInstances))
            return null;

        AudioEmitter emitter = ReserveEmitter();
        if (!emitter)
            return null;

        emitter.Volume = Mathf.Clamp(volume, MinVolume, MaxVolume);
        emitter.Pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);

        registry.Register(emitter);
        emitter.gameObject.SetActive(true);
        emitter.onAudioFinished -= ReleaseEmitter;
        emitter.onAudioFinished -= OnEmitterRelease;
        emitter.onAudioFinished += ReleaseEmitter;
        emitter.onAudioFinished += OnEmitterRelease;
        emitter.Configure(data);

        // A request may override the clip's ramp; absent means the clip's static values, which Configure has
        // already written. 请求可以覆盖 clip 的渐变; 不设置即使用 Configure 已写入的 clip 静态值.
        if (fadeIn.HasValue)
            emitter.FadeIn = fadeIn.Value;

        if (fadeOut.HasValue)
            emitter.FadeOut = fadeOut.Value;

        emitter.SurviveFreeze = willSurviveFreeze;

        if (position.HasValue)
            emitter.SetPosition(position.Value);

        if (followTarget)
            emitter.SetFollowTarget(followTarget);

        SoundHandle handle = new SoundHandle(emitter, requested);

        emitter.Play();

        return handle;
    }

    /// <summary>
    /// Single entry point for pushing the persisted player volumes onto the mixer's buses.
    /// Implementation approach: reads the Setting subsystem's live data -- which is loaded before any scene
    /// object awakes, so calling this from Awake is safe -- and writes one decibel value per exposed parameter.
    /// The dependency direction is one way: audio reads Setting, and Setting knows nothing about audio. This is
    /// called once during initialization, and is public so that whoever changes a volume can make it audible
    /// immediately; nothing else calls it, because a change notification does not exist yet.
    /// 把玩家持久化的音量推送到 mixer 各总线上的单一入口.
    /// 实现思路: 读取 Setting 子系统的当前数据 —— 它在任何场景对象 Awake 之前就已加载, 因此从 Awake 调用是安全的 ——
    /// 并为每个暴露参数写入一个分贝值. 依赖方向是单向的: 音频读取 Setting, 而 Setting 对音频一无所知.
    /// 它在初始化时调用一次, 并且是公开的, 使"改了音量想立刻听见"的调用方可以主动调用;
    /// 除此之外没有别处调用, 因为目前尚不存在变更通知.
    /// </summary>
    public void ApplyAudioSettings()
    {
        if (!configs || !configs.Mixer)
        {
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned("mixer"))
                .Action(LogAction.Ignore)
                .Write();

            return;
        }

        AudioSettings settings = SettingBootstrap.Settings.Audio;

        ApplyBusVolume(configs.MasterVolumeParameter, settings.MasterVolume);
        ApplyBusVolume(configs.OstVolumeParameter, settings.OstVolume);
        ApplyBusVolume(configs.SfxVolumeParameter, settings.SfxVolume);

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(
                $"Bus volumes applied: master {settings.MasterVolume}, ost {settings.OstVolume}, " +
                $"sfx {settings.SfxVolume}. "))
            .Write();
    }

    /// <summary>
    /// Single entry point for turning an AudioId into its clip data.
    /// Implementation approach: resolves through the configs and reports a missing entry as an error, so the
    /// caller can treat null as "refused" without deciding why.
    /// 把 AudioId 解析为 clip 数据的单一入口.
    /// 实现思路: 经 configs 解析, 并把缺失项记为错误, 使调用方可以把 null 直接当作"已拒绝", 而无需判断原因.
    /// </summary>
    private AudioClipData ResolveClip(AudioId requested)
    {
        if (configs.TryGetClip(requested, out AudioClipData data))
            return data;

        GameLog.Error(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.CannotFind(nameof(requested), nameof(configs)))
            .Action(LogAction.Ignore)
            .Write();

        return null;
    }

    /// <summary>
    /// Single entry point for deciding whether a request may enter while the game is frozen.
    /// Implementation approach: a frozen request is served only when it both permits entry and would actually be
    /// kept. Allowing entry alone is not enough: a playback that the freeze will silence anyway must not take a
    /// pooled emitter, because during the freeze its completion check cannot advance and the emitter would stay
    /// checked out, invisible in the hierarchy, producing nothing.
    /// 判断某请求是否可在游戏冻结期间进入的单一入口.
    /// 实现思路: 冻结期间的请求, 只有**同时**允许进入**且**确实会被保留时才被服务.
    /// 仅允许进入是不够的: 一个无论如何都会被冻结静音的播放不应占用池化 emitter ——
    /// 因为冻结期间它的结束判定无法推进, emitter 会一直处于被占用状态, 在 Hierarchy 中不可见, 也不产生任何声音.
    /// </summary>
    private bool IsFrozenEntryAllowed(AudioId requested, bool? allowWhileFrozen, bool willSurviveFreeze)
    {
        if (GameStateManager.Current != GameState.Freezed)
            return true;

        if ((allowWhileFrozen ?? false) && willSurviveFreeze)
            return true;

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(willSurviveFreeze
                ? $"Request for {requested} refused: the game is frozen and the request did not allow entry. "
                : $"Request for {requested} refused: the game is frozen and the playback would not be kept. "))
            .Action(LogAction.Ignore)
            .Write();

        return false;
    }

    /// <summary>
    /// Single entry point for satisfying the instance limits before an emitter is reserved.
    /// Implementation approach: applies the per-clip limit first and the global limit second, and lets the
    /// ordinary completion path do the rest of the work, so pool release, registry removal and handle
    /// invalidation all happen in one place. A looping sound is never chosen, and a limit that cannot be
    /// satisfied by preemption refuses the request instead of being exceeded: the caps are hard, and the old
    /// behaviour of selecting a looping victim and then giving up silently let the per-clip cap be exceeded
    /// without bound.
    /// 在预定 emitter 之前满足实例上限的单一入口.
    /// 实现思路: 先应用每 clip 上限, 再应用全局上限, 其余工作交给常规结束路径,
    /// 因此池归还, 注册表注销与句柄失效都只发生在一处. 循环音永不被选中;
    /// 若某条上限无法靠抢占满足, 则**拒绝**该请求而不是突破上限 —— 上限是硬的,
    /// 而旧实现"选中循环受害者后就此作罢"会让每 clip 上限被无限突破.
    /// </summary>
    private bool TryApplyInstanceLimits(AudioId requested, int maxInstances)
    {
        if (registry.CountOf(requested) >= maxInstances)
        {
            if (registry.TryGetOldest(requested, false, out AudioEmitter sameClipVictim))
                Preempt(sameClipVictim, requested);
            else
                return RefuseOverLimit(
                    $"Per-clip limit of {maxInstances} is reached for {requested} and every instance is " +
                    "looping, so none of them may be preempted. ");
        }

        if (registry.Count >= configs.MaxSoundInstance)
        {
            if (registry.TryGetOldest(false, out AudioEmitter globalVictim))
                Preempt(globalVictim, requested);
            else
                return RefuseOverLimit(
                    $"Global limit of {configs.MaxSoundInstance} is reached and every active sound is " +
                    "looping, so none of them may be preempted. ");
        }

        return true;
    }

    /// <summary>
    /// Single entry point for refusing a request that a hard limit cannot accommodate.
    /// Implementation approach: one warning naming the limit and the reason, then false, so the caller can
    /// return null without having to decide why.
    /// 拒绝一个无法被硬上限容纳的请求的单一入口.
    /// 实现思路: 记一条指出上限与原因的 warning, 然后返回 false, 使调用方可以直接返回 null 而无需判断原因.
    /// </summary>
    private bool RefuseOverLimit(string issue)
    {
        GameLog.Warning(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(issue))
            .Action(LogAction.Ignore)
            .Write();

        return false;
    }

    /// <summary>
    /// Single entry point for stopping one sound so its slot can serve a new request.
    /// Implementation approach: the victim is chosen by the caller from the non-looping candidates only, so a
    /// loop is passed over rather than cut off, and the guard below is the belt to that suspenders. The stop is
    /// deliberately a hard cut rather than a ramp: this must free its pool slot in the same frame, and a fade
    /// cannot.
    /// 停止一个声音以便其槽位服务新请求的单一入口.
    /// 实现思路: 受害者由调用方**只从非循环候选**中挑出, 因此循环音是被跳过而不是被切断; 下面的守卫是第二道保险.
    /// 这里刻意是硬切而非渐变: 它必须在同一帧释放池槽位, 而淡出做不到这一点.
    /// TODO: 抢占时的淡出. 未实现原因: 它需要一种"正在淡出的 emitter 不再占用池槽位"的记账模型,
    /// 而那属于 `Core/GameObjectPool`(另一个交付单元). 用户已于 2026-10-05 裁决**保持硬切**(选项甲),
    /// 因此本项**已决定不做**; 若将来要改, 必须先动 GameObjectPool 的记账.
    /// </summary>
    private void Preempt(AudioEmitter victim, AudioId requested)
    {
        if (!victim || victim.IsLooping)
            return;

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Preempted {victim.AudioId} for {requested}. "))
            .Write();

        victim.Stop();
    }

    /// <summary>
    /// Single entry point for reserving an emitter without letting the pool grow past maxPoolSize.
    /// Implementation approach: Unity's ObjectPool creates a new instance instead of refusing when it is
    /// empty, so the cap is enforced through GameObjectPool.CanReuse: when nothing is idle and maxPoolSize
    /// emitters already exist, the request is refused with a warning rather than silently expanding the pool.
    /// 预定 emitter 且不让池超过 maxPoolSize 的单一入口.
    /// 实现思路: Unity 的 ObjectPool 在池空时会新建实例而不是拒绝, 因此上限通过
    /// GameObjectPool.CanReuse 强制: 当没有空闲实例且已达到 maxPoolSize 时,
    /// 拒绝该请求并记录警告, 而不是静默扩容.
    /// </summary>
    private AudioEmitter ReserveEmitter()
    {
        if (!emitterPool.CanReuse(configs.MaxPoolSize))
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify(
                    $"Pool exhausted at {configs.MaxPoolSize} emitters; request refused. "))
                .Action(LogAction.Ignore)
                .Write();

            return null;
        }

        return emitterPool.Get();
    }

    private void OnEmitterGet(AudioEmitter emitter)
    {
        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify("AudioEmitter is pooled. "))
            .Write();
    }

    /// <summary>
    /// Single entry point for the cleanup that follows a finished playback.
    /// Implementation approach: this method runs as the pool's own release callback, so it must never call
    /// GameObjectPool.Release itself. Doing so re-enters this method through actionOnRelease and recurses until
    /// the stack overflows, because the handler passed to the pool constructor is this very method. Returning
    /// the emitter to the pool is therefore driven from outside: ReleaseEmitter is subscribed to the emitter's
    /// completion callback, so the pool's Release is invoked exactly once per playback. What remains here is
    /// cleanup only -- unsubscribe, drop the registry entry, reset the emitter, reparent it, and log.
    /// 单次播放结束后的清理的单一入口.
    /// 实现思路: 本方法作为池自身的归还回调运行, 因此**绝不能**自己调用 GameObjectPool.Release.
    /// 传给池构造函数的处理器就是这个方法本身, 自调用会经 actionOnRelease 重新进入本方法, 一路递归到栈溢出.
    /// 归还池的动作因此由外部驱动: 把 ReleaseEmitter 订阅到 emitter 的完成回调上, 使池的 Release 每次播放恰好调用一次.
    /// 留在这里的只有清理 —— 退订, 注销注册表项, 重置 emitter, 重挂父级, 记录日志.
    /// </summary>
    private void OnEmitterRelease(AudioEmitter emitter)
    {
        emitter.onAudioFinished -= ReleaseEmitter;
        emitter.onAudioFinished -= OnEmitterRelease;

        registry.Unregister(emitter);

        emitter.ResetEmitter();
        emitter.transform.SetParent(emitterRoot, false);
        emitter.transform.localPosition = Vector3.zero;

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify("AudioEmitter is released. "))
            .Write();
    }

    /// <summary>
    /// Single entry point for the emitter's completion callback.
    /// Implementation approach: a thin forwarder to GameObjectPool.Release, deliberately separate from
    /// OnEmitterRelease so that the pool's release input and its release output can never call each other.
    /// 处理 emitter 完成回调的单一入口.
    /// 实现思路: 一个转发到 GameObjectPool.Release 的薄方法, 刻意与 OnEmitterRelease 分开,
    /// 使池的"归还输入"与"归还输出"永远不会互相调用.
    /// </summary>
    private void ReleaseEmitter(AudioEmitter emitter)
        => emitterPool.Release(emitter);

    /// <summary>
    /// Single entry point for reacting to a game state change.
    /// Implementation approach: pauses or resumes the audio listener, which is the whole of what a state change
    /// does to already-playing sounds; it never stops an individual emitter. Which of those sounds are cut is
    /// decided earlier and per playback, by SurviveFreeze, not here: this method treats every emitter alike.
    /// A surviving sound keeps playing and still reports its end through the unscaled-time timer, while a cut
    /// one is silenced and released the same way. Whether a request may start during a freeze is a separate
    /// question answered by the gate in Play, not by this method.
    /// 响应游戏状态变化的单一入口.
    /// 实现思路: 暂停或恢复音频监听器 —— 这就是状态变化对"已在播放的音"所做的全部; 它从不停止任何单个 emitter.
    /// 其中哪些音被切断, 是更早且逐次由 SurviveFreeze 决定的, 不在此处: 本方法对所有 emitter 一视同仁.
    /// 被保留的音继续播放, 并仍通过 unscaled time 的 Timer 报告结束; 被切断的音则以同样方式静音并归还.
    /// 冻结期间某个请求是否可开始, 是另一个问题, 由 Play 入口的那道门回答, 不由本方法回答.
    /// </summary>
    private void HandleGameStateChanged(GameState state)
    {
        bool shouldPause = state == GameState.Freezed;

        if (AudioListener.pause == shouldPause)
            return;

        AudioListener.pause = shouldPause;

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify(shouldPause
                ? "Audio paused with the game. "
                : "Audio resumed with the game. "))
            .Write();
    }

    /// <summary>
    /// Single entry point for writing one linear volume onto one exposed mixer parameter.
    /// Implementation approach: a mixer volume parameter is expressed in decibels, so a linear 0..1 setting is
    /// converted through 20*log10. Zero has no logarithm, so anything at or below the floor becomes the
    /// mixer's own silence threshold; an empty parameter name is reported rather than handed to Unity, because
    /// SetFloat on a name that is not exposed fails silently.
    /// 把一条线性音量写入一个 mixer 暴露参数的单一入口.
    /// 实现思路: mixer 的音量参数以分贝表示, 因此线性 0..1 的设置经 20*log10 换算.
    /// 零没有对数, 因此低到门槛以下的值一律取 mixer 自身的静音阈值;
    /// 参数名为空时记录一条警告而不是交给 Unity, 因为对未暴露的名字调用 SetFloat 会静默失败.
    /// </summary>
    private void ApplyBusVolume(string parameter, float linearVolume)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid("mixer parameter name"))
                .Action(LogAction.Ignore)
                .Write();

            return;
        }

        float decibels = linearVolume <= MinLinearVolume
            ? MinDecibels
            : Mathf.Log10(linearVolume) * DecibelsPerDecade;

        configs.Mixer.SetFloat(parameter, decibels);
    }

    private void InitializeInternal()
    {
        registry = new AudioRegistry();

        if (!emitterPrefab)
        {
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(emitterPrefab)))
                .Action(LogAction.DisableGameObject)
                .Write();
            gameObject.SetActive(false);
        }

        if (!emitterRoot)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(emitterRoot)))
                .Action(LogAction.UseFallbackValue("self transform"))
                .Write();
            emitterRoot = transform;
        }

        if (!configs)
        {
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(configs)))
                .Action(LogAction.DisableComponent)
                .Write();
            enabled = false;
            return;
        }

        emitterPool = BuildEmitterPool();
        ApplyAudioSettings();
    }

    /// <summary>
    /// Single entry point for assembling the emitter pool.
    /// Implementation approach: called from InitializeInternal, so the pool exists as soon as this component
    /// has awoken and a playback requested from another component's Start can be served.
    /// 装配 emitter 池的单一入口.
    /// 实现思路: 由 InitializeInternal 调用, 因此本组件 Awake 之后池即存在,
    /// 其他组件在 Start 里发起的播放便可以被服务.
    /// </summary>
    private GameObjectPool<AudioEmitter> BuildEmitterPool()
        => new(
            emitterPrefab,
            emitterRoot,
            OnEmitterGet,
            OnEmitterRelease,
            configs.CollectionCheck,
            configs.DefaultCapacity,
            configs.MaxPoolSize);


#region Debug

    [SerializeField] private AudioId debugAudioRequest;
    [SerializeField] private int requestTime = 1;
    [SerializeField] private bool DebugSurviveFreeze;
    [SerializeField] private bool DebugAllowWhileFrozen;

    /// <summary>
    /// Debug entry point for re-applying the persisted volumes without leaving play mode.
    /// Implementation approach: guarded by isPlaying, because the settings store is created on play-mode entry,
    /// so reading it while editing would find nothing.
    /// 在播放模式内重新应用持久化音量的调试入口.
    /// 实现思路: 以 isPlaying 守卫, 因为设置存储是在进入播放模式时建立的, 在编辑模式下读取只会读到空.
    /// </summary>
    [Button("Apply Audio Settings")]
    private void DebugApplyAudioSettings()
    {
        if (!Application.isPlaying)
        {
            GameLog.Info(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Please run in play mode."))
                .Action(LogAction.Return)
                .Write();
            return;
        }

        ApplyAudioSettings();
    }

    [Button("Test Audio Request")]
    private void TestAudioRequest()
    {
        if (!Application.isPlaying)
        {
            GameLog.Info(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Please run in play mode."))
                .Action(LogAction.Return)
                .Write();
            return;
        }

        for (int i = 0; i < requestTime; i++)
            CreateBuilder().WithSurviveFreeze(DebugSurviveFreeze)
                .WithAllowWhileFrozen(DebugAllowWhileFrozen)
                .Play(debugAudioRequest);
        
        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Manual requested Audio: {debugAudioRequest}. \n" +
                                    $"Request time: {requestTime}\n" +
                                    $"Survive freeze: {DebugSurviveFreeze}\n" +
                                    $"Allow while frozen: {DebugAllowWhileFrozen}. "))
            .Write();
    }

#endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (requestTime <= 0)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(requestTime)))
                .Action(LogAction.UseFallbackValue("1"))
                .Write();
            requestTime = 1;
        }
    }
#endif
}
