using UnityEngine;
using System;

/// <summary>
/// One pooled audio voice. It plays one clip and reports when that playback is over.
/// Subsystem: Core (Audio).
/// Where it lives: on the pooled emitter prefab, instantiated under AudioRoot by AudioManager's pool.
/// Responsibility: apply the clip's static characteristics to its AudioSource, expose the live volume, pitch
/// and ramp durations, keep its own world position in sync with the Transform the builder supplied, and report
/// the end of a playback exactly once so the pool can take the emitter back.
/// Does NOT own: which clip plays next, the decision to play at all, the instance limits, preemption, which
/// Transform to follow (AudioBuilder decides that), or the registry.
/// Lifetime: normally created by the pool; external or parent destruction can also destroy it.
/// OnDestroy cancels its timer but does not notify completion. One instance serves many
/// playbacks, so Configure establishes defaults and the request overrides them before Play. The emitter is inert
/// on the frame it is returned.
/// Completion model: Play arms a Timer that completes as soon as the source stops playing. That timer is the
/// natural-end detector; a handle requests a graceful stop and preemption stops immediately. All three paths
/// therefore funnel through one completion signal.
/// Ramp model: a playback may fade in from silence, and it may fade out either at the tail of a non-looping
/// clip or when it is stopped gracefully. Ramps run on unscaled time and multiply the playback volume rather
/// than replacing it. They are configured through AudioClipData or a builder override: 0 reproduces the hard
/// start and hard cut exactly. Preemption never ramps, because it must free its pool slot in the same frame.
/// Paradigms: none. It is a pooled component driven by AudioManager.
/// 单个池化的音频声部. 它播放一个 clip 并报告该次播放结束.
/// Subsystem 归属: Core (Audio).
/// 存在位置: 池化的 emitter prefab 上, 由 AudioManager 的池实例化到 AudioRoot 之下.
/// 职能: 把 clip 的静态特征写入自身 AudioSource, 暴露实时音量, 音高与渐变时长,
/// 使自身世界坐标与 Builder 传入的 Transform 同步, 并且只报告一次播放结束, 以便池收回该 emitter.
/// 不负责: 下一个播放什么 clip, 是否播放, 实例上限, 抢占, 跟随哪个 Transform(由 AudioBuilder 决定),
/// 以及注册表.
/// 生命周期: 正常由池创建, 外部或父级销毁也会销毁它; OnDestroy 只取消 Timer, 不发送完成通知.
/// 一个实例服务多次播放,
/// 因此每次由 Configure 确立默认值, 再由请求覆盖; 归还的那一帧它是惰性的.
/// 结束检测: Play 装载一个 Timer, 在 source 停止时报告自然结束;
/// 句柄请求优雅停止, 抢占则立即停止. 三条路径汇入同一个结束信号.
/// 渐变模型: 一次播放可以从静音淡入; 也可以在非循环 clip 的尾部淡出, 或被优雅停止时淡出.
/// 渐变走 unscaled time, 且是**乘在**播放音量上的, 而不是替换它. 渐变由 AudioClipData 或 Builder 覆盖配置:
/// 时长为 0 即精确复现原先的硬起与硬切. 抢占永不走渐变, 因为它必须在同一帧释放池槽位.
/// 使用范式: 无. 它是由 AudioManager 驱动的池化组件.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class AudioEmitter : MonoBehaviour
{
    public event Action<AudioEmitter> onAudioFinished;

#region APIs
    public AudioId AudioId => data ? data.AudioId : AudioId.DefaultSfx;
    public bool IsPlaying => source && source.isPlaying;
    public bool IsFinished => isFinished;
    public bool IsLooping => data && data.Loop;
    public bool IsFollowingTarget => followTarget;

    public float Volume
    {
        get => volume;
        set
        {
            volume = Mathf.Clamp(value, MinVolume, MaxVolume);
            isVolumeDirty = true;
        }
    }

    public float Pitch
    {
        get => pitch;
        set
        {
            pitch = Mathf.Clamp(value, MinPitch, MaxPitch);

            InitializeInternal();
            source.pitch = pitch;
        }
    }

    /// <summary>
    /// Whether this playback is kept while the game is frozen, by mapping onto the source's listener-pause
    /// opt-out. A static initial may come from the clip, but the value is owned by whoever starts the playback,
    /// because being kept is a per-request decision rather than a property of the clip.
    /// 本次播放是否在游戏冻结时被保留: 通过映射到该音源的"无视监听器暂停"开关实现.
    /// 静态初值可以来自 clip, 但该值归发起播放的一方所有, 因为"是否被保留"是一次请求的决定, 而不是 clip 的属性.
    /// </summary>
    public bool SurviveFreeze
    {
        get => surviveFreeze;
        set
        {
            surviveFreeze = value;

            InitializeInternal();
            source.ignoreListenerPause = surviveFreeze;
        }
    }

    /// <summary>
    /// Seconds this playback spends ramping up from silence. 0 means it starts at full volume, which is the
    /// original behaviour.
    /// Implementation approach: written by Configure from the clip, and optionally overridden by the request
    /// that started the playback, exactly like SurviveFreeze.
    /// 本次播放从静音渐变到满音量所用的秒数. 0 表示直接以满音量开始, 即原先的行为.
    /// 实现思路: 由 Configure 从 clip 写入, 并可由发起该次播放的请求覆盖, 与 SurviveFreeze 完全一致.
    /// </summary>
    public float FadeIn
    {
        get => fadeInDuration;
        set => fadeInDuration = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Seconds this playback spends ramping down: used by a graceful stop, and as the tail of a natural
    /// playback of a non-looping clip. 0 means a hard cut, which is the original behaviour.
    /// 本次播放渐变到静音所用的秒数: 用于优雅停止, 以及非循环 clip 自然播放的尾部. 0 表示硬切, 即原先的行为.
    /// </summary>
    public float FadeOut
    {
        get => fadeOutDuration;
        set => fadeOutDuration = Mathf.Max(0f, value);
    }
#endregion

    private AudioSource source;
    private AudioClipData data;
    private Timer completionTimer;

    private Transform followTarget;
    private Vector3 lastTargetPosition;

    private float volume = 1f;
    private float pitch = 1f;
    private bool isPlaying;
    private bool isFinished;
    private bool surviveFreeze;
    private bool isVolumeDirty;
    private float fadeInDuration;
    private float fadeOutDuration;
    private float fadeElapsed;
    private float fadeFrom = 1f;
    private float fadeTo = 1f;
    private float fadeScale = 1f;
    private FadePhase fadePhase;

    private const float MinVolume = 0f;
    private const float MaxVolume = 1f;
    private const float MinPitch = 0.1f;
    private const float MaxPitch = 3f;

    private bool willFollowTarget;

    private void Awake()
    {
        InitializeInternal();
    }

    private void OnDestroy()
    {
        completionTimer?.Stop();
        completionTimer = null;
    }

    /// <summary>
    /// Single entry point for the per-frame work of a live playback.
    /// Implementation approach: advances any ramp first and re-checks the playback afterwards, because a
    /// finished fade-out stops the playback inside that ramp; then applies a pending volume immediately for
    /// every playback, because a handle may change volume at any time; then syncs position only while a
    /// follow target is actually being requested.
    /// Those last two are independent: gating the volume write on follow would silently drop it for every
    /// sound that does not follow, and syncing position for a sound that follows nothing would read a null
    /// target.
    /// The gate is this emitter's own running flag rather than AudioSource.isPlaying, so a ramp keeps
    /// advancing while the game is frozen and the source is paused: an emitter stopped during a freeze must
    /// still be able to finish its fade and return to the pool.
    /// 进行中的播放每帧工作的单一入口.
    /// 实现思路: 先推进渐变并在其后重新确认播放状态 —— 因为淡出完成正是在该渐变内部停止播放的;
    /// 再对每种播放立即写入待应用的音量, 因为句柄随时可能改音量; 之后只在确实请求了跟随目标时同步位置.
    /// 后两者互相独立: 用跟随去门控音量写入会让每个不跟随的声音静默丢失音量变更;
    /// 而给一个不跟随任何东西的声音做位置同步则会去读一个空目标.
    /// 这里的门控用的是本 emitter 自己的运行标志, 而不是 AudioSource.isPlaying:
    /// 因此游戏冻结, 音源被暂停时渐变仍在推进 —— 冻结期间被停止的 emitter 必须仍能跑完淡出并归还池.
    /// </summary>
    private void LateUpdate()
    {
        if (!isPlaying)
        {
            return;
        }

        AdvanceFade();

        if (!isPlaying)
        {
            return;
        }

        if (isVolumeDirty)
        {
            ApplyVolume();
            isVolumeDirty = false;
        }

        if (!willFollowTarget)
        {
            return;
        }

        SyncPosition();
    }

    /// <summary>
    /// Single entry point for arming this emitter with one clip's static characteristics.
    /// Implementation approach: copies only what the clip owns. Volume and pitch are
    /// initialized from clip defaults here, then overridden by the request before Play.
    /// 用某个 clip 的静态特征武装本 emitter 的单一入口.
    /// 实现思路: 复制 clip 的静态特征与音量、音高默认值, 再由单次请求在 Play 前覆盖实时参数.
    /// </summary>
    public void Configure(AudioClipData data)
    {
        InitializeInternal();

        this.data = data;

        if (!data)
        {
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.IsNull(nameof(data)))
                .Action(LogAction.Ignore)
                .Write();

            return;
        }

        source.clip = data.Clip;
        source.loop = data.Loop;
        source.outputAudioMixerGroup = data.MixerGroup;

        source.spatialBlend = data.SpatialBlend;
        source.minDistance = data.MinDistance;
        source.maxDistance = data.MaxDistance;

        // The clip supplies only the static initial; whoever starts the playback may override it. Setting it
        // here as well keeps a freshly configured emitter consistent even before the override is applied.
        // clip 只提供静态初值; 发起播放的一方可以覆盖它.
        // 在此同时写入, 使刚配置好的 emitter 在覆盖生效之前也是自洽的.
        SurviveFreeze = data.DefaultSurviveFreeze;

        // Fades are static characteristics too, so they are copied here with the rest of the clip's data.
        // 淡入淡出同样属于静态特征, 因此与其他 clip 数据一并在此复制.
        FadeIn = data.FadeIn;
        FadeOut = data.FadeOut;
        Volume = data.Volume;
        Pitch = data.Pitch;
    }

    /// <summary>
    /// Single entry point for starting a playback.
    /// Implementation approach: ignores a duplicate start of a live playback. Configure and the request
    /// already established volume and pitch, so Play preserves them, starts the ramp and arms one owned
    /// Timer for natural completion. Stopping or resetting the emitter cancels that timer before reuse.
    /// A looping clip gets no timer, because it has no natural end.
    /// 开始一次播放的单一入口.
    /// 实现思路: 忽略进行中播放的重复启动. Configure 与请求已确立音量和音高, Play 保留这些值,
    /// 启动渐变, 并持有一个负责自然结束的 Timer. 停止或重置时先取消 Timer, 防止影响后续复用.
    /// 循环 clip 没有自然结尾, 因此不创建 Timer.
    /// </summary>
    public void Play()
    {
        if (isPlaying)
        {
            return;
        }

        InitializeInternal();
        source.Stop();

        isPlaying = true;
        isFinished = false;

        source.pitch = pitch;
        isVolumeDirty = false;
        BeginFadeOnPlay();

        if (!source.clip)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned("clip"))
                .Action(LogAction.Ignore)
                .Write();

            Complete(false);
            return;
        }

        source.Play();

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Audio {AudioId} played. "))
            .Write();

        if (data && data.Loop)
        {
            return;
        }

        completionTimer = new Timer(Mathf.Infinity)
            .CompleteWhen(() => !source.isPlaying)
            .OnComplete(FinishNaturally)
            .UseUnscaledTime();
        completionTimer.Start();
    }

    /// <summary>
    /// Single entry point for immediately interrupting a playback.
    /// Implementation approach: idempotent, so the callers that race in practice -- a handle's Stop and a
    /// preemption -- cannot report one playback twice. Guarding on AudioSource.isPlaying is not enough, because
    /// the source may already have stopped while the completion callback is still pending.
    /// This is always a hard cut: RequestStop is the graceful variant, and preemption deliberately uses this
    /// one so that it releases its pool slot in the same frame.
    /// 立即打断一次播放的单一入口, 结束原因记为非自然完成.
    /// 实现思路: 幂等, 使实际会竞争的两个调用方(句柄的 Stop 与抢占)无法把同一次播放报告两次.
    /// 使用内部 isPlaying 守卫, 不只依赖 AudioSource.isPlaying, 因为回调尚未执行时音源可能已自行停止.
    /// 本方法永远是硬切: 优雅的变体是 RequestStop, 而抢占刻意使用本方法, 以便在同一帧释放池槽位.
    /// </summary>
    public void Stop()
    {
        if (!isPlaying)
        {
            return;
        }

        InitializeInternal();
        source.Stop();
        Complete(false);
    }

    /// <summary>
    /// Single entry point for ending a playback gracefully.
    /// Implementation approach: a playback with positive effective FadeOut ramps down first, and Stop follows from
    /// AdvanceFade; otherwise this is exactly Stop. The emitter stays checked out until the ramp ends, which
    /// is the price of hearing the tail. The effective duration can come from the clip or the builder.
    /// 优雅结束一次播放的单一入口.
    /// 实现思路: 有效 FadeOut 为正时先跑渐变, 再由 AdvanceFade 调用 Stop; 否则与 Stop 完全等价.
    /// 渐变结束前 emitter 仍占用池槽; 有效时长可来自 clip 或 Builder 覆盖.
    /// </summary>
    public void RequestStop()
    {
        if (!isPlaying)
        {
            return;
        }

        if (fadeOutDuration <= 0f)
        {
            Stop();
            return;
        }

        BeginFade(FadePhase.Out, 0f);
    }

    /// <summary>
    /// Single entry point for placing this emitter at an explicit point.
    /// Implementation approach: writes only the world position and leaves following untouched, so the caller
    /// may pass a position, a follow target, or neither. With none of them the emitter stays where the pool
    /// left it. Spatial playback still uses that Transform position without either call.
    /// The manager's normal release path resets the local position to zero under AudioRoot.
    /// 把本 emitter 放到指定点的单一入口.
    /// 实现思路: 只写世界坐标, 不触碰跟随状态, 因此调用方可以只给位置, 只给跟随目标, 或两者都不给.
    /// 两者都不给时仍按当前 Transform 坐标空间化; 管理器的正常归还路径会复位到 AudioRoot 的局部零点.
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }

    /// <summary>
    /// Single entry point for handing this emitter its position source.
    /// Implementation approach: stores the Transform and tracks it from LateUpdate instead of reparenting,
    /// so the emitter is not a child of the target and does not die with it.
    /// 把位置来源交给本 emitter 的单一入口.
    /// 实现思路: 保存该 Transform 并在 LateUpdate 中跟踪, 而不是改变父子关系,
    /// 因此 emitter 不是目标的子物体, 不会随它销毁.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        if (!target)
        {
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.IsNull(nameof(target)))
                .Action(LogAction.Ignore)
                .Write();

            followTarget = null;
            return;
        }

        followTarget = target;
        lastTargetPosition = target.position;
        transform.position = lastTargetPosition;
        willFollowTarget = true;
    }

    /// <summary>
    /// Single entry point for returning a pooled emitter to its inert state.
    /// Implementation approach: clears the per-playback data, the follow target and the ramp, then restores the
    /// default volume and pitch. IsFinished is retained until Play; events, spatial settings, mixer and
    /// Transform are not reset here. Normal manager reuse also repositions and calls Configure.
    /// 把池化 emitter 恢复到惰性状态的单一入口.
    /// 实现思路: 清空单次播放数据, 跟随目标与渐变状态, 再恢复默认音量与音高,
    /// IsFinished 保留至 Play; 此处不重置事件、空间设置、混音组或 Transform, 正常复用还需管理器重定位和 Configure.
    /// </summary>
    public void ResetEmitter()
    {
        completionTimer?.Stop();
        completionTimer = null;
        InitializeInternal();

        source.Stop();
        source.clip = null;
        source.loop = false;

        data = null;
        followTarget = null;
        willFollowTarget = false;

        isPlaying = false;
        // Keep the result until Play so handles notified after pool release can latch it.
        // 结果保留到下一次 Play, 使池归还之后收到回调的句柄仍能读取结束原因.

        volume = 1f;
        pitch = 1f;
        surviveFreeze = false;
        fadeInDuration = 0f;
        fadeOutDuration = 0f;
        fadeElapsed = 0f;
        fadeFrom = 1f;
        fadeTo = 1f;
        fadeScale = 1f;
        fadePhase = FadePhase.None;
        source.volume = volume;
        source.pitch = pitch;
        source.ignoreListenerPause = false;
        isVolumeDirty = false;
    }

    /// <summary>
    /// Single entry point for latching the end of a playback.
    /// Implementation approach: clears the running flag, records the supplied completion classification, and raises
    /// the public callback exactly once, so the pool release path runs once per playback.
    /// 锁定一次播放结束的单一入口.
    /// 实现思路: 清除运行标志, 记录传入的结束分类, 并触发公开回调,
    /// 使池的归还路径每次播放只执行一次.
    /// </summary>
    private void Complete(bool finishedNaturally)
    {
        completionTimer?.Stop();
        completionTimer = null;
        isPlaying = false;
        isFinished = finishedNaturally;

        // A consumer failure must not prevent the pool's completion handler from running.
        // 某个调用方抛异常时, 仍须执行池的归还回调.
        Delegate[] callbacks = onAudioFinished?.GetInvocationList();
        if (callbacks == null)
        {
            return;
        }

        foreach (Delegate callback in callbacks)
        {
            try
            {
                ((Action<AudioEmitter>)callback)(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void FinishNaturally()
    {
        completionTimer = null;
        if (isPlaying)
        {
            Complete(true);
        }
    }

    private void SyncPosition()
    {
        if (!followTarget)
        {
            HandleLostTarget();
            return;
        }

        lastTargetPosition = followTarget.position;
        transform.position = lastTargetPosition;
    }

    /// <summary>
    /// Single entry point for reacting to a followed Transform that no longer exists.
    /// Implementation approach: leaves a one-shot playing where the target was last seen, but stops a loop,
    /// because a loop has no natural end and would hold this pooled emitter forever.
    /// 处理被跟随的 Transform 已不存在这一情况的单一入口.
    /// 实现思路: 一次性音在目标最后出现的位置播完; 循环音则停止,
    /// 因为循环没有自然结尾, 会永久占住这个池化 emitter.
    /// </summary>
    private void HandleLostTarget()
    {
        followTarget = null;
        transform.position = lastTargetPosition;
        willFollowTarget = false;

        if (IsLooping)
        {
            Stop();
        }
    }

    /// <summary>
    /// Single entry point for starting the ramp of a new playback.
    /// Implementation approach: a fade-in starts from silence; without a fade-in the volume is left at its
    /// baseline. Whatever a previous playback left behind is overwritten here, because the emitter is reused.
    /// 开始一次新播放的渐变的单一入口.
    /// 实现思路: 有淡入则从静音开始; 没有淡入则音量直接停在基线值.
    /// 上一次播放留下的渐变状态在此被全部覆盖, 因为 emitter 是复用的.
    /// </summary>
    private void BeginFadeOnPlay()
    {
        if (fadeInDuration <= 0f)
        {
            ClearFade();
            return;
        }

        fadeScale = 0f;
        BeginFade(FadePhase.In, 1f);
        ApplyVolume();
    }

    private void BeginFade(FadePhase phase, float target)
    {
        fadePhase = phase;
        fadeElapsed = 0f;
        fadeFrom = fadeScale;
        fadeTo = target;
    }

    /// <summary>
    /// Single entry point for advancing whatever ramp this emitter is running.
    /// Implementation approach: driven from LateUpdate with unscaled time, so a ramp keeps running while
    /// GameStateManager has set Time.timeScale to 0. The scale multiplies the playback volume rather than
    /// replacing it, so a handle that changes volume mid-ramp still takes effect once the ramp ends.
    /// A ramp that reaches its target either ends, or ends and stops the playback when it was a stop.
    /// 推进本 emitter 正在执行的渐变的单一入口.
    /// 实现思路: 由 LateUpdate 以 unscaled time 驱动, 因此 GameStateManager 把 Time.timeScale 设为 0 时渐变仍在推进.
    /// 该系数是**乘在**播放音量上的, 而不是替换它, 因此渐变途中改音量的效果在渐变结束后依然生效.
    /// 到达目标的渐变要么就此结束, 要么在它是"停止"时结束并停止播放.
    /// </summary>
    private void AdvanceFade()
    {
        if (fadePhase == FadePhase.None)
        {
            BeginTailFadeIfDue();
            return;
        }

        fadeElapsed += Time.unscaledDeltaTime;

        float duration = fadePhase == FadePhase.In ? fadeInDuration : fadeOutDuration;
        float progress = duration > 0f ? Mathf.Clamp01(fadeElapsed / duration) : 1f;

        fadeScale = Mathf.Lerp(fadeFrom, fadeTo, progress);
        ApplyVolume();

        if (progress < 1f)
        {
            return;
        }

        if (fadePhase == FadePhase.In)
        {
            ClearFade();
            return;
        }

        bool shouldStop = fadePhase == FadePhase.Out;

        fadePhase = FadePhase.None;
        fadeElapsed = 0f;
        fadeScale = 0f;

        if (shouldStop)
        {
            Stop();
        }
    }

    /// <summary>
    /// Single entry point for deciding whether a natural playback has reached its tail.
    /// Implementation approach: reads the position from the source itself, so a schedule left over from an
    /// earlier playback cannot start a ramp on a later one, and a paused source simply reaches its tail later.
    /// A looping clip has no tail, and a fade-out at least as long as the clip is ignored rather than fading
    /// from the very first frame.
    /// 判断一次自然播放是否已到尾巴的单一入口.
    /// 实现思路: 播放位置直接取自音源本身, 因此上一次播放遗留的排程不可能给后一次播放启动渐变;
    /// 被暂停的音源只是更晚到达尾巴. 循环 clip 没有尾巴; 淡出时长不小于 clip 长度时忽略,
    /// 而不是从第一帧就开始淡出.
    /// </summary>
    private void BeginTailFadeIfDue()
    {
        if (fadeOutDuration <= 0f)
        {
            return;
        }

        if (!source.clip || source.loop)
        {
            return;
        }

        float tailStart = source.clip.length - fadeOutDuration;

        if (tailStart <= 0f || source.time < tailStart)
        {
            return;
        }

        BeginFade(FadePhase.Tail, 0f);
    }

    private void ClearFade()
    {
        fadePhase = FadePhase.None;
        fadeElapsed = 0f;
        fadeFrom = 1f;
        fadeTo = 1f;
        fadeScale = 1f;
        ApplyVolume();
    }

    private void ApplyVolume()
        => source.volume = volume * fadeScale;

    private void InitializeInternal()
    {
        if (!source)
        {
            source = GetComponent<AudioSource>();
        }
    }

    /// <summary>
    /// Which volume ramp, if any, this emitter is running. Private because it never leaves this file.
    /// 本 emitter 当前正在执行的音量渐变, 若无可为 None. 私有类型, 因为它不出本文件.
    /// </summary>
    private enum FadePhase
    {
        None,
        In,
        Tail,
        Out
    }
}
