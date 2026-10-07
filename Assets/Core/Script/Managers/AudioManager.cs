using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Core audio request service, normally accessed through CoreFacade and AudioBuilder.
/// Awake validates references and builds the registry/pool; Start prewarms and applies player volumes.
/// Requests resolve clip data, check frozen entry, apply per-ID/global limits, then reserve an emitter.
/// MaxPoolSize controls idle retention and also limits normal request growth through CanReuse.
/// Limit checks each preempt at most one non-looping voice; they do not normalize a dynamically lowered cap.
/// Rejections return null; some early exits intentionally have no log.
/// Core 音频请求服务, 通常经 CoreFacade 与 AudioBuilder 使用.
/// Awake 检查引用并创建注册表/池, Start 预热并应用玩家音量.
/// 请求依次解析数据、检查冻结入口、每 ID/全局上限, 再借出声部.
/// MaxPoolSize 同时控制闲置保留量与正常请求的扩容; 每级至多抢占一个非循环声部, 不负责归一化动态降低的上限.
/// 拒绝时返回 null, 部分前置退出不输出日志. 清理与播放生命周期边界见本目录 README.
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
    private ISoundHandle backgroundMusic;
    private bool isChangingBackgroundMusic;

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
    /// Prewarm the pool and apply current player volumes.
    /// Implementation approach: the pool itself is built in InitializeInternal so that a playback requested
    /// from another component's Start already works. Prewarming and mixer application are deferred here.
    /// 预热对象池并应用当前玩家音量.
    /// 实现思路: 池本身在 InitializeInternal 中构建, 以便其他组件的 Start 里发起的播放已经可用;
    /// 预热和混音器音量应用放到 Start.
    /// </summary>
    private void Start()
    {
        emitterPool.Prewarm(configs.PrewarmAmount);
        ApplyAudioSettings();
    }

    /// <summary>
    /// Subscribe while enabled; OnDisable unsubscribes. This does not immediately synchronize current state.
    /// 启用时订阅状态, 禁用时退订; 此处不立即同步当前状态.
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
        if (backgroundMusic != null)
        {
            backgroundMusic.Finished -= HandleBackgroundMusicFinished;
            backgroundMusic = null;
        }
        emitterPool?.Clear();
    }

    /// <summary>
    /// Reuse the current BGM, including while paused; a different AudioId replaces it.
    /// Core owns this playback across scene loads. Finished releases the retained handle.
    /// 复用当前 BGM（包括暂停时）；不同 AudioId 才换曲。Core 跨场景持有播放，结束时释放句柄。
    /// Reentrant requests during a change return null so completion callbacks cannot stack BGM voices.
    /// 换曲期间重入请求返回 null，防止结束回调叠加 BGM 声部。
    /// </summary>
    public ISoundHandle PlayBackgroundMusic(AudioId requested)
    {
        if (isChangingBackgroundMusic)
        {
            return null;
        }
        if (backgroundMusic != null && backgroundMusic.AudioId == requested)
        {
            return backgroundMusic;
        }

        isChangingBackgroundMusic = true;
        try
        {
            backgroundMusic?.Stop();
            backgroundMusic = CreateBuilder().WithSurviveFreeze(false).WithFade(0f, 0f).Play(requested);
            if (backgroundMusic != null)
            {
                backgroundMusic.Finished += HandleBackgroundMusicFinished;
            }
            return backgroundMusic;
        }
        finally
        {
            isChangingBackgroundMusic = false;
        }
    }

    private void HandleBackgroundMusicFinished(ISoundHandle finished)
    {
        finished.Finished -= HandleBackgroundMusicFinished;
        if (ReferenceEquals(backgroundMusic, finished))
        {
            backgroundMusic = null;
        }
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
    /// Submit a request, normally through AudioBuilder. Returns null when unavailable or refused.
    /// Checks enabled/configs/pool, clip, numeric parameters, frozen entry, limits, then pool capacity; missing clip and unavailable
    /// service exits do not all log. Registers and configures the reserved voice before starting it.
    /// During Freezed, both AllowWhileFrozen and effective SurviveFreeze must be true.
    /// SurviveFreeze maps to ignoreListenerPause, independently of looping; it does not directly release voices.
    /// 提交播放请求, 正常由 AudioBuilder 调用. 服务不可用或拒绝时返回 null.
    /// 依次检查 enabled/configs/pool、clip、数值参数、冻结入口、实例上限与池容量; 部分前置退出不记录日志.
    /// 借出后先注册和配置, 再启动. Freezed 期间需 AllowWhileFrozen 与有效 SurviveFreeze 同为 true.
    /// SurviveFreeze 对应 ignoreListenerPause, 与循环独立, 不直接停止或归还声部.
    /// </summary>
    public ISoundHandle Play(
        AudioId requested,
        float? volume,
        float? pitch,
        Vector3? position,
        Transform followTarget,
        bool? surviveFreeze,
        bool? allowWhileFrozen,
        float? fadeIn = null,
        float? fadeOut = null)
    {
        if (!enabled || !configs || emitterPool == null)
        {
            return null;
        }

        AudioClipData data = ResolveClip(requested);

        if (!data || !data.Clip)
        {
            return null;
        }

        // Validate before preemption or checkout: an invalid request must not interrupt a live voice.
        // 抢占或借出前校验, 无效请求不得打断正在播放的声部.
        if (float.IsNaN(volume ?? data.Volume) || float.IsNaN(pitch ?? data.Pitch) ||
            float.IsNaN(data.Volume) || float.IsNaN(data.Pitch) ||
            !IsFiniteDuration(fadeIn ?? data.FadeIn) || !IsFiniteDuration(fadeOut ?? data.FadeOut) ||
            !IsFiniteDuration(data.FadeIn) || !IsFiniteDuration(data.FadeOut))
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify("Audio request contains NaN volume/pitch or a non-finite fade duration."))
                .Action(LogAction.Ignore)
                .Write();
            return null;
        }

        bool willSurviveFreeze = surviveFreeze ?? data.DefaultSurviveFreeze;

        if (!IsFrozenEntryAllowed(requested, allowWhileFrozen, willSurviveFreeze))
        {
            return null;
        }

        if (!TryApplyInstanceLimits(requested, data))
        {
            return null;
        }

        AudioEmitter emitter = ReserveEmitter();
        if (!emitter)
        {
            return null;
        }

        registry.Register(emitter);
        emitter.gameObject.SetActive(true);
        emitter.onAudioFinished -= ReleaseEmitter;
        emitter.onAudioFinished -= OnEmitterRelease;
        emitter.Configure(data);
        emitter.Volume = Mathf.Clamp(volume ?? data.Volume, MinVolume, MaxVolume);
        emitter.Pitch = Mathf.Clamp(pitch ?? data.Pitch, MinPitch, MaxPitch);

        // A request may override the clip's ramp; absent means the clip's static values, which Configure has
        // already written. 请求可以覆盖 clip 的渐变; 不设置即使用 Configure 已写入的 clip 静态值.
        if (fadeIn.HasValue)
        {
            emitter.FadeIn = fadeIn.Value;
        }

        if (fadeOut.HasValue)
        {
            emitter.FadeOut = fadeOut.Value;
        }

        emitter.SurviveFreeze = willSurviveFreeze;

        if (position.HasValue)
        {
            emitter.SetPosition(position.Value);
        }

        if (followTarget)
        {
            emitter.SetFollowTarget(followTarget);
        }

        emitter.onAudioFinished += ReleaseEmitter;
        SoundHandle handle = new SoundHandle(emitter, requested);

        emitter.Play();

        return handle;
    }

    /// <summary>
    /// Apply current in-memory player volumes to configured mixer parameters; this does not save settings.
    /// Called from Start, the play-mode debug button, and SettingsScreen after a successful save.
    /// There is no automatic property-change subscription. The settings bootstrap must already be ready.
    /// 将当前内存中的玩家音量写入配置的 Mixer 参数, 不负责保存.
    /// Start、播放模式调试按钮以及 SettingsScreen 保存成功后调用; 不自动订阅属性变更, 要求设置自举已完成.
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
        {
            return data;
        }

        GameLog.Error(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.CannotFind(nameof(requested), nameof(configs)))
            .Action(LogAction.Ignore)
            .Write();

        return null;
    }

    private static bool IsFiniteDuration(float duration)
    {
        return !float.IsNaN(duration) && !float.IsInfinity(duration);
    }

    /// <summary>
    /// Outside Freezed, allow entry. During Freezed require explicit allowance and effective SurviveFreeze.
    /// 非 Freezed 状态直接放行; Freezed 期间要求显式允许进入且有效 SurviveFreeze 为 true.
    /// </summary>
    private bool IsFrozenEntryAllowed(AudioId requested, bool? allowWhileFrozen, bool willSurviveFreeze)
    {
        if (GameStateManager.Current != GameState.Freezed)
        {
            return true;
        }

        if ((allowWhileFrozen ?? false) && willSurviveFreeze)
        {
            return true;
        }

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
    /// Check the per-ID cap, then the global cap; each check can hard-stop one oldest non-looping candidate.
    /// No candidate means refusal. Earlier preemption is not rolled back if a later check fails.
    /// Recheck after preemption and check both current caps before admission, because Finished may reenter Play.
    /// If one preemption cannot make room after a cap reduction or callback, refuse without chasing new victims.
    /// 先检查每 ID 上限, 再检查全局上限; 每级最多硬停一个最旧的非循环候选, 无候选则拒绝.
    /// 抢占后复查, 放行前检查两项当前上限; Finished 可能重入 Play 或更改上限.
    /// 降低上限或回调后一次抢占仍不能腾出名额则拒绝, 不继续追逐新候选, 也不撤销之前的抢占.
    /// </summary>
    private bool TryApplyInstanceLimits(AudioId requested, AudioClipData data)
    {
        if (data.MaxInstances <= 0 || configs.MaxSoundInstance <= 0)
        {
            return RefuseOverLimit($"Non-positive instance limit prevents playback of {requested}. ");
        }

        if (registry.CountOf(requested) >= data.MaxInstances)
        {
            if (registry.TryGetOldest(requested, false, out AudioEmitter sameClipVictim))
            {
                Preempt(sameClipVictim, requested);
            }
            else
            {
                return RefuseOverLimit(
                    $"Per-clip limit of {data.MaxInstances} is reached for {requested} and every instance is " +
                    "looping, so none of them may be preempted. ");
            }

            if (registry.CountOf(requested) >= data.MaxInstances)
            {
                return RefuseOverLimit($"Per-clip limit of {data.MaxInstances} remains reached for {requested} after preemption. ");
            }
        }

        if (configs.MaxSoundInstance <= 0)
        {
            return RefuseOverLimit($"Global limit of {configs.MaxSoundInstance} prevents playback after completion callbacks. ");
        }

        if (registry.Count >= configs.MaxSoundInstance)
        {
            if (registry.TryGetOldest(false, out AudioEmitter globalVictim))
            {
                Preempt(globalVictim, requested);
            }
            else
            {
                return RefuseOverLimit(
                    $"Global limit of {configs.MaxSoundInstance} is reached and every active sound is " +
                    "looping, so none of them may be preempted. ");
            }
        }

        // Global preemption callbacks may refill either cap, including the requested ID's slot.
        // 全局抢占的回调可能占回任一名额, 包括请求 ID 的名额; 读取当前配置以覆盖回调内降限.
        if (registry.CountOf(requested) >= data.MaxInstances)
        {
            return RefuseOverLimit($"Per-clip limit of {data.MaxInstances} is reached for {requested} after completion callbacks. ");
        }

        if (registry.Count >= configs.MaxSoundInstance)
        {
            return RefuseOverLimit($"Global limit of {configs.MaxSoundInstance} remains reached after preemption. ");
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
        {
            return;
        }

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
    /// Set listener pause exactly when the notification is Freezed; Loading therefore unpauses it.
    /// Does not stop individual emitters. Per-playback SurviveFreeze controls ignoreListenerPause.
    /// 仅通知参数为 Freezed 时暂停监听器, Loading 因此解除暂停. 不停止单个声部;
    /// 每次播放的 SurviveFreeze 控制是否忽略监听器暂停.
    /// </summary>
    private void HandleGameStateChanged(GameState state)
    {
        bool shouldPause = state == GameState.Freezed;

        if (AudioListener.pause == shouldPause)
        {
            return;
        }

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
    /// local -80 dB floor; an empty parameter name is reported rather than handed to Unity, because
    /// SetFloat on a name that is not exposed fails silently.
    /// 把一条线性音量写入一个 mixer 暴露参数的单一入口.
    /// 实现思路: mixer 的音量参数以分贝表示, 因此线性 0..1 的设置经 20*log10 换算.
    /// 零没有对数, 因此低到门槛以下的值一律取本类定义的 -80 dB 下限;
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

        if (!configs.Mixer.SetFloat(parameter, decibels))
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"Mixer parameter {parameter} is not exposed. "))
                .Action(LogAction.Ignore)
                .Write();
        }
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
            return;
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
    [FormerlySerializedAs("DebugSurviveFreeze")]
    [SerializeField] private bool debugSurviveFreeze;
    [FormerlySerializedAs("DebugAllowWhileFrozen")]
    [SerializeField] private bool debugAllowWhileFrozen;

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
        {
            CreateBuilder().WithSurviveFreeze(debugSurviveFreeze)
                .WithAllowWhileFrozen(debugAllowWhileFrozen)
                .Play(debugAudioRequest);
        }

        GameLog.Info(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Manual requested Audio: {debugAudioRequest}. \n" +
                                    $"Request time: {requestTime}\n" +
                                    $"Survive freeze: {debugSurviveFreeze}\n" +
                                    $"Allow while frozen: {debugAllowWhileFrozen}. "))
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
