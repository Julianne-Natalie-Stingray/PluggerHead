using UnityEngine;
using System;

/// <summary>
/// One pooled audio voice. It plays one clip and reports when that playback is over.
/// Subsystem: Core (Audio).
/// Where it lives: on the pooled emitter prefab, instantiated under AudioRoot by AudioManager's pool.
/// Responsibility: apply the clip's static characteristics to its AudioSource, expose the live volume and
/// pitch, keep its own world position in sync with the Transform the builder supplied, and report the end
/// of a playback exactly once so the pool can take the emitter back.
/// Does NOT own: which clip plays next, the decision to play at all, the instance limits, preemption, which
/// Transform to follow (AudioBuilder decides that), or the registry.
/// Lifetime: created by the pool, which is the only thing that destroys it. One instance serves many
/// playbacks, so every value a playback depends on is re-established in Play, and the emitter is left inert
/// on the frame it is returned.
/// Completion model: Play arms a Timer that completes as soon as the source stops playing. That timer is the
/// only caller of Stop for a natural end; a handle's Stop and a preemption call it directly. All three paths
/// therefore funnel through one completion signal.
/// Paradigms: none. It is a pooled component driven by AudioManager.
/// 单个池化的音频声部. 它播放一个 clip 并报告该次播放结束.
/// Subsystem 归属: Core (Audio).
/// 存在位置: 池化的 emitter prefab 上, 由 AudioManager 的池实例化到 AudioRoot 之下.
/// 职能: 把 clip 的静态特征写入自身 AudioSource, 暴露实时音量与音高,
/// 使自身世界坐标与 Builder 传入的 Transform 同步, 并且只报告一次播放结束, 以便池收回该 emitter.
/// 不负责: 下一个播放什么 clip, 是否播放, 实例上限, 抢占, 跟随哪个 Transform(由 AudioBuilder 决定),
/// 以及注册表.
/// 生命周期: 由池创建, 也只有池会销毁它. 一个实例服务多次播放,
/// 因此每次播放依赖的值都在 Play 中重新确立; 归还的那一帧它是惰性的.
/// 结束检测: Play 装载一个 Timer, 只要 source 停止播放它就完成. 该 Timer 是"自然播完"路径上 Stop 的唯一调用者;
/// 句柄的 Stop 与抢占则直接调用. 三条路径因此汇入同一个结束信号.
/// 使用范式: 无. 它是由 AudioManager 驱动的池化组件.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class AudioEmitter : MonoBehaviour
{
    public event Action<AudioEmitter> onAudioFinished;

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

    private AudioSource source;
    private AudioClipData data;

    private Transform followTarget;
    private Vector3 lastTargetPosition;

    private float volume = 1f;
    private float pitch = 1f;
    private bool isPlaying;
    private bool isFinished;
    private bool surviveFreeze;
    private bool isVolumeDirty;

    private const float MinVolume = 0f;
    private const float MaxVolume = 1f;
    private const float MinPitch = 0.1f;
    private const float MaxPitch = 3f;

    private bool willFollowTarget;

    private void Awake()
    {
        InitializeInternal();
    }

    /// <summary>
    /// Single entry point for the per-frame work of a live playback.
    /// Implementation approach: applies a pending volume immediately for every playback, because a handle may
    /// change volume at any time; then syncs position only while a follow target is actually being requested.
    /// Those two are independent: gating the volume write on follow would silently drop it for every sound
    /// that does not follow, and syncing position for a sound that follows nothing would read a null target.
    /// 进行中的播放每帧工作的单一入口.
    /// 实现思路: 待应用的音量对每种播放都立即写入, 因为句柄随时可能改音量;
    /// 位置同步只在确实请求了跟随目标时进行.
    /// 二者互相独立: 用跟随去门控音量写入会让每个不跟随的声音静默丢失音量变更;
    /// 而给一个不跟随任何东西的声音做位置同步则会去读一个空目标.
    /// </summary>
    private void LateUpdate()
    {
        if (!IsPlaying)
            return;

        if (isVolumeDirty)
        {
            source.volume = volume;
            isVolumeDirty = false;
        }

        if (!willFollowTarget)
            return;

        SyncPosition();
    }

    /// <summary>
    /// Single entry point for arming this emitter with one clip's static characteristics.
    /// Implementation approach: copies only what the clip owns. Live effects such as volume and pitch are
    /// deliberately not applied here, because they belong to a single playback and are established in Play.
    /// 用某个 clip 的静态特征武装本 emitter 的单一入口.
    /// 实现思路: 只复制 clip 拥有的东西. 音量与音高这类实时效果刻意不在此应用,
    /// 因为它们属于单次播放, 在 Play 中确立.
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
    }

    /// <summary>
    /// Single entry point for starting a playback.
    /// Implementation approach: ends any playback still in flight through Stop first, so the completion
    /// callback runs and the pool can account for the emitter it handed out; Play is reached on a reused
    /// emitter while the previous playback is unfinished, and silently returning there would let the pool
    /// starve while requests kept arriving. It then re-establishes the per-playback baseline and arms a Timer
    /// that completes when the source stops. A looping clip gets no timer, because it has no natural end.
    /// 开始一次播放的单一入口.
    /// 实现思路: 先用 Stop 结束仍在进行中的播放, 以便完成回调执行, 池能对已交出的 emitter 记账;
    /// Play 会在复用 emitter 且上一次播放尚未结束时被调用, 此时静默返回会让池在请求持续到来时枯竭.
    /// 之后重新确立单次播放的基线, 并装载一个在 source 停止时完成的 Timer. 循环 clip 不装 Timer, 因为它没有自然结尾.
    /// </summary>
    public void Play()
    {
        Stop();

        InitializeInternal();
        source.Stop();

        isPlaying = true;
        isFinished = false;

        volume = data ? data.Volume : 1f;
        pitch = data ? data.Pitch : 1f;

        source.volume = volume;
        source.pitch = pitch;
        isVolumeDirty = false;

        if (!source.clip)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned("clip"))
                .Action(LogAction.Ignore)
                .Write();

            isPlaying = false;
            return;
        }

        source.Play();

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Audio {AudioId} played. "))
            .Write();

        if (data && data.Loop)
            return;

        _ = new Timer(Mathf.Infinity)
            .CompleteWhen(() => !source.isPlaying)
            .OnComplete(Stop)
            .UseUnscaledTime()
            .Start();
    }

    /// <summary>
    /// Single entry point for ending a playback, early or naturally.
    /// Implementation approach: idempotent, so the callers that race in practice -- a handle's Stop and a
    /// preemption -- cannot report one playback twice. Guarding on isPlaying alone is not enough, because
    /// the source may already have stopped while the completion callback is still pending.
    /// 结束一次播放的单一入口, 无论提前还是自然结束.
    /// 实现思路: 幂等, 使实际会竞争的两个调用方(句柄的 Stop 与抢占)无法把同一次播放报告两次.
    /// 只用 isPlaying 守卫是不够的, 因为完成回调尚未执行时 source 可能已经自行停止.
    /// </summary>
    public void Stop()
    {
        if (!isPlaying)
            return;

        InitializeInternal();
        source.Stop();
        Complete(false);
    }

    /// <summary>
    /// Single entry point for placing this emitter at an explicit point.
    /// Implementation approach: writes only the world position and leaves following untouched, so the caller
    /// may pass a position, a follow target, or neither. With none of them the emitter stays where the pool
    /// left it, which is AudioRoot. A position is what makes a spatial clip audible from somewhere other than
    /// the listener, so a clip with spatialBlend above zero is inert unless this or SetFollowTarget is used.
    /// 把本 emitter 放到指定点的单一入口.
    /// 实现思路: 只写世界坐标, 不触碰跟随状态, 因此调用方可以只给位置, 只给跟随目标, 或两者都不给.
    /// 两者都不给时 emitter 停在池留下的位置, 即 AudioRoot.
    /// 位置正是让空间化 clip 从非听者位置发声的前提, 所以 spatialBlend 大于零的 clip
    /// 若不使用本方法或 SetFollowTarget, 其空间化是无效的.
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
    /// Implementation approach: clears the per-playback data and the follow target, then restores the default
    /// volume and pitch, so a later playback cannot inherit anything from the previous one.
    /// 把池化 emitter 恢复到惰性状态的单一入口.
    /// 实现思路: 清空单次播放数据与跟随目标, 再恢复默认音量与音高,
    /// 使之后的播放不会继承上一次的任何东西.
    /// </summary>
    public void ResetEmitter()
    {
        InitializeInternal();

        source.Stop();
        source.clip = null;
        source.loop = false;

        data = null;
        followTarget = null;
        willFollowTarget = false;

        isPlaying = false;
        isFinished = false;

        volume = 1f;
        pitch = 1f;
        surviveFreeze = false;
        source.volume = volume;
        source.pitch = pitch;
        source.ignoreListenerPause = false;
        isVolumeDirty = false;
    }

    /// <summary>
    /// Single entry point for latching the end of a playback.
    /// Implementation approach: clears the running flag, records whether the clip reached its end, and raises
    /// the public callback exactly once, so the pool release path runs once per playback.
    /// 锁定一次播放结束的单一入口.
    /// 实现思路: 清除运行标志, 记录是否为自然播完, 并只触发一次公开回调,
    /// 使池的归还路径每次播放只执行一次.
    /// </summary>
    private void Complete(bool finishedNaturally)
    {
        isPlaying = false;
        isFinished = finishedNaturally;

        onAudioFinished?.Invoke(this);
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
            Stop();
    }

    private void InitializeInternal()
    {
        if (!source)
            source = GetComponent<AudioSource>();
    }
}
